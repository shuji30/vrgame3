using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// いまゲームの途中か(1投・1射でもしてから、最後まで終わる前か)。メニューの「ハブへもどる」で確認を出すかに使う。
    /// 各種目が投げ始めたら InProgress を true、終わったか始め直したら false にする。シーンを切り替えると false に戻る。
    /// </summary>
    public static class GameSession
    {
        public static bool InProgress { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            InProgress = false;
        }
    }
}
