using System;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// 的の点数の計算(docs/plan.md「アーチェリー」のルール)。Unity に依存しない。
    /// 中心からの距離で 10〜1 点、外は 0 点。線に掛かったら高い方(矢の太さの分だけ内側として数える)。
    /// 中心の直径 6cm(10点リングの内側半分)は X。
    /// </summary>
    public static class ArcheryScoring
    {
        public const int RingCount = 10;

        /// <param name="distanceFromCenter">刺さった点と的の中心の距離(m)</param>
        /// <param name="targetDiameter">的の直径(m)</param>
        /// <param name="arrowRadius">矢の太さの半分(m)。線に掛かったかを決める</param>
        /// <param name="isX">中心の X に入ったか</param>
        public static int Score(float distanceFromCenter, float targetDiameter, float arrowRadius, out bool isX)
        {
            float ringWidth = targetDiameter / 2f / RingCount;
            // 矢が線に掛かっていれば内側(高い方)に数える
            float d = Math.Max(0f, distanceFromCenter - arrowRadius);
            isX = d <= ringWidth / 2f;
            if (d >= targetDiameter / 2f)
            {
                isX = false;
                return 0;
            }

            int ring = (int)Math.Floor(d / ringWidth);
            return Math.Max(1, RingCount - ring);
        }
    }
}
