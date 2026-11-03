using System;
using UnityEngine;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// 放った矢。重力で山なりに飛び、先を飛ぶ向きへ向ける。当たりは前のフレームの位置からのレイで取る
    /// (高速ですり抜けないため。docs/plan.md「アーチェリー」)。この物の原点が矢の先。
    /// 的に刺さったら的の子にして残し、それ以外に刺さったら少しして消す。
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        /// <summary>矢が止まった結果。</summary>
        public struct Hit
        {
            public bool OnTarget;
            public int Score;
            public bool IsX;
            public Vector3 Point;
            public ArcheryTarget Target;
        }

        [Tooltip("刺さったときに先が面の奥へ入る深さ(m)")]
        [SerializeField] private float penetration = 0.06f;

        [Tooltip("的以外に刺さった矢を消すまでの秒数")]
        [SerializeField] private float missLifetime = 5f;

        [Tooltip("どこにも当たらずに飛び続けたら消す秒数")]
        [SerializeField] private float maxFlightTime = 3f;

        [Tooltip("射場の奥(Z)をこれより越えたら消す(m)")]
        [SerializeField] private float killDistance = 45f;

        [Tooltip("ここより下に落ちたら消す高さ(m)")]
        [SerializeField] private float killHeight = -5f;

        private Vector3 velocity;
        private float gravityScale = 1f;
        private LayerMask hitLayers = ~0;
        private float flightTime;
        private bool flying;
        private Action<Arrow, Hit> onStopped;

        /// <summary>飛んでいる途中か。</summary>
        public bool IsFlying => flying;

        /// <summary>矢を放つ。止まったら onStopped を1回だけ呼ぶ。</summary>
        public void Launch(Vector3 initialVelocity, float gravity, LayerMask layers, Action<Arrow, Hit> stopped)
        {
            transform.SetParent(null, true);
            velocity = initialVelocity;
            gravityScale = gravity;
            hitLayers = layers;
            onStopped = stopped;
            flightTime = 0f;
            flying = true;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }
        }

        private void Update()
        {
            if (!flying)
            {
                return;
            }

            float dt = Time.deltaTime;
            flightTime += dt;
            Vector3 start = transform.position;
            velocity += Physics.gravity * (gravityScale * dt);
            Vector3 step = velocity * dt;
            float distance = step.magnitude;

            if (distance > 0f && TryHit(start, step / distance, distance, out RaycastHit hit))
            {
                Stick(hit, step / distance);
                return;
            }

            transform.position = start + step;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }

            if (flightTime > maxFlightTime || transform.position.y < killHeight || transform.position.z > killDistance)
            {
                Stop(new Hit { OnTarget = false, Point = transform.position });
                Destroy(gameObject);
            }
        }

        /// <summary>一番近い当たり(自分の体や手 = XR Origin の子は除く)。</summary>
        private bool TryHit(Vector3 start, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, distance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < best && hit.collider.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() == null)
                {
                    best = hit.distance;
                    nearest = hit;
                }
            }

            return best < float.MaxValue;
        }

        private void Stick(RaycastHit hit, Vector3 direction)
        {
            transform.position = hit.point + direction * penetration;
            transform.rotation = Quaternion.LookRotation(direction);

            var target = hit.collider.GetComponentInParent<ArcheryTarget>();
            if (target != null)
            {
                // 拡大縮小していない的の親の子にする(畳の Cube の子にすると形がゆがむ)
                transform.SetParent(target.transform, true);
                int score = target.ScoreAt(hit.point, out bool isX);
                target.AddStuckArrow(transform);
                Stop(new Hit { OnTarget = true, Score = score, IsX = isX, Point = hit.point, Target = target });
            }
            else
            {
                Stop(new Hit { OnTarget = false, Point = hit.point });
                Destroy(gameObject, missLifetime);
            }
        }

        private void Stop(Hit result)
        {
            flying = false;
            Action<Arrow, Hit> callback = onStopped;
            onStopped = null;
            callback?.Invoke(this, result);
        }
    }
}
