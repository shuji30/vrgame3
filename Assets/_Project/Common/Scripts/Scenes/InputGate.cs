using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRGame3.Common
{
    /// <summary>
    /// メニューや休憩案内が出ている間、物を新しく掴めないようにするための共通の目印。
    /// 止めたい側が BlockGrab(自分)、やめるときに UnblockGrab(自分) を呼ぶ。各種目は GrabBlocked を見て掴めるかを決める。
    /// シーンが変わると全部外す(止めた側がシーンごと消えて外し忘れても残らない)。
    /// </summary>
    public static class InputGate
    {
        private static readonly HashSet<object> Blockers = new HashSet<object>();

        /// <summary>いま物を掴めないようにしているか。</summary>
        public static bool GrabBlocked => Blockers.Count > 0;

        public static void BlockGrab(object owner)
        {
            Blockers.Add(owner);
        }

        public static void UnblockGrab(object owner)
        {
            Blockers.Remove(owner);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Blockers.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Blockers.Clear();
        }
    }
}
