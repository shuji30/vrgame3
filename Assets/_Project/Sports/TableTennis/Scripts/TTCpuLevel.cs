using System;
using UnityEngine;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>卓球の CPU の強さ1段分の値(docs/plan.md M3「卓球の CPU」の表)。</summary>
    [Serializable]
    public class TTCpuLevel
    {
        [Tooltip("強さの名前(スコア板に出す)")]
        public string label = "ふつう";

        [Tooltip("プレイヤーが打ってから動き始めるまでの秒数")]
        public float reaction = 0.18f;

        [Tooltip("左右に動く速さ(m/秒)")]
        public float moveSpeed = 2.5f;

        [Tooltip("体の中心からラケットが届く横の範囲(m)")]
        public float reach = 0.75f;

        [Tooltip("返球の水平の速さ(最小, 最大)(m/秒)")]
        public Vector2 returnSpeed = new Vector2(7f, 10f);

        [Tooltip("ねらった着地点のばらつき(m)")]
        public float scatter = 0.18f;

        [Tooltip("返せる球でミスする確率(0〜1)")]
        [Range(0f, 1f)]
        public float missRate = 0.08f;

        [Tooltip("速い球(10m/秒以上)でミスが増える分(0〜1)")]
        [Range(0f, 1f)]
        public float fastMissBonus = 0.12f;

        [Tooltip("サーブの水平の速さ(最小, 最大)(m/秒)")]
        public Vector2 serveSpeed = new Vector2(5f, 7f);

        [Tooltip("ねらう場所:0 = 中央付近、1 = 左右に散らす、2 = 隅とプレイヤーのいない側")]
        [Range(0, 2)]
        public int aimStyle = 1;
    }
}
