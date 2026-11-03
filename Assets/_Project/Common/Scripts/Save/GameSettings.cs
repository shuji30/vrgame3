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

        /// <summary>卓球の入りやすさの補助を使うか。</summary>
        public bool hitAssist = true;

        /// <summary>卓球の CPU の強さ(0 やさしい / 1 ふつう / 2 つよい)。</summary>
        public int cpuLevel = 1;

        /// <summary>時間帯(0 あさ / 1 ひる / 2 ゆうやけ)。版 3 から。</summary>
        public int timeOfDay = 1;

        /// <summary>音量(全体)0〜10。</summary>
        public int masterVolume = 10;

        /// <summary>音量(BGM・環境音)0〜10。</summary>
        public int ambientVolume = 10;

        /// <summary>選んだ見た目(ボールの柄・弓の色・ラケットの色・手袋の色)0〜3。</summary>
        public int ballStyle;
        public int bowStyle;
        public int racketStyle;
        public int gloveStyle;

        /// <summary>読み込んだ値が範囲の外なら直す。</summary>
        public void Clamp()
        {
            heightCm = Math.Max(MinHeightCm, Math.Min(MaxHeightCm, heightCm));
            moveSpeedLevel = Math.Max(0, Math.Min(2, moveSpeedLevel));
            cpuLevel = Math.Max(0, Math.Min(2, cpuLevel));
            timeOfDay = Math.Max(0, Math.Min(2, timeOfDay));
            masterVolume = Math.Max(0, Math.Min(10, masterVolume));
            ambientVolume = Math.Max(0, Math.Min(10, ambientVolume));
            ballStyle = Math.Max(0, Math.Min(3, ballStyle));
            bowStyle = Math.Max(0, Math.Min(3, bowStyle));
            racketStyle = Math.Max(0, Math.Min(3, racketStyle));
            gloveStyle = Math.Max(0, Math.Min(3, gloveStyle));
        }
    }
}
