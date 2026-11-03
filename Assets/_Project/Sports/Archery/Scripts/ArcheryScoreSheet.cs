using System.Collections.Generic;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// アーチェリー1ゲーム(3ラウンド × 6本)のスコア表。Unity に依存しない。
    /// </summary>
    public class ArcheryScoreSheet
    {
        public const int RoundCount = 3;
        public const int ArrowsPerRound = 6;

        private readonly List<int>[] scores = new List<int>[RoundCount];
        private readonly List<bool>[] xs = new List<bool>[RoundCount];

        public ArcheryScoreSheet()
        {
            for (int i = 0; i < RoundCount; i++)
            {
                scores[i] = new List<int>(ArrowsPerRound);
                xs[i] = new List<bool>(ArrowsPerRound);
            }
        }

        /// <summary>いまのラウンド(0 始まり)。ゲーム終了後は最後のラウンド。</summary>
        public int CurrentRound { get; private set; }

        /// <summary>18本を射ち終えたか。</summary>
        public bool IsGameOver { get; private set; }

        /// <summary>
        /// 1本の結果を記録する。戻り値はこの1本でラウンドが終わったか(最後のラウンドならゲームの終わりでもある)。
        /// </summary>
        public bool AddArrow(int score, bool isX)
        {
            if (IsGameOver)
            {
                return false;
            }

            scores[CurrentRound].Add(score < 0 ? 0 : score > 10 ? 10 : score);
            xs[CurrentRound].Add(isX && score == 10);
            if (scores[CurrentRound].Count < ArrowsPerRound)
            {
                return false;
            }

            if (CurrentRound == RoundCount - 1)
            {
                IsGameOver = true;
            }
            else
            {
                CurrentRound++;
            }

            return true;
        }

        public IReadOnlyList<int> GetScores(int round) => scores[round];

        public bool IsX(int round, int index) => index < xs[round].Count && xs[round][index];

        public int GetRoundTotal(int round)
        {
            int total = 0;
            foreach (int s in scores[round])
            {
                total += s;
            }

            return total;
        }

        public int GetTotal()
        {
            int total = 0;
            for (int i = 0; i < RoundCount; i++)
            {
                total += GetRoundTotal(i);
            }

            return total;
        }

        /// <summary>X の本数(同点のときの目安。表示用)。</summary>
        public int GetXCount()
        {
            int count = 0;
            foreach (List<bool> round in xs)
            {
                foreach (bool x in round)
                {
                    count += x ? 1 : 0;
                }
            }

            return count;
        }

        /// <summary>残りの本数。</summary>
        public int ArrowsLeft
        {
            get
            {
                int used = 0;
                foreach (List<int> round in scores)
                {
                    used += round.Count;
                }

                return RoundCount * ArrowsPerRound - used;
            }
        }

        /// <summary>いまのラウンドで射った本数。</summary>
        public int ArrowsInCurrentRound => IsGameOver ? ArrowsPerRound : scores[CurrentRound].Count;

        /// <summary>表示用の記号。X、外れは -、それ以外は数字。まだなら空。</summary>
        public string GetSymbol(int round, int index)
        {
            if (index >= scores[round].Count)
            {
                return string.Empty;
            }

            if (IsX(round, index))
            {
                return "X";
            }

            int s = scores[round][index];
            return s == 0 ? "-" : s.ToString();
        }
    }
}
