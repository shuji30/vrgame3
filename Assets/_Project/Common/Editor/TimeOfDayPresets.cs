using UnityEditor;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// 時間帯3つの値(docs/art-direction.md の M4「時間帯」)を TimeOfDayApplier に入れるエディタ処理。
    /// 値の調整はシーンの「時間帯」の Inspector でもできる。
    /// </summary>
    public static class TimeOfDayPresets
    {
        public static void Fill(TimeOfDayApplier applier)
        {
            var so = new SerializedObject(applier);
            SerializedProperty presets = so.FindProperty("presets");
            presets.arraySize = 3;
            // あさ:低い太陽を背中の左から、やわらかい黄色
            Set(presets.GetArrayElementAtIndex(0), "あさ", 22f, -40f, "#FFE2B8", 0.9f, 0.45f, "#BFD8F0", "#E8DCC8", "#9C8C74",
                "#9CC4E8", "#C8B8A0", 1.1f, 0.9f, "#E6EEF4", 50f, 220f);
            // ひる:今までと同じ
            Set(presets.GetArrayElementAtIndex(1), "ひる", 50f, 30f, "#FFF6E0", 1.1f, 0.6f, "#A8D0F2", "#C0C8B8", "#8C7A60",
                "#80B4E6", "#9E9A8C", 1.2f, 1.0f, "#CFF1FF", 60f, 260f);
            // ゆうやけ:低い太陽を背中の右から、だいだい色(ピン・的・球が見える明るさを残す)
            Set(presets.GetArrayElementAtIndex(2), "ゆうやけ", 14f, 150f, "#FFB27A", 1.0f, 0.4f, "#C9A0C8", "#F0B898", "#7A5A60",
                "#C08AA8", "#8A6A70", 1.0f, 1.8f, "#F4C6A8", 45f, 200f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedProperty p, string label, float elevation, float yaw, string sunColor, float intensity,
            float shadow, string sky, string equator, string ground, string skyTint, string skyGround, float exposure,
            float atmosphere, string fog, float fogStart, float fogEnd)
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
            p.FindPropertyRelative("skyTint").colorValue = CommonSceneBuilder.Hex(skyTint);
            p.FindPropertyRelative("skyGround").colorValue = CommonSceneBuilder.Hex(skyGround);
            p.FindPropertyRelative("skyExposure").floatValue = exposure;
            p.FindPropertyRelative("atmosphere").floatValue = atmosphere;
            p.FindPropertyRelative("fogColor").colorValue = CommonSceneBuilder.Hex(fog);
            p.FindPropertyRelative("fogStart").floatValue = fogStart;
            p.FindPropertyRelative("fogEnd").floatValue = fogEnd;
        }
    }
}
