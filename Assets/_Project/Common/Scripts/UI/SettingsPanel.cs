using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// 共通の設定パネル(docs/plan.md「設定と保存」、見た目は docs/art-direction.md 6.6)。
    /// ハブの「せってい」看板とメニューの「せってい」から開く。上の2つのボタンで面を切り替える(M4)。
    /// 「からだ・いどう」:身長・利き手・連続移動(オン/オフと速さ3段)・休憩案内。
    /// 「けしき・おと」:時間帯(ハブでだけ変えられる)・音量2つ・「あそびかたを さいしょから」。
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

        [Header("面の切り替え(M4)")]
        [Tooltip("「からだ・いどう」の面")]
        [SerializeField] private GameObject bodyPage;

        [Tooltip("「けしき・おと」の面")]
        [SerializeField] private GameObject sceneryPage;

        [Tooltip("面の切り替えボタンの背景(からだ・いどう / けしき・おと)")]
        [SerializeField] private Image[] tabImages = new Image[2];

        [Tooltip("面の切り替えボタンの文字")]
        [SerializeField] private TMP_Text[] tabTexts = new TMP_Text[2];

        [Tooltip("時間帯のボタンの背景(あさ / ひる / ゆうやけ)")]
        [SerializeField] private Image[] timeImages = new Image[3];

        [Tooltip("時間帯のボタンの文字")]
        [SerializeField] private TMP_Text[] timeTexts = new TMP_Text[3];

        [Tooltip("時間帯のボタン(ハブ以外では押せない)")]
        [SerializeField] private Button[] timeButtons = new Button[3];

        [Tooltip("「ハブで かえられます」の添え書き")]
        [SerializeField] private TMP_Text timeNote;

        [Tooltip("音量(全体)の数字")]
        [SerializeField] private TMP_Text masterText;

        [Tooltip("音量(BGM・かんきょう音)の数字")]
        [SerializeField] private TMP_Text ambientText;

        [Tooltip("「つぎから また出ます」の知らせ")]
        [SerializeField] private TMP_Text resetMessage;

        [Tooltip("音量を変えたときに鳴らす試しの音")]
        [SerializeField] private AudioSource testSource;

        [SerializeField] private AudioClip testClip;

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
            sceneryShown = false;
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

        public void ShowBodyPage() => ShowPage(false);
        public void ShowSceneryPage() => ShowPage(true);

        public void TimeMorning() => SelectTime(0);
        public void TimeNoon() => SelectTime(1);
        public void TimeEvening() => SelectTime(2);

        public void MasterDown() => ChangeVolume(true, -1);
        public void MasterUp() => ChangeVolume(true, 1);
        public void AmbientDown() => ChangeVolume(false, -1);
        public void AmbientUp() => ChangeVolume(false, 1);

        /// <summary>「あそびかたを さいしょから」:てびきを見た印を消す。</summary>
        public void ResetTutorials()
        {
            // 交代プレイ中はその人の身長・利き手を保存しないため、押せないことにする
            if (TurnSession.Active)
            {
                return;
            }

            SaveStore.ResetTutorials();
            if (resetMessage != null)
            {
                resetMessage.text = "つぎから また出ます";
                resetMessageHideAt = Time.time + 2f;
            }
        }

        private float resetMessageHideAt;
        private bool sceneryShown;

        private void Update()
        {
            if (resetMessage != null && resetMessageHideAt > 0f && Time.time >= resetMessageHideAt)
            {
                resetMessage.text = string.Empty;
                resetMessageHideAt = 0f;
            }
        }

        private void ShowPage(bool scenery)
        {
            sceneryShown = scenery;
            Refresh();
        }

        private void SelectTime(int index)
        {
            if (!TimeOfDayApplier.CanChangeHere)
            {
                return;
            }

            Settings.timeOfDay = index;
            SaveStore.SaveSettings();
            if (TimeOfDayApplier.Current != null)
            {
                TimeOfDayApplier.Current.ChangeWithFade();
            }
        }

        private void ChangeVolume(bool master, int delta)
        {
            if (master)
            {
                Settings.masterVolume = Mathf.Clamp(Settings.masterVolume + delta, 0, 10);
            }
            else
            {
                Settings.ambientVolume = Mathf.Clamp(Settings.ambientVolume + delta, 0, 10);
            }

            SaveStore.SaveSettings();
            // 全体の行だけ試しの音を鳴らす(まわりの音の行は鳴らさない。docs/art-direction.md 6.16)
            if (master && testSource != null && testClip != null)
            {
                testSource.PlayOneShot(testClip, 0.4f);
            }
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
            if (bodyPage != null)
            {
                bodyPage.SetActive(!sceneryShown);
            }

            if (sceneryPage != null)
            {
                sceneryPage.SetActive(sceneryShown);
            }

            string[] tabLabels = { "からだ・いどう", "けしき・おと" };
            for (int i = 0; i < 2; i++)
            {
                SetTab(i < tabImages.Length ? tabImages[i] : null, i < tabTexts.Length ? tabTexts[i] : null, tabLabels[i],
                    sceneryShown == (i == 1));
            }

            bool canChangeTime = TimeOfDayApplier.CanChangeHere;
            string[] timeLabels = { "あさ", "ひる", "ゆうやけ" };
            for (int i = 0; i < 3; i++)
            {
                if (i < timeButtons.Length && timeButtons[i] != null)
                {
                    timeButtons[i].interactable = canChangeTime;
                }

                SetTab(i < timeImages.Length ? timeImages[i] : null, i < timeTexts.Length ? timeTexts[i] : null, timeLabels[i],
                    Settings.timeOfDay == i, canChangeTime);
            }

            if (timeNote != null)
            {
                timeNote.text = canChangeTime ? string.Empty : "ハブで かえられます";
            }

            if (masterText != null)
            {
                masterText.text = Settings.masterVolume.ToString();
            }

            if (ambientText != null)
            {
                ambientText.text = Settings.ambientVolume.ToString();
            }

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

        private void SetTab(Image image, TMP_Text text, string label, bool selected, bool usable = true)
        {
            if (image != null)
            {
                image.color = !usable ? disabledColor : selected ? selectedColor : unselectedColor;
            }

            if (text != null)
            {
                text.text = selected ? $"● {label}" : label;
                text.color = !usable ? disabledTextColor : selected ? selectedTextColor : unselectedTextColor;
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
