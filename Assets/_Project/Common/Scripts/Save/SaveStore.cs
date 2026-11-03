using System;
using System.IO;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 設定と記録の保存。Application.persistentDataPath の JSON ファイル1つに書く(docs/plan.md の「設定と保存」)。
    /// ・最初に使ったときに読み込み、以後はメモリ上の値を使う
    /// ・書くときは一時ファイルに書いてから置き換える(書いている途中で落ちても壊れない)
    /// ・読めないファイルは別名で残し、初期値で始める
    /// </summary>
    public static class SaveStore
    {
        // 2:クリアしたチャレンジの一覧を足した
        // 3:時間帯・音量・選んだ見た目・てびきを見た印を足した
        public const int CurrentVersion = 3;
        private const string FileName = "save.json";

        private static SaveData data;

        // 読めないファイルを退避できなかったときは、上書きして消さないよう保存を止める
        private static bool saveDisabled;

        /// <summary>設定か記録が変わって保存したときに呼ばれる(表示を更新するため)。</summary>
        public static event Action Changed;

        /// <summary>保存ファイルの場所。</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>保存している中身(まだ読み込んでいなければ読み込む)。</summary>
        public static SaveData Data
        {
            get
            {
                if (data == null)
                {
                    data = Load();
                }

                return data;
            }
        }

        /// <summary>共通の設定。</summary>
        public static GameSettings Settings => Data.settings;

        /// <summary>種目の記録。まだなければ作る。</summary>
        public static SportRecord GetRecord(string sportId)
        {
            foreach (SportRecord record in Data.records)
            {
                if (record.sportId == sportId)
                {
                    return record;
                }
            }

            var created = new SportRecord { sportId = sportId };
            Data.records.Add(created);
            return created;
        }

        /// <summary>
        /// オンの間は設定の変更をファイルに書かない(交代プレイで、その人の身長・利き手を保存しないため)。
        /// </summary>
        public static bool SuppressSettingsSave { get; set; }

        /// <summary>設定を変えたあとに呼んで保存する。</summary>
        public static void SaveSettings()
        {
            Settings.Clamp();
            if (SuppressSettingsSave)
            {
                Changed?.Invoke();
                return;
            }

            Save();
        }

        /// <summary>その場所のてびきを見終わったか。</summary>
        public static bool HasSeenTutorial(string id) => Data.seenTutorials.Contains(id);

        /// <summary>てびきを見終わった印を付けて保存する。</summary>
        public static void MarkTutorialSeen(string id)
        {
            if (!Data.seenTutorials.Contains(id))
            {
                Data.seenTutorials.Add(id);
                Save();
            }
        }

        /// <summary>てびきを見た印を全部消す(「あそびかたを さいしょから」)。</summary>
        public static void ResetTutorials()
        {
            Data.seenTutorials.Clear();
            Save();
        }

        /// <summary>チャレンジをクリアしたか。</summary>
        public static bool IsChallengeCleared(string id) => Data.clearedChallenges.Contains(id);

        /// <summary>チャレンジのクリアを記録して保存する。初めてのクリアなら true。</summary>
        public static bool MarkChallengeCleared(string id)
        {
            if (Data.clearedChallenges.Contains(id))
            {
                return false;
            }

            Data.clearedChallenges.Add(id);
            Save();
            return true;
        }

        /// <summary>ID がその種目の頭(例 "bowling.")で始まるクリア済みのチャレンジの数。</summary>
        public static int ClearedChallengeCount(string prefix)
        {
            int count = 0;
            foreach (string id in Data.clearedChallenges)
            {
                if (id.StartsWith(prefix))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 最後まで遊んだゲームを記録して保存する(ベスト・遊んだ回数・腕前ポイント・ランク)。
        /// 途中でやめたゲームでは呼ばない。
        /// </summary>
        public static GameRecordResult RecordFinishedGame(string sportId, int score)
        {
            SportRecord record = GetRecord(sportId);
            var result = new GameRecordResult
            {
                SportId = sportId,
                Score = score,
                BestBefore = record.best,
                NewBest = record.plays == 0 || score > record.best,
                Points = SkillPointRules.Current.Apply(sportId, score, record.points, record.rank),
            };

            record.plays++;
            if (result.NewBest)
            {
                record.best = score;
            }

            record.points = result.Points.PointsAfter;
            record.rank = result.Points.RankAfter;
            Save();
            return result;
        }

        /// <summary>今の中身をファイルに書く。</summary>
        public static void Save()
        {
            if (saveDisabled)
            {
                Changed?.Invoke();
                return;
            }

            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[保存] 保存できませんでした: {path}\n{e.Message}");
            }

            Changed?.Invoke();
        }

        /// <summary>ファイルから読み直す(確かめる用)。</summary>
        public static void Reload()
        {
            data = Load();
            Changed?.Invoke();
        }

        private static SaveData Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
            {
                return new SaveData();
            }

            try
            {
                var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (loaded == null)
                {
                    throw new InvalidDataException("中身が空です");
                }

                loaded.settings ??= new GameSettings();
                loaded.records ??= new System.Collections.Generic.List<SportRecord>();
                loaded.clearedChallenges ??= new System.Collections.Generic.List<string>();
                loaded.seenTutorials ??= new System.Collections.Generic.List<string>();
                loaded.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.sportId));
                loaded.settings.Clamp();
                loaded.version = CurrentVersion;
                return loaded;
            }
            catch (Exception e)
            {
                // 壊れたファイルは消さずに別名で残し、初期値で始める
                string broken = Path.Combine(Path.GetDirectoryName(path),
                    $"save.broken-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
                for (int i = 2; File.Exists(broken); i++)
                {
                    broken = Path.Combine(Path.GetDirectoryName(path),
                        $"save.broken-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{i}.json");
                }

                try
                {
                    File.Move(path, broken);
                }
                catch (Exception moveError)
                {
                    // 別名にできなければ、上書きされないようにこのセッションでは保存しない
                    saveDisabled = true;
                    Debug.LogWarning($"[保存] 読めないファイルを別名にできないので、このあいだは保存しません: {moveError.Message}");
                }

                Debug.LogWarning($"[保存] 保存ファイルを読めないので初期値で始めます(元のファイル: {broken})\n{e.Message}");
                return new SaveData();
            }
        }

        /// <summary>
        /// プレイモードに入るたびに読み直す(ドメインの再読み込みを切っている場合でも古い値を残さない)。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            data = null;
            Changed = null;
            saveDisabled = false;
            SuppressSettingsSave = false;
        }
    }
}
