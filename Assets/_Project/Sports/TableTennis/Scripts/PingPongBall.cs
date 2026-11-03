using System;
using UnityEngine;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球のボール。物理エンジンを使わず PingPongPhysics で動かし、台・ネット・床・ラケットに当たったら知らせる。
    /// 状態は「隠れている」「手に持っている(サーブ前)」「飛んでいる」の3つ。ラケットのあとに動く。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PingPongBall : MonoBehaviour
    {
        private enum Mode
        {
            Hidden,
            Held,
            Flying,
        }

        [Tooltip("ボールの動きの決まり")]
        [SerializeField] private PingPongPhysics.Settings physics = new PingPongPhysics.Settings();

        [Tooltip("プレイヤーのラケット")]
        [SerializeField] private Racket racket;

        [Tooltip("ラケットに当たったあと、同じラケットに当たらない秒数")]
        [SerializeField] private float rehitDelay = 0.15f;

        [Tooltip("手に持っている間、手から見たボールの位置(m)")]
        [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0.03f, 0.06f);

        private Mode mode = Mode.Hidden;
        private Transform holdHand;
        private Vector3 position;
        private Vector3 velocity;
        private float lastRacketHit = -10f;
        private Vector3 lastHeldPosition;
        private Vector3 heldVelocity;
        private readonly System.Collections.Generic.List<PingPongPhysics.Event> events =
            new System.Collections.Generic.List<PingPongPhysics.Event>();

        /// <summary>台・ネット・床・ラケットに当たったときに呼ばれる。</summary>
        public event Action<PingPongPhysics.Event> Bounced;

        /// <summary>台の形(進行役が身長に合わせて設定する)。</summary>
        public PingPongPhysics.Table Table { get; set; } = PingPongPhysics.Table.Standard(new Vector3(0f, 0.76f, 0f));

        public PingPongPhysics.Settings Physics => physics;

        public bool IsFlying => mode == Mode.Flying;
        public bool IsHeld => mode == Mode.Held;
        public Vector3 Position => position;
        public Vector3 Velocity => velocity;

        /// <summary>飛んでいる時間(迷子の判定に使う)。</summary>
        public float FlightTime { get; private set; }

        private void Awake()
        {
            Hide();
        }

        /// <summary>ボールを隠す。</summary>
        public void Hide()
        {
            mode = Mode.Hidden;
            holdHand = null;
            gameObject.SetActive(false);
        }

        /// <summary>手の中にボールを出して持たせる(サーブの前)。</summary>
        public void Hold(Transform hand)
        {
            holdHand = hand;
            mode = Mode.Held;
            position = hand.TransformPoint(holdOffset);
            lastHeldPosition = position;
            heldVelocity = Vector3.zero;
            transform.position = position;
            gameObject.SetActive(true);
        }

        /// <summary>持っているボールを、手の速さのまま離す。</summary>
        public void ReleaseFromHand()
        {
            if (mode != Mode.Held)
            {
                return;
            }

            Launch(position, heldVelocity);
        }

        /// <summary>決めた位置と速さで飛ばす(CPU が打つとき、補助で直すときも使う)。</summary>
        public void Launch(Vector3 from, Vector3 initialVelocity)
        {
            position = from;
            velocity = initialVelocity;
            mode = Mode.Flying;
            holdHand = null;
            FlightTime = 0f;
            transform.position = position;
            gameObject.SetActive(true);
        }

        /// <summary>飛んでいる速さだけを書き換える(入りやすさの補助)。</summary>
        public void SetVelocity(Vector3 newVelocity)
        {
            velocity = newVelocity;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (mode == Mode.Held)
            {
                if (holdHand == null)
                {
                    Hide();
                    return;
                }

                position = holdHand.TransformPoint(holdOffset);
                heldVelocity = Vector3.Lerp(heldVelocity, (position - lastHeldPosition) / Mathf.Max(dt, 0.0001f), 0.6f);
                lastHeldPosition = position;
                transform.position = position;
                return;
            }

            if (mode != Mode.Flying || dt <= 0f)
            {
                return;
            }

            FlightTime += dt;
            bool canHit = racket != null && racket.IsEquipped && Time.time - lastRacketHit >= rehitDelay;
            PingPongPhysics.RacketPose prev = canHit ? racket.Previous : default;
            PingPongPhysics.RacketPose now = canHit ? racket.Current : default;
            events.Clear();
            PingPongPhysics.Advance(ref position, ref velocity, dt, physics, Table, canHit, prev, now, events);
            transform.position = position;

            // 1フレームに起きた出来事を順にすべて知らせる(弾んだ直後に打ったときも取りこぼさない)
            foreach (PingPongPhysics.Event e in events)
            {
                if (e.Kind == PingPongPhysics.EventKind.Racket)
                {
                    lastRacketHit = Time.time;
                    racket.Haptic(e.Speed);
                }

                if (e.Kind == PingPongPhysics.EventKind.Floor)
                {
                    // 床に落ちたら止める(拾わせない。進行役が消す)
                    mode = Mode.Hidden;
                }

                Bounced?.Invoke(e);
                if (mode != Mode.Flying)
                {
                    break;
                }
            }
        }
    }
}
