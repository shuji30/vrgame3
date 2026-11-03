using System.Collections;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 視界を単色で覆って暗くする・明るくする(シーン切り替えのフェード)。
    /// カメラの子に置いた小さな球(VRGame3/FadeSphere のマテリアル)の色の濃さを変える。
    /// 切り替えで入ったシーンでは、覆った状態から始めて明るくする。
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("カメラを囲む球(内側に色を塗る)")]
        [SerializeField] private Renderer sphere;

        [Tooltip("覆う色")]
        [SerializeField] private Color color = new Color32(0x1C, 0x1A, 0x2E, 0xFF);

        [Tooltip("暗くする・明るくするのにかける秒数")]
        [SerializeField] private float fadeTime = 0.3f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Material material;
        private float alpha;

        /// <summary>いまのシーンのフェード役。</summary>
        public static ScreenFader Current { get; private set; }

        private void Awake()
        {
            if (sphere != null)
            {
                material = sphere.material;
            }

            SetAlpha(SceneTransition.Arriving ? 1f : 0f);
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Start()
        {
            if (SceneTransition.Arriving)
            {
                SceneTransition.Arriving = false;
                StartCoroutine(FadeTo(0f));
            }
        }

        /// <summary>濃さ(0 = 何もない、1 = 完全に覆う)を時間をかけて変える。</summary>
        public IEnumerator FadeTo(float target)
        {
            float start = alpha;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / fadeTime));
                yield return null;
            }

            SetAlpha(target);
        }

        private void SetAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);
            if (material == null)
            {
                return;
            }

            Color c = color;
            c.a = alpha;
            material.SetColor(ColorId, c);
            // 透明なときは描かない(描画負荷を増やさない)
            sphere.enabled = alpha > 0.001f;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
