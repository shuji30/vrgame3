using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 腕前ランクの4段階(docs/plan.md「ゲームモードと成長要素」、見せ方は docs/art-direction.md 6.8)。
    /// 必要なポイントと、名前・色・星の数をまとめる。
    /// </summary>
    public static class SkillRank
    {
        public const int Count = 4;

        private static readonly string[] Names = { "かいがら", "さんご", "しんじゅ", "にじいろ" };
        private static readonly int[] RequiredPoints = { 0, 100, 300, 600 };

        private static readonly Color32[] MainColors =
        {
            new Color32(0xF0, 0xC9, 0x95, 0xFF),
            new Color32(0xFF, 0x7F, 0x6B, 0xFF),
            new Color32(0xFF, 0xFD, 0xF7, 0xFF),
            new Color32(0xFF, 0xD2, 0x4A, 0xFF),
        };

        private static readonly Color32[] RimColors =
        {
            new Color32(0x8A, 0x5A, 0x3B, 0xFF),
            new Color32(0xE0, 0x52, 0x5E, 0xFF),
            new Color32(0xB8, 0xB4, 0xC8, 0xFF),
            new Color32(0x3A, 0xA7, 0xFF, 0xFF),
        };

        public static int Clamp(int rank) => Mathf.Clamp(rank, 0, Count - 1);

        public static string Name(int rank) => Names[Clamp(rank)];

        public static int Required(int rank) => RequiredPoints[Clamp(rank)];

        public static Color MainColor(int rank) => MainColors[Clamp(rank)];

        public static Color RimColor(int rank) => RimColors[Clamp(rank)];

        /// <summary>星の文字(ランク1 = ★、ランク4 = ★★★★)。</summary>
        public static string Stars(int rank) => new string('★', Clamp(rank) + 1);

        /// <summary>そのポイントで届いている一番上のランク。</summary>
        public static int ForPoints(int points)
        {
            int rank = 0;
            for (int i = 0; i < Count; i++)
            {
                if (points >= RequiredPoints[i])
                {
                    rank = i;
                }
            }

            return rank;
        }
    }
}
