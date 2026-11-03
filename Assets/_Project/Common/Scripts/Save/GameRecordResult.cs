namespace VRGame3.Common
{
    /// <summary>最後まで遊んだ1ゲームを記録した結果(結果の表示に使う)。</summary>
    public struct GameRecordResult
    {
        /// <summary>種目(SportIds の値)。</summary>
        public string SportId;

        public int Score;

        /// <summary>このゲームの前のベスト(初めてなら 0)。</summary>
        public int BestBefore;

        /// <summary>ベストを更新したか(初めて遊んだときも更新とする)。</summary>
        public bool NewBest;

        /// <summary>腕前ポイントとランクの変化。</summary>
        public SkillPoints.Outcome Points;
    }
}
