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

        [Tooltip("光らせるときの色(発光色)")]
        [SerializeField] private Color glowColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("光らせる見た目。空なら子の Renderer を使う")]
        [SerializeField] private Renderer glowRenderer;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock glowBlock;
        private Rigidbody body;
        private Vector3 spotPosition;
        private Quaternion spotRotation;

        /// <summary>並べ直した回数。演出が「並べ直す前のピン」にだけ効くよう、途中で変わったら止めるのに使う。</summary>
        public int ResetCount { get; private set; }

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
            if (glowRenderer == null)
            {
                glowRenderer = GetComponentInChildren<Renderer>();
            }

            body.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);
            spotPosition = transform.position;
            spotRotation = transform.rotation;
        }

        /// <summary>置いた位置に立て直す。</summary>
        public void ResetToSpot()
        {
            gameObject.SetActive(true);
            ResetCount++;
            SetGlow(0f);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spotPosition;
            body.rotation = spotRotation;
            transform.SetPositionAndRotation(spotPosition, spotRotation);
            body.Sleep();
        }

        /// <summary>
        /// ピンの白い胴を光らせる(0 で消灯、1 で最大)。マテリアルの発光(Emission)を有効にしておくこと。
        /// </summary>
        public void SetGlow(float amount)
        {
            if (glowRenderer == null)
            {
                return;
            }

            glowBlock ??= new MaterialPropertyBlock();
            glowRenderer.GetPropertyBlock(glowBlock, 0);
            glowBlock.SetColor(EmissionColorId, glowColor * Mathf.Max(0f, amount));
            glowRenderer.SetPropertyBlock(glowBlock, 0);
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
