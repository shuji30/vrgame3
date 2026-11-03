using System;
using UnityEngine;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球のボールの動きを、物理エンジンを使わずに計算する(docs/plan.md「卓球」のボールの動き)。
    /// 重力と空気の抵抗(速さの2乗に比例)で進め、台・ネット・床・ラケットとの当たりを細かい刻みで調べる。
    /// ラケットは前の刻みと今の刻みの位置・向きを補間し、ボールが面を通り越したかで当たりを取る(連続衝突判定)。
    /// Vector3 の計算だけを使い、シーンの物には触れない(単体テストできるように)。
    /// 座標:台の上面の中心が tableCenter、長さ方向が Z(プレイヤーは -Z 側)、幅が X。
    /// </summary>
    public static class PingPongPhysics
    {
        /// <summary>動きの決まり(Inspector で調整する値)。</summary>
        [Serializable]
        public class Settings
        {
            [Tooltip("ボールの半径(m)")]
            public float ballRadius = 0.02f;

            [Tooltip("重力(m/秒²)")]
            public float gravity = 9.81f;

            [Tooltip("空気の抵抗の係数(速さの2乗に比例)")]
            public float drag = 0.14f;

            [Tooltip("台で弾むときの縦の跳ね返り")]
            public float tableBounce = 0.88f;

            [Tooltip("台で弾むときの横の速さの倍率")]
            public float tableFriction = 0.95f;

            [Tooltip("ネットに当たったときの跳ね返り")]
            public float netBounce = 0.1f;

            [Tooltip("ラケットの跳ね返り")]
            public float racketBounce = 0.85f;

            [Tooltip("ラケットに当たったとき、面に沿う速さを残す割合")]
            public float racketGrip = 0.6f;

            [Tooltip("打つ力の倍率(ラケットの速さに掛ける)")]
            public float hitPower = 1f;

            [Tooltip("ボールの最高速(m/秒)")]
            public float maxSpeed = 25f;

            [Tooltip("ラケットの面の当たりの半径(m)。見た目の 1.1 倍が目安")]
            public float racketRadius = 0.085f;

            [Tooltip("計算の刻み(秒)")]
            public float step = 1f / 240f;

            [Tooltip("台で弾む縦の速さがこれより遅ければ、弾ませずに台の上を転がす(m/秒)")]
            public float restSpeed = 0.3f;
        }

        /// <summary>台とネットの形。</summary>
        public struct Table
        {
            /// <summary>台の上面の中心。</summary>
            public Vector3 Center;
            public float Length;
            public float Width;
            public float NetHeight;

            /// <summary>ネットが台の横にはみ出す長さ。</summary>
            public float NetOverhang;

            public float Top => Center.y;

            public static Table Standard(Vector3 center)
            {
                return new Table { Center = center, Length = 2.74f, Width = 1.525f, NetHeight = 0.1525f, NetOverhang = 0.1525f };
            }

            /// <summary>その点が台の上(縁の内側)か。</summary>
            public bool IsOver(Vector3 p, float margin = 0f)
            {
                return Mathf.Abs(p.x - Center.x) <= Width / 2f - margin && Mathf.Abs(p.z - Center.z) <= Length / 2f - margin;
            }

            /// <summary>その点がどちらのコートか(Z で決める)。</summary>
            public TTSide SideOf(Vector3 p) => p.z < Center.z ? TTSide.Player : TTSide.Cpu;
        }

        /// <summary>ラケットの面の、ある瞬間の姿勢と動き。</summary>
        public struct RacketPose
        {
            public Vector3 Center;

            /// <summary>面の向き(どちらの面でも打てる)。</summary>
            public Vector3 Normal;

            /// <summary>面の中心の速さ。</summary>
            public Vector3 Velocity;

            /// <summary>回転の速さ(ラジアン/秒、ワールド座標)。先端の速さを出すのに使う。</summary>
            public Vector3 AngularVelocity;
        }

        /// <summary>1回の計算で起きた出来事。</summary>
        public enum EventKind
        {
            None,
            Table,
            Net,
            Floor,
            Racket,
        }

        public struct Event
        {
            public EventKind Kind;
            public Vector3 Point;
            public TTSide Side;

            /// <summary>当たったときのボールの速さ(音と振動の強さに使う)。</summary>
            public float Speed;
        }

        /// <summary>
        /// ボールを dt 秒だけ進める。racketPrev / racketNow はこの dt の始めと終わりのラケット(無ければ hasRacket = false)。
        /// 最初に起きた出来事を返す(床に落ちたらそこで止める)。ラケットに当たったら速さを書き換える。
        /// </summary>
        public static Event Advance(ref Vector3 position, ref Vector3 velocity, float dt, Settings s, Table table,
            bool hasRacket, RacketPose racketPrev, RacketPose racketNow)
        {
            return Advance(ref position, ref velocity, dt, s, table, hasRacket, racketPrev, racketNow, null);
        }

        /// <summary>
        /// Advance と同じだが、この dt の中で起きた出来事をすべて events に足す(台で弾んだ直後に打つときなど、
        /// 1フレームに2つ起きても取りこぼさないため)。戻り値は最初の出来事。
        /// </summary>
        public static Event Advance(ref Vector3 position, ref Vector3 velocity, float dt, Settings s, Table table,
            bool hasRacket, RacketPose racketPrev, RacketPose racketNow, System.Collections.Generic.List<Event> events)
        {
            var first = new Event { Kind = EventKind.None };
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / Mathf.Max(0.0005f, s.step)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float t0 = (float)i / steps;
                float t1 = (float)(i + 1) / steps;
                Event e = Step(ref position, ref velocity, h, s, table, hasRacket,
                    Lerp(racketPrev, racketNow, t0), Lerp(racketPrev, racketNow, t1));
                if (e.Kind != EventKind.None)
                {
                    events?.Add(e);
                    if (first.Kind == EventKind.None)
                    {
                        first = e;
                    }
                }

                if (e.Kind == EventKind.Floor)
                {
                    return e;
                }

                // ラケットに当たったら、この dt の残りではもう当てない(二度当たりを防ぐ)
                if (e.Kind == EventKind.Racket)
                {
                    hasRacket = false;
                }
            }

            return first;
        }

        /// <summary>1刻み進める。</summary>
        public static Event Step(ref Vector3 p, ref Vector3 v, float h, Settings s, Table table, bool hasRacket,
            RacketPose r0, RacketPose r1)
        {
            Vector3 start = p;

            // 重力と空気の抵抗
            v += Vector3.down * (s.gravity * h);
            v -= v * (s.drag * v.magnitude * h);
            p += v * h;

            // ラケット(前の刻みと今の刻みで、ボールが面のどちら側にいたかが変わったか)
            if (hasRacket)
            {
                float d0 = Vector3.Dot(start - r0.Center, r0.Normal);
                float d1 = Vector3.Dot(p - r1.Center, r1.Normal);
                bool crossed = (d0 > 0f) != (d1 > 0f);
                bool touching = Mathf.Abs(d1) < s.ballRadius;
                Vector3 onFace = p - r1.Center - r1.Normal * d1;
                if ((crossed || touching) && onFace.magnitude <= s.racketRadius)
                {
                    Vector3 contact = r1.Center + onFace;
                    Vector3 racketVelocity = r1.Velocity + Vector3.Cross(r1.AngularVelocity, contact - r1.Center);
                    Vector3 rel = v - racketVelocity;
                    // ボールが来た側の面で打つ
                    Vector3 n = d0 >= 0f ? r1.Normal : -r1.Normal;
                    float into = Vector3.Dot(rel, n);
                    if (into < 0f)
                    {
                        Vector3 normalPart = n * into;
                        Vector3 tangent = rel - normalPart;
                        Vector3 outRel = -normalPart * s.racketBounce + tangent * s.racketGrip;
                        v = racketVelocity * s.hitPower + outRel;
                        if (v.magnitude > s.maxSpeed)
                        {
                            v = v.normalized * s.maxSpeed;
                        }

                        p = contact + n * (s.ballRadius + 0.002f);
                        return new Event { Kind = EventKind.Racket, Point = contact, Side = table.SideOf(contact), Speed = v.magnitude };
                    }
                }
            }

            // ネット(Z = 台の中心を越えたとき、ネットの高さの中なら跳ね返す)
            bool crossedNet = (start.z - table.Center.z) * (p.z - table.Center.z) <= 0f && start.z != p.z;
            if (crossedNet)
            {
                float f = (table.Center.z - start.z) / (p.z - start.z);
                Vector3 at = Vector3.Lerp(start, p, f);
                bool inWidth = Mathf.Abs(at.x - table.Center.x) <= table.Width / 2f + table.NetOverhang;
                bool belowTop = at.y - s.ballRadius <= table.Top + table.NetHeight && at.y >= table.Top;
                if (inWidth && belowTop)
                {
                    // 上の縁をかすったときは少し上へ逃がし、それ以外は手前へ跳ね返す
                    bool clipTop = at.y + s.ballRadius * 0.5f > table.Top + table.NetHeight;
                    if (clipTop)
                    {
                        v = new Vector3(v.x * 0.5f, Mathf.Abs(v.y) * 0.5f + 0.5f, v.z * 0.4f);
                        p = new Vector3(p.x, table.Top + table.NetHeight + s.ballRadius + 0.001f, p.z);
                    }
                    else
                    {
                        float back = Mathf.Sign(start.z - table.Center.z);
                        v = new Vector3(v.x * 0.3f, v.y * 0.3f, -v.z * s.netBounce);
                        p = new Vector3(at.x, at.y, table.Center.z + back * (s.ballRadius + 0.002f));
                    }

                    return new Event { Kind = EventKind.Net, Point = at, Side = table.SideOf(start), Speed = v.magnitude };
                }
            }

            // 台の上面
            if (v.y < 0f && p.y - s.ballRadius <= table.Top && start.y - s.ballRadius >= table.Top - 0.02f && table.IsOver(p))
            {
                float speed = v.magnitude;
                p = new Vector3(p.x, table.Top + s.ballRadius, p.z);
                if (-v.y * s.tableBounce < s.restSpeed)
                {
                    // 止まりかけ:弾ませずに台の上を転がす(出来事は出さない。音が鳴り続けないように)
                    v = new Vector3(v.x * (1f - 2f * h), 0f, v.z * (1f - 2f * h));
                    return new Event { Kind = EventKind.None };
                }

                v = new Vector3(v.x * s.tableFriction, -v.y * s.tableBounce, v.z * s.tableFriction);
                return new Event { Kind = EventKind.Table, Point = p, Side = table.SideOf(p), Speed = speed };
            }

            // 床
            if (p.y - s.ballRadius <= 0f)
            {
                p = new Vector3(p.x, s.ballRadius, p.z);
                return new Event { Kind = EventKind.Floor, Point = p, Side = table.SideOf(p), Speed = v.magnitude };
            }

            return new Event { Kind = EventKind.None };
        }

        private static RacketPose Lerp(RacketPose a, RacketPose b, float t)
        {
            return new RacketPose
            {
                Center = Vector3.Lerp(a.Center, b.Center, t),
                // 1刻みでの向きの変化は小さいので、正規化した線形補間で足りる(Slerp はネイティブ呼び出しで単体テストできない)
                Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                Velocity = Vector3.Lerp(a.Velocity, b.Velocity, t),
                AngularVelocity = Vector3.Lerp(a.AngularVelocity, b.AngularVelocity, t),
            };
        }

        /// <summary>
        /// ボールが台に落ちる点を先回りして求める(打ったあと最初に台か床に落ちるところまで)。
        /// 台に落ちれば true。落ちる点と時刻、ネットに当たったかを返す。
        /// </summary>
        public static bool PredictLanding(Vector3 position, Vector3 velocity, Settings s, Table table, out Vector3 landing,
            out float time, out bool hitNet, float maxTime = 3f)
        {
            return PredictBounce(position, velocity, s, table, out landing, out time, out hitNet, out _, maxTime);
        }

        /// <summary>
        /// PredictLanding と同じく最初に台か床に落ちるところまでたどり、台で弾んだ直後の速さも返す。
        /// </summary>
        public static bool PredictBounce(Vector3 position, Vector3 velocity, Settings s, Table table, out Vector3 landing,
            out float time, out bool hitNet, out Vector3 afterBounce, float maxTime = 3f)
        {
            hitNet = false;
            time = 0f;
            var none = new RacketPose();
            while (time < maxTime)
            {
                Event e = Step(ref position, ref velocity, s.step, s, table, false, none, none);
                time += s.step;
                if (e.Kind == EventKind.Net)
                {
                    hitNet = true;
                }
                else if (e.Kind == EventKind.Table)
                {
                    landing = e.Point;
                    afterBounce = velocity;
                    return true;
                }
                else if (e.Kind == EventKind.Floor)
                {
                    landing = e.Point;
                    afterBounce = velocity;
                    return false;
                }
            }

            landing = position;
            afterBounce = velocity;
            return false;
        }

        /// <summary>
        /// from から打って、target(台の上の点)に落ちる速さを求める。水平の速さ horizontalSpeed を基準に、
        /// 縦の速さを二分探索で合わせる。ネットを越えられないときは、水平の速さを少しずつ落として高く打つ。
        /// 見つからなければ false(そのときも一番近い速さを返す)。
        /// </summary>
        public static bool SolveShot(Vector3 from, Vector3 target, float horizontalSpeed, Settings s, Table table,
            out Vector3 velocity)
        {
            Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
            Vector3 dir = flat.normalized;
            float want = flat.magnitude;
            velocity = dir * horizontalSpeed;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                float speed = horizontalSpeed * (1f - 0.12f * attempt);
                float lo = -4f;
                float hi = 7f;
                Vector3 best = dir * speed;
                bool ok = false;
                for (int i = 0; i < 18; i++)
                {
                    float vy = (lo + hi) / 2f;
                    Vector3 v = dir * speed + Vector3.up * vy;
                    bool landed = PredictLanding(from, v, s, table, out Vector3 landing, out _, out bool net);
                    float got = Vector3.Dot(new Vector3(landing.x - from.x, 0f, landing.z - from.z), dir);
                    best = v;
                    if (net || (!landed && got < want))
                    {
                        // ネットに掛かるか手前に落ちる:もっと上へ
                        lo = vy;
                        continue;
                    }

                    if (got > want)
                    {
                        hi = vy;
                    }
                    else
                    {
                        lo = vy;
                    }

                    ok = landed && Mathf.Abs(got - want) < 0.05f;
                    if (ok)
                    {
                        break;
                    }
                }

                velocity = best;
                if (ok)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
