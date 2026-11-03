using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// 「はじめに」パネルを組み込むエディタ処理。周囲確認とストラップの案内、身長と利き手の設定、「はじめる」ボタンを
    /// ワールド空間の Canvas に並べ、コントローラーのレイ(トリガー)で押せるようにする。
    /// 大きさは docs/art-direction.md 6.1(手元の説明は 0.8〜1.2m 先に 2.5〜4cm)、見た目は 6.4 に合わせる。
    /// </summary>
    internal static class BowlingStartPanelBuilder
    {
        /// <summary>
        /// パネルを作る。panelPosition はパネルの中心、eye はプレイヤーの目の位置(パネルをそちらへ向ける)。
        /// 失敗したときは理由を返す。
        /// </summary>
        public static string AddStartPanel(GameObject root, BowlingGame game, BowlingRack rack, Vector3 panelPosition,
            Vector3 eye)
        {
            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                return $"日本語フォントが見つかりません({UIBuildUtility.FontPath})。「はじめに」パネルは作りませんでした";
            }

            UIBuildUtility.EnsureEventSystem();

            GameObject canvasObject = UIBuildUtility.CreateCanvas(root.transform, "はじめにパネル", new Vector2(800f, 620f),
                UIBuildUtility.Background);
            canvasObject.transform.SetPositionAndRotation(panelPosition, Quaternion.LookRotation(panelPosition - eye, Vector3.up));
            Transform c = canvasObject.transform;
            Color light = UIBuildUtility.TextLight;

            var panel = root.AddComponent<BowlingStartPanel>();

            UIBuildUtility.Label(c, "見出し", "はじめに", font, 52, new Vector2(0f, 250f), new Vector2(760f, 70f), light);
            UIBuildUtility.Label(c, "案内1", "まわりに人や物がないか確かめてください", font, 30,
                new Vector2(0f, 175f), new Vector2(760f, 44f), light);
            UIBuildUtility.Label(c, "案内2", "コントローラーのストラップを手首に通してください", font, 30,
                new Vector2(0f, 128f), new Vector2(760f, 44f), light);

            // 身長
            UIBuildUtility.Label(c, "身長の見出し", "身長", font, 36, new Vector2(-270f, 30f), new Vector2(180f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を下げる", "-", font, 48, new Vector2(-80f, 30f), new Vector2(100f, 76f),
                panel.DecreaseHeight);
            TMP_Text heightText = UIBuildUtility.Label(c, "身長の数字", "170 cm", font, 40, new Vector2(80f, 30f),
                new Vector2(200f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を上げる", "+", font, 48, new Vector2(240f, 30f), new Vector2(100f, 76f),
                panel.IncreaseHeight);

            // 利き手
            UIBuildUtility.Label(c, "利き手の見出し", "利き手", font, 36, new Vector2(-270f, -80f), new Vector2(180f, 60f), light);
            (Image leftImage, TMP_Text leftText) = UIBuildUtility.NormalButton(c, "左利き", "左", font, 38,
                new Vector2(-40f, -80f), new Vector2(170f, 76f), panel.SelectLeftHand);
            (Image rightImage, TMP_Text rightText) = UIBuildUtility.PrimaryButton(c, "右利き", "● 右", font, 38,
                new Vector2(160f, -80f), new Vector2(170f, 76f), panel.SelectRightHand);

            // はじめる
            UIBuildUtility.PrimaryButton(c, "はじめる", "はじめる", font, 46, new Vector2(0f, -215f), new Vector2(340f, 96f),
                panel.StartGame);

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
    }
}
