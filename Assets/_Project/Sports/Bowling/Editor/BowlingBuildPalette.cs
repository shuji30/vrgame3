using UnityEngine;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// ボウリング場を組み立てるときの仮の色。docs/art-direction.md の 2.3 のパレットから選ぶ。
    /// モデルを差し替えるときに、ここで作ったマテリアルも置き換える。
    /// </summary>
    internal class BowlingBuildPalette
    {
        public readonly Material Sand;
        public readonly Material Wood;
        public readonly Material WoodLight;
        public readonly Material Stone;
        public readonly Material Pin;
        public readonly Material Accent;
        public readonly Material Board;
        public readonly Material BallA;
        public readonly Material BallB;
        public readonly Material BallC;
        public readonly Material Line;
        public readonly Material Reticle;
        public readonly Color TextColor = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        public readonly Color HighlightColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        public BowlingBuildPalette(string folder)
        {
            Sand = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Sand", "#F2D49B", false);
            Wood = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Wood", "#C98B55", false);
            WoodLight = SceneBuildUtility.GetOrCreateMaterial(folder, "B_WoodLight", "#F0C995", false);
            Stone = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Stone", "#B8B4C8", false);
            Pin = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Pin", "#FFFDF7", false);
            Accent = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Accent", "#FF7F6B", false);
            Board = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Board", "#4A4660", false);
            BallA = SceneBuildUtility.GetOrCreateMaterial(folder, "B_BallA", "#2FB5B0", false);
            BallB = SceneBuildUtility.GetOrCreateMaterial(folder, "B_BallB", "#E0525E", false);
            BallC = SceneBuildUtility.GetOrCreateMaterial(folder, "B_BallC", "#6C8FCF", false);
            Line = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Line", "#E0525E", true);
            Reticle = SceneBuildUtility.GetOrCreateMaterial(folder, "B_Reticle", "#3AA7FF", true);
        }
    }
}
