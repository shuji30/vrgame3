using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 最後に選んだモード・強さ・人数を、アプリを閉じるまで覚えておく(保存はしない。docs/plan.md「モードの選び方」)。
    /// 種目ごとに持つ。
    /// </summary>
    public static class SessionChoice
    {
        /// <summary>1種目分の選択。</summary>
        public class Choice
        {
            public SportMode Mode;
            public int PlayerCount = 2;
            public string ChallengeId = string.Empty;
        }

        private static readonly Dictionary<string, Choice> Choices = new Dictionary<string, Choice>();

        /// <summary>その種目の選択(無ければ defaultMode で作る)。</summary>
        public static Choice For(string sportId, SportMode defaultMode)
        {
            if (!Choices.TryGetValue(sportId, out Choice choice))
            {
                choice = new Choice { Mode = defaultMode };
                Choices[sportId] = choice;
            }

            return choice;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Choices.Clear();
        }
    }
}
