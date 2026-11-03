using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRGame3.Common;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球の「はじめに」パネル。周囲確認(台にもたれない)とストラップの案内、CPU の強さ3段、入りやすさの補助、
    /// 身長・利き手の設定、「はじめる」。値は保存した設定から始め、変えたらすぐ保存する。
    /// </summary>
    public class TTStartPanel : MonoBehaviour
    {
        /// <summary>選択肢のボタンの並び。</summary>
        public enum Choice
        {
            Easy,
            Normal,
            Hard,
            AssistOn,
            AssistOff,
            LeftHand,
            RightHand,
        }

        private static readonly string[] Labels = { "やさしい", "ふつう", "つよい", "オン", "オフ", "左", "右" };

        [Tooltip("進行役")]
        [SerializeField] private TableTennisGame game;

        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("身長の数字")]
        [SerializeField] private TMP_Text heightText;

        [Tooltip("選択肢のボタンの背景(Choice の順)")]
        [SerializeField] private Image[] choiceImages = new Image[7];

        [Tooltip("選択肢のボタンの文字(Choice の順)")]
        [SerializeField] private TMP_Text[] choiceTexts = new TMP_Text[7];

        [Tooltip("ボタン1回で変わる身長(cm)")]
        [SerializeField] private int heightStep = 5;

        [Tooltip("選んでいるボタンの色")]
        [SerializeField] private Color selectedColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("選んでいないボタンの色")]
        [SerializeField] private Color unselectedColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);

        [Tooltip("選んでいるボタンの文字の色")]
        [SerializeField] private Color selectedTextColor = new Color32(0x3A, 0x35, 0x50, 0xFF);

        [Tooltip("選んでいないボタンの文字の色")]
        [SerializeField] private Color unselectedTextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);

        private bool shown;

        private static GameSettings Settings => SaveStore.Settings;

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

        private void Update()
        {
            // メニューや休憩案内が出ている間は隠す(2枚を重ねて出さない)
            bool visible = shown && !InputGate.GrabBlocked;
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }

        public void Show()
        {
            Refresh();
            shown = true;
            panelRoot.SetActive(!InputGate.GrabBlocked);
        }

        /// <summary>「はじめる」(ボタンから呼ぶ)。</summary>
        public void StartGame()
        {
            shown = false;
            panelRoot.SetActive(false);
            game.StartGame();
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

        public void Easy() => Set(() => Settings.cpuLevel = 0);
        public void Normal() => Set(() => Settings.cpuLevel = 1);
        public void Hard() => Set(() => Settings.cpuLevel = 2);
        public void AssistOn() => Set(() => Settings.hitAssist = true);
        public void AssistOff() => Set(() => Settings.hitAssist = false);
        public void SelectLeftHand() => Set(() => Settings.leftHanded = true);
        public void SelectRightHand() => Set(() => Settings.leftHanded = false);

        private static void Set(System.Action change)
        {
            change();
            SaveStore.SaveSettings();
        }

        private void Refresh()
        {
            if (heightText != null)
            {
                heightText.text = $"{Settings.heightCm} cm";
            }

            bool[] selected =
            {
                Settings.cpuLevel == 0, Settings.cpuLevel == 1, Settings.cpuLevel == 2,
                Settings.hitAssist, !Settings.hitAssist, Settings.leftHanded, !Settings.leftHanded,
            };
            for (int i = 0; i < selected.Length; i++)
            {
                // 選んでいる方は色に加えて「●」の印でも示す(色だけに頼らない)
                if (i < choiceImages.Length && choiceImages[i] != null)
                {
                    choiceImages[i].color = selected[i] ? selectedColor : unselectedColor;
                }

                if (i < choiceTexts.Length && choiceTexts[i] != null)
                {
                    choiceTexts[i].text = selected[i] ? $"● {Labels[i]}" : Labels[i];
                    choiceTexts[i].color = selected[i] ? selectedTextColor : unselectedTextColor;
                }
            }
        }
    }
}
