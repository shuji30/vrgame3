using System.Collections.Generic;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボウリング1ゲーム(10フレーム)のスコア表。Unity に依存しない計算だけを持つ。
    /// 投球ごとに倒した本数を AddRoll で渡すと、フレームの進行・ピンを並べ直すかどうか・
    /// 各フレームの累計点・表示用の記号(X / - と数字)を求める。
    /// </summary>
    public class BowlingScoreSheet
    {
        public const int FrameCount = 10;
        public const int PinCount = 10;

        private readonly List<int>[] frames = new List<int>[FrameCount];

        public BowlingScoreSheet()
        {
            for (int i = 0; i < FrameCount; i++)
            {
                frames[i] = new List<int>(3);
            }
        }

        /// <summary>いま投げているフレーム(0 始まり)。ゲーム終了後は最後のフレーム。</summary>
        public int CurrentFrame { get; private set; }

        /// <summary>10フレームを投げ終えたか。</summary>
        public bool IsGameOver { get; private set; }

        /// <summary>フレームの投球結果(倒した本数)。</summary>
        public IReadOnlyList<int> GetRolls(int frame) => frames[frame];

        /// <summary>
        /// 1投の結果を記録する。戻り値は「次の投球の前に10本すべてを並べ直すか」。
        /// false のときは倒れたピンだけを片付け、残りのピンに続けて投げる。
        /// </summary>
        public bool AddRoll(int pins)
        {
            if (IsGameOver)
            {
                return true;
            }

            List<int> rolls = frames[CurrentFrame];
            rolls.Add(Clamp(pins, 0, PinsStandingBefore(CurrentFrame, rolls.Count)));

            if (CurrentFrame < FrameCount - 1)
            {
                // 1〜9フレーム:ストライクか2投目でフレームが終わる
                if (rolls[0] == PinCount || rolls.Count == 2)
                {
                    CurrentFrame++;
                    return true;
                }

                return false;
            }

            return AddRollInLastFrame(rolls);
        }

        /// <summary>
        /// 10フレーム目。ストライクかスペアを取ると3投目まで投げられる。
        /// </summary>
        private bool AddRollInLastFrame(List<int> rolls)
        {
            switch (rolls.Count)
            {
                case 1:
                    return rolls[0] == PinCount;
                case 2:
                    if (rolls[0] == PinCount)
                    {
                        // 1投目ストライク:2投目もストライクなら並べ直す。どちらでも3投目がある
                        return rolls[1] == PinCount;
                    }

                    if (rolls[0] + rolls[1] == PinCount)
                    {
                        return true;
                    }

                    IsGameOver = true;
                    return true;
                default:
                    IsGameOver = true;
                    return true;
            }
        }

        /// <summary>
        /// そのフレームの rollIndex 投目を投げる前に立っているピンの本数。
        /// </summary>
        public int PinsStandingBefore(int frame, int rollIndex)
        {
            List<int> rolls = frames[frame];
            int standing = PinCount;
            for (int i = 0; i < rollIndex && i < rolls.Count; i++)
            {
                standing -= rolls[i];
                if (standing == 0)
                {
                    // 10フレーム目はストライク・スペアのあと並べ直す
                    standing = PinCount;
                }
            }

            return standing;
        }

        /// <summary>
        /// そのフレームまでの累計点。ストライク・スペアのボーナスがまだ決まらないときは null。
        /// </summary>
        public int? GetCumulativeScore(int frame)
        {
            int total = 0;
            for (int i = 0; i <= frame; i++)
            {
                int? score = GetFrameScore(i);
                if (score == null)
                {
                    return null;
                }

                total += score.Value;
            }

            return total;
        }

        /// <summary>決まっている分の合計点(ボーナス待ちのフレームは数えない)。</summary>
        public int GetTotalScore()
        {
            int total = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                int? score = GetFrameScore(i);
                if (score == null)
                {
                    break;
                }

                total += score.Value;
            }

            return total;
        }

        /// <summary>
        /// そのフレーム単体の点(ボーナス込み)。まだ決まらないときは null。
        /// </summary>
        public int? GetFrameScore(int frame)
        {
            List<int> rolls = frames[frame];
            if (frame == FrameCount - 1)
            {
                bool finished = IsGameOver;
                if (!finished)
                {
                    return null;
                }

                int sum = 0;
                foreach (int r in rolls)
                {
                    sum += r;
                }

                return sum;
            }

            if (rolls.Count == 0)
            {
                return null;
            }

            if (rolls[0] == PinCount)
            {
                return SumNextRolls(frame, 2, out int bonus) ? PinCount + bonus : (int?)null;
            }

            if (rolls.Count < 2)
            {
                return null;
            }

            int pins = rolls[0] + rolls[1];
            if (pins == PinCount)
            {
                return SumNextRolls(frame, 1, out int bonus) ? PinCount + bonus : (int?)null;
            }

            return pins;
        }

        /// <summary>
        /// そのフレームより後の投球を count 投ぶん足す。投球が足りなければ false。
        /// </summary>
        private bool SumNextRolls(int frame, int count, out int sum)
        {
            sum = 0;
            int found = 0;
            for (int f = frame + 1; f < FrameCount && found < count; f++)
            {
                foreach (int r in frames[f])
                {
                    sum += r;
                    found++;
                    if (found == count)
                    {
                        break;
                    }
                }
            }

            return found == count;
        }

        /// <summary>
        /// 表示用の記号。ストライクは X、スペアは /、0本は -、それ以外は数字。
        /// docs/art-direction.md の 6.3 に合わせる。
        /// </summary>
        public string GetRollSymbol(int frame, int rollIndex)
        {
            List<int> rolls = frames[frame];
            if (rollIndex >= rolls.Count)
            {
                return string.Empty;
            }

            int pins = rolls[rollIndex];
            int standingBefore = PinsStandingBefore(frame, rollIndex);
            if (pins == standingBefore && pins > 0)
            {
                // 10本並んだ状態から全部倒せばストライク、残りを全部倒せばスペア
                return standingBefore == PinCount ? "X" : "/";
            }

            return pins == 0 ? "-" : pins.ToString();
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
