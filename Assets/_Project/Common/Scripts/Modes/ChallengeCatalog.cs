using System.Collections.Generic;

namespace VRGame3.Common
{
    /// <summary>
    /// チャレンジの一覧(docs/plan.md「チャレンジ」)。種目ごとに3つ。ID は保存ファイルに書くので、一度決めたら変えない。
    /// クリアの条件の数値は各種目の進行役の Inspector で調整する(ここは名前と説明だけ)。
    /// </summary>
    public static class ChallengeCatalog
    {
        /// <summary>チャレンジ1つ分。</summary>
        public class Entry
        {
            public string Id;
            public string SportId;
            public string Name;
            public string Condition;
        }

        private static readonly Entry[] All =
        {
            new Entry { Id = "bowling.seven", SportId = SportIds.Bowling, Name = "ぴったり7本", Condition = "1投でちょうど7本たおす(5投まで)" },
            new Entry { Id = "bowling.spare", SportId = SportIds.Bowling, Name = "スペアをねらえ", Condition = "ストライクかスペアをとる(3フレームまで)" },
            new Entry { Id = "bowling.corner", SportId = SportIds.Bowling, Name = "はしっこの1本", Condition = "はしに1本だけ立ったピンをたおす(3投まで)" },
            new Entry { Id = "archery.keep", SportId = SportIds.Archery, Name = "ねらいを保て", Condition = "10mで3本つづけて8点以上(6本まで)" },
            new Entry { Id = "archery.far", SportId = SportIds.Archery, Name = "とおくの的", Condition = "30mで6本中3本を的に当てる" },
            new Entry { Id = "archery.45", SportId = SportIds.Archery, Name = "6本で45点", Condition = "20mで6本の合計45点以上" },
            new Entry { Id = "tabletennis.rally", SportId = SportIds.TableTennis, Name = "ラリー10回", Condition = "やさしいCPUとラリーを10往復(3回まで)" },
            new Entry { Id = "tabletennis.aim", SportId = SportIds.TableTennis, Name = "ねらい打ち", Condition = "隅の円に入れる(10球中3球)" },
            new Entry { Id = "tabletennis.match5", SportId = SportIds.TableTennis, Name = "5点マッチ", Condition = "ふつうのCPUに5点先取で勝つ" },
        };

        /// <summary>その種目のチャレンジ(3つ)。</summary>
        public static List<Entry> ForSport(string sportId)
        {
            var list = new List<Entry>();
            foreach (Entry e in All)
            {
                if (e.SportId == sportId)
                {
                    list.Add(e);
                }
            }

            return list;
        }

        public static Entry Find(string id)
        {
            foreach (Entry e in All)
            {
                if (e.Id == id)
                {
                    return e;
                }
            }

            return null;
        }

        /// <summary>一覧の本文(名前・条件・クリア済みの印)。</summary>
        public static string ListText(string sportId)
        {
            string text = string.Empty;
            foreach (Entry e in ForSport(sportId))
            {
                string mark = SaveStore.IsChallengeCleared(e.Id) ? "◎ クリア  " : string.Empty;
                text += $"{mark}{e.Name}\n<size=80%>{e.Condition}</size>\n";
            }

            return text.TrimEnd();
        }
    }
}
