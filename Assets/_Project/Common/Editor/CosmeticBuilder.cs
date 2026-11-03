using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// 見た目のご褒美(M4)を組み込むエディタ処理。各シーンに CosmeticApplier を置き、ハブにはきせかえのパネルを作る。
    /// </summary>
    public static class CosmeticBuilder
    {
        /// <summary>
        /// CosmeticApplier を置き、シーンの名前から当てる物を探してつなぐ(ボール・弓・ラケット・両手のコントローラー)。
        /// </summary>
        public static void AddApplier(Transform parent)
        {
            var holder = new GameObject("かざり");
            holder.transform.SetParent(parent, false);
            var applier = holder.AddComponent<CosmeticApplier>();
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Renderer Named(string name, string under = null) => all
                .Where(t => t.name == name && (under == null || HasParent(t, under)))
                .Select(t => t.GetComponent<Renderer>()).FirstOrDefault(r => r != null);

            Renderer[] AllNamed(string prefix, string under) => all
                .Where(t => t.name.StartsWith(prefix) && HasParent(t, under))
                .Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToArray();

            var so = new SerializedObject(applier);
            // ボウリング:置き台のボール(CPU のボールは替えない)
            SetArray(so.FindProperty("balls"), all.Where(t => t.name.StartsWith("ボール") && HasParent(t, "ボール置き台"))
                .Select(t => t.GetComponent<Renderer>()).Where(r => r != null).Cast<Object>().ToArray());
            // アーチェリー
            SetArray(so.FindProperty("bowArms"), new Object[] { Named("上の腕", "弓"), Named("下の腕", "弓") }.Where(o => o != null).ToArray());
            so.FindProperty("bowRiser").objectReferenceValue = Named("胴", "弓");
            so.FindProperty("bowGrip").objectReferenceValue = Named("握り", "弓");
            SetArray(so.FindProperty("bowTips"), new Object[] { Named("上の先", "弓"), Named("下の先", "弓") }.Where(o => o != null).ToArray());
            SetArray(so.FindProperty("arrowFeathers"), AllNamed("羽根", "矢の見本").Cast<Object>().ToArray());
            // 卓球(プレイヤーのラケットだけ)
            so.FindProperty("racketRubber").objectReferenceValue = Named("ラバー(表)", "ラケット");
            so.FindProperty("racketHandle").objectReferenceValue = Named("柄", "ラケット");
            so.FindProperty("racketOrnament").objectReferenceValue = Named("柄の飾り", "ラケット");
            so.FindProperty("leftController").objectReferenceValue = all.FirstOrDefault(t => t.name == "Left Controller");
            so.FindProperty("rightController").objectReferenceValue = all.FirstOrDefault(t => t.name == "Right Controller");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>直接の親が parentName か(CPU のラケットなど、同じ名前の別物を除くため)。</summary>
        private static bool HasParent(Transform t, string parentName)
        {
            return t.parent != null && t.parent.name == parentName;
        }

        /// <summary>きせかえのパネル(1000 × 760px)。開くのは Open。</summary>
        public static CosmeticPanel AddPanel(Transform parent, TMP_FontAsset font)
        {
            var holder = new GameObject("きせかえ");
            holder.transform.SetParent(parent, false);
            var panel = holder.AddComponent<CosmeticPanel>();
            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "きせかえのパネル", new Vector2(1000f, 760f), UIBuildUtility.Background);
            canvas.transform.position = new Vector3(0f, 1.45f, 1.1f);
            Transform c = canvas.transform;
            UIBuildUtility.Label(c, "見出し", "かざり", font, 52, new Vector2(0f, 320f), new Vector2(960f, 70f), UIBuildUtility.TextLight);

            var images = new Image[16];
            var texts = new TMP_Text[16];
            var buttons = new Button[16];
            for (int row = 0; row < 4; row++)
            {
                float y = 200f - row * 130f;
                UIBuildUtility.Label(c, $"項目{row + 1}", RewardCatalog.ItemName((RewardCatalog.Item)row), font, 30,
                    new Vector2(-360f, y), new Vector2(260f, 60f), UIBuildUtility.TextLight);
                for (int col = 0; col < 4; col++)
                {
                    int index = row * 4 + col;
                    (Image image, TMP_Text text) = UIBuildUtility.Button(c, $"かざり{index}", "", font, 26,
                        new Vector2(-160f + 200f * col, y), new Vector2(190f, 110f), UIBuildUtility.ButtonColor, UIBuildUtility.TextLight, null);
                    text.textWrappingMode = TextWrappingModes.Normal;
                    var button = image.GetComponent<Button>();
                    UnityEventTools.AddIntPersistentListener(button.onClick, panel.Select, index);
                    images[index] = image;
                    texts[index] = text;
                    buttons[index] = button;
                }
            }

            UIBuildUtility.PrimaryButton(c, "とじる", "とじる", font, 44, new Vector2(0f, -320f), new Vector2(340f, 90f), panel.Close);

            var audio = holder.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            SetArray(so.FindProperty("images"), images);
            SetArray(so.FindProperty("texts"), texts);
            SetArray(so.FindProperty("buttons"), buttons);
            so.FindProperty("source").objectReferenceValue = audio;
            so.FindProperty("equipClip").objectReferenceValue = CommonSceneBuilder.PlaceholderClip("SFX_RewardEquip");
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
            return panel;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
