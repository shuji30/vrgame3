using UnityEngine;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボウリングのボールを投げやすくする補正。ThrowableObject と同じボールに付ける。
    /// ・上下の向きを捨てて、レーンに沿った水平な投げにする(振り上げながら離しても上に飛ばず、床に叩きつけもしない)
    /// ・左右のぶれをレーンの向きに寄せる(狙いの細かい調整は残す)
    /// </summary>
    public class BowlingThrowAssist : MonoBehaviour, IThrowAdjuster
    {
        [Tooltip("レーンの向きの基準。ファウルラインを指定し、その青い矢印(Z+)をピンの方向とする")]
        [SerializeField] private Transform laneDirection;

        [Tooltip("左右のぶれをどれだけレーンの向きに寄せるか。0 で補正なし、1 で常にまっすぐ")]
        [Range(0f, 1f)]
        [SerializeField] private float directionAssist = 0.6f;

        [Tooltip("補正したあとの、レーンの向きからの最大の角度(度)。先頭のピンまで 18m なので、3° でおよそ 1m ずれる")]
        [Range(0f, 15f)]
        [SerializeField] private float maxAngle = 4f;

        [Tooltip("上下の速さのうち、前に進む速さに足す割合。振り下ろす勢いも前への勢いとして生かす。0 で上下の速さを捨てる")]
        [Range(0f, 1f)]
        [SerializeField] private float verticalToForward = 0.5f;

        [Tooltip("投げたとみなす最低の速さ(m/秒)。これより遅ければ補正せず、そのまま落とす(置き台に戻すときなど)")]
        [SerializeField] private float minSpeed = 0.8f;

        /// <summary>レーンの向きの基準を設定する(シーンを組み立てるときに使う)。</summary>
        public Transform LaneDirection
        {
            get => laneDirection;
            set => laneDirection = value;
        }

        public void AdjustThrow(ref Vector3 velocity, ref Vector3 angularVelocity)
        {
            if (laneDirection == null || velocity.magnitude < minSpeed)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(laneDirection.forward, Vector3.up).normalized;
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);

            // 前に進んでいないとき(後ろへ振りながら離したなど)は投げとみなさない
            if (Vector3.Dot(horizontal, forward) <= 0f)
            {
                return;
            }

            // 左右の角度を縮めてから上限で切る
            float angle = Vector3.SignedAngle(forward, horizontal, Vector3.up);
            float adjustedAngle = Mathf.Clamp(angle * (1f - directionAssist), -maxAngle, maxAngle);
            Vector3 direction = Quaternion.AngleAxis(adjustedAngle, Vector3.up) * forward;

            // 上下の勢いの一部を前への勢いに回す
            float speed = horizontal.magnitude + Mathf.Abs(velocity.y) * verticalToForward;
            velocity = direction * speed;

            // 手首のひねりの回転は、向きが狂う原因になるので今は捨てる(カーブは後で別に扱う)。
            // 転がる回転はレーンとの摩擦で自然に付く
            angularVelocity = Vector3.zero;
        }
    }
}
