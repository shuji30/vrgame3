using System;
using System.Collections.Generic;

namespace VRGame3.Common
{
    /// <summary>保存ファイル1つ分の中身。JsonUtility で JSON にする。</summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>保存形式の版。中身を変えたら上げ、読み込むときに直す。</summary>
        public int version = SaveStore.CurrentVersion;

        public GameSettings settings = new GameSettings();

        public List<SportRecord> records = new List<SportRecord>();

        /// <summary>クリアしたチャレンジの ID(版 2 から)。</summary>
        public List<string> clearedChallenges = new List<string>();
    }
}
