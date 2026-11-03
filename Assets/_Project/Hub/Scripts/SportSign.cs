using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRGame3.Common;

namespace VRGame3.Hub
{
    /// <summary>
    /// ハブの種目の看板。ランク・ポイント・ベストを出し、「あそぶ」でその種目の会場へ切り替える(docs/plan.md「島のハブ」)。
    /// 会場のシーンがまだ Build Settings に無いときは「じゅんびちゅう」にして押せなくする。
    /// </summary>
    public class SportSign : MonoBehaviour
    {
        [Tooltip("記録の種目名(SportIds の値)")]
        [SerializeField] private string sportId = SportIds.Bowling;

        [Tooltip("会場のシーンの名前")]
        [SerializeField] private string sceneName = "Bowling";

        [Tooltip("ランクのメダル")]
        [SerializeField] private RankMedal medal;

        [Tooltip("ポイントの文字")]
        [SerializeField] private TMP_Text pointsText;

        [Tooltip("ベストの文字")]
        [SerializeField] private TMP_Text bestText;

        [Tooltip("チャレンジのクリア数の文字")]
        [SerializeField] private TMP_Text challengeText;

        [Tooltip("「あそぶ」ボタン")]
        [SerializeField] private Button playButton;

        [Tooltip("「あそぶ」ボタンの文字")]
        [SerializeField] private TMP_Text playLabel;

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
            Refresh();
            bool ready = Application.CanStreamedLevelBeLoaded(sceneName);
            if (playButton != null)
            {
                playButton.interactable = ready;
            }

            if (playLabel != null && !ready)
            {
                playLabel.text = "じゅんびちゅう";
            }
        }

        /// <summary>会場へ行く(「あそぶ」ボタンから呼ぶ)。</summary>
        public void Play()
        {
            // 種目に1つ入ったら、ハブのてびき(看板の札)は見たことにする
            SaveStore.MarkTutorialSeen(HubTutorial.Id);
            SceneTransition.Load(sceneName);
        }

        private void Refresh()
        {
            SportRecord record = SaveStore.GetRecord(sportId);
            if (medal != null)
            {
                medal.Show(record.rank);
            }

            if (pointsText != null)
            {
                pointsText.text = $"ポイント {record.points}";
            }

            if (bestText != null)
            {
                bestText.text = record.plays > 0 ? $"ベスト {record.best}" : "ベスト —";
            }

            if (challengeText != null)
            {
                int total = ChallengeCatalog.ForSport(sportId).Count;
                int cleared = SaveStore.ClearedChallengeCount(sportId + ".");
                challengeText.text = cleared >= total && total > 0
                    ? $"チャレンジ {cleared}/{total} <color=#FFD24A>◎</color>"
                    : $"チャレンジ {cleared}/{total}";
            }
        }
    }
}
