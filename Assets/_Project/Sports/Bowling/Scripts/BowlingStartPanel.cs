using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// 投げ始める前に出す「はじめに」パネル。周囲確認とストラップの案内を出し、身長と利き手を設定してから
    /// 「はじめる」を押すまで、ボールを掴めないようにする(企画書の快適性と安全設計、M1)。
    /// 10フレームが終わると少し待ってから結果を出し、「もういちど」でこのパネルを出し直す(次の人が設定し直せるように)。
    /// 身長と利き手は保存した値(SaveStore)から始め、変えたらすぐ保存する。
    /// </summary>
    public class BowlingStartPanel : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private BowlingGame game;

        [Tooltip("ボール置き台(身長と利き手に合わせて動かす)")]
        [SerializeField] private BowlingRack rack;

        [Tooltip("モードの行")]
        [SerializeField] private ModeSelector modeSelector;

        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("身長の数字")]
        [SerializeField] private TMP_Text heightText;

        [Tooltip("「左」ボタンの背景")]
        [SerializeField] private Image leftButtonImage;

        [Tooltip("「右」ボタンの背景")]
        [SerializeField] private Image rightButtonImage;

        [Tooltip("「左」ボタンの文字")]
        [SerializeField] private TMP_Text leftButtonText;

        [Tooltip("「右」ボタンの文字")]
        [SerializeField] private TMP_Text rightButtonText;

        [Header("身長")]
        [Tooltip("ボタン1回で変わる量(cm)")]
        [SerializeField] private int heightStep = 5;

        [Header("その他")]
        [Tooltip("ゲームの結果の表示。「もういちど」でこのパネルを出し直す")]
        [SerializeField] private ResultPanel resultPanel;

        [Tooltip("10フレームが終わってから、結果(結果の表示がなければこのパネル)を出すまでの秒数")]
        [SerializeField] private float showAgainDelay = 3f;

        [Tooltip("選んでいる利き手のボタンの色")]
        [SerializeField] private Color selectedColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("選んでいない利き手のボタンの色")]
        [SerializeField] private Color unselectedColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);

        [Tooltip("選んでいるボタンの文字の色")]
        [SerializeField] private Color selectedTextColor = new Color32(0x3A, 0x35, 0x50, 0xFF);

        [Tooltip("選んでいないボタンの文字の色")]
        [SerializeField] private Color unselectedTextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);

        private void OnEnable()
        {
            if (game != null)
            {
                game.GameFinished += OnGameFinished;
                game.WaitingForStart += Show;
            }

            // メニューの設定パネルで身長・利き手を変えたときも、表示とボール置き台に反映する
            SaveStore.Changed += OnSaveChanged;
            if (resultPanel != null)
            {
                resultPanel.Retry.AddListener(Show);
            }
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.GameFinished -= OnGameFinished;
                game.WaitingForStart -= Show;
            }

            SaveStore.Changed -= OnSaveChanged;
            if (resultPanel != null)
            {
                resultPanel.Retry.RemoveListener(Show);
            }
        }

        private void OnSaveChanged()
        {
            ApplySettings(false);
        }

        private void Update()
        {
            // 投球中に設定が変わったときは、投げ終わって待っている状態になってから置き台を動かす
            TryMoveRack();

            // メニューや設定パネル・休憩案内が出ている間は隠す(2枚を重ねて出さない。docs/art-direction.md 6.4)
            bool visible = shown && !InputGate.GrabBlocked;
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }

        private void Start()
        {
            ApplySettings(false);
            Show();
        }

        private static GameSettings Settings => SaveStore.Settings;

        // パネルを出している状態か(メニューが出ている間は隠すが、この値は変えない)
        private bool shown;

        // 置き台に最後に当てはめた設定
        private int appliedHeight = -1;
        private bool appliedLeftHanded;

        /// <summary>身長を上げる(ボタンから呼ぶ)。</summary>
        public void IncreaseHeight()
        {
            Settings.heightCm = Mathf.Min(GameSettings.MaxHeightCm, Settings.heightCm + heightStep);
            ApplySettings(true);
        }

        /// <summary>身長を下げる(ボタンから呼ぶ)。</summary>
        public void DecreaseHeight()
        {
            Settings.heightCm = Mathf.Max(GameSettings.MinHeightCm, Settings.heightCm - heightStep);
            ApplySettings(true);
        }

        /// <summary>左利きにする(ボタンから呼ぶ)。</summary>
        public void SelectLeftHand()
        {
            Settings.leftHanded = true;
            ApplySettings(true);
        }

        /// <summary>右利きにする(ボタンから呼ぶ)。</summary>
        public void SelectRightHand()
        {
            Settings.leftHanded = false;
            ApplySettings(true);
        }

        /// <summary>パネルを閉じて投げ始める(ボタンから呼ぶ)。</summary>
        public void StartGame()
        {
            shown = false;
            panelRoot.SetActive(false);
            if (game == null)
            {
                return;
            }

            SportMode mode = modeSelector != null ? modeSelector.Mode : SportMode.Solo;
            if (mode == SportMode.Challenge)
            {
                // 課題を選ぶまでボールは掴めないまま
                game.ShowChallengeList();
                return;
            }

            // 毎ゲームの始めに新しいゲームにする(途中のゲームから「はじめに」を出し直したときも。てびきもここで始める)
            game.StartSelectedMode();

            game.SetInputLocked(false);
        }

        /// <summary>パネルを出し、閉じるまでボールを掴めないようにする。</summary>
        public void Show()
        {
            shown = true;
            panelRoot.SetActive(!InputGate.GrabBlocked);
            if (modeSelector != null)
            {
                modeSelector.Refresh();
            }

            if (game != null)
            {
                game.SetInputLocked(true);
            }
        }

        private void OnGameFinished(GameRecordResult result)
        {
            // 合計点を見せている間もボールは掴めないようにし、パネルの「はじめる」だけから次のゲームを始める
            if (game != null)
            {
                game.SetInputLocked(true);
            }

            StartCoroutine(ShowAfterDelay(result));
        }

        private IEnumerator ShowAfterDelay(GameRecordResult result)
        {
            yield return new WaitForSeconds(showAgainDelay);
            if (resultPanel != null)
            {
                resultPanel.Show(result);
            }
            else
            {
                Show();
            }
        }

        /// <summary>設定を表示とボール置き台に反映する。save が true なら保存もする。</summary>
        private void ApplySettings(bool save)
        {
            if (save)
            {
                SaveStore.SaveSettings();
            }

            int heightCm = Settings.heightCm;
            bool leftHanded = Settings.leftHanded;
            if (heightText != null)
            {
                heightText.text = $"{heightCm} cm";
            }

            // 選んでいる方は色に加えて「●」の印でも示す(色だけに頼らない)
            SetHandButton(leftButtonImage, leftButtonText, "左", leftHanded);
            SetHandButton(rightButtonImage, rightButtonText, "右", !leftHanded);

            TryMoveRack();
        }

        /// <summary>
        /// 置き台を今の設定に合わせる。置き台を動かすと掴んでいないボールも置き直すので、
        /// ボールが転がっている間や数えている間は動かさず、待っている状態になるまで後回しにする。
        /// </summary>
        private void TryMoveRack()
        {
            if (rack == null || (Settings.heightCm == appliedHeight && Settings.leftHanded == appliedLeftHanded))
            {
                return;
            }

            if (game != null && !game.CanMoveRack)
            {
                return;
            }

            appliedHeight = Settings.heightCm;
            appliedLeftHanded = Settings.leftHanded;
            rack.Apply(appliedHeight, appliedLeftHanded);
        }

        private void SetHandButton(Image image, TMP_Text text, string label, bool selected)
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
    }
}
