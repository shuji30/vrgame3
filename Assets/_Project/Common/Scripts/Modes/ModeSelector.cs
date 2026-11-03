using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// 「はじめに」パネルのモードの行と、強さ / 人数の行(docs/art-direction.md 6.10)。3種目で共通に使う。
    /// 選んだモードと人数はアプリを閉じるまで覚え(SessionChoice)、CPU の強さは設定に保存する。
    /// 交代プレイの2人目以降は、モードと人数を変えられないようにする。
    /// </summary>
    public class ModeSelector : MonoBehaviour
    {
        [Tooltip("記録の種目名(SportIds の値)")]
        [SerializeField] private string sportId = SportIds.Bowling;

        [Tooltip("この種目で選べるモード(ボタンの順)")]
        [SerializeField] private SportMode[] modes = { SportMode.Solo };

        [Tooltip("モードのボタンの背景(modes と同じ順)")]
        [SerializeField] private Image[] modeImages = new Image[0];

        [Tooltip("モードのボタンの文字(modes と同じ順)")]
        [SerializeField] private TMP_Text[] modeTexts = new TMP_Text[0];

        [Tooltip("強さ / 人数のボタンの背景(3つ)")]
        [SerializeField] private Image[] optionImages = new Image[3];

        [Tooltip("強さ / 人数のボタンの文字(3つ)")]
        [SerializeField] private TMP_Text[] optionTexts = new TMP_Text[3];

        [Tooltip("強さ / 人数のボタン(3つ)")]
        [SerializeField] private Button[] optionButtons = new Button[3];

        [Tooltip("「はじめる」ボタンの文字(チャレンジでは「かだいをえらぶ」にする)")]
        [SerializeField] private TMP_Text startLabel;

        [Tooltip("パネルの見出し(交代プレイでは「● プレイヤー2」にする)")]
        [SerializeField] private TMP_Text titleText;

        private static readonly string[] ModeLabels = { "ひとりで", "CPUと", "こうたいで", "チャレンジ" };
        private static readonly string[] LevelLabels = { "やさしい ▲", "ふつう ▲▲", "つよい ▲▲▲" };
        private static readonly string[] CountLabels = { "2人", "3人", "4人" };

        private static readonly Color Selected = new Color32(0xFF, 0xD2, 0x4A, 0xFF);
        private static readonly Color Unselected = new Color32(0x6C, 0x8F, 0xCF, 0xFF);
        private static readonly Color Disabled = new Color32(0xB8, 0xB4, 0xC8, 0xFF);
        private static readonly Color SelectedText = new Color32(0x3A, 0x35, 0x50, 0xFF);
        private static readonly Color UnselectedText = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        private static readonly Color DisabledText = new Color32(0x4A, 0x46, 0x60, 0xFF);

        /// <summary>選んだモードなどが変わったとき。</summary>
        public event Action Changed;

        public string SportId => sportId;

        public SessionChoice.Choice Choice => SessionChoice.For(sportId, modes.Length > 0 ? modes[0] : SportMode.Solo);

        public SportMode Mode => Choice.Mode;

        private bool Locked => TurnSession.Active && TurnSession.Current > 0;

        private void OnEnable()
        {
            SaveStore.Changed += Refresh;
        }

        private void OnDisable()
        {
            SaveStore.Changed -= Refresh;
        }

        private void Start()
        {
            // 前に選んだモードがこの種目に無ければ最初のモードにする
            if (Array.IndexOf(modes, Choice.Mode) < 0 && modes.Length > 0)
            {
                Choice.Mode = modes[0];
            }

            Refresh();
        }

        public void SelectMode0() => SelectMode(0);
        public void SelectMode1() => SelectMode(1);
        public void SelectMode2() => SelectMode(2);
        public void SelectMode3() => SelectMode(3);
        public void SelectOption0() => SelectOption(0);
        public void SelectOption1() => SelectOption(1);
        public void SelectOption2() => SelectOption(2);

        private void SelectMode(int index)
        {
            if (Locked || index >= modes.Length)
            {
                return;
            }

            Choice.Mode = modes[index];
            Refresh();
            Changed?.Invoke();
        }

        private void SelectOption(int index)
        {
            if (Locked)
            {
                return;
            }

            if (Choice.Mode == SportMode.Cpu)
            {
                SaveStore.Settings.cpuLevel = index;
                SaveStore.SaveSettings();
            }
            else if (Choice.Mode == SportMode.Turns)
            {
                Choice.PlayerCount = index + 2;
            }

            Refresh();
            Changed?.Invoke();
        }

        /// <summary>表示を書き直す。</summary>
        public void Refresh()
        {
            bool locked = Locked;
            for (int i = 0; i < modes.Length && i < modeImages.Length; i++)
            {
                bool selected = modes[i] == Choice.Mode;
                SetButton(modeImages[i], i < modeTexts.Length ? modeTexts[i] : null, ModeLabels[(int)modes[i]], selected,
                    !locked || selected);
            }

            SportMode mode = Choice.Mode;
            for (int i = 0; i < 3; i++)
            {
                string label = mode == SportMode.Turns ? CountLabels[i] : LevelLabels[i];
                bool usable = !locked && (mode == SportMode.Cpu || mode == SportMode.Turns);
                bool selected = mode == SportMode.Cpu
                    ? SaveStore.Settings.cpuLevel == i
                    : mode == SportMode.Turns && Choice.PlayerCount == i + 2;
                if (i < optionButtons.Length && optionButtons[i] != null)
                {
                    optionButtons[i].interactable = usable;
                }

                SetButton(i < optionImages.Length ? optionImages[i] : null, i < optionTexts.Length ? optionTexts[i] : null, label,
                    selected && usable, usable);
            }

            if (startLabel != null)
            {
                startLabel.text = mode == SportMode.Challenge ? "かだいをえらぶ" : "はじめる";
            }

            if (titleText != null)
            {
                titleText.text = TurnSession.Active ? TurnSession.Label(TurnSession.Current) : "はじめに";
            }
        }

        private static void SetButton(Image image, TMP_Text text, string label, bool selected, bool usable)
        {
            if (image != null)
            {
                image.color = !usable ? Disabled : selected ? Selected : Unselected;
            }

            if (text != null)
            {
                text.text = selected ? $"● {label}" : label;
                text.color = !usable ? DisabledText : selected ? SelectedText : UnselectedText;
            }
        }
    }
}
