using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 見た目のご褒美(docs/plan.md M4「見た目のご褒美」、色と柄は docs/art-direction.md「M4 見た目のご褒美」)。
    /// 品は4つ(ボールの柄・弓の色・ラケットの色・リストバンド)で、それぞれ 0(標準)+解放3つ。
    /// 解放は保存せず、ランクとチャレンジのクリア数から毎回求める(ランクは下がらないので消えない)。
    /// </summary>
    public static class RewardCatalog
    {
        /// <summary>品。</summary>
        public enum Item
        {
            Ball,
            Bow,
            Racket,
            Wristband,
        }

        private static readonly string[][] Names =
        {
            new[] { "ふつう", "しましま", "みずたま", "ほし" },
            new[] { "ふつう", "ラグーン", "ハイビスカス", "ひまわり" },
            new[] { "ふつう", "ラグーン", "ハイビスカス", "ひまわり" },
            new[] { "なし", "サンセット", "ラグーン", "なないろ" },
        };

        private static readonly string[] ItemNames = { "ボールのがら", "弓のいろ", "ラケットのいろ", "リストバンド" };

        /// <summary>確認用:オンにすると全部解放する(試験用の切り替え)。</summary>
        public static bool UnlockAllForTest { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            UnlockAllForTest = false;
        }

        public static string ItemName(Item item) => ItemNames[(int)item];

        public static string Name(Item item, int index) => Names[(int)item][Mathf.Clamp(index, 0, 3)];

        /// <summary>その見た目を使えるか。</summary>
        public static bool IsUnlocked(Item item, int index)
        {
            if (index <= 0 || UnlockAllForTest)
            {
                return true;
            }

            switch (item)
            {
                case Item.Ball:
                    return SaveStore.GetRecord(SportIds.Bowling).rank >= index;
                case Item.Bow:
                    return SaveStore.GetRecord(SportIds.Archery).rank >= index;
                case Item.Racket:
                    return SaveStore.GetRecord(SportIds.TableTennis).rank >= index;
                default:
                    return SaveStore.Data.clearedChallenges.Count >= index * 3;
            }
        }

        /// <summary>まだのときの条件の文。</summary>
        public static string Condition(Item item, int index)
        {
            string[] ranks = { "", "さんご", "しんじゅ", "にじいろ" };
            switch (item)
            {
                case Item.Ball:
                    return $"ボウリングで\n{ranks[index]}";
                case Item.Bow:
                    return $"アーチェリーで\n{ranks[index]}";
                case Item.Racket:
                    return $"卓球で\n{ranks[index]}";
                default:
                    return $"チャレンジ\n{index * 3}";
            }
        }

        /// <summary>選んでいる番号(使えないものを選んでいれば 0)。</summary>
        public static int Selected(Item item)
        {
            GameSettings s = SaveStore.Settings;
            int index = item == Item.Ball ? s.ballStyle : item == Item.Bow ? s.bowStyle : item == Item.Racket ? s.racketStyle : s.gloveStyle;
            return IsUnlocked(item, index) ? index : 0;
        }

        /// <summary>選んで保存する(使えないものは選べない)。</summary>
        public static void Select(Item item, int index)
        {
            if (!IsUnlocked(item, index))
            {
                return;
            }

            GameSettings s = SaveStore.Settings;
            switch (item)
            {
                case Item.Ball:
                    s.ballStyle = index;
                    break;
                case Item.Bow:
                    s.bowStyle = index;
                    break;
                case Item.Racket:
                    s.racketStyle = index;
                    break;
                default:
                    s.gloveStyle = index;
                    break;
            }

            SaveStore.SaveSettings();
        }

        /// <summary>その種目のランクアップで新しく解放された品(無ければ null)。</summary>
        public static string NewlyUnlockedBy(string sportId, int rankAfter)
        {
            if (rankAfter < 1 || rankAfter > 3)
            {
                return null;
            }

            Item item = sportId == SportIds.Bowling ? Item.Ball : sportId == SportIds.Archery ? Item.Bow : Item.Racket;
            return $"{ItemName(item)} {Name(item, rankAfter)}";
        }
    }
}
