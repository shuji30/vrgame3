using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 交代プレイ(docs/plan.md「交代プレイ」)。2〜4人が1ゲームずつ順に遊ぶ。
    /// 番の間の身長・利き手はその人のもので、保存しない(始めに保存した設定を覚えておき、終わりかやめたときに戻す)。
    /// ベスト・回数・腕前ポイントは記録しない。
    /// </summary>
    public static class TurnSession
    {
        private static readonly List<int> Scores = new List<int>();
        private static int savedHeight;
        private static bool savedLeftHanded;

        /// <summary>交代プレイの途中か。</summary>
        public static bool Active { get; private set; }

        /// <summary>人数。</summary>
        public static int PlayerCount { get; private set; }

        /// <summary>いまの番の人(0 始まり)。</summary>
        public static int Current { get; private set; }

        /// <summary>全員の番が終わったか。</summary>
        public static bool AllDone => Active && Scores.Count >= PlayerCount;

        public static IReadOnlyList<int> PlayerScores => Scores;

        /// <summary>交代プレイを始める。</summary>
        public static void Begin(int players)
        {
            // 1人も終わっていないうちに人数だけ変えたときは、保存した設定はそのまま使う
            if (!Active)
            {
                savedHeight = SaveStore.Settings.heightCm;
                savedLeftHanded = SaveStore.Settings.leftHanded;
            }

            Active = true;
            PlayerCount = Mathf.Clamp(players, 2, 4);
            Current = 0;
            Scores.Clear();
            SaveStore.SuppressSettingsSave = true;
        }

        /// <summary>いまの人のスコアを記録して次の人へ進む。</summary>
        public static void RecordScore(int score)
        {
            if (!Active)
            {
                return;
            }

            Scores.Add(score);
            Current = Mathf.Min(Scores.Count, PlayerCount - 1);
        }

        /// <summary>終わり(またはやめた)。保存した身長・利き手に戻す。</summary>
        public static void End()
        {
            if (!Active)
            {
                return;
            }

            Active = false;
            Scores.Clear();
            SaveStore.SuppressSettingsSave = false;
            SaveStore.Settings.heightCm = savedHeight;
            SaveStore.Settings.leftHanded = savedLeftHanded;
            SaveStore.SaveSettings();
        }

        /// <summary>順位(同点は同じ順位)。index の人の順位(1 始まり)。</summary>
        public static int RankOf(int index)
        {
            int rank = 1;
            for (int i = 0; i < Scores.Count; i++)
            {
                if (Scores[i] > Scores[index])
                {
                    rank++;
                }
            }

            return rank;
        }

        /// <summary>プレイヤーの名前と印(docs/art-direction.md 6.12)。</summary>
        public static string Label(int index)
        {
            string[] marks = { "●", "■", "◆", "▼" };
            return $"{marks[Mathf.Clamp(index, 0, 3)]} プレイヤー{index + 1}";
        }

        /// <summary>プレイヤーの色(docs/art-direction.md 6.12)。</summary>
        public static Color ColorOf(int index)
        {
            Color32[] colors =
            {
                new Color32(0x3A, 0xA7, 0xFF, 0xFF), new Color32(0xFF, 0x7F, 0x6B, 0xFF),
                new Color32(0x6C, 0xC3, 0x6A, 0xFF), new Color32(0xFF, 0xD2, 0x4A, 0xFF),
            };
            return colors[Mathf.Clamp(index, 0, 3)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Active = false;
            Scores.Clear();
        }
    }
}
