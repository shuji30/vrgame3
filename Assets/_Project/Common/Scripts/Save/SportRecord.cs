using System;

namespace VRGame3.Common
{
    /// <summary>
    /// 種目ごとの記録(保存する)。ベストスコア・最後まで遊んだ回数・腕前ポイント・ランク。
    /// </summary>
    [Serializable]
    public class SportRecord
    {
        /// <summary>種目の名前(SportIds の値)。</summary>
        public string sportId;

        /// <summary>ベストスコア。まだ遊んでいなければ 0。</summary>
        public int best;

        /// <summary>最後まで遊んだ回数。</summary>
        public int plays;

        /// <summary>腕前ポイント。</summary>
        public int points;

        /// <summary>ランク(0 から始まる段階)。</summary>
        public int rank;
    }
}
