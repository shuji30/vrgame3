using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// M0 のゲート確認用。「正面の目標に向けて10回投げ、8回以上が目標の左右1m以内に飛ぶ」を数える。
    /// 目標の地点に置き、確認したい投げる物を登録する。
    /// 落ちた地点に色付きの目印を置き(命中=青、外れ=赤)、規定回数で目標の色が合否に変わる。
    /// 文字を使わないので VR の中でそのまま確認できる。結果はコンソールにも出す。
    /// </summary>
    public class ThrowAccuracyTest : MonoBehaviour
    {
        [Tooltip("数える対象の投げる物")]
        [SerializeField] private List<ThrowableObject> throwables = new List<ThrowableObject>();

        [Tooltip("投げた位置と目標を結ぶ線から、左右にこの距離(m)以内に落ちたら命中")]
        [SerializeField] private float lateralTolerance = 1f;

        [Tooltip("投げた位置からこの距離(m)より手前に落ちたものは数えない(取り落としを除く)")]
        [SerializeField] private float minThrowDistance = 2f;

        [Tooltip("1セットで投げる回数")]
        [SerializeField] private int throwsPerSet = 10;

        [Tooltip("合格に必要な命中回数")]
        [SerializeField] private int requiredHits = 8;

        [Tooltip("合否を色で表す目標の Renderer。空ならこのオブジェクトから探す")]
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("命中の目印と合格の色に使うマテリアル")]
        [SerializeField] private Material hitMaterial;

        [Tooltip("外れの目印と不合格の色に使うマテリアル")]
        [SerializeField] private Material missMaterial;

        [Tooltip("落ちた地点に置く目印の大きさ(m)")]
        [SerializeField] private float markerSize = 0.15f;

        private readonly Dictionary<ThrowableObject, Vector3> releasePositions = new Dictionary<ThrowableObject, Vector3>();
        private readonly List<GameObject> markers = new List<GameObject>();
        private Material originalTargetMaterial;
        private int throwCount;
        private int hitCount;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (targetRenderer != null)
            {
                originalTargetMaterial = targetRenderer.sharedMaterial;
            }
        }

        private void OnEnable()
        {
            foreach (ThrowableObject throwable in throwables)
            {
                if (throwable == null)
                {
                    continue;
                }

                throwable.Released += OnReleased;
                throwable.FirstImpact += OnFirstImpact;
            }
        }

        private void OnDisable()
        {
            foreach (ThrowableObject throwable in throwables)
            {
                if (throwable == null)
                {
                    continue;
                }

                throwable.Released -= OnReleased;
                throwable.FirstImpact -= OnFirstImpact;
            }
        }

        /// <summary>
        /// 数え直す。Inspector の右上メニュー(⋮)からも呼べる。
        /// </summary>
        [ContextMenu("数え直す")]
        public void ResetCount()
        {
            throwCount = 0;
            hitCount = 0;
            releasePositions.Clear();
            foreach (GameObject marker in markers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }

            markers.Clear();
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = originalTargetMaterial;
            }
        }

        private void OnReleased(ThrowableObject throwable, Vector3 position)
        {
            // 前のセットが終わっていたら、次に投げた時点で数え直す
            if (throwCount >= throwsPerSet)
            {
                ResetCount();
            }

            releasePositions[throwable] = position;
        }

        private void OnFirstImpact(ThrowableObject throwable, Vector3 point)
        {
            if (!releasePositions.TryGetValue(throwable, out Vector3 release))
            {
                return;
            }

            releasePositions.Remove(throwable);

            Vector3 up = Vector3.up;
            Vector3 toTarget = Vector3.ProjectOnPlane(transform.position - release, up);
            Vector3 toLanding = Vector3.ProjectOnPlane(point - release, up);
            if (toTarget.sqrMagnitude < 0.01f || toLanding.magnitude < minThrowDistance)
            {
                return;
            }

            // 投げた位置→目標の線からの左右のずれ。後ろに飛んだものは外れ
            Vector3 forward = toTarget.normalized;
            float along = Vector3.Dot(toLanding, forward);
            float lateral = (toLanding - forward * along).magnitude;
            bool hit = along > 0f && lateral <= lateralTolerance;

            throwCount++;
            if (hit)
            {
                hitCount++;
            }

            PlaceMarker(point, hit);
            Debug.Log($"[M0 投てき確認] {throwCount}/{throwsPerSet} 回目: {(hit ? "命中" : "外れ")}" +
                      $"(左右のずれ {lateral:F2}m、前方 {along:F2}m)命中 {hitCount} 回");

            if (throwCount >= throwsPerSet)
            {
                bool passed = hitCount >= requiredHits;
                if (targetRenderer != null)
                {
                    Material result = passed ? hitMaterial : missMaterial;
                    if (result != null)
                    {
                        targetRenderer.sharedMaterial = result;
                    }
                }

                Debug.Log($"[M0 投てき確認] 結果: {(passed ? "合格" : "不合格")}" +
                          $"(命中 {hitCount}/{throwsPerSet}、合格ライン {requiredHits})");
            }
        }

        private void PlaceMarker(Vector3 point, bool hit)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = hit ? "命中の目印" : "外れの目印";

            // 目印が投げる物に当たらないよう、当たり判定は消す
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = point;
            marker.transform.localScale = Vector3.one * markerSize;

            Material material = hit ? hitMaterial : missMaterial;
            if (material != null)
            {
                marker.GetComponent<Renderer>().sharedMaterial = material;
            }

            markers.Add(marker);
        }
    }
}
