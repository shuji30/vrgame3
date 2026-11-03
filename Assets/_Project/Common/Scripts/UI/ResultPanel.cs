using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRGame3.Common
{
    /// <summary>
    /// ゲームの終わりに出す結果の表示(両種目共通。docs/art-direction.md 6.9、7.3)。
    /// 合計を数え上げ、ベスト・ランク・ポイントの増減を出す。「もういちど」「ハブへ」を選べる。
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("合計の文字")]
        [SerializeField] private TMP_Text totalText;

        [Tooltip("見出し(「けっか」。勝敗のある種目では「かち! 11 ― 7」にする)")]
        [SerializeField] private TMP_Text headlineText;

        [Tooltip("「ベスト更新!」の札")]
        [SerializeField] private GameObject newBestBadge;

        [Tooltip("ベストの文字")]
        [SerializeField] private TMP_Text bestText;

        [Tooltip("ランクのメダル")]
        [SerializeField] private RankMedal medal;

        [Tooltip("ポイントの文字")]
        [SerializeField] private TMP_Text pointsText;

        [Tooltip("「ランクアップ!」の文字")]
        [SerializeField] private GameObject rankUpText;

        [Tooltip("合計を 0 から数え上げる秒数")]
        [SerializeField] private float countUpTime = 1f;

        [Tooltip("結果のジングル")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("結果のジングル")]
        [SerializeField] private AudioClip resultClip;

        [Tooltip("ランクアップの音")]
        [SerializeField] private AudioClip rankUpClip;

        [Range(0f, 1f)]
        [Tooltip("音量(0〜1)")]
        [SerializeField] private float volume = 0.4f;

        [Tooltip("ベスト更新かランクアップのときに出す紙吹雪(なくてもよい)")]
        [SerializeField] private ParticleSystem celebration;

        [Tooltip("「もういちど」を押したとき")]
        [SerializeField] private UnityEvent retry = new UnityEvent();

        // 結果を出している状態か(メニューが出ている間は隠すが、この値は変えない)
        private bool shown;

        /// <summary>結果が出ているか。</summary>
        public bool IsOpen => shown;

        /// <summary>「もういちど」を押したときに呼ばれる。</summary>
        public UnityEvent Retry => retry;

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Update()
        {
            // メニューや設定パネル・休憩案内が出ている間は隠す(2枚を重ねて出さない。docs/art-direction.md 6.4)
            bool visible = shown && !InputGate.GrabBlocked;
            if (panelRoot != null && panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }

        /// <summary>結果を出す。</summary>
        public void Show(GameRecordResult result)
        {
            Show(result, "けっか");
        }

        /// <summary>見出しを決めて結果を出す。</summary>
        public void Show(GameRecordResult result, string headline)
        {
            if (headlineText != null)
            {
                headlineText.text = headline;
            }

            shown = true;
            panelRoot.SetActive(!InputGate.GrabBlocked);
            if (newBestBadge != null)
            {
                newBestBadge.SetActive(result.NewBest);
            }

            int best = result.NewBest ? result.Score : result.BestBefore;
            if (bestText != null)
            {
                bestText.text = $"ベスト {best}";
            }

            if (medal != null)
            {
                medal.Show(result.Points.RankAfter);
            }

            if (pointsText != null)
            {
                int delta = result.Points.Delta;
                string sign = delta >= 0 ? "+" : "−";
                pointsText.text = $"ポイント {result.Points.PointsAfter}({sign}{Mathf.Abs(delta)})";
            }

            if (rankUpText != null)
            {
                rankUpText.SetActive(result.Points.RankedUp);
            }

            StopAllCoroutines();
            StartCoroutine(CountUp(result.Score));

            if (audioSource != null && resultClip != null)
            {
                audioSource.PlayOneShot(resultClip, volume);
            }

            if (result.Points.RankedUp && audioSource != null && rankUpClip != null)
            {
                StartCoroutine(PlayLater(rankUpClip, resultClip != null ? resultClip.length : 0f));
            }

            if ((result.NewBest || result.Points.RankedUp) && celebration != null)
            {
                celebration.Play();
            }
        }

        /// <summary>結果を閉じる。</summary>
        public void Hide()
        {
            StopAllCoroutines();
            shown = false;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        /// <summary>「もういちど」(ボタンから呼ぶ)。</summary>
        public void OnRetry()
        {
            Hide();
            retry.Invoke();
        }

        /// <summary>「ハブへ」(ボタンから呼ぶ)。</summary>
        public void OnHub()
        {
            Hide();
            SceneTransition.Load(SceneTransition.HubScene);
        }

        private IEnumerator CountUp(int score)
        {
            for (float t = 0f; t < countUpTime; t += Time.deltaTime)
            {
                SetTotal(Mathf.RoundToInt(Mathf.Lerp(0f, score, t / countUpTime)));
                yield return null;
            }

            SetTotal(score);
        }

        private IEnumerator PlayLater(AudioClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);
            audioSource.PlayOneShot(clip, volume);
        }

        private void SetTotal(int value)
        {
            if (totalText != null)
            {
                totalText.text = $"ごうけい {value}";
            }
        }
    }
}
