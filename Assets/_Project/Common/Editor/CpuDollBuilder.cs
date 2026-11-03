using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// CPU の仮人形(こけし風の木の人形、身長 約 1.5m)を組み立てるエディタ処理。docs/art-direction.md「M3 CPU の体」。
    /// 卓球とボウリングで共通に使う。体の原点は足元、+Z が向いている方。名札(強さの色)は頭の上 0.3m。
    /// </summary>
    public static class CpuDollBuilder
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Common";

        /// <summary>組み立てた人形の部品。</summary>
        public class Parts
        {
            public Transform Body;
            public Transform Head;
            public List<Transform> Eyes = new List<Transform>();
            public Transform RightHand;
            public Transform LeftHand;
            public Image NameTagBack;
            public TMP_Text NameTagText;
        }

        public static Parts Build(Transform parent, string name, Vector3 position, Quaternion rotation, TMP_FontAsset font)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var parts = new Parts { Body = root.transform };

            Material skin = Mat("C_DollSkin", "#F0C995");
            Material ink = Mat("C_DollInk", "#4A4660");
            Part(root.transform, "胴", PrimitiveType.Capsule, new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 0.55f, 0.4f), Mat("C_DollBody", "#FF7F6B"));
            Part(root.transform, "胴の帯", PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.41f, 0.03f, 0.41f), Mat("C_DollBand", "#FFFDF7"));

            var head = new GameObject("頭");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            parts.Head = head.transform;
            Part(head.transform, "顔", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0f), Vector3.one * 0.32f, skin);
            Part(head.transform, "髪", PrimitiveType.Sphere, new Vector3(0f, 0.23f, -0.02f), new Vector3(0.34f, 0.2f, 0.34f), Mat("C_DollHair", "#8A5A3B"));
            foreach (float x in new[] { -0.06f, 0.06f })
            {
                parts.Eyes.Add(Part(head.transform, "目", PrimitiveType.Sphere, new Vector3(x, 0.15f, 0.15f), new Vector3(0.035f, 0.05f, 0.02f), ink).transform);
                Part(head.transform, "ほお", PrimitiveType.Sphere, new Vector3(x * 1.67f, 0.09f, 0.14f), new Vector3(0.04f, 0.025f, 0.01f), Mat("C_DollCheek", "#FFB4A2"));
            }

            parts.RightHand = Part(root.transform, "右手", PrimitiveType.Sphere, new Vector3(0.3f, 1.0f, 0.25f), Vector3.one * 0.11f, skin).transform;
            parts.LeftHand = Part(root.transform, "左手", PrimitiveType.Sphere, new Vector3(-0.3f, 0.95f, 0.25f), Vector3.one * 0.11f, skin).transform;

            // 名札:頭の上 0.3m。人形の向きの反対側(人形を見ている人)へ向ける
            GameObject tag = UIBuildUtility.CreateCanvas(root.transform, "名札", new Vector2(600f, 180f), Color.white);
            tag.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            tag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Object.DestroyImmediate(tag.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            parts.NameTagBack = tag.GetComponent<Image>();
            parts.NameTagText = UIBuildUtility.Label(tag.transform, "文字", "ふつう ▲▲", font, 140, Vector2.zero, new Vector2(580f, 170f),
                UIBuildUtility.TextDark);
            return parts;
        }

        private static Material Mat(string name, string hex)
        {
            return SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, name, hex, false);
        }

        /// <summary>人形の部品。動く物なので影を落とす。当たり判定は付けない。</summary>
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale,
            Material material)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, Vector3.zero, Vector3.one, material, false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            return go;
        }
    }
}
