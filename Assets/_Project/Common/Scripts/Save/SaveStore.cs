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
        public const int CurrentVersion = 1;
        private const string FileName = "save.json";

        private static SaveData data;

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

        /// <summary>設定を変えたあとに呼んで保存する。</summary>
        public static void SaveSettings()
        {
            Settings.Clamp();
            Save();
        }

        /// <summary>
        /// 最後まで遊んだゲームを記録して保存する。戻り値はベストを更新したか。
        /// (腕前ポイントとランクは M2 の順5で足す)
        /// </summary>
        public static bool RecordFinishedGame(string sportId, int score)
        {
            SportRecord record = GetRecord(sportId);
            bool newBest = record.plays == 0 || score > record.best;
            record.plays++;
            if (newBest)
            {
                record.best = score;
            }

            Save();
            return newBest;
        }

        /// <summary>今の中身をファイルに書く。</summary>
        public static void Save()
        {
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
                loaded.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.sportId));
                loaded.settings.Clamp();
                loaded.version = CurrentVersion;
                return loaded;
            }
            catch (Exception e)
            {
                // 壊れたファイルは消さずに別名で残し、初期値で始める
                string broken = Path.Combine(Path.GetDirectoryName(path),
                    $"save.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                try
                {
                    File.Move(path, broken);
                }
                catch (Exception moveError)
                {
                    Debug.LogWarning($"[保存] 読めないファイルを別名にできませんでした: {moveError.Message}");
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
        }
    }
}
