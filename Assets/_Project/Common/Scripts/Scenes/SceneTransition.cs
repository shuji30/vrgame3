using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRGame3.Common
{
    /// <summary>
    /// シーン(ハブ・各会場)の切り替え。暗くしてから非同期で読み込み、新しいシーンで明るくする
    /// (docs/plan.md「快適性と安全設計」)。カメラは頭の動きに追従したままで、視点を動かす演出は入れない。
    /// </summary>
    public static class SceneTransition
    {
        public const string HubScene = "Hub";

        /// <summary>いま切り替えの途中か。</summary>
        public static bool IsLoading { get; internal set; }

        /// <summary>前にいたシーンの名前(ハブで「前に入った会場の看板の前」に出すため)。最初の起動では空。</summary>
        public static string PreviousScene { get; internal set; } = string.Empty;

        /// <summary>切り替えで入ったシーンか(新しいシーンの ScreenFader が暗い状態から明るくする)。</summary>
        internal static bool Arriving { get; set; }

        /// <summary>シーンを切り替える。切り替えの途中なら何もしない。</summary>
        public static void Load(string sceneName)
        {
            if (IsLoading || string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            IsLoading = true;
            var runner = new GameObject("シーン切り替え").AddComponent<SceneTransitionRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.Run(sceneName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsLoading = false;
            Arriving = false;
            PreviousScene = string.Empty;
        }
    }
}
