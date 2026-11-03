using UnityEditor;
using UnityEngine;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// 時間帯3つの値(docs/art-direction.md 5.2)を TimeOfDayApplier に入れるエディタ処理。
    /// 各会場の正面は +Z で、太陽は常に背中側に置く。値の調整はシーンの「時間帯」の Inspector でもできる。
    /// シーンの海(名前が「海」の物)の色も時間帯で替える。
    /// </summary>
    public static class TimeOfDayPresets
    {
        public static void Fill(TimeOfDayApplier applier)
        {
            var so = new SerializedObject(applier);
            SerializedProperty presets = so.FindProperty("presets");
            presets.arraySize = 3;
            // あさ:右後ろの低い太陽、やわらかい黄色
            Set(presets.GetArrayElementAtIndex(0), "あさ", 22f, -55f, "#FFE2B8", 0.9f, 0.6f, "#A2BCD8", "#E6D2BE", "#AE9A80",
                0.05f, 1.15f, 1.25f, "#9FB6D0", "#E6D6C4", "#EADFD2", 35f, 120f, "#3FC2BC", 0.8f);
            // ひる:今の見た目
            Set(presets.GetArrayElementAtIndex(1), "ひる", 50f, 30f, "#FFF6E6", 1.0f, 0.75f, "#9CC8E6", "#DCD6C4", "#B49E7C",
                0.04f, 0.9f, 1.35f, "#6FA8E0", "#BFE6EE", "#C8E8F4", 50f, 150f, "#2FB5B0", 1.0f);
            // ゆうやけ:左後ろの低い太陽、だいだい色(ピン・的・球が見える明るさを残す)
            Set(presets.GetArrayElementAtIndex(2), "ゆうやけ", 14f, 65f, "#FFB485", 0.8f, 0.5f, "#8E86C0", "#DDA090", "#8C7072",
                0.06f, 1.6f, 1.1f, "#B89AB8", "#D89A8E", "#D9A39A", 40f, 130f, "#2B8FA3", 0.6f);

            // 海の色を時間帯で替える
            SerializedProperty seas = so.FindProperty("seaRenderers");
            int count = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.gameObject.name == "海")
                {
                    seas.arraySize = count + 1;
                    seas.GetArrayElementAtIndex(count).objectReferenceValue = r;
                    count++;
                }
            }

            // 時間帯ごとの環境音(8.1.1):朝は鳥を多め、夕焼けは波を強め・鳥を少なめ・虫
            SetSounds(presets.GetArrayElementAtIndex(0), 0.8f, 1.25f, 0.6f, 0f);
            SetSounds(presets.GetArrayElementAtIndex(1), 1f, 1f, 1f, 0f);
            SetSounds(presets.GetArrayElementAtIndex(2), 1.25f, 0.5f, 1.8f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 霧のシェーダーがビルドで削られないよう、シーンは霧オン・昼の値で保存する
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = CommonSceneBuilder.Hex("#C8E8F4");
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 150f;
        }

        private static void SetSounds(SerializedProperty p, float wave, float birdVolume, float birdInterval, float insects)
        {
            p.FindPropertyRelative("waveScale").floatValue = wave;
            p.FindPropertyRelative("birdVolumeScale").floatValue = birdVolume;
            p.FindPropertyRelative("birdIntervalScale").floatValue = birdInterval;
            p.FindPropertyRelative("insectsScale").floatValue = insects;
        }

        /// <summary>
        /// 環境音の音源に種類を付け、夕焼け用の虫の声(風のループの 0.7 倍の音量)を足す。各シーンの環境音を作ったあとに呼ぶ。
        /// </summary>
        public static void TagAmbient(Transform parent, AudioClip insects, float windVolume)
        {
            foreach (SoundCategory category in Object.FindObjectsByType<SoundCategory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string name = category.gameObject.name;
                SoundCategory.Kind kind = name.StartsWith("波") || name.StartsWith("環境音1") ? SoundCategory.Kind.Wave
                    : name.StartsWith("鳥") ? SoundCategory.Kind.Bird
                    : name.StartsWith("風") ? SoundCategory.Kind.Wind
                    : SoundCategory.Kind.Other;
                var so = new SerializedObject(category);
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (insects == null)
            {
                return;
            }

            var go = new GameObject("虫");
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = insects;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.volume = windVolume * 0.7f;
            var tag = go.AddComponent<SoundCategory>();
            var tagSo = new SerializedObject(tag);
            tagSo.FindProperty("kind").enumValueIndex = (int)SoundCategory.Kind.Insects;
            tagSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedProperty p, string label, float elevation, float yaw, string sunColor, float intensity,
            float shadow, string sky, string equator, string ground, float sunSize, float atmosphere, float exposure,
            string skyTint, string skyGround, string fog, float fogStart, float fogEnd, string sea, float reflection)
        {
            p.FindPropertyRelative("label").stringValue = label;
            p.FindPropertyRelative("sunElevation").floatValue = elevation;
            p.FindPropertyRelative("sunYaw").floatValue = yaw;
            p.FindPropertyRelative("sunColor").colorValue = CommonSceneBuilder.Hex(sunColor);
            p.FindPropertyRelative("sunIntensity").floatValue = intensity;
            p.FindPropertyRelative("shadowStrength").floatValue = shadow;
            p.FindPropertyRelative("ambientSky").colorValue = CommonSceneBuilder.Hex(sky);
            p.FindPropertyRelative("ambientEquator").colorValue = CommonSceneBuilder.Hex(equator);
            p.FindPropertyRelative("ambientGround").colorValue = CommonSceneBuilder.Hex(ground);
            p.FindPropertyRelative("sunSize").floatValue = sunSize;
            p.FindPropertyRelative("atmosphere").floatValue = atmosphere;
            p.FindPropertyRelative("skyExposure").floatValue = exposure;
            p.FindPropertyRelative("skyTint").colorValue = CommonSceneBuilder.Hex(skyTint);
            p.FindPropertyRelative("skyGround").colorValue = CommonSceneBuilder.Hex(skyGround);
            p.FindPropertyRelative("fogColor").colorValue = CommonSceneBuilder.Hex(fog);
            p.FindPropertyRelative("fogStart").floatValue = fogStart;
            p.FindPropertyRelative("fogEnd").floatValue = fogEnd;
            p.FindPropertyRelative("seaColor").colorValue = CommonSceneBuilder.Hex(sea);
            p.FindPropertyRelative("reflectionIntensity").floatValue = reflection;
        }
    }
}
