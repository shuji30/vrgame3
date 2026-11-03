using System;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 腕前ポイントの決まり(docs/plan.md「腕前ポイントとランク」)を Inspector で調整するための設定アセット。
    /// Resources/SkillPointRules.asset に置く。無ければ企画書の既定値を使う。
    /// </summary>
    [CreateAssetMenu(menuName = "VRGame3/腕前ポイントの決まり", fileName = "SkillPointRules")]
    public class SkillPointRules : ScriptableObject
    {
        /// <summary>種目ごとの基準スコアと割る数。</summary>
        [Serializable]
        public class SportRule
        {
            [Tooltip("種目の名前(SportIds の値)")]
            public string sportId;

            [Tooltip("スコアの差を割る数")]
            public float divisor = 2f;

            [Tooltip("ランクごとの基準スコア(かいがら / さんご / しんじゅ / にじいろ)")]
            public int[] baseScores = new int[SkillRank.Count];
        }

        [Tooltip("ランクごとの必要ポイント(かいがら / さんご / しんじゅ / にじいろ)")]
        [SerializeField] private int[] requiredPoints = { 0, 100, 300, 600 };

        [Tooltip("最後まで遊んだときのボーナス")]
        [SerializeField] private int finishBonus = 5;

        [Tooltip("スコアによる増減の下限")]
        [SerializeField] private int minSkillDelta = -20;

        [Tooltip("スコアによる増減の上限")]
        [SerializeField] private int maxSkillDelta = 40;

        [Tooltip("種目ごとの決まり")]
        [SerializeField] private SportRule[] sports =
        {
            new SportRule { sportId = SportIds.Bowling, divisor = 2f, baseScores = new[] { 70, 110, 150, 190 } },
            new SportRule { sportId = SportIds.Archery, divisor = 1.5f, baseScores = new[] { 60, 90, 120, 150 } },
        };

        private static SkillPointRules cached;

        /// <summary>使う決まり(Resources にあればそれ、無ければ既定値)。</summary>
        public static SkillPointRules Current
        {
            get
            {
                if (cached == null)
                {
                    cached = Resources.Load<SkillPointRules>("SkillPointRules");
                    if (cached == null)
                    {
                        cached = CreateInstance<SkillPointRules>();
                    }
                }

                return cached;
            }
        }

        /// <summary>1ゲームの結果をポイントとランクに当てはめる。</summary>
        public SkillPoints.Outcome Apply(string sportId, int score, int points, int rank)
        {
            SportRule rule = Array.Find(sports, s => s.sportId == sportId) ?? sports[0];
            return SkillPoints.Apply(score, points, rank, rule.baseScores, rule.divisor, requiredPoints, finishBonus,
                minSkillDelta, maxSkillDelta);
        }
    }
}
