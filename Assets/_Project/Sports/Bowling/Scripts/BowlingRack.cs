using UnityEngine;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボール置き台。身長に合わせて台の高さを、利き手に合わせて置く側(レーンの右か左)を変える。
    /// 小型スコア板も置き台と同じ側へ動かす。設定の保存は M2。
    /// </summary>
    public class BowlingRack : MonoBehaviour
    {
        [Tooltip("台の本体(高さに合わせて縦に伸び縮みさせる)")]
        [SerializeField] private Transform table;

        [Tooltip("台の上のレール")]
        [SerializeField] private Transform[] rails = new Transform[0];

        [Tooltip("置き台に載せるボール")]
        [SerializeField] private ThrowableObject[] balls = new ThrowableObject[0];

        [Tooltip("置き台と一緒に左右を入れ替える小型スコア板")]
        [SerializeField] private Transform smallScoreBoard;

        [Header("位置")]
        [Tooltip("右利きのときの置き台の X(レーンの右、2レーンの間)")]
        [SerializeField] private float rightHandX = 0.9f;

        [Tooltip("左利きのときの置き台の X(レーンの左)")]
        [SerializeField] private float leftHandX = -0.9f;

        [Header("高さ")]
        [Tooltip("台の高さ = 身長 × この割合。0.47 で身長 170cm のとき 0.8m(腰の高さ)")]
        [Range(0.3f, 0.6f)]
        [SerializeField] private float heightRatio = 0.47f;

        [Tooltip("台の高さの下限(m)")]
        [SerializeField] private float minHeight = 0.55f;

        [Tooltip("台の高さの上限(m)")]
        [SerializeField] private float maxHeight = 1.0f;

        [Tooltip("ボールの直径(m)")]
        [SerializeField] private float ballDiameter = 0.218f;

        // 組み立てたとき(右利き)の、置き台から見た小型スコア板の左右のずれと向き
        private float boardOffsetX;
        private Quaternion boardRotation;

        private void Awake()
        {
            if (smallScoreBoard != null)
            {
                boardOffsetX = smallScoreBoard.position.x - rightHandX;
                boardRotation = smallScoreBoard.rotation;
            }
        }

        /// <summary>
        /// 身長(cm)と利き手に合わせて置き台を動かし、ボールを載せ直す。
        /// </summary>
        public void Apply(float heightCm, bool leftHanded)
        {
            float rackHeight = Mathf.Clamp(heightCm / 100f * heightRatio, minHeight, maxHeight);
            float x = leftHanded ? leftHandX : rightHandX;
            Vector3 basePosition = transform.position;

            // 小型スコア板は置き台のプレイヤー側(レーンの内側)に置く。組み立てたとき(右利き)の位置を左右反転する
            if (smallScoreBoard != null)
            {
                float boardX = x + (leftHanded ? -boardOffsetX : boardOffsetX);
                smallScoreBoard.position = new Vector3(boardX, smallScoreBoard.position.y, smallScoreBoard.position.z);
                Vector3 euler = boardRotation.eulerAngles;
                smallScoreBoard.rotation = leftHanded ? Quaternion.Euler(euler.x, -euler.y, euler.z) : boardRotation;
            }

            // 子(台・レール・ボール)は親と一緒に動くので、レールの左右のずれは動かす前に測っておく
            var railOffsets = new float[rails.Length];
            for (int i = 0; i < rails.Length; i++)
            {
                railOffsets[i] = rails[i] != null ? rails[i].position.x - basePosition.x : 0f;
            }

            transform.position = new Vector3(x, basePosition.y, basePosition.z);

            if (table != null)
            {
                Vector3 scale = table.localScale;
                table.localScale = new Vector3(scale.x, rackHeight, scale.z);
                table.position = new Vector3(x, transform.position.y + rackHeight / 2f, table.position.z);
            }

            for (int i = 0; i < rails.Length; i++)
            {
                if (rails[i] != null)
                {
                    rails[i].position = new Vector3(x + railOffsets[i], transform.position.y + rackHeight + 0.02f,
                        rails[i].position.z);
                }
            }

            foreach (ThrowableObject ball in balls)
            {
                if (ball == null || ball.IsHeld)
                {
                    continue;
                }

                Vector3 position = new Vector3(x, transform.position.y + rackHeight + ballDiameter / 2f + 0.02f,
                    ball.transform.position.z);
                var body = ball.GetComponent<Rigidbody>();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
                ball.transform.position = position;
                ball.CancelThrow();

                var returner = ball.GetComponent<ReturnToHome>();
                if (returner != null)
                {
                    returner.RecaptureHome();
                }
            }
        }
    }
}
