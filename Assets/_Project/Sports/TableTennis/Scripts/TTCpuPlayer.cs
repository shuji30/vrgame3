using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球の CPU(docs/plan.md M3「卓球の CPU」)。アニメーション素材を使わず、スクリプトだけで動かす。
    /// プレイヤーが打った瞬間に、ボールと同じ計算で先回りして打点と時刻を求め、体を左右に動かして手を振る。
    /// 決めた時刻にボールの速さを書き換えて返す(CPU のラケットは物理で当てない)。ミスは空振り・ネット・オーバーを 2:1:1。
    /// 体の見た目(仮人形や girl.vrm)は差し替えられるよう、ここでは体の根元と手の位置だけを動かす。
    /// </summary>
    public class TTCpuPlayer : MonoBehaviour
    {
        private enum Miss
        {
            None,
            Whiff,
            Net,
            Over,
        }

        [Tooltip("ボール")]
        [SerializeField] private PingPongBall ball;

        [Tooltip("体の根元(左右に動かす)")]
        [SerializeField] private Transform body;

        [Tooltip("ラケットを持つ手")]
        [SerializeField] private Transform racketHand;

        [Tooltip("ボールを投げ上げる手")]
        [SerializeField] private Transform tossHand;

        [Tooltip("体から見た、構えているときのラケットの手の位置(m)")]
        [SerializeField] private Vector3 handRest = new Vector3(0.3f, 1.0f, 0.25f);

        [Tooltip("体から見た、構えているときの投げ上げる手の位置(m)")]
        [SerializeField] private Vector3 tossRest = new Vector3(-0.3f, 0.95f, 0.25f);

        [Tooltip("強さ3段階(やさしい / ふつう / つよい)")]
        [SerializeField] private TTCpuLevel[] levels =
        {
            new TTCpuLevel { label = "やさしい", marks = "▲", color = new Color32(0x6C, 0xC3, 0x6A, 0xFF), reaction = 0.30f, moveSpeed = 1.5f, reach = 0.6f, returnSpeed = new Vector2(5f, 7f), scatter = 0.30f, missRate = 0.20f, fastMissBonus = 0.25f, serveSpeed = new Vector2(4f, 5f), aimStyle = 0 },
            new TTCpuLevel { label = "ふつう", marks = "▲▲", color = new Color32(0x3A, 0xA7, 0xFF, 0xFF), reaction = 0.18f, moveSpeed = 2.5f, reach = 0.75f, returnSpeed = new Vector2(7f, 10f), scatter = 0.18f, missRate = 0.08f, fastMissBonus = 0.12f, serveSpeed = new Vector2(5f, 7f), aimStyle = 1 },
            new TTCpuLevel { label = "つよい", marks = "▲▲▲", color = new Color32(0xFF, 0x8A, 0x3D, 0xFF), reaction = 0.10f, moveSpeed = 3.5f, reach = 0.9f, returnSpeed = new Vector2(9f, 13f), scatter = 0.10f, missRate = 0.03f, fastMissBonus = 0.05f, serveSpeed = new Vector2(7f, 9f), aimStyle = 2 },
        };

        [Header("見た目(強さの色と表情)")]
        [Tooltip("強さの色に塗る物(足元の敷物・ラケットのラバー)")]
        [SerializeField] private Renderer[] levelColorRenderers = new Renderer[0];

        [Tooltip("名札の背景(強さの色)")]
        [SerializeField] private Image nameTagBack;

        [Tooltip("名札の文字(「ふつう ▲▲」)")]
        [SerializeField] private TMP_Text nameTagText;

        [Tooltip("頭(失点でうつむく)")]
        [SerializeField] private Transform head;

        [Tooltip("目(得点で細める)")]
        [SerializeField] private Transform[] eyes = new Transform[0];

        [Tooltip("振りはじめから打つまでの秒数")]
        [SerializeField] private float swingTime = 0.25f;

        [Tooltip("台の端から立つ位置までの距離(m)")]
        [SerializeField] private float standBack = 0.55f;

        private int level = 1;
        private float baseY;
        private bool planned;
        private float planStart;
        private float hitTime;
        private Vector3 hitPoint;
        private Miss miss;
        private float targetX;
        private float reactTimer;
        private float reactJump;
        private bool serving;
        private float serveHitTime;

        private void Awake()
        {
            baseY = body != null ? body.position.y : 0f;
            eyeScales = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                eyeScales[i] = eyes[i] != null ? eyes[i].localScale : Vector3.one;
            }

            if (head != null)
            {
                headRest = head.localRotation;
            }
        }

        /// <summary>CPU がボールを打ったときに呼ばれる。</summary>
        public event Action Hit;

        public TTCpuLevel Level => levels[Mathf.Clamp(level, 0, levels.Length - 1)];

        /// <summary>強さを決める(0〜2)。</summary>
        public void SetLevel(int value)
        {
            level = Mathf.Clamp(value, 0, levels.Length - 1);
            TTCpuLevel l = Level;
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in levelColorRenderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, l.color);
                r.SetPropertyBlock(block);
            }

            if (nameTagBack != null)
            {
                nameTagBack.color = l.color;
            }

            if (nameTagText != null)
            {
                nameTagText.text = $"{l.label} {l.marks}";
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Vector3[] eyeScales = new Vector3[0];
        private Quaternion headRest;

        private PingPongPhysics.Table Table => ball.Table;

        private float StandZ => Table.Center.z + Table.Length / 2f + standBack;

        /// <summary>構えの位置に戻す(1点の始め)。</summary>
        public void ResetStance()
        {
            planned = false;
            serving = false;
            targetX = Table.Center.x;
            body.position = new Vector3(targetX, baseY, StandZ);
        }

        /// <summary>予定していた返球をやめる(ラリーをやり直すとき)。</summary>
        public void Cancel()
        {
            planned = false;
            serving = false;
        }

        /// <summary>
        /// プレイヤーが打った直後に呼ぶ。ボールの行き先を先回りして、返せるなら打点と時刻を決める。
        /// </summary>
        public void PlanReturn()
        {
            planned = false;
            PingPongPhysics.Settings s = ball.Physics;
            if (!PingPongPhysics.PredictBounce(ball.Position, ball.Velocity, s, Table, out Vector3 landing, out float landTime,
                    out _, out Vector3 v) || Table.SideOf(landing) != TTSide.Cpu)
            {
                // こちらのコートに入らない球は見送る
                return;
            }

            // 弾んだあとの道筋をたどり、打ちやすい高さ(頂点か、台の端を越えるところ)を打点にする
            Vector3 p = landing;
            float t = landTime;
            float bestY = float.MinValue;
            Vector3 best = p;
            float bestTime = t;
            var none = new PingPongPhysics.RacketPose();
            float endZ = Table.Center.z + Table.Length / 2f;
            for (int i = 0; i < 400; i++)
            {
                PingPongPhysics.Event e = PingPongPhysics.Step(ref p, ref v, s.step, s, Table, false, none, none);
                t += s.step;
                if (e.Kind == PingPongPhysics.EventKind.Floor || e.Kind == PingPongPhysics.EventKind.Table)
                {
                    break;
                }

                if (p.y > bestY)
                {
                    bestY = p.y;
                    best = p;
                    bestTime = t;
                }

                if (p.z > endZ + 0.15f || (v.y < 0f && p.y < bestY - 0.05f))
                {
                    break;
                }
            }

            hitPoint = best;
            hitTime = Time.time + bestTime;
            planStart = Time.time;
            // 体はプレイヤーの方(-Z)を向いているので、手の横のずれはワールドの向きで測る
            float handOffsetX = body.TransformVector(handRest).x;
            targetX = hitPoint.x - handOffsetX;
            float incoming = ball.Velocity.magnitude;
            TTCpuLevel l = Level;

            // 届くか(反応してから動ける距離 + 腕の届く範囲)
            float canMove = Mathf.Max(0f, bestTime - l.reaction) * l.moveSpeed;
            bool reachable = Mathf.Abs(hitPoint.x - body.position.x - handOffsetX) <= canMove + l.reach;
            float missChance = l.missRate + (incoming >= 10f ? l.fastMissBonus : 0f);
            miss = !reachable ? Miss.Whiff : Random.value < missChance ? PickMiss() : Miss.None;
            planned = true;
        }

        /// <summary>CPU のサーブ。投げ上げてから打つ。</summary>
        public void Serve()
        {
            ResetStance();
            Vector3 from = body.TransformPoint(tossRest);
            ball.Launch(from, new Vector3(0f, 2.6f, 0f));
            serving = true;
            serveHitTime = Time.time + 0.5f;
        }

        /// <summary>得点・失点の反応(得点なら小さく跳ね、失点なら少しうなだれる)。</summary>
        public void React(bool won)
        {
            reactTimer = 0.6f;
            reactJump = won ? 1f : -1f;
        }

        private void Update()
        {
            TTCpuLevel l = Level;
            float dt = Time.deltaTime;

            if (serving && Time.time >= serveHitTime)
            {
                serving = false;
                if (ball.IsFlying)
                {
                    Vector3 target = PlayerCourtTarget(l, true);
                    float speed = Random.Range(l.serveSpeed.x, l.serveSpeed.y);
                    PingPongPhysics.SolveShot(ball.Position, target, speed, ball.Physics, Table, out Vector3 v);
                    ball.Launch(ball.Position, v);
                    Hit?.Invoke();
                }
            }

            // 体を打点の方へ動かす(反応の時間がたってから)
            if (planned && Time.time - planStart >= l.reaction)
            {
                Vector3 p = body.position;
                float x = Mathf.MoveTowards(p.x, targetX, l.moveSpeed * dt);
                body.position = new Vector3(x, p.y, StandZ);
            }

            UpdateHands();
            UpdateReaction(dt);

            if (planned && Time.time >= hitTime)
            {
                planned = false;
                TryHit(l);
            }
        }

        private void TryHit(TTCpuLevel l)
        {
            if (!ball.IsFlying || miss == Miss.Whiff)
            {
                return;
            }

            // 実際のボールが打点の近くにあるか(届かなければ空振り)
            Vector3 hand = body.TransformPoint(handRest);
            if (Mathf.Abs(ball.Position.x - hand.x) > l.reach || Table.SideOf(ball.Position) != TTSide.Cpu)
            {
                return;
            }

            Vector3 target = PlayerCourtTarget(l, false);
            float speed = Random.Range(l.returnSpeed.x, l.returnSpeed.y);
            PingPongPhysics.SolveShot(ball.Position, target, speed, ball.Physics, Table, out Vector3 v);
            if (miss == Miss.Net)
            {
                v.y -= 1.5f;
            }
            else if (miss == Miss.Over)
            {
                v = new Vector3(v.x * 1.4f, v.y + 0.8f, v.z * 1.4f);
            }

            ball.Launch(ball.Position, v);
            Hit?.Invoke();
        }

        /// <summary>プレイヤーのコートでねらう点(強さのねらい方とばらつき)。</summary>
        private Vector3 PlayerCourtTarget(TTCpuLevel l, bool serve)
        {
            PingPongPhysics.Table t = Table;
            float halfW = t.Width / 2f - 0.12f;
            float nearZ = t.Center.z - 0.35f;
            float farZ = t.Center.z - t.Length / 2f + 0.15f;
            float x;
            float z;
            switch (l.aimStyle)
            {
                case 0:
                    x = Random.Range(-0.2f, 0.2f);
                    z = Random.Range(-0.95f, -0.75f);
                    break;
                case 2:
                    // 隅か、プレイヤーのいない側
                    Transform head = Camera.main != null ? Camera.main.transform : null;
                    float away = head != null ? -Mathf.Sign(head.position.x - t.Center.x) : (Random.value < 0.5f ? -1f : 1f);
                    x = away * Random.Range(0.45f, halfW);
                    z = Random.Range(farZ + 0.05f, farZ + 0.35f) - t.Center.z;
                    break;
                default:
                    x = Random.Range(-0.5f, 0.5f);
                    z = Random.Range(-1.1f, -0.65f);
                    break;
            }

            if (serve)
            {
                z = Mathf.Min(z, -0.6f);
            }

            Vector2 jitter = Random.insideUnitCircle * l.scatter;
            x = Mathf.Clamp(x + jitter.x, -halfW, halfW);
            z = Mathf.Clamp(z + jitter.y, farZ - t.Center.z, nearZ - t.Center.z);
            return new Vector3(t.Center.x + x, t.Top, t.Center.z + z);
        }

        private Miss PickMiss()
        {
            float r = Random.value * 4f;
            return r < 2f ? Miss.Whiff : r < 3f ? Miss.Net : Miss.Over;
        }

        /// <summary>手の動き:ふだんは構え、打つ前に引いてから打点へ振り出す。</summary>
        private void UpdateHands()
        {
            if (racketHand != null)
            {
                Vector3 rest = body.TransformPoint(handRest);
                Vector3 goal = rest;
                if (planned)
                {
                    float toHit = hitTime - Time.time;
                    if (toHit < swingTime && toHit > 0f)
                    {
                        float k = 1f - toHit / swingTime;
                        Vector3 back = rest + body.forward * -0.25f + Vector3.up * 0.05f;
                        Vector3 contact = new Vector3(hitPoint.x, hitPoint.y, hitPoint.z + 0.05f);
                        goal = k < 0.4f ? Vector3.Lerp(rest, back, k / 0.4f) : Vector3.Lerp(back, contact, (k - 0.4f) / 0.6f);
                    }
                }

                racketHand.position = Vector3.Lerp(racketHand.position, goal, 0.5f);
            }

            if (tossHand != null)
            {
                Vector3 rest = body.TransformPoint(tossRest);
                tossHand.position = Vector3.Lerp(tossHand.position, rest, 0.3f);
            }
        }

        private void UpdateReaction(float dt)
        {
            if (reactTimer <= 0f)
            {
                return;
            }

            reactTimer -= dt;
            float k = Mathf.Sin(Mathf.Clamp01(1f - reactTimer / 0.6f) * Mathf.PI);
            Vector3 p = body.position;
            // 得点は 0.12m 跳ねて目を細め、失点は少し沈んで頭を前へ 20° 傾ける(docs/art-direction.md の反応)
            float y = reactJump > 0f ? k * 0.12f : -k * 0.05f;
            body.position = new Vector3(p.x, baseY + y, p.z);
            bool done = reactTimer <= 0f;
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] != null)
                {
                    Vector3 rest = eyeScales[i];
                    eyes[i].localScale = reactJump > 0f && !done ? new Vector3(rest.x, rest.y * 0.3f, rest.z) : rest;
                }
            }

            if (head != null)
            {
                head.localRotation = reactJump < 0f && !done ? headRest * Quaternion.Euler(20f * k, 0f, 0f) : headRest;
            }
        }

    }
}
