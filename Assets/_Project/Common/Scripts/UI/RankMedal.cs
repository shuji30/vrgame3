using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// ランクのメダル(丸・縁・星・名前)を表示する。docs/art-direction.md 6.8。
    /// しんじゅは上に小さな白い丸、にじいろは縁を4色の帯にする。
    /// </summary>
    public class RankMedal : MonoBehaviour
    {
        [Tooltip("メダルの丸")]
        [SerializeField] private Image face;

        [Tooltip("縁(メダルより一回り大きい丸)")]
        [SerializeField] private Image rim;

        [Tooltip("星の文字")]
        [SerializeField] private TMP_Text stars;

        [Tooltip("ランクの名前")]
        [SerializeField] private TMP_Text rankName;

        [Tooltip("しんじゅの印(上の小さな白い丸)")]
        [SerializeField] private GameObject pearl;

        [Tooltip("にじいろの4色の帯")]
        [SerializeField] private GameObject rainbow;

        public void Show(int rank)
        {
            rank = SkillRank.Clamp(rank);
            if (face != null)
            {
                face.color = SkillRank.MainColor(rank);
            }

            if (rim != null)
            {
                rim.color = SkillRank.RimColor(rank);
                // にじいろは縁を帯で見せるので、普通の縁は隠す
                rim.enabled = rank != 3;
            }

            if (stars != null)
            {
                stars.text = SkillRank.Stars(rank);
            }

            if (rankName != null)
            {
                rankName.text = SkillRank.Name(rank);
            }

            if (pearl != null)
            {
                pearl.SetActive(rank == 2);
            }

            if (rainbow != null)
            {
                rainbow.SetActive(rank == 3);
            }
        }
    }
}
