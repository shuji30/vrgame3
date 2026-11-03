using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// アーチェリーの的(1つ)。刺さった点と的の中心の距離から点数を出し、ラウンドの始めに次の距離へ置き直す。
    /// この物の位置が的の面の中心で、-Z(プレイヤーの方)を向いている。docs/art-direction.md「M2 アーチェリー場の仮組み」。
    /// </summary>
    public class ArcheryTarget : MonoBehaviour
    {
        [Tooltip("的の直径(m)。リングの幅は直径 ÷ 2 ÷ 10")]
        [SerializeField] private float diameter = 1.2f;

        [Tooltip("矢の太さの半分(m)。線に掛かったら高い方にするために使う")]
        [SerializeField] private float arrowRadius = 0.004f;

        [Tooltip("脚(地面から的の中心まで伸ばす)")]
        [SerializeField] private Transform[] legs = new Transform[0];

        [Tooltip("足元の横木(地面から 0.1m)")]
        [SerializeField] private Transform crossbar;

        [Tooltip("置き直すときに縮める見た目の親")]
        [SerializeField] private Transform visual;

        private readonly List<Transform> stuckArrows = new List<Transform>();

        public float Diameter => diameter;

        /// <summary>的の面の上の点の点数。isX は中心の X に入ったか。</summary>
        public int ScoreAt(Vector3 worldPoint, out bool isX)
        {
            Vector3 offset = worldPoint - transform.position;
            Vector3 onFace = Vector3.ProjectOnPlane(offset, transform.forward);
            return ArcheryScoring.Score(onFace.magnitude, diameter, arrowRadius, out isX);
        }

        /// <summary>刺さった矢を覚えておく(ラウンドの終わりにまとめて消すため)。</summary>
        public void AddStuckArrow(Transform arrow)
        {
            stuckArrows.Add(arrow);
        }

        /// <summary>すぐに置く(アニメーションなし)。</summary>
        public void PlaceAt(float distance, float height)
        {
            transform.position = new Vector3(transform.position.x, height, distance);
            FitLegs(height);
        }

        /// <summary>刺さった矢を縮めて消し、的を縮めて次の距離へ移して元の大きさに戻す(7.3)。</summary>
        public IEnumerator MoveTo(float distance, float height)
        {
            yield return ShrinkArrows(0.2f);
            yield return Scale(1f, 0.01f, 0.2f);
            PlaceAt(distance, height);
            yield return Scale(0.01f, 1f, 0.3f);
        }

        /// <summary>刺さった矢を縮めて消す。</summary>
        public IEnumerator ShrinkArrows(float time)
        {
            stuckArrows.RemoveAll(a => a == null);
            var scales = new List<Vector3>();
            foreach (Transform arrow in stuckArrows)
            {
                scales.Add(arrow.localScale);
            }

            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                for (int i = 0; i < stuckArrows.Count; i++)
                {
                    if (stuckArrows[i] != null)
                    {
                        stuckArrows[i].localScale = scales[i] * (1f - t / time);
                    }
                }

                yield return null;
            }

            foreach (Transform arrow in stuckArrows)
            {
                if (arrow != null)
                {
                    Destroy(arrow.gameObject);
                }
            }

            stuckArrows.Clear();
        }

        private IEnumerator Scale(float from, float to, float time)
        {
            Transform target = visual != null ? visual : transform;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                target.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
                yield return null;
            }

            target.localScale = Vector3.one * to;
        }

        /// <summary>的の中心の高さに合わせて、脚の長さと横木の位置を直す。</summary>
        private void FitLegs(float height)
        {
            foreach (Transform leg in legs)
            {
                if (leg == null)
                {
                    continue;
                }

                Vector3 scale = leg.localScale;
                scale.y = height;
                leg.localScale = scale;
                Vector3 position = leg.localPosition;
                position.y = -height / 2f;
                leg.localPosition = position;
            }

            if (crossbar != null)
            {
                Vector3 position = crossbar.localPosition;
                position.y = -height + 0.1f;
                crossbar.localPosition = position;
            }
        }
    }
}
