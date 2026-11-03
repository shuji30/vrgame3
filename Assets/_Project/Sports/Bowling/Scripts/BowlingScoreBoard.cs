using TMPro;
using UnityEngine;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// スコア板の表示。大型板(10フレームの表)と小型板(現在フレームと合計)、
    /// ストライクなどの大きな文字をまとめて更新する。
    /// </summary>
    public class BowlingScoreBoard : MonoBehaviour
    {
        [Header("大型スコア板")]
        [Tooltip("フレーム番号(1〜10)の文字。現在のフレームを色で示す")]
        [SerializeField] private TMP_Text[] frameNumberTexts = new TMP_Text[BowlingScoreSheet.FrameCount];

        [Tooltip("各フレームの投球記号(X / - と数字)")]
        [SerializeField] private TMP_Text[] rollTexts = new TMP_Text[BowlingScoreSheet.FrameCount];

        [Tooltip("各フレームまでの累計点")]
        [SerializeField] private TMP_Text[] scoreTexts = new TMP_Text[BowlingScoreSheet.FrameCount];

        [Tooltip("合計点")]
        [SerializeField] private TMP_Text totalText;

        [Header("小型スコア板")]
        [Tooltip("現在のフレームと何投目か")]
        [SerializeField] private TMP_Text smallFrameText;

        [Tooltip("合計点")]
        [SerializeField] private TMP_Text smallTotalText;

        [Header("大きな文字")]
        [Tooltip("STRIKE! などを出す文字")]
        [SerializeField] private TMP_Text messageText;

        [Tooltip("大きな文字を出しておく秒数")]
        [SerializeField] private float messageDuration = 2.5f;

        [Header("色")]
        [Tooltip("フレーム番号の通常の色")]
        [SerializeField] private Color normalColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);

        [Tooltip("いま投げているフレーム番号の色")]
        [SerializeField] private Color currentColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        private float messageHideTime;

        private void Update()
        {
            if (messageText != null && messageText.gameObject.activeSelf && Time.time >= messageHideTime)
            {
                messageText.gameObject.SetActive(false);
            }
        }

        /// <summary>スコア表の内容をすべて書き直す。</summary>
        public void Refresh(BowlingScoreSheet sheet)
        {
            for (int f = 0; f < BowlingScoreSheet.FrameCount; f++)
            {
                SetText(rollTexts, f, BuildRollText(sheet, f));
                int? score = sheet.GetCumulativeScore(f);
                SetText(scoreTexts, f, score.HasValue ? score.Value.ToString() : string.Empty);

                if (f < frameNumberTexts.Length && frameNumberTexts[f] != null)
                {
                    bool isCurrent = !sheet.IsGameOver && f == sheet.CurrentFrame;
                    frameNumberTexts[f].color = isCurrent ? currentColor : normalColor;
                }
            }

            string total = sheet.GetTotalScore().ToString();
            if (totalText != null)
            {
                totalText.text = total;
            }

            if (smallTotalText != null)
            {
                smallTotalText.text = $"TOTAL  {total}";
            }

            if (smallFrameText != null)
            {
                int roll = sheet.GetRolls(sheet.CurrentFrame).Count + 1;
                smallFrameText.text = sheet.IsGameOver
                    ? "GAME OVER"
                    : $"FRAME {sheet.CurrentFrame + 1}  BALL {roll}";
            }
        }

        /// <summary>大きな文字を一定時間出す。</summary>
        public void ShowMessage(string message)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = message;
            messageText.gameObject.SetActive(true);
            messageHideTime = Time.time + messageDuration;
        }

        private static string BuildRollText(BowlingScoreSheet sheet, int frame)
        {
            int count = sheet.GetRolls(frame).Count;
            if (count == 0)
            {
                return string.Empty;
            }

            // 1〜9フレームのストライクは X だけを出す
            if (frame < BowlingScoreSheet.FrameCount - 1 && sheet.GetRolls(frame)[0] == BowlingScoreSheet.PinCount)
            {
                return "X";
            }

            // 10フレーム目は3投分がマスに収まるよう、記号の間を詰める
            string separator = frame == BowlingScoreSheet.FrameCount - 1 ? string.Empty : " ";
            string text = sheet.GetRollSymbol(frame, 0);
            for (int i = 1; i < count; i++)
            {
                text += separator + sheet.GetRollSymbol(frame, i);
            }

            return text;
        }

        private static void SetText(TMP_Text[] texts, int index, string value)
        {
            if (index < texts.Length && texts[index] != null)
            {
                texts[index].text = value;
            }
        }
    }
}
