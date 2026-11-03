using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// メニューや案内のパネルを、開いた瞬間の頭の正面に1回だけ置くための計算(頭には追従させない)。
    /// </summary>
    public static class PanelPlacement
    {
        /// <summary>
        /// 頭の水平な正面 distance m 先、目の高さから drop m 下の位置と、頭の方を向いた向き(パネルの表が頭へ向く)。
        /// </summary>
        public static Pose InFrontOfHead(Transform head, float distance, float drop)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                // 真上・真下を向いているときは頭の上方向から前を決める
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            forward.Normalize();
            Vector3 position = head.position + forward * distance + Vector3.down * drop;
            // ワールド空間の Canvas は +Z が奥なので、頭から見て奥向きにする
            return new Pose(position, Quaternion.LookRotation(position - head.position, Vector3.up));
        }

        /// <summary>メインカメラ(頭)。見つからなければ null。</summary>
        public static Transform FindHead()
        {
            Camera camera = Camera.main;
            return camera != null ? camera.transform : null;
        }
    }
}
