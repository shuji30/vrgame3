using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRGame3.Common;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// アーチェリーの「はじめに」パネル。周囲確認とストラップの案内、身長・利き手・ねらいの補助の設定、「はじめる」。
    /// 値は保存した設定(SaveStore)から始め、変えたらすぐ保存する。ゲームの始めごとに出す(docs/plan.md「設定と保存」)。
    /// </summary>
    public class ArcheryStartPanel : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private ArcheryGame game;

        [Tooltip("モードの行(チャレンジなら課題の一覧へ進む)")]
        [SerializeField] private ModeSelector modeSelector;

        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("身長の数字")]
        [SerializeField] private TMP_Text heightText;

        [Tooltip("選択肢のボタン:左利き・右利き・補助オン・補助オフの順")]
        [SerializeField] private Image[] choiceImages = new Image[4];

        [Tooltip("選択肢のボタンの文字(choiceImages と同じ順)")]
        [SerializeField] private TMP_Text[] choiceTexts = new TMP_Text[4];

        [Tooltip("ボタン1回で変わる身長(cm)")]
        [SerializeField] private int heightStep = 5;

        [Header("色")]
        [Tooltip("選んでいるボタンの色")]
        [SerializeField] private Color selectedColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("選んでいないボタンの色")]
        [SerializeField] private Color unselectedColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);

        [Tooltip("選んでいるボタンの文字の色")]
        [SerializeField] private Color selectedTextColor = new Color32(0x3A, 0x35, 0x50, 0xFF);

        [Tooltip("選んでいないボタンの文字の色")]
        [SerializeField] private Color unselectedTextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);

        private static readonly string[] ChoiceLabels = { "左", "右", "オン", "オフ" };

        private static GameSettings Settings => SaveStore.Settings;

        // パネルを出している状態か(メニューが出ている間は隠すが、この値は変えない)
        private bool shown;

        private void OnEnable()
        {
            SaveStore.Changed += Refresh;
            if (game != null)
            {
                game.WaitingForStart += Show;
            }
        }

        private void OnDisable()
        {
            SaveStore.Changed -= Refresh;
            if (game != null)
            {
                game.WaitingForStart -= Show;
            }
        }

        private void Start()
        {
            Show();
        }

        public void Show()
        {
            Refresh();
            if (modeSelector != null)
            {
                modeSelector.Refresh();
            }

            shown = true;
            panelRoot.SetActive(!InputGate.GrabBlocked);
        }

        private void Update()
        {
            // メニューや設定パネル・休憩案内が出ている間は隠す(2枚を重ねて出さない)
            bool visible = shown && !InputGate.GrabBlocked;
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }

        /// <summary>「はじめる」(ボタンから呼ぶ)。</summary>
        public void StartGame()
        {
            shown = false;
            panelRoot.SetActive(false);
            if (modeSelector != null && modeSelector.Mode == SportMode.Challenge)
            {
                game.ShowChallengeList();
            }
            else
            {
                game.StartGame();
            }
        }

        public void IncreaseHeight()
        {
            Settings.heightCm = Mathf.Min(GameSettings.MaxHeightCm, Settings.heightCm + heightStep);
            SaveStore.SaveSettings();
        }

        public void DecreaseHeight()
        {
            Settings.heightCm = Mathf.Max(GameSettings.MinHeightCm, Settings.heightCm - heightStep);
            SaveStore.SaveSettings();
        }

        public void SelectLeftHand()
        {
            Settings.leftHanded = true;
            SaveStore.SaveSettings();
        }

        public void SelectRightHand()
        {
            Settings.leftHanded = false;
            SaveStore.SaveSettings();
        }

        public void AimAssistOn()
        {
            Settings.aimAssist = true;
            SaveStore.SaveSettings();
        }

        public void AimAssistOff()
        {
            Settings.aimAssist = false;
            SaveStore.SaveSettings();
        }

        private void Refresh()
        {
            if (heightText != null)
            {
                heightText.text = $"{Settings.heightCm} cm";
            }

            bool[] selected = { Settings.leftHanded, !Settings.leftHanded, Settings.aimAssist, !Settings.aimAssist };
            for (int i = 0; i < selected.Length; i++)
            {
                // 選んでいる方は色に加えて「●」の印でも示す(色だけに頼らない)
                if (i < choiceImages.Length && choiceImages[i] != null)
                {
                    choiceImages[i].color = selected[i] ? selectedColor : unselectedColor;
                }

                if (i < choiceTexts.Length && choiceTexts[i] != null)
                {
                    choiceTexts[i].text = selected[i] ? $"● {ChoiceLabels[i]}" : ChoiceLabels[i];
                    choiceTexts[i].color = selected[i] ? selectedTextColor : unselectedTextColor;
                }
            }
        }
    }
}
