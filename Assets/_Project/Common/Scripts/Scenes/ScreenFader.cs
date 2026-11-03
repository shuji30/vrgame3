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

        [Tooltip("覆う色(docs/art-direction.md 7.4。純黒にしない)")]
        [SerializeField] private Color color = new Color32(0x2A, 0x26, 0x40, 0xFF);

        [Tooltip("暗くする・明るくするのにかける秒数")]
        [SerializeField] private float fadeTime = 0.3f;

        [Tooltip("暗くし始めるときの音(シーン切り替え)")]
        [SerializeField] private AudioSource audioSource;

        [SerializeField] private AudioClip fadeOutClip;

        [Tooltip("切り替えの音の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.13f;

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

        private IEnumerator Start()
        {
            if (!SceneTransition.Arriving)
            {
                yield break;
            }

            SceneTransition.Arriving = false;
            // 出現位置(ArrivalPoint)が頭の姿勢を待って動かし終えてから明るくする
            for (int i = 0; i <= ArrivalPoint.WaitFrames; i++)
            {
                yield return null;
            }

            yield return FadeTo(0f);
        }

        /// <summary>濃さ(0 = 何もない、1 = 完全に覆う)を時間をかけて変える。</summary>
        public IEnumerator FadeTo(float target)
        {
            float start = alpha;
            if (target > start && audioSource != null && fadeOutClip != null)
            {
                audioSource.PlayOneShot(fadeOutClip, volume);
            }

            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / fadeTime)));
                yield return null;
            }

            SetAlpha(target);
        }

        private void SetAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);
            // 暗くするのと同時に BGM・環境音も下げる(docs/art-direction.md 7.4)
            SoundCategory.SetDuck(1f - alpha);
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
