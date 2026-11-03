using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// きせかえのパネル(docs/plan.md M4「見た目のご褒美」、見た目は docs/art-direction.md 6.15)。ハブの「きせかえ」看板から開く。
    /// 品ごとに1行、4つ(標準+3つ)のボタン。まだ手に入れていないものは使えないボタンにして条件を書く。押した瞬間に選んで保存する。
    /// </summary>
    public class CosmeticPanel : MonoBehaviour
    {
        [Tooltip("パネル全体")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("ボタンの背景(品ごとに4つ × 4行。ボール・弓・ラケット・リストバンドの順)")]
        [SerializeField] private Image[] images = new Image[16];

        [Tooltip("ボタンの文字")]
        [SerializeField] private TMP_Text[] texts = new TMP_Text[16];

        [Tooltip("ボタン")]
        [SerializeField] private Button[] buttons = new Button[16];

        [Tooltip("選んだときの音")]
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip equipClip;

        [Tooltip("頭からパネルまでの距離(m)")]
        [SerializeField] private float distance = 1.1f;

        private static readonly Color Selected = new Color32(0xFF, 0xD2, 0x4A, 0xFF);
        private static readonly Color Unselected = new Color32(0x6C, 0x8F, 0xCF, 0xFF);
        private static readonly Color Locked = new Color32(0xB8, 0xB4, 0xC8, 0xFF);
        private static readonly Color SelectedText = new Color32(0x3A, 0x35, 0x50, 0xFF);
        private static readonly Color UnselectedText = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        private static readonly Color LockedText = new Color32(0x4A, 0x46, 0x60, 0xFF);

        private void Awake()
        {
            panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            InputGate.UnblockGrab(this);
        }

        /// <summary>頭の前に開く(看板から呼ぶ)。</summary>
        public void Open()
        {
            Transform head = PanelPlacement.FindHead();
            if (head != null)
            {
                Pose pose = PanelPlacement.InFrontOfHead(head, distance, 0.15f);
                panelRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }

            Refresh();
            panelRoot.SetActive(true);
            InputGate.BlockGrab(this);
        }

        public void Close()
        {
            panelRoot.SetActive(false);
            InputGate.UnblockGrab(this);
        }

        /// <summary>ボタンから呼ぶ(番号 = 行 × 4 + 列)。</summary>
        public void Select(int index)
        {
            var item = (RewardCatalog.Item)(index / 4);
            int style = index % 4;
            if (!RewardCatalog.IsUnlocked(item, style))
            {
                return;
            }

            RewardCatalog.Select(item, style);
            if (source != null && equipClip != null)
            {
                source.PlayOneShot(equipClip, 0.3f);
            }

            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < 16; i++)
            {
                var item = (RewardCatalog.Item)(i / 4);
                int style = i % 4;
                bool unlocked = RewardCatalog.IsUnlocked(item, style);
                bool selected = RewardCatalog.Selected(item) == style;
                if (i < buttons.Length && buttons[i] != null)
                {
                    buttons[i].interactable = unlocked;
                }

                if (i < images.Length && images[i] != null)
                {
                    images[i].color = !unlocked ? Locked : selected ? Selected : Unselected;
                }

                if (i < texts.Length && texts[i] != null)
                {
                    string name = RewardCatalog.Name(item, style);
                    texts[i].text = !unlocked ? $"?\n<size=70%>{RewardCatalog.Condition(item, style)}</size>"
                        : selected ? $"● {name}" : name;
                    texts[i].color = !unlocked ? LockedText : selected ? SelectedText : UnselectedText;
                }
            }
        }
    }
}
