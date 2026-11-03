using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// 共通の設定パネル(docs/plan.md「設定と保存」)。ハブの「せってい」看板とメニューの「せってい」から開く。
    /// 変えた値はすぐ保存する(SaveStore)。今は身長と利き手。連続移動と休憩案内は M2 の順6・7で足す。
    /// この部品はパネル(panelRoot)とは別の、いつも有効な物に付ける。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("身長の数字")]
        [SerializeField] private TMP_Text heightText;

        [Tooltip("「左」ボタンの背景")]
        [SerializeField] private Image leftButtonImage;

        [Tooltip("「左」ボタンの文字")]
        [SerializeField] private TMP_Text leftButtonText;

        [Tooltip("「右」ボタンの背景")]
        [SerializeField] private Image rightButtonImage;

        [Tooltip("「右」ボタンの文字")]
        [SerializeField] private TMP_Text rightButtonText;

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

        private void Refresh()
        {
            if (heightText != null)
            {
                heightText.text = $"{Settings.heightCm} cm";
            }

            SetChoice(leftButtonImage, leftButtonText, "左", Settings.leftHanded);
            SetChoice(rightButtonImage, rightButtonText, "右", !Settings.leftHanded);
        }

        /// <summary>選んでいる方は色に加えて「●」の印でも示す(色だけに頼らない)。</summary>
        private void SetChoice(Image image, TMP_Text text, string label, bool selected)
        {
            if (image != null)
            {
                image.color = selected ? selectedColor : unselectedColor;
            }

            if (text != null)
            {
                text.text = selected ? $"● {label}" : label;
                text.color = selected ? selectedTextColor : unselectedTextColor;
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
