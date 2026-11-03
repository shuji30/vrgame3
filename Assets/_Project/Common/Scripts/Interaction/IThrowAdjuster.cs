using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 投げる速度を種目ごとに補正する部品の決まり。ThrowableObject と同じ物に付けると、
    /// 手を離した瞬間に、倍率をかける前の速度と角速度を受け取って書き換えられる。
    /// </summary>
    public interface IThrowAdjuster
    {
        void AdjustThrow(ref Vector3 velocity, ref Vector3 angularVelocity);
    }
}
