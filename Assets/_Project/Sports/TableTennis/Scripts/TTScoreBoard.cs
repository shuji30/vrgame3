using TMPro;
using UnityEngine;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 台の横のスコア板。両者の点・サーブ権の印・CPU の強さと、決着したときの理由の短い文字を出す。
    /// </summary>
    public class TTScoreBoard : MonoBehaviour
    {
        [Tooltip("プレイヤーの点")]
        [SerializeField] private TMP_Text playerPoints;

        [Tooltip("CPU の点")]
        [SerializeField] private TMP_Text cpuPoints;

        [Tooltip("プレイヤーのサーブの印")]
        [SerializeField] private GameObject playerServeMark;

        [Tooltip("CPU のサーブの印")]
        [SerializeField] private GameObject cpuServeMark;

        [Tooltip("CPU の強さの文字")]
        [SerializeField] private TMP_Text levelText;

        [Tooltip("決着の理由の文字")]
        [SerializeField] private TMP_Text reasonText;

        [Tooltip("理由を出しておく秒数")]
        [SerializeField] private float reasonTime = 1.8f;

        private float hideReasonAt;

        private void Update()
        {
            if (reasonText != null && reasonText.gameObject.activeSelf && Time.time >= hideReasonAt)
            {
                reasonText.gameObject.SetActive(false);
            }
        }

        /// <summary>点とサーブ権と強さを書き直す。</summary>
        public void Refresh(TableTennisMatch match, string levelLabel)
        {
            if (playerPoints != null)
            {
                playerPoints.text = match.PlayerPoints.ToString();
            }

            if (cpuPoints != null)
            {
                cpuPoints.text = match.CpuPoints.ToString();
            }

            bool over = match.IsOver;
            if (playerServeMark != null)
            {
                playerServeMark.SetActive(!over && match.Server == TTSide.Player);
            }

            if (cpuServeMark != null)
            {
                cpuServeMark.SetActive(!over && match.Server == TTSide.Cpu);
            }

            if (levelText != null)
            {
                levelText.text = $"CPU:{levelLabel}";
            }
        }

        /// <summary>決着の理由を少しのあいだ出す。</summary>
        public void ShowReason(string text)
        {
            if (reasonText == null)
            {
                return;
            }

            reasonText.text = text;
            reasonText.gameObject.SetActive(true);
            hideReasonAt = Time.time + reasonTime;
        }
    }
}
