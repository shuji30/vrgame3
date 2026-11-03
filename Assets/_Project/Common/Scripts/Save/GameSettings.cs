using System;

namespace VRGame3.Common
{
    /// <summary>
    /// 全種目で共通の設定(保存する)。docs/plan.md の「設定と保存」。
    /// 値を変えたら SaveStore.SaveSettings() を呼んで保存する。
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public const int MinHeightCm = 110;
        public const int MaxHeightCm = 200;

        /// <summary>身長(cm)。ボウリングのボール置き台やアーチェリーの的の高さを合わせる。</summary>
        public int heightCm = 170;

        /// <summary>左利きか。</summary>
        public bool leftHanded;

        /// <summary>連続移動を使うか(オフのときはテレポート)。</summary>
        public bool continuousMove;

        /// <summary>連続移動の速さの段階(0 おそい / 1 ふつう / 2 はやい)。</summary>
        public int moveSpeedLevel = 1;

        /// <summary>休憩案内を出すか。</summary>
        public bool restReminder = true;

        /// <summary>アーチェリーのねらいの補助を出すか。</summary>
        public bool aimAssist = true;

        /// <summary>読み込んだ値が範囲の外なら直す。</summary>
        public void Clamp()
        {
            heightCm = Math.Max(MinHeightCm, Math.Min(MaxHeightCm, heightCm));
            moveSpeedLevel = Math.Max(0, Math.Min(2, moveSpeedLevel));
        }
    }
}
