using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// 共通の設定パネル(docs/plan.md「設定と保存」、見た目は docs/art-direction.md 6.6)。
    /// ハブの「せってい」看板とメニューの「せってい」から開く。中身は身長・利き手・連続移動(オン/オフと速さ3段)・休憩案内。
    /// 変えた値はすぐ保存する(SaveStore)。この部品はパネル(panelRoot)とは別の、いつも有効な物に付ける。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        /// <summary>選択肢のボタンの並び(choiceImages・choiceTexts・choiceButtons の順番)。</summary>
        public enum Choice
        {
            LeftHand,
            RightHand,
            MoveOff,
            MoveOn,
            SpeedSlow,
            SpeedNormal,
            SpeedFast,
            RestOn,
            RestOff,
        }

        private static readonly string[] Labels = { "左", "右", "オフ", "オン", "おそい", "ふつう", "はやい", "オン", "オフ" };

        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("身長の数字")]
        [SerializeField] private TMP_Text heightText;

        [Tooltip("選択肢のボタンの背景(Choice の順)")]
        [SerializeField] private Image[] choiceImages = new Image[9];

        [Tooltip("選択肢のボタンの文字(Choice の順)")]
        [SerializeField] private TMP_Text[] choiceTexts = new TMP_Text[9];

        [Tooltip("選択肢のボタン(Choice の順。速さは連続移動がオフの間は押せなくする)")]
        [SerializeField] private Button[] choiceButtons = new Button[9];

        [Tooltip("ボタン1回で変わる身長(cm)")]
        [SerializeField] private int heightStep = 5;

        [Tooltip("頭からパネルまでの距離(m)。看板から開くとき")]
        [SerializeField] private float distance = 1f;

        [Tooltip("目の高さからパネルの中心までの下げ幅(m)")]
        [SerializeField] private float drop = 0.15f;

        [Header("色")]
        [Tooltip("選んでいるボタンの色")]
        [SerializeField] private Color selectedColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("選んでいないボタンの色")]
        [SerializeField] private Color unselectedColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);

        [Tooltip("選んでいるボタンの文字の色")]
        [SerializeField] private Color selectedTextColor = new Color32(0x3A, 0x35, 0x50, 0xFF);

        [Tooltip("選んでいないボタンの文字の色")]
        [SerializeField] private Color unselectedTextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);

        [Tooltip("使えないボタンの色")]
        [SerializeField] private Color disabledColor = new Color32(0xB8, 0xB4, 0xC8, 0xFF);

        [Tooltip("使えないボタンの文字の色")]
        [SerializeField] private Color disabledTextColor = new Color32(0x4A, 0x46, 0x60, 0xFF);

        /// <summary>パネルが出ているか。</summary>
        public bool IsOpen { get; private set; }

        private static GameSettings Settings => SaveStore.Settings;

        private void Awake()
        {
            SetOpen(false);
        }

        private void OnEnable()
        {
            SaveStore.Changed += Refresh;
        }

        private void OnDisable()
        {
            SaveStore.Changed -= Refresh;
            InputGate.UnblockGrab(this);
        }

        /// <summary>頭の正面に開く(看板のボタンから呼ぶ)。</summary>
        public void Open()
        {
            Transform head = PanelPlacement.FindHead();
            if (head == null)
            {
                OpenAt(new Pose(panelRoot.transform.position, panelRoot.transform.rotation));
                return;
            }

            OpenAt(PanelPlacement.InFrontOfHead(head, distance, drop));
        }

        /// <summary>決めた位置に開く(メニューから開くときはメニューと同じ場所)。</summary>
        public void OpenAt(Pose pose)
        {
            panelRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            Refresh();
            SetOpen(true);
        }

        /// <summary>閉じる(「とじる」ボタンからも呼ぶ)。</summary>
        public void Close()
        {
            SetOpen(false);
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

        public void SelectLeftHand() => Set(() => Settings.leftHanded = true);
        public void SelectRightHand() => Set(() => Settings.leftHanded = false);
        public void MoveOff() => Set(() => Settings.continuousMove = false);
        public void MoveOn() => Set(() => Settings.continuousMove = true);
        public void SpeedSlow() => Set(() => Settings.moveSpeedLevel = 0);
        public void SpeedNormal() => Set(() => Settings.moveSpeedLevel = 1);
        public void SpeedFast() => Set(() => Settings.moveSpeedLevel = 2);
        public void RestOn() => Set(() => Settings.restReminder = true);
        public void RestOff() => Set(() => Settings.restReminder = false);

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

            bool move = Settings.continuousMove;
            bool[] selected =
            {
                Settings.leftHanded, !Settings.leftHanded, !move, move,
                Settings.moveSpeedLevel == 0, Settings.moveSpeedLevel == 1, Settings.moveSpeedLevel == 2,
                Settings.restReminder, !Settings.restReminder,
            };
            for (int i = 0; i < selected.Length; i++)
            {
                bool isSpeed = i >= (int)Choice.SpeedSlow && i <= (int)Choice.SpeedFast;
                SetChoice(i, Labels[i], selected[i], !isSpeed || move);
            }
        }

        /// <summary>選んでいる方は色に加えて「●」の印でも示す(色だけに頼らない)。使えないボタンは灰色で押せなくする。</summary>
        private void SetChoice(int index, string label, bool selected, bool usable)
        {
            if (index < choiceButtons.Length && choiceButtons[index] != null)
            {
                choiceButtons[index].interactable = usable;
            }

            if (index < choiceImages.Length && choiceImages[index] != null)
            {
                choiceImages[index].color = !usable ? disabledColor : selected ? selectedColor : unselectedColor;
            }

            if (index < choiceTexts.Length && choiceTexts[index] != null)
            {
                choiceTexts[index].text = selected ? $"● {label}" : label;
                choiceTexts[index].color = !usable ? disabledTextColor : selected ? selectedTextColor : unselectedTextColor;
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (panelRoot != null)
            {
                panelRoot.SetActive(open);
            }

            if (open)
            {
                InputGate.BlockGrab(this);
            }
            else
            {
                InputGate.UnblockGrab(this);
            }
        }
    }
}
