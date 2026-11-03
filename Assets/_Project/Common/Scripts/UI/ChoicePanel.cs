using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// 見出し・本文・ボタン(最大4つ)を出す汎用のパネル。交代の案内、順位表、チャレンジの一覧と結果などに使う
    /// (docs/art-direction.md 6.11・6.12)。押したときの処理は出すたびに渡す。
    /// メニューや休憩案内が出ている間は隠す(2枚を重ねない)。
    /// </summary>
    public class ChoicePanel : MonoBehaviour
    {
        /// <summary>ボタン1つ分。</summary>
        public struct Option
        {
            public string Label;
            public Action Action;
            public bool Primary;

            public Option(string label, Action action, bool primary = false)
            {
                Label = label;
                Action = action;
                Primary = primary;
            }
        }

        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("見出し")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("本文(複数行)")]
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("ボタンの背景(上から順)")]
        [SerializeField] private Image[] buttonImages = new Image[4];

        [Tooltip("ボタンの文字(上から順)")]
        [SerializeField] private TMP_Text[] buttonTexts = new TMP_Text[4];

        [Tooltip("主ボタンの色")]
        [SerializeField] private Color primaryColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Tooltip("普通のボタンの色")]
        [SerializeField] private Color normalColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);

        [Tooltip("主ボタンの文字の色")]
        [SerializeField] private Color primaryTextColor = new Color32(0x3A, 0x35, 0x50, 0xFF);

        [Tooltip("普通のボタンの文字の色")]
        [SerializeField] private Color normalTextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);

        [Tooltip("知らせの音を鳴らす AudioSource(2D)")]
        [SerializeField] private AudioSource source;

        [Tooltip("知らせの音の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.3f;

        [Tooltip("チャレンジのクリア・順位表で出す紙吹雪(なくてもよい)")]
        [SerializeField] private ParticleSystem celebration;

        [Tooltip("交代の案内の音")]
        [SerializeField] private AudioClip turnChangeClip;

        [Tooltip("チャレンジのクリアの音")]
        [SerializeField] private AudioClip clearClip;

        [Tooltip("チャレンジの「ざんねん」の音")]
        [SerializeField] private AudioClip failClip;

        public AudioClip TurnChangeClip => turnChangeClip;
        public AudioClip ClearClip => clearClip;
        public AudioClip FailClip => failClip;

        private readonly Action[] actions = new Action[4];

        /// <summary>紙吹雪を出す(チャレンジのクリア・順位表)。</summary>
        public void Celebrate()
        {
            if (celebration != null)
            {
                celebration.Play();
            }
        }

        /// <summary>知らせの音を鳴らす(クリア・交代など)。</summary>
        public void PlaySound(AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }
        private bool shown;

        public bool IsOpen => shown;

        private void Awake()
        {
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            bool visible = shown && !InputGate.GrabBlocked;
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }

        /// <summary>パネルを出す。</summary>
        public void Show(string title, string body, params Option[] options)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (bodyText != null)
            {
                bodyText.text = body;
            }

            for (int i = 0; i < buttonImages.Length; i++)
            {
                bool used = i < options.Length;
                actions[i] = used ? options[i].Action : null;
                if (buttonImages[i] == null)
                {
                    continue;
                }

                buttonImages[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                buttonImages[i].color = options[i].Primary ? primaryColor : normalColor;
                if (i < buttonTexts.Length && buttonTexts[i] != null)
                {
                    buttonTexts[i].text = options[i].Label;
                    buttonTexts[i].color = options[i].Primary ? primaryTextColor : normalTextColor;
                }
            }

            shown = true;
            panelRoot.SetActive(!InputGate.GrabBlocked);
        }

        public void Hide()
        {
            shown = false;
            panelRoot.SetActive(false);
        }

        public void OnButton0() => Press(0);
        public void OnButton1() => Press(1);
        public void OnButton2() => Press(2);
        public void OnButton3() => Press(3);

        private void Press(int index)
        {
            Action action = actions[index];
            Hide();
            action?.Invoke();
        }
    }
}
