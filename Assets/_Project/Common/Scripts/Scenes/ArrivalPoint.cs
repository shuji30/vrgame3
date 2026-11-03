using Unity.XR.CoreUtils;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 決まったシーンから戻ってきたときの出現位置(ハブでは「前に入った会場の看板の前」)。
    /// 前のシーンの名前が fromScene と同じなら、頭がこの位置の真上に来て、この向き(青い矢印 Z+)を向くように XR Origin を動かす。
    /// 切り替えのフェードで覆っている間に動かすので、プレイヤーには視点が動いて見えない。
    /// </summary>
    public class ArrivalPoint : MonoBehaviour
    {
        [Tooltip("このシーンから戻ってきたときにここへ出す(シーンの名前)")]
        [SerializeField] private string fromScene;

        [Tooltip("動かす XR Origin。空ならシーンから探す")]
        [SerializeField] private XROrigin xrOrigin;

        private void Start()
        {
            if (string.IsNullOrEmpty(fromScene) || fromScene != SceneTransition.PreviousScene)
            {
                return;
            }

            if (xrOrigin == null)
            {
                xrOrigin = FindFirstObjectByType<XROrigin>();
            }

            if (xrOrigin == null || xrOrigin.Camera == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward);
            Vector3 head = xrOrigin.Camera.transform.position;
            xrOrigin.MoveCameraToWorldLocation(new Vector3(transform.position.x, head.y, transform.position.z));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
    }
}
