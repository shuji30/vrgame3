namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球1ゲームの点数とサーブ権(docs/plan.md「卓球」のルール)。Unity に依存しない。
    /// 先取点(既定 11)。先取点 −1 で並んだら2点差がつくまで続ける(デュース)。
    /// サーブは最初プレイヤー、2点ごとに交代、デュースでは1点ごとに交代。
    /// </summary>
    public class TableTennisMatch
    {
        private readonly int target;
        private readonly bool deuce;

        /// <param name="allowDeuce">false なら先取点で必ず決着(チャレンジの5点マッチ)</param>
        public TableTennisMatch(int pointsToWin = 11, TTSide firstServer = TTSide.Player, bool allowDeuce = true)
        {
            target = pointsToWin;
            deuce = allowDeuce;
            FirstServer = firstServer;
            Server = firstServer;
        }

        public TTSide FirstServer { get; }
        public int PlayerPoints { get; private set; }
        public int CpuPoints { get; private set; }
        public TTSide Server { get; private set; }
        public bool IsOver { get; private set; }
        public TTSide Winner { get; private set; }

        /// <summary>デュースに入っているか。</summary>
        public bool IsDeuce => deuce && PlayerPoints >= target - 1 && CpuPoints >= target - 1;

        public int PointsOf(TTSide side) => side == TTSide.Player ? PlayerPoints : CpuPoints;

        /// <summary>1点を入れる。</summary>
        public void AddPoint(TTSide winner)
        {
            if (IsOver)
            {
                return;
            }

            if (winner == TTSide.Player)
            {
                PlayerPoints++;
            }
            else
            {
                CpuPoints++;
            }

            int lead = PlayerPoints - CpuPoints;
            if ((PlayerPoints >= target || CpuPoints >= target) && (!deuce || lead >= 2 || lead <= -2))
            {
                IsOver = true;
                Winner = lead > 0 ? TTSide.Player : TTSide.Cpu;
                return;
            }

            int total = PlayerPoints + CpuPoints;
            if (IsDeuce)
            {
                // デュースでは1点ごとに交代
                int deucePoints = total - 2 * (target - 1);
                Server = deucePoints % 2 == 0 ? FirstServer : FirstServer.Other();
            }
            else
            {
                Server = (total / 2) % 2 == 0 ? FirstServer : FirstServer.Other();
            }
        }
    }
}
