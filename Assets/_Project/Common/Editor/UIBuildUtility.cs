using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using VRGame3.Common;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// ワールド空間の UI(パネル・看板)を組み立てるエディタ処理の共通部品。docs/art-direction.md 6.4。
    /// 1 ピクセル = 1mm(スケール 0.001)。文字は日本語フォント(M PLUS Rounded 1c、SIL OFL 1.1)の動的フォントアセットで描く。
    /// コントローラーのレイ(トリガー)で押せるよう、Canvas に TrackedDeviceGraphicRaycaster を付ける。
    /// </summary>
    public static class UIBuildUtility
    {
        public const string FontPath = "Assets/ThirdParty/Fonts/MPLUSRounded1c/MPLUSRounded1c-Medium.ttf";
        public const string FontAssetFolder = "Assets/_Project/Art/Fonts";
        public const string FontAssetPath = FontAssetFolder + "/MPLUSRounded1c SDF.asset";

        public const float PixelToMeter = 0.001f;

        // 6.4 の色
        public static readonly Color Background = new Color32(0x4A, 0x46, 0x60, 0xFF);
        public static readonly Color TextLight = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        public static readonly Color TextDark = new Color32(0x3A, 0x35, 0x50, 0xFF);
        public static readonly Color TextSub = new Color32(0xB8, 0xB4, 0xC8, 0xFF);
        public static readonly Color ButtonColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);
        public static readonly Color PrimaryColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        /// <summary>
        /// 日本語フォントの TextMeshPro 用アセット。文字は使うときに作る(動的)ので、全部の漢字を先に焼かなくてよい。
        /// フォントがなければ null。
        /// </summary>
        public static TMP_FontAsset GetOrCreateFontAsset()
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
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();
        }

        /// <summary>
        /// ワールド空間の Canvas。background が透明(a = 0)なら背景の Image を付けない。
        /// </summary>
        public static GameObject CreateCanvas(Transform parent, string name, Vector2 sizePx, Color background)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = sizePx;
            rect.localScale = Vector3.one * PixelToMeter;
            if (background.a > 0f)
            {
                canvasObject.AddComponent<Image>().color = background;
            }

            return canvasObject;
        }

        /// <summary>Canvas の中の1枚の板(ページ)。中身を差し替えるときに使う。</summary>
        public static GameObject Page(Transform parent, string name, Vector2 sizePx, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizePx;
            go.AddComponent<Image>().color = background;
            return go;
        }

        /// <summary>中央ぞろえの文字。</summary>
        public static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, float size,
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

        /// <summary>押すと onClick を呼ぶボタン。背景と文字を返す(選択肢の色を後から変えるため)。</summary>
        public static (Image, TMP_Text) Button(Transform parent, string name, string text, TMP_FontAsset font, float size,
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
            if (onClick != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, onClick);
            }

            TMP_Text label = Label(go.transform, "文字", text, font, size, Vector2.zero, boxSize, textColor);
            return (image, label);
        }

        /// <summary>主ボタン(黄色・暗い文字)。</summary>
        public static (Image, TMP_Text) PrimaryButton(Transform parent, string name, string text, TMP_FontAsset font,
            float size, Vector2 position, Vector2 boxSize, UnityAction onClick)
        {
            return Button(parent, name, text, font, size, position, boxSize, PrimaryColor, TextDark, onClick);
        }

        /// <summary>普通のボタン(青・明るい文字)。</summary>
        public static (Image, TMP_Text) NormalButton(Transform parent, string name, string text, TMP_FontAsset font,
            float size, Vector2 position, Vector2 boxSize, UnityAction onClick)
        {
            return Button(parent, name, text, font, size, position, boxSize, ButtonColor, TextLight, onClick);
        }
    }
}
