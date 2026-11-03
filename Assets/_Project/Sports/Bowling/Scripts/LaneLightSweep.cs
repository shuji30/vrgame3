using System.Collections;
using UnityEngine;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// レーン脇に並べた小さな明かりを、奥(ピン側)から手前へ1回流すストライクの演出。docs/art-direction.md 7.2。
    /// 実際のライトは使わず、明かりのマテリアルの発光だけを上げ下げする(描画負荷を増やさない)。
    /// 点滅させず、光の山がなめらかに手前へ動くようにする。
    /// </summary>
    public class LaneLightSweep : MonoBehaviour
    {
        [Tooltip("明かり。並べる順番は問わない(Z の位置で奥から順に光る)")]
        [SerializeField] private Renderer[] lamps = new Renderer[0];

        [Tooltip("光らせる色")]
        [SerializeField] private Color glowColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("光の強さ")]
        [Range(0f, 4f)]
        [SerializeField] private float intensity = 2f;

        [Tooltip("奥から手前まで流れきる秒数")]
        [SerializeField] private float sweepTime = 1.2f;

        [Tooltip("光の山の半分の幅(m)。山の中心からこの距離までの明かりが光る")]
        [SerializeField] private float halfWidth = 3f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Material[] materials = new Material[0];
        private float[] positions = new float[0];
        private Coroutine running;

        private void Awake()
        {
            // 明かりごとにマテリアルを分ける(MaterialPropertyBlock と違い SRP Batcher が効いたままになる)
            materials = new Material[lamps.Length];
            positions = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null)
                {
                    continue;
                }

                materials[i] = lamps[i].material;
                materials[i].EnableKeyword("_EMISSION");
                materials[i].SetColor(EmissionColorId, Color.black);
                // 光る順番は Z で決める(左右の同じ Z の明かりは一緒に光る)
                positions[i] = lamps[i].transform.position.z;
            }
        }

        /// <summary>光を奥から手前へ1回流す。流れている途中なら最初からやり直す。</summary>
        public void Play()
        {
            if (lamps.Length == 0)
            {
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(Sweep());
        }

        private IEnumerator Sweep()
        {
            float far = float.MinValue;
            float near = float.MaxValue;
            foreach (float z in positions)
            {
                far = Mathf.Max(far, z);
                near = Mathf.Min(near, z);
            }

            // 山の中心を、奥の外側から手前の外側まで動かす(端の明かりもなめらかに点いて消える)
            float start = far + halfWidth;
            float end = near - halfWidth;
            for (float t = 0f; t < sweepTime; t += Time.deltaTime)
            {
                float center = Mathf.Lerp(start, end, t / sweepTime);
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    float distance = Mathf.Abs(positions[i] - center) / halfWidth;
                    float level = distance >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, distance);
                    materials[i].SetColor(EmissionColorId, glowColor * (level * intensity));
                }

                yield return null;
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    material.SetColor(EmissionColorId, Color.black);
                }
            }

            running = null;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }
    }
}
