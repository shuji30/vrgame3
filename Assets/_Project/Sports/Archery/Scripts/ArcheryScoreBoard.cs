using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// 射つ位置の横の小型スコア板(ラウンド・6本の点・ラウンド合計・総合計・残り本数)と、
    /// 的の横に浮かぶ点数、空中のラウンドの見出しを出す。docs/art-direction.md「M2 アーチェリー場の仮組み」と 7.3。
    /// </summary>
    public class ArcheryScoreBoard : MonoBehaviour
    {
        [Header("小型スコア板")]
        [SerializeField] private TMP_Text roundText;

        [Tooltip("6本の点の文字")]
        [SerializeField] private TMP_Text[] slotTexts = new TMP_Text[ArcheryScoreSheet.ArrowsPerRound];

        [Tooltip("6本の点の枠")]
        [SerializeField] private Image[] slotImages = new Image[ArcheryScoreSheet.ArrowsPerRound];

        [SerializeField] private TMP_Text roundsText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text leftText;

        [Header("的の横の点数")]
        [Tooltip("的の横に出す 3D の文字")]
        [SerializeField] private TextMeshPro hitText;

        [Tooltip("的の横の点数を出しておく秒数")]
        [SerializeField] private float hitTextTime = 2.5f;

        [Tooltip("文字の高さ = 距離 × この値(m)")]
        [SerializeField] private float hitTextHeightPerMeter = 0.04f;

        [Header("ラウンドの見出し")]
        [Tooltip("空中に出す「ラウンド2 20m」")]
        [SerializeField] private TextMeshPro bannerText;

        [SerializeField] private float bannerTime = 2f;

        [Header("色")]
        [SerializeField] private Color slotColor = new Color32(0x6C, 0x8F, 0xCF, 0xFF);
        [SerializeField] private Color currentSlotColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);
        [SerializeField] private Color normalTextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        [SerializeField] private Color tenColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);
        [SerializeField] private Color xColor = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        [SerializeField] private Color missColor = new Color32(0xB8, 0xB4, 0xC8, 0xFF);

        private Coroutine hitRoutine;
        private Coroutine bannerRoutine;

        private void Awake()
        {
            if (hitText != null)
            {
                hitText.gameObject.SetActive(false);
            }

            if (bannerText != null)
            {
                bannerText.gameObject.SetActive(false);
            }
        }

        /// <summary>小型スコア板を書き直す。</summary>
        public void Refresh(ArcheryScoreSheet sheet, float distance)
        {
            int round = sheet.CurrentRound;
            if (roundText != null)
            {
                roundText.text = $"ラウンド{round + 1}  {distance:0}m";
            }

            for (int i = 0; i < ArcheryScoreSheet.ArrowsPerRound; i++)
            {
                if (i < slotTexts.Length && slotTexts[i] != null)
                {
                    slotTexts[i].text = sheet.GetSymbol(round, i);
                }

                if (i < slotImages.Length && slotImages[i] != null)
                {
                    bool current = !sheet.IsGameOver && i == sheet.ArrowsInCurrentRound;
                    slotImages[i].color = current ? currentSlotColor : slotColor;
                }
            }

            if (roundsText != null)
            {
                string text = string.Empty;
                for (int r = 0; r < ArcheryScoreSheet.RoundCount; r++)
                {
                    bool played = sheet.GetScores(r).Count > 0;
                    text += $"R{r + 1} {(played ? sheet.GetRoundTotal(r).ToString() : "—")}   ";
                }

                roundsText.text = text.TrimEnd();
            }

            if (totalText != null)
            {
                totalText.text = $"ごうけい {sheet.GetTotal()}";
            }

            if (leftText != null)
            {
                leftText.text = $"のこり {sheet.ArrowsLeft}本";
            }
        }

        /// <summary>的の横に点数を出す(0.2m だけゆっくり浮かせて消す)。</summary>
        public void ShowHit(ArcheryTarget target, int score, bool isX, bool onTarget)
        {
            if (hitText == null || target == null)
            {
                return;
            }

            float distance = Mathf.Max(1f, target.transform.position.z);
            float height = distance * hitTextHeightPerMeter;
            string text = !onTarget || score == 0 ? "-" : isX ? "X" : score.ToString();
            Color color = !onTarget || score == 0 ? missColor : isX ? xColor : score == 10 ? tenColor : normalTextColor;
            float scale = isX ? 1.5f : 1f;

            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
            }

            hitRoutine = StartCoroutine(FloatText(target, text, color, height * scale, onTarget ? hitTextTime : 1.5f));
        }

        /// <summary>空中にラウンドの見出しを出す。</summary>
        public void ShowBanner(int round, float distance)
        {
            if (bannerText == null)
            {
                return;
            }

            if (bannerRoutine != null)
            {
                StopCoroutine(bannerRoutine);
            }

            bannerRoutine = StartCoroutine(Banner($"ラウンド{round + 1}  {distance:0}m"));
        }

        private IEnumerator FloatText(ArcheryTarget target, string text, Color color, float height, float time)
        {
            hitText.text = text;
            hitText.color = color;
            // TextMeshPro はフォントサイズ 10 でおよそ高さ 1m
            hitText.fontSize = height * 10f;
            Vector3 start = target.transform.position + new Vector3(target.Diameter * 0.75f + height, 0f, 0f);
            hitText.transform.position = start;
            hitText.gameObject.SetActive(true);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                hitText.transform.position = start + Vector3.up * (0.2f * t / time);
                yield return null;
            }

            hitText.gameObject.SetActive(false);
            hitRoutine = null;
        }

        private IEnumerator Banner(string text)
        {
            bannerText.text = text;
            bannerText.gameObject.SetActive(true);
            yield return new WaitForSeconds(bannerTime);
            bannerText.gameObject.SetActive(false);
            bannerRoutine = null;
        }
    }
}
