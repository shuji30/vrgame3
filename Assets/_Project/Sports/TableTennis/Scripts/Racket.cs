using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球のラケット。「はじめる」で利き手のコントローラーに付き、トリガーを引き続けなくてよい(docs/plan.md「卓球」)。
    /// ボールとの当たりは PingPongPhysics が計算するので、ここでは毎フレームの面の姿勢と速さ(直近数フレームの平均)を出す。
    /// 先に動くよう実行順を早くし、ボール(PingPongBall)はそのあとで動く。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Racket : MonoBehaviour
    {
        [Tooltip("面の中心(この物の子)。青い矢印(Z)が面の向き")]
        [SerializeField] private Transform face;

        [Tooltip("コントローラーから見たラケットの位置(m)")]
        [SerializeField] private Vector3 holdPosition = new Vector3(0f, 0f, 0.02f);

        [Tooltip("コントローラーから見たラケットの向き(度)。持ちやすい角度に直す")]
        [SerializeField] private Vector3 holdRotation = new Vector3(0f, 0f, 0f);

        [Tooltip("速さを平均するフレーム数")]
        [Range(1, 5)]
        [SerializeField] private int averageFrames = 3;

        [Tooltip("打ったときの振動の強さ(ボールが遅いとき, 速いとき)")]
        [SerializeField] private Vector2 hitHaptic = new Vector2(0.3f, 0.9f);

        [Tooltip("打ったときの振動の長さ(秒)")]
        [SerializeField] private float hitHapticDuration = 0.04f;

        private XRBaseInputInteractor interactor;
        private readonly Vector3[] velocities = new Vector3[5];
        private readonly Vector3[] angularVelocities = new Vector3[5];
        private int sampleIndex;
        private int samples;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private bool hasLast;

        /// <summary>前のフレームの面。</summary>
        public PingPongPhysics.RacketPose Previous { get; private set; }

        /// <summary>今のフレームの面。</summary>
        public PingPongPhysics.RacketPose Current { get; private set; }

        /// <summary>手に付いているか。</summary>
        public bool IsEquipped => gameObject.activeSelf && transform.parent != null;

        /// <summary>ラケットを手に付ける。</summary>
        public void Equip(Transform hand, XRBaseInputInteractor handInteractor)
        {
            transform.SetParent(hand, false);
            transform.localPosition = holdPosition;
            transform.localRotation = Quaternion.Euler(holdRotation);
            interactor = handInteractor;
            hasLast = false;
            samples = 0;
            sampleIndex = 0;
            System.Array.Clear(velocities, 0, velocities.Length);
            System.Array.Clear(angularVelocities, 0, angularVelocities.Length);
            gameObject.SetActive(true);
        }

        /// <summary>手から外して隠す。</summary>
        public void Unequip()
        {
            transform.SetParent(null, true);
            gameObject.SetActive(false);
        }

        /// <summary>打ったときの振動(ボールの速さ 0〜25m/秒で強さを変える)。</summary>
        public void Haptic(float ballSpeed)
        {
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                float t = Mathf.InverseLerp(2f, 20f, ballSpeed);
                interactor.SendHapticImpulse(Mathf.Lerp(hitHaptic.x, hitHaptic.y, t), hitHapticDuration);
            }
        }

        private void Update()
        {
            Vector3 position = face.position;
            Quaternion rotation = face.rotation;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            if (!hasLast)
            {
                lastPosition = position;
                lastRotation = rotation;
                hasLast = true;
            }

            velocities[sampleIndex] = (position - lastPosition) / dt;
            Quaternion delta = rotation * Quaternion.Inverse(lastRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            angularVelocities[sampleIndex] = float.IsNaN(axis.x) || float.IsInfinity(axis.x)
                ? Vector3.zero
                : axis * (angle * Mathf.Deg2Rad / dt);
            sampleIndex = (sampleIndex + 1) % averageFrames;
            samples = Mathf.Min(samples + 1, averageFrames);

            Vector3 velocity = Vector3.zero;
            Vector3 angular = Vector3.zero;
            for (int i = 0; i < samples; i++)
            {
                velocity += velocities[i];
                angular += angularVelocities[i];
            }

            velocity /= Mathf.Max(1, samples);
            angular /= Mathf.Max(1, samples);

            Previous = new PingPongPhysics.RacketPose
            {
                Center = lastPosition,
                Normal = lastRotation * Vector3.forward,
                Velocity = velocity,
                AngularVelocity = angular,
            };
            Current = new PingPongPhysics.RacketPose
            {
                Center = position,
                Normal = rotation * Vector3.forward,
                Velocity = velocity,
                AngularVelocity = angular,
            };

            lastPosition = position;
            lastRotation = rotation;
        }
    }
}
