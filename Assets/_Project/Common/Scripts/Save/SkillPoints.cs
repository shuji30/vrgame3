using System;

namespace VRGame3.Common
{
    /// <summary>
    /// 腕前ポイントの増減の計算(docs/plan.md「腕前ポイントとランク」)。Unity に依存しない。
    /// 1ゲームの増減 = 完走ボーナス + (スコア − 今のランクの基準スコア) ÷ 割る数(四捨五入、範囲に収める)。
    /// ランクは下がらず、ポイントは今のランクの必要ポイントより下にならない。
    /// </summary>
    public static class SkillPoints
    {
        /// <summary>1ゲームの結果でポイントとランクがどう変わるか。</summary>
        public struct Outcome
        {
            public int PointsBefore;
            public int PointsAfter;
            public int RankBefore;
            public int RankAfter;

            /// <summary>実際に増減した量(下限で止まった分は含まない)。</summary>
            public int Delta => PointsAfter - PointsBefore;

            public bool RankedUp => RankAfter > RankBefore;
        }

        /// <param name="score">ゲームのスコア</param>
        /// <param name="points">今のポイント</param>
        /// <param name="rank">今のランク(0 始まり)</param>
        /// <param name="baseScores">ランクごとの基準スコア</param>
        /// <param name="divisor">割る数</param>
        /// <param name="requiredPoints">ランクごとの必要ポイント(昇順)</param>
        public static Outcome Apply(int score, int points, int rank, int[] baseScores, float divisor, int[] requiredPoints,
            int finishBonus = 5, int minSkillDelta = -20, int maxSkillDelta = 40)
        {
            int ranks = Math.Min(baseScores.Length, requiredPoints.Length);
            rank = Math.Max(0, Math.Min(ranks - 1, rank));
            int skill = (int)Math.Round((score - baseScores[rank]) / (double)divisor, MidpointRounding.AwayFromZero);
            skill = Math.Max(minSkillDelta, Math.Min(maxSkillDelta, skill));

            int after = Math.Max(requiredPoints[rank], points + finishBonus + skill);
            int newRank = rank;
            for (int i = rank + 1; i < ranks; i++)
            {
                if (after >= requiredPoints[i])
                {
                    newRank = i;
                }
            }

            return new Outcome { PointsBefore = points, PointsAfter = after, RankBefore = rank, RankAfter = newRank };
        }
    }
}
