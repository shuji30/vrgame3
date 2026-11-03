using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VRGame3.Common
{
    /// <summary>
    /// 時間帯(あさ・ひる・ゆうやけ)を、シーンに実行時に当てる(docs/plan.md M4「時間帯」、値は docs/art-direction.md)。
    /// 太陽の向き・色・強さ、環境光(空・地平・地面の3色)、空(プロシージャルのスカイボックス)、霧を差し替える。
    /// ライトマップは使わない。会場ではシーンに入った暗い間に当て、プレイ中は変えない。
    /// ハブでは設定パネルで変えたとき、0.3 秒暗くして差し替え、0.3 秒で明るくする。各シーンに1つ置く。
    /// </summary>
    public class TimeOfDayApplier : MonoBehaviour
    {
        /// <summary>時間帯1つ分の値。</summary>
        [Serializable]
        public class Preset
        {
            [Tooltip("名前")]
            public string label = "ひる";

            [Tooltip("太陽の仰角(度)")]
            public float sunElevation = 50f;

            [Tooltip("太陽の向き(会場の正面 +Z に対する Y 回転, 度)。背中側か横に置く")]
            public float sunYaw = 30f;

            [Tooltip("太陽の色")]
            public Color sunColor = new Color(1f, 0.96f, 0.88f);

            [Tooltip("太陽の強さ")]
            public float sunIntensity = 1.1f;

            [Tooltip("影の強さ(0〜1)")]
            [Range(0f, 1f)]
            public float shadowStrength = 0.6f;

            [Tooltip("環境光:空の色")]
            public Color ambientSky = new Color(0.66f, 0.82f, 0.95f);

            [Tooltip("環境光:地平の色")]
            public Color ambientEquator = new Color(0.75f, 0.78f, 0.72f);

            [Tooltip("環境光:地面の色")]
            public Color ambientGround = new Color(0.55f, 0.48f, 0.38f);

            [Tooltip("空の色合い(プロシージャルのスカイボックスの Sky Tint)")]
            public Color skyTint = new Color(0.5f, 0.7f, 0.9f);

            [Tooltip("地面側の空の色(Ground)")]
            public Color skyGround = new Color(0.62f, 0.6f, 0.55f);

            [Tooltip("空の明るさ(Exposure)")]
            public float skyExposure = 1.2f;

            [Tooltip("大気の厚さ(Atmosphere Thickness。大きいほど夕焼けらしく赤くなる)")]
            public float atmosphere = 1f;

            [Tooltip("太陽の円の大きさ(Sun Size)")]
            public float sunSize = 0.04f;

            [Tooltip("海の色")]
            public Color seaColor = new Color32(0x2F, 0xB5, 0xB0, 0xFF);

            [Tooltip("映り込みの強さ(Reflection Intensity)")]
            public float reflectionIntensity = 1f;

            [Tooltip("環境音の倍率:波")]
            public float waveScale = 1f;

            [Tooltip("環境音の倍率:鳥の音量")]
            public float birdVolumeScale = 1f;

            [Tooltip("環境音の倍率:鳥の間隔(小さいほどよく鳴く)")]
            public float birdIntervalScale = 1f;

            [Tooltip("環境音の倍率:虫(0 なら鳴かない)")]
            public float insectsScale;

            [Tooltip("霧の色")]
            public Color fogColor = new Color(0.8f, 0.9f, 0.97f);

            [Tooltip("霧が始まる距離(m)")]
            public float fogStart = 60f;

            [Tooltip("霧が濃くなりきる距離(m)")]
            public float fogEnd = 250f;
        }

        [Tooltip("太陽(Directional Light)。空ならシーンから探す")]
        [SerializeField] private Light sun;

        [Tooltip("色を時間帯で替える海")]
        [SerializeField] private Renderer[] seaRenderers = new Renderer[0];

        [Tooltip("あさ / ひる / ゆうやけ")]
        [SerializeField] private Preset[] presets = { new Preset { label = "あさ" }, new Preset(), new Preset { label = "ゆうやけ" } };

        [Tooltip("確認用:オンにすると、設定にかかわらずこの時間帯に固定する")]
        [SerializeField] private bool forcePreset;

        [Tooltip("確認用:固定する時間帯(0 あさ / 1 ひる / 2 ゆうやけ)")]
        [Range(0, 2)]
        [SerializeField] private int forcedIndex = 1;

        private Material skyInstance;
        private int applied = -1;

        /// <summary>いまのシーンの時間帯の係。</summary>
        public static TimeOfDayApplier Current { get; private set; }

        /// <summary>時間帯を変えてよいシーンか(ハブだけ)。</summary>
        public static bool CanChangeHere => SceneManager.GetActiveScene().name == SceneTransition.HubScene;

        private void Awake()
        {
            Current = this;
            if (sun == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional)
                    {
                        sun = l;
                        break;
                    }
                }
            }

            // スカイボックスは共有のマテリアルを書き換えないよう、複製して使う
            if (RenderSettings.skybox != null)
            {
                skyInstance = new Material(RenderSettings.skybox);
                RenderSettings.skybox = skyInstance;
            }

            // シーンに入った暗い間に当てる
            Apply(Index);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            if (skyInstance != null)
            {
                Destroy(skyInstance);
            }
        }

        private int Index => forcePreset ? forcedIndex : SaveStore.Settings.timeOfDay;

        /// <summary>ハブで時間帯を変えたとき:暗くしてから差し替え、明るくする。</summary>
        public void ChangeWithFade()
        {
            if (applied == Index)
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(FadeAndApply());
        }

        private IEnumerator FadeAndApply()
        {
            ScreenFader fader = ScreenFader.Current;
            if (fader != null)
            {
                yield return fader.FadeTo(1f);
            }

            Apply(Index);
            if (fader != null)
            {
                yield return fader.FadeTo(0f);
            }
        }

        /// <summary>時間帯の値を当てる。</summary>
        public void Apply(int index)
        {
            if (presets.Length == 0)
            {
                return;
            }

            Preset p = presets[Mathf.Clamp(index, 0, presets.Length - 1)];
            applied = index;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunElevation, p.sunYaw, 0f);
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.shadowStrength = p.shadowStrength;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;

            if (skyInstance != null)
            {
                SetIfHas(skyInstance, "_SkyTint", p.skyTint);
                SetIfHas(skyInstance, "_GroundColor", p.skyGround);
                SetIfHas(skyInstance, "_Exposure", p.skyExposure);
                SetIfHas(skyInstance, "_AtmosphereThickness", p.atmosphere);
                SetIfHas(skyInstance, "_SunSize", p.sunSize);
            }

            // 海は複製したマテリアルの色だけを替える(アセットは書き換えない)
            foreach (Renderer sea in seaRenderers)
            {
                if (sea != null && sea.material.HasProperty("_BaseColor"))
                {
                    sea.material.SetColor("_BaseColor", p.seaColor);
                }
            }

            RenderSettings.reflectionIntensity = p.reflectionIntensity;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            DynamicGI.UpdateEnvironment();
            ApplySounds(p);
        }

        /// <summary>時間帯ごとの環境音(docs/art-direction.md 8.1.1)。</summary>
        private void ApplySounds(Preset p)
        {
            foreach (SoundCategory category in FindObjectsByType<SoundCategory>(FindObjectsSortMode.None))
            {
                switch (category.SoundKind)
                {
                    case SoundCategory.Kind.Wave:
                        category.SetTimeScale(p.waveScale);
                        break;
                    case SoundCategory.Kind.Bird:
                        category.SetTimeScale(p.birdVolumeScale);
                        if (category.TryGetComponent(out RandomOneShot birds))
                        {
                            birds.SetIntervalScale(p.birdIntervalScale);
                        }

                        break;
                    case SoundCategory.Kind.Insects:
                        category.SetTimeScale(p.insectsScale);
                        break;
                }
            }
        }

        private static void SetIfHas(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetIfHas(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
