using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRGame3.Common;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// 「はじめに」パネルを組み込むエディタ処理。周囲確認とストラップの案内、モード(ひとりで・CPUと・こうたいで・チャレンジ)、
    /// 強さ / 人数、身長と利き手の設定、「はじめる」ボタンをワールド空間の Canvas に並べ、コントローラーのレイで押せるようにする。
    /// 大きさと並びは docs/art-direction.md 6.10 のボウリングの行(1000 × 830)。交代の案内やチャレンジの一覧のパネルも同じ位置に置く。
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

            GameObject canvasObject = UIBuildUtility.CreateCanvas(root.transform, "はじめにパネル", new Vector2(1000f, 830f),
                UIBuildUtility.Background);
            canvasObject.transform.SetPositionAndRotation(panelPosition, Quaternion.LookRotation(panelPosition - eye, Vector3.up));
            Transform c = canvasObject.transform;
            Color light = UIBuildUtility.TextLight;

            var panel = root.AddComponent<BowlingStartPanel>();

            TMP_Text title = UIBuildUtility.Label(c, "見出し", "はじめに", font, 52, new Vector2(0f, 360f), new Vector2(960f, 70f), light);
            UIBuildUtility.Label(c, "案内1", "まわりに人や物がないか確かめてください", font, 30,
                new Vector2(0f, 290f), new Vector2(960f, 44f), light);
            UIBuildUtility.Label(c, "案内2", "コントローラーのストラップを手首に通してください", font, 30,
                new Vector2(0f, 243f), new Vector2(960f, 44f), light);

            // 身長
            UIBuildUtility.Label(c, "身長の見出し", "身長", font, 36, new Vector2(-360f, -60f), new Vector2(260f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を下げる", "-", font, 48, new Vector2(-110f, -60f), new Vector2(100f, 76f),
                panel.DecreaseHeight);
            TMP_Text heightText = UIBuildUtility.Label(c, "身長の数字", "170 cm", font, 40, new Vector2(70f, -60f),
                new Vector2(200f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を上げる", "+", font, 48, new Vector2(250f, -60f), new Vector2(100f, 76f),
                panel.IncreaseHeight);

            // 利き手
            Vector2 wide = new Vector2(220f, 76f);
            UIBuildUtility.Label(c, "利き手の見出し", "利き手", font, 36, new Vector2(-360f, -165f), new Vector2(260f, 60f), light);
            (Image leftImage, TMP_Text leftText) = UIBuildUtility.NormalButton(c, "左利き", "左", font, 38,
                new Vector2(-110f, -165f), wide, panel.SelectLeftHand);
            (Image rightImage, TMP_Text rightText) = UIBuildUtility.PrimaryButton(c, "右利き", "● 右", font, 38,
                new Vector2(125f, -165f), wide, panel.SelectRightHand);

            // はじめる
            (_, TMP_Text startLabel) = UIBuildUtility.PrimaryButton(c, "はじめる", "はじめる", font, 46, new Vector2(0f, -300f),
                new Vector2(340f, 96f), panel.StartGame);

            // モード(ひとりで・CPUと・こうたいで・チャレンジ)と強さ / 人数
            ModeSelector selector = CommonSceneBuilder.AddModeRows(root, c, font, SportIds.Bowling,
                new[] { SportMode.Solo, SportMode.Cpu, SportMode.Turns, SportMode.Challenge }, 150f, 45f, startLabel, title);
            ChoicePanel choicePanel = CommonSceneBuilder.AddChoicePanel(root.transform, panelPosition, eye);

            var so = new SerializedObject(panel);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("rack").objectReferenceValue = rack;
            so.FindProperty("modeSelector").objectReferenceValue = selector;
            so.FindProperty("panelRoot").objectReferenceValue = canvasObject;
            so.FindProperty("heightText").objectReferenceValue = heightText;
            so.FindProperty("leftButtonImage").objectReferenceValue = leftImage;
            so.FindProperty("rightButtonImage").objectReferenceValue = rightImage;
            so.FindProperty("leftButtonText").objectReferenceValue = leftText;
            so.FindProperty("rightButtonText").objectReferenceValue = rightText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var gameSo = new SerializedObject(game);
            gameSo.FindProperty("modeSelector").objectReferenceValue = selector;
            gameSo.FindProperty("choicePanel").objectReferenceValue = choicePanel;
            gameSo.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }
    }
}
