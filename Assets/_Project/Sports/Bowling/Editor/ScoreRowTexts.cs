using TMPro;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// 大型スコア板の1段分の文字(各フレームの投球記号・累計・合計)。ボウリング場を組み立てるときだけ使う。
    /// </summary>
    internal class ScoreRowTexts
    {
        public TMP_Text[] Rolls;
        public TMP_Text[] Scores;
        public TMP_Text Total;
    }
}
