namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 1点のあいだの判定(docs/plan.md「卓球」のルール)。Unity に依存しない。
    /// ボールの出来事(打った・台で弾んだ・床に落ちた・迷子)を順に渡すと、決着したときにどちらの点かと理由を返す。
    /// ・打った球は相手のコートに入らなければ打った側の失点(サーブだけは自分のコートで1回弾んでよい)
    /// ・相手のコートで2回弾むか、相手が返せずに床に落ちたら打った側の得点
    /// ・まだ誰も打っていない(投げ上げただけの)ボールが落ちても決着にしない(サーブのやり直し)
    /// ・迷子は最後に打った側の失点
    /// </summary>
    public class RallyJudge
    {
        /// <summary>決着の結果。</summary>
        public struct Point
        {
            public TTSide Winner;
            public string Reason;
        }

        private readonly TTSide server;
        private TTSide? lastHitter;
        private bool landedOnReceiver;
        private int hitterSideBounces;
        private int receiverBounces;
        private int hits;

        public RallyJudge(TTSide server)
        {
            this.server = server;
        }

        /// <summary>最後に打った側(まだなら null)。</summary>
        public TTSide? LastHitter => lastHitter;

        /// <summary>打った回数(サーブを含む)。</summary>
        public int Hits => hits;

        /// <summary>打った球がもう相手のコートに入ったか(CPU が打ち返してよいか)。</summary>
        public bool LandedOnReceiver => landedOnReceiver;

        /// <summary>ラケットで打った。同じ側が続けて打った(相手のコートに入る前)ときは打った側の失点。</summary>
        public Point? OnHit(TTSide who)
        {
            if (lastHitter == who && !landedOnReceiver && hits > 0)
            {
                return new Point { Winner = who.Other(), Reason = "2どうち" };
            }

            lastHitter = who;
            landedOnReceiver = false;
            hitterSideBounces = 0;
            receiverBounces = 0;
            hits++;
            return null;
        }

        /// <summary>台で弾んだ。courtSide はどちらのコートか。</summary>
        public Point? OnBounce(TTSide courtSide)
        {
            if (lastHitter == null)
            {
                return null;
            }

            TTSide hitter = lastHitter.Value;
            if (courtSide == hitter)
            {
                if (landedOnReceiver)
                {
                    return null;
                }

                // サーブだけは、自分のコートで1回弾んでから相手のコートに入ってよい
                bool isServe = hits == 1 && hitter == server;
                hitterSideBounces++;
                if (isServe && hitterSideBounces == 1)
                {
                    return null;
                }

                return new Point { Winner = hitter.Other(), Reason = "じぶんのコート" };
            }

            receiverBounces++;
            if (!landedOnReceiver)
            {
                landedOnReceiver = true;
                return null;
            }

            return receiverBounces >= 2 ? new Point { Winner = hitter, Reason = "2バウンド" } : (Point?)null;
        }

        /// <summary>床に落ちた。まだ誰も打っていなければ null(サーブのやり直し)。</summary>
        public Point? OnFloor()
        {
            if (lastHitter == null)
            {
                return null;
            }

            TTSide hitter = lastHitter.Value;
            return landedOnReceiver
                ? new Point { Winner = hitter, Reason = "かえせなかった" }
                : new Point { Winner = hitter.Other(), Reason = "アウト" };
        }

        /// <summary>迷子(遠くへ行った・長く決着しない)。まだ誰も打っていなければ null。</summary>
        public Point? OnStray()
        {
            if (lastHitter == null)
            {
                return null;
            }

            return new Point { Winner = lastHitter.Value.Other(), Reason = "アウト" };
        }
    }
}
