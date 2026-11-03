using System.Collections;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// パネルを出すときに、少し小さい大きさから元の大きさへ広げる(docs/art-direction.md 6.4:0.15 秒で 0.9 → 1.0 倍)。
    /// 透明度のフェードや点滅はしない。パネル(Canvas)に付ける。
    /// </summary>
    public class PanelPop : MonoBehaviour
    {
        [Tooltip("出し始めの大きさ(元の大きさに対する倍率)")]
        [SerializeField] private float startScale = 0.9f;

        [Tooltip("広げる秒数")]
        [SerializeField] private float duration = 0.15f;

        private Vector3 baseScale;
        private bool captured;

        private void Awake()
        {
            Capture();
        }

        private void OnEnable()
        {
            Capture();
            StartCoroutine(Pop());
        }

        private void OnDisable()
        {
            if (captured)
            {
                transform.localScale = baseScale;
            }
        }

        private void Capture()
        {
            if (!captured)
            {
                baseScale = transform.localScale;
                captured = true;
            }
        }

        private IEnumerator Pop()
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                transform.localScale = baseScale * Mathf.Lerp(startScale, 1f, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }

            transform.localScale = baseScale;
        }
    }
}
