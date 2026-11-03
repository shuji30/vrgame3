using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRGame3.Common
{
    /// <summary>
    /// 掴んで投げる物の共通部品。XRGrabInteractable と一緒に付ける。
    /// 掴んでいる間の動きを記録し、離した瞬間の速度に倍率をかけて投げる。
    /// XRI 標準の「離したときに投げる」は使わず、この部品で速度を決める。
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableObject : MonoBehaviour
    {
        [Header("投げる強さ")]
        [Tooltip("離した瞬間の手の速さにかける倍率。実際の腕振りは小さいので 1 より大きくすると「強く投げた感」が出る")]
        [Range(0.5f, 3f)]
        [SerializeField] private float velocityScale = 1.3f;

        [Tooltip("離した瞬間の回転の速さにかける倍率")]
        [Range(0f, 3f)]
        [SerializeField] private float angularVelocityScale = 1f;

        [Tooltip("投げる速さの上限(m/秒)。手がぶれて異常に速く飛ぶのを防ぐ")]
        [SerializeField] private float maxSpeed = 15f;

        [Tooltip("回転の速さの上限(ラジアン/秒)")]
        [SerializeField] private float maxAngularSpeed = 30f;

        [Header("速度の計算")]
        [Tooltip("離す直前の何秒間の動きから速度を求めるか。短いと敏感、長いと安定する")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float sampleWindow = 0.08f;

        /// <summary>手から離れたとき。引数は離した位置。</summary>
        public event Action<ThrowableObject, Vector3> Released;

        /// <summary>離したあと最初に何かに当たったとき。引数は当たった位置。</summary>
        public event Action<ThrowableObject, Vector3> FirstImpact;

        private const int MaxSamples = 64;

        private XRGrabInteractable grab;
        private Rigidbody body;

        // 掴んでいる間の位置と向きの履歴(リングバッファ)
        private readonly float[] sampleTimes = new float[MaxSamples];
        private readonly Vector3[] samplePositions = new Vector3[MaxSamples];
        private readonly Quaternion[] sampleRotations = new Quaternion[MaxSamples];
        private int sampleCount;
        private int sampleHead;

        private bool isHeld;
        private bool hasPendingThrow;
        private Vector3 pendingVelocity;
        private Vector3 pendingAngularVelocity;
        private bool waitingForFirstImpact;

        /// <summary>いま手に持たれているか。</summary>
        public bool IsHeld => isHeld;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();

            // 投げる速度はこの部品で決めるので、XRI 側の投げ処理は止める
            grab.throwOnDetach = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        private void Update()
        {
            if (isHeld)
            {
                AddSample(Time.time, transform.position, transform.rotation);
            }
        }

        private void FixedUpdate()
        {
            // XRI が Rigidbody を物理挙動に戻してから速度を入れる(キネマティックのままだと速度が無視される)
            if (!hasPendingThrow || isHeld || body.isKinematic)
            {
                return;
            }

            // 速い物のすり抜けを防ぐため、手を離れている間は連続衝突判定にする
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = pendingVelocity;
            body.angularVelocity = pendingAngularVelocity;
            hasPendingThrow = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!waitingForFirstImpact || isHeld)
            {
                return;
            }

            waitingForFirstImpact = false;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            FirstImpact?.Invoke(this, point);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isHeld = true;
            hasPendingThrow = false;
            waitingForFirstImpact = false;
            sampleCount = 0;
            sampleHead = 0;

            // 掴んでいる間はキネマティックになることがあるので、対応している判定方式に戻す
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // 別の手に持ち替えたときは投げない
            if (grab.isSelected)
            {
                return;
            }

            isHeld = false;
            AddSample(Time.time, transform.position, transform.rotation);
            EstimateVelocity(out Vector3 velocity, out Vector3 angularVelocity);

            pendingVelocity = Vector3.ClampMagnitude(velocity * velocityScale, maxSpeed);
            pendingAngularVelocity = Vector3.ClampMagnitude(angularVelocity * angularVelocityScale, maxAngularSpeed);
            hasPendingThrow = true;
            waitingForFirstImpact = true;

            Released?.Invoke(this, transform.position);
        }

        private void AddSample(float time, Vector3 position, Quaternion rotation)
        {
            sampleTimes[sampleHead] = time;
            samplePositions[sampleHead] = position;
            sampleRotations[sampleHead] = rotation;
            sampleHead = (sampleHead + 1) % MaxSamples;
            sampleCount = Mathf.Min(sampleCount + 1, MaxSamples);
        }

        /// <summary>
        /// 直近 sampleWindow 秒の最初と最後の記録から、速度と角速度を求める。
        /// </summary>
        private void EstimateVelocity(out Vector3 velocity, out Vector3 angularVelocity)
        {
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            if (sampleCount < 2)
            {
                return;
            }

            int newest = (sampleHead - 1 + MaxSamples) % MaxSamples;
            float newestTime = sampleTimes[newest];

            // 窓の中で一番古い記録を探す
            int oldest = newest;
            for (int i = 1; i < sampleCount; i++)
            {
                int index = (newest - i + MaxSamples) % MaxSamples;
                if (newestTime - sampleTimes[index] > sampleWindow)
                {
                    break;
                }

                oldest = index;
            }

            float dt = newestTime - sampleTimes[oldest];
            if (oldest == newest || dt <= Mathf.Epsilon)
            {
                return;
            }

            velocity = (samplePositions[newest] - samplePositions[oldest]) / dt;

            Quaternion delta = sampleRotations[newest] * Quaternion.Inverse(sampleRotations[oldest]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
            {
                angularVelocity = axis * (angle * Mathf.Deg2Rad / dt);
            }
        }
    }
}
