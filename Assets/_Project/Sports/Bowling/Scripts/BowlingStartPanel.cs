using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// 投げ始める前に出す「はじめに」パネル。周囲確認とストラップの案内を出し、身長と利き手を設定してから
    /// 「はじめる」を押すまで、ボールを掴めないようにする(企画書の快適性と安全設計、M1)。
    /// 10フレームが終わると少し待ってからもう一度出し、次の人が設定し直せるようにする(設定の保存は M2)。
    /// </summary>
    public class BowlingStartPanel : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private BowlingGame game;

        [Tooltip("ボール置き台(身長と利き手に合わせて動かす)")]
        [SerializeField] private BowlingRack rack;

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
        [Tooltip("最初の身長(cm)")]
        [SerializeField] private int heightCm = 170;

        [Tooltip("ボタン1回で変わる量(cm)")]
        [SerializeField] private int heightStep = 5;

        [SerializeField] private int minHeightCm = 110;
        [SerializeField] private int maxHeightCm = 200;

        [Header("利き手")]
        [SerializeField] private bool leftHanded;

        [Header("その他")]
        [Tooltip("10フレームが終わってから、パネルをもう一度出すまでの秒数")]
        [SerializeField] private float showAgainDelay = 4f;

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
            }
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.GameFinished -= OnGameFinished;
            }
        }

        private void Start()
        {
            ApplySettings();
            Show();
        }

        /// <summary>身長を上げる(ボタンから呼ぶ)。</summary>
        public void IncreaseHeight()
        {
            heightCm = Mathf.Min(maxHeightCm, heightCm + heightStep);
            ApplySettings();
        }

        /// <summary>身長を下げる(ボタンから呼ぶ)。</summary>
        public void DecreaseHeight()
        {
            heightCm = Mathf.Max(minHeightCm, heightCm - heightStep);
            ApplySettings();
        }

        /// <summary>左利きにする(ボタンから呼ぶ)。</summary>
        public void SelectLeftHand()
        {
            leftHanded = true;
            ApplySettings();
        }

        /// <summary>右利きにする(ボタンから呼ぶ)。</summary>
        public void SelectRightHand()
        {
            leftHanded = false;
            ApplySettings();
        }

        /// <summary>パネルを閉じて投げ始める(ボタンから呼ぶ)。</summary>
        public void StartGame()
        {
            if (game != null && game.IsGameOver)
            {
                game.NewGame();
            }

            panelRoot.SetActive(false);
            SetBallsGrabbable(true);
        }

        /// <summary>パネルを出し、閉じるまでボールを掴めないようにする。</summary>
        public void Show()
        {
            panelRoot.SetActive(true);
            SetBallsGrabbable(false);
        }

        private void OnGameFinished(int total)
        {
            StartCoroutine(ShowAfterDelay());
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(showAgainDelay);
            Show();
        }

        private void ApplySettings()
        {
            if (heightText != null)
            {
                heightText.text = $"{heightCm} cm";
            }

            // 選んでいる方は色に加えて「●」の印でも示す(色だけに頼らない)
            SetHandButton(leftButtonImage, leftButtonText, "左", leftHanded);
            SetHandButton(rightButtonImage, rightButtonText, "右", !leftHanded);

            if (rack != null)
            {
                rack.Apply(heightCm, leftHanded);
            }
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

        private void SetBallsGrabbable(bool grabbable)
        {
            if (game == null)
            {
                return;
            }

            foreach (ThrowableObject ball in game.Balls)
            {
                if (ball != null && ball.TryGetComponent(out XRGrabInteractable grab))
                {
                    grab.enabled = grabbable;
                }
            }
        }
    }
}
