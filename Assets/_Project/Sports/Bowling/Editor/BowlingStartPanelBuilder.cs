using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// 「はじめに」パネルを組み込むエディタ処理。周囲確認とストラップの案内、身長と利き手の設定、「はじめる」ボタンを
    /// ワールド空間の Canvas に並べ、コントローラーのレイ(トリガー)で押せるようにする。
    /// 文字は日本語フォント(M PLUS Rounded 1c、SIL OFL 1.1)の動的フォントアセットで描く。
    /// 大きさは docs/art-direction.md 6.1(手元の説明は 0.8〜1.2m 先に 2.5〜4cm)に合わせる。
    /// </summary>
    internal static class BowlingStartPanelBuilder
    {
        private const string FontPath = "Assets/ThirdParty/Fonts/MPLUSRounded1c/MPLUSRounded1c-Medium.ttf";
        private const string FontAssetFolder = "Assets/_Project/Art/Fonts";
        private const string FontAssetPath = FontAssetFolder + "/MPLUSRounded1c SDF.asset";

        // 1 ピクセル = 1mm
        private const float PixelToMeter = 0.001f;

        private static readonly Color Background = new Color32(0x4A, 0x46, 0x60, 0xFF);
        private static readonly Color TextLight = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        private static readonly Color TextDark = new Color32(0x3A, 0x35, 0x50, 0xFF);
        private static readonly Color ButtonColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);
        private static readonly Color StartColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        /// <summary>
        /// パネルを作る。panelPosition はパネルの中心、eye はプレイヤーの目の位置(パネルをそちらへ向ける)。
        /// 失敗したときは理由を返す。
        /// </summary>
        public static string AddStartPanel(GameObject root, BowlingGame game, BowlingRack rack, Vector3 panelPosition,
            Vector3 eye)
        {
            TMP_FontAsset font = GetOrCreateFontAsset();
            if (font == null)
            {
                return $"日本語フォントが見つかりません({FontPath})。「はじめに」パネルは作りませんでした";
            }

            EnsureEventSystem();

            // Canvas(ワールド空間)。レイで押せるよう TrackedDeviceGraphicRaycaster を付ける
            var canvasObject = new GameObject("はじめにパネル", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = new Vector2(800f, 620f);
            rect.localScale = Vector3.one * PixelToMeter;
            rect.SetPositionAndRotation(panelPosition, Quaternion.LookRotation(panelPosition - eye, Vector3.up));

            var background = canvasObject.AddComponent<Image>();
            background.color = Background;

            var panel = root.AddComponent<BowlingStartPanel>();

            Label(canvasObject.transform, "見出し", "はじめに", font, 52, new Vector2(0f, 250f), new Vector2(760f, 70f), TextLight);
            Label(canvasObject.transform, "案内1", "まわりに人や物がないか確かめてください", font, 30,
                new Vector2(0f, 175f), new Vector2(760f, 44f), TextLight);
            Label(canvasObject.transform, "案内2", "コントローラーのストラップを手首に通してください", font, 30,
                new Vector2(0f, 128f), new Vector2(760f, 44f), TextLight);

            // 身長
            Label(canvasObject.transform, "身長の見出し", "身長", font, 36, new Vector2(-270f, 30f), new Vector2(180f, 60f), TextLight);
            Button(canvasObject.transform, "身長を下げる", "-", font, 48, new Vector2(-80f, 30f), new Vector2(100f, 76f),
                ButtonColor, TextLight, panel.DecreaseHeight);
            TMP_Text heightText = Label(canvasObject.transform, "身長の数字", "170 cm", font, 40, new Vector2(80f, 30f),
                new Vector2(200f, 60f), TextLight);
            Button(canvasObject.transform, "身長を上げる", "+", font, 48, new Vector2(240f, 30f), new Vector2(100f, 76f),
                ButtonColor, TextLight, panel.IncreaseHeight);

            // 利き手
            Label(canvasObject.transform, "利き手の見出し", "利き手", font, 36, new Vector2(-270f, -80f), new Vector2(180f, 60f), TextLight);
            (Image leftImage, TMP_Text leftText) = Button(canvasObject.transform, "左利き", "左", font, 38, new Vector2(-40f, -80f),
                new Vector2(170f, 76f), ButtonColor, TextLight, panel.SelectLeftHand);
            (Image rightImage, TMP_Text rightText) = Button(canvasObject.transform, "右利き", "● 右", font, 38, new Vector2(160f, -80f),
                new Vector2(170f, 76f), StartColor, TextDark, panel.SelectRightHand);

            // はじめる
            Button(canvasObject.transform, "はじめる", "はじめる", font, 46, new Vector2(0f, -215f), new Vector2(340f, 96f),
                StartColor, TextDark, panel.StartGame);

            var so = new SerializedObject(panel);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("rack").objectReferenceValue = rack;
            so.FindProperty("panelRoot").objectReferenceValue = canvasObject;
            so.FindProperty("heightText").objectReferenceValue = heightText;
            so.FindProperty("leftButtonImage").objectReferenceValue = leftImage;
            so.FindProperty("rightButtonImage").objectReferenceValue = rightImage;
            so.FindProperty("leftButtonText").objectReferenceValue = leftText;
            so.FindProperty("rightButtonText").objectReferenceValue = rightText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        /// <summary>
        /// 日本語フォントの TextMeshPro 用アセット。文字は使うときに作る(動的)ので、全部の漢字を先に焼かなくてよい。
        /// </summary>
        private static TMP_FontAsset GetOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                return null;
            }

            SceneBuildUtility.EnsureFolder(FontAssetFolder);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic);
            asset.name = "MPLUSRounded1c SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            // 文字の画像(アトラス)とマテリアルを、フォントアセットの中に一緒に保存する
            foreach (Texture2D texture in asset.atlasTextures)
            {
                texture.name = "MPLUSRounded1c Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }

            asset.material.name = "MPLUSRounded1c Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>UI をコントローラーで操作するための EventSystem(XR 用の入力モジュール)。</summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();
        }

        private static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, float size,
            Vector2 position, Vector2 boxSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static (Image, TMP_Text) Button(Transform parent, string name, string text, TMP_FontAsset font, float size,
            Vector2 position, Vector2 boxSize, Color color, Color textColor, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, onClick);

            TMP_Text label = Label(go.transform, "文字", text, font, size, Vector2.zero, boxSize, textColor);
            return (image, label);
        }
    }
}
