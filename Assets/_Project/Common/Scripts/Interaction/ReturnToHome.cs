using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRGame3.Common
{
    /// <summary>
    /// 投げた物を自動で元の位置に戻す部品。
    /// 元の位置から離れた所で止まって一定時間たったとき、または床より下に落ちたときに、
    /// ゲーム開始時の位置と向きへ戻す。手に持っている間は何もしない。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ReturnToHome : MonoBehaviour
    {
        [Header("戻すタイミング")]
        [Tooltip("止まってから元の位置に戻すまでの秒数")]
        [Range(0.5f, 10f)]
        [SerializeField] private float stopDelay = 3f;

        [Tooltip("この速さ(m/秒)より遅ければ「止まった」とみなす")]
        [SerializeField] private float stopSpeed = 0.05f;

        [Tooltip("元の位置からこの距離(m)以内なら、止まっていても戻さない(置き台の上で転がった程度は無視する)")]
        [SerializeField] private float ignoreDistance = 0.3f;

        [Tooltip("この高さ(m)より下に落ちたら、止まるのを待たずにすぐ戻す")]
        [SerializeField] private float fallHeight = -2f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private ThrowableObject throwable;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float stoppedTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            throwable = GetComponent<ThrowableObject>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            // 手に持っている間(XRI がキネマティックにしている間も含む)は何もしない
            if ((grab != null && grab.isSelected) || body.isKinematic)
            {
                stoppedTime = 0f;
                return;
            }

            if (body.position.y < fallHeight)
            {
                ReturnNow();
                return;
            }

            bool isStopped = body.linearVelocity.magnitude < stopSpeed;
            bool isAway = (body.position - homePosition).sqrMagnitude > ignoreDistance * ignoreDistance;
            if (!isStopped || !isAway)
            {
                stoppedTime = 0f;
                return;
            }

            stoppedTime += Time.fixedDeltaTime;
            if (stoppedTime >= stopDelay)
            {
                ReturnNow();
            }
        }

        /// <summary>
        /// すぐに元の位置へ戻す。Inspector の右上メニュー(⋮)からも呼べる。
        /// </summary>
        [ContextMenu("元の位置に戻す")]
        public void ReturnNow()
        {
            stoppedTime = 0f;

            // 戻した先(置き台)への着地を、投げた結果として数えないようにする
            if (throwable != null)
            {
                throwable.CancelThrow();
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = homePosition;
            body.rotation = homeRotation;
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }
    }
}
