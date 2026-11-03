using UnityEngine;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// ボウリング場を組み立てるときの仮の色。docs/art-direction.md の 2.3 のパレットと、
    /// 第9章 M1「ボウリング小屋の仮組み」の色の表に合わせる。モデルを差し替えるときにここも置き換える。
    /// </summary>
    internal class BowlingBuildPalette
    {
        public readonly Material Sand;          // 外の砂地
        public readonly Material Sea;           // 海
        public readonly Material Wood;          // 横の板・笠木・室内の床・腰壁・ボール置き台
        public readonly Material WoodLight;     // レーン・助走路・上の帯・前壁・奥壁
        public readonly Material WoodDark;      // 柱・梁・棟・ヤシの幹
        public readonly Material Roof;          // 屋根(室内を明るく保つ淡色)
        public readonly Material Stone;         // ガター・ピットの底
        public readonly Material Accent;        // 幕板・日よけ・奥のクッション
        public readonly Material Leaf;          // ヤシの葉
        public readonly Material Pin;
        public readonly Material PinStripe;     // ピンの首の赤い帯
        public readonly Material Board;         // スコア板
        public readonly Material TagPlayer;     // スコア板のプレイヤーの段の札
        public readonly Material TagCpu;        // スコア板の CPU の段の札
        public readonly Material BallA;
        public readonly Material BallB;
        public readonly Material BallC;
        public readonly Material Line;          // ファウルライン
        public readonly Material Reticle;       // テレポートの着地点
        public readonly Color TextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        public readonly Color HighlightColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        public BowlingBuildPalette(string folder)
        {
            Sand = Lit(folder, "B_Sand", "#F2D49B");
            Sea = Lit(folder, "B_Sea", "#2FB5B0");
            Wood = Lit(folder, "B_Wood", "#C98B55");
            WoodLight = Lit(folder, "B_WoodLight", "#F0C995");
            WoodDark = Lit(folder, "B_WoodDark", "#8A5A3B");
            Roof = Lit(folder, "B_Roof", "#FFF1CF");
            Stone = Lit(folder, "B_Stone", "#B8B4C8");
            Accent = Lit(folder, "B_Accent", "#FF7F6B");
            Leaf = Lit(folder, "B_Leaf", "#6CC36A");
            Pin = Lit(folder, "B_Pin", "#FFFDF7");
            PinStripe = Lit(folder, "B_PinStripe", "#E0525E");
            Board = Lit(folder, "B_Board", "#4A4660");
            TagPlayer = Lit(folder, "B_TagPlayer", "#3AA7FF");
            TagCpu = Lit(folder, "B_TagCpu", "#FF8A3D");
            BallA = Lit(folder, "B_BallA", "#2FB5B0");
            BallB = Lit(folder, "B_BallB", "#E0525E");
            BallC = Lit(folder, "B_BallC", "#6C8FCF");
            Line = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Line", "#E0525E", true);
            Reticle = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Reticle", "#3AA7FF", true);
        }

        private static Material Lit(string folder, string name, string hex) =>
            SceneBuildUtility.GetOrCreateMaterial(folder, name, hex, false);
    }
}
