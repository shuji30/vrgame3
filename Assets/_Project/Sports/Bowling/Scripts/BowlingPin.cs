using UnityEngine;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボウリングのピン1本。置いた位置を覚えておき、倒れたかどうかを判定する。
    /// 傾きが一定以上か、ピン置き場から落ちたら「倒れた」とみなす。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BowlingPin : MonoBehaviour
    {
        [Tooltip("この角度(度)以上傾いたら倒れたとみなす")]
        [Range(5f, 80f)]
        [SerializeField] private float knockedAngle = 30f;

        [Tooltip("置いた高さからこの距離(m)以上下がったら倒れたとみなす(ピットに落ちた場合など)")]
        [SerializeField] private float knockedDrop = 0.1f;

        [Tooltip("重心の高さ(m、ピンの底から)。低いほど倒れにくい。本物のピンは高さ 38cm に対しておよそ 14cm")]
        [Range(0.05f, 0.3f)]
        [SerializeField] private float centerOfMassHeight = 0.14f;

        private Rigidbody body;
        private Vector3 spotPosition;
        private Quaternion spotRotation;

        /// <summary>倒れているか。片付けて非表示にしたピンも倒れた扱い。</summary>
        public bool IsKnockedDown
        {
            get
            {
                if (!gameObject.activeSelf)
                {
                    return true;
                }

                float tilt = Vector3.Angle(transform.up, Vector3.up);
                return tilt >= knockedAngle || transform.position.y < spotPosition.y - knockedDrop;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);
            spotPosition = transform.position;
            spotRotation = transform.rotation;
        }

        /// <summary>置いた位置に立て直す。</summary>
        public void ResetToSpot()
        {
            gameObject.SetActive(true);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spotPosition;
            body.rotation = spotRotation;
            transform.SetPositionAndRotation(spotPosition, spotRotation);
            body.Sleep();
        }

        /// <summary>倒れたピンを片付ける(非表示にする)。</summary>
        public void Remove()
        {
            gameObject.SetActive(false);
        }

        /// <summary>立っているピンの揺れを止める。2投目の前に呼ぶ。</summary>
        public void Calm()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
