using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// 本物に近い形のボウリングピンのメッシュを作る。ピンの断面の輪郭(高さと半径)を
    /// Y 軸のまわりに回して作る回転体で、原点が底の中心、高さ 38.1cm。
    /// サブメッシュ 0 が白い胴、サブメッシュ 1 が首の赤い帯2本。
    /// 三角形はおよそ 650 個(docs/art-direction.md 3.1 の小物 300〜1,500 に収める)。
    /// </summary>
    internal static class BowlingPinMeshBuilder
    {
        private const string MeshName = "BowlingPin";

        // 周方向の分割数
        private const int Segments = 16;

        // 輪郭(高さ cm, 半径 cm)。本物のピンの寸法(底の直径約 5cm、胴の最大約 12cm、首の最小約 4.5cm、頭約 6.4cm)に合わせる
        private static readonly Vector2[] Profile =
        {
            new Vector2(0f, 2.6f),
            new Vector2(1.9f, 3.6f),
            new Vector2(4.5f, 4.9f),
            new Vector2(7.6f, 5.7f),
            new Vector2(11.4f, 6.05f),
            new Vector2(15.2f, 5.7f),
            new Vector2(19.0f, 4.6f),
            new Vector2(22.9f, 3.0f),
            new Vector2(25.4f, 2.4f),
            new Vector2(27.0f, 2.25f),
            new Vector2(29.2f, 2.5f),
            new Vector2(31.8f, 3.0f),
            new Vector2(34.3f, 3.2f),
            new Vector2(36.2f, 2.9f),
            new Vector2(37.4f, 2.1f),
            new Vector2(37.9f, 1.2f),
            new Vector2(38.1f, 0f),
        };

        // 首の赤い帯(下端と上端の高さ cm)
        private static readonly Vector2[] Stripes =
        {
            new Vector2(23.4f, 24.4f),
            new Vector2(25.6f, 26.6f),
        };

        /// <summary>
        /// ピンのメッシュを返す。まだなければ folder に作って保存する。
        /// </summary>
        public static Mesh GetOrCreate(string folder)
        {
            SceneBuildUtility.EnsureFolder(folder);
            string path = $"{folder}/{MeshName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = Build();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh Build()
        {
            // 帯の境目の高さも輪郭に入れて、帯の縁をはっきりさせる
            var heights = new SortedSet<float>();
            foreach (Vector2 p in Profile)
            {
                heights.Add(p.x);
            }

            foreach (Vector2 s in Stripes)
            {
                heights.Add(s.x);
                heights.Add(s.y);
            }

            var rings = new List<Vector2>();
            foreach (float h in heights)
            {
                rings.Add(new Vector2(h, RadiusAt(h)));
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var body = new List<int>();
            var stripe = new List<int>();

            // 側面:隣り合う2本の輪ごとに頂点を持たせ、帯と胴でサブメッシュを分ける
            for (int k = 0; k < rings.Count - 1; k++)
            {
                int start = vertices.Count;
                AddRing(rings, k, vertices, normals);
                AddRing(rings, k + 1, vertices, normals);

                float middle = (rings[k].x + rings[k + 1].x) / 2f;
                List<int> target = IsStripe(middle) ? stripe : body;
                for (int i = 0; i < Segments; i++)
                {
                    int next = (i + 1) % Segments;
                    int a = start + i;
                    int b = start + next;
                    int c = start + Segments + i;
                    int d = start + Segments + next;
                    target.AddRange(new[] { a, c, d, a, d, b });
                }
            }

            // 底のふた
            int center = vertices.Count;
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.down);
            float baseRadius = rings[0].y / 100f;
            for (int i = 0; i < Segments; i++)
            {
                vertices.Add(PointOnRing(baseRadius, 0f, i));
                normals.Add(Vector3.down);
            }

            for (int i = 0; i < Segments; i++)
            {
                body.AddRange(new[] { center, center + 1 + i, center + 1 + (i + 1) % Segments });
            }

            var mesh = new Mesh { name = MeshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(stripe, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// k 番目の輪の頂点と法線を足す。法線は前後の輪とのつながり(輪郭の傾き)から求める。
        /// </summary>
        private static void AddRing(List<Vector2> rings, int k, List<Vector3> vertices, List<Vector3> normals)
        {
            Vector2 below = rings[Mathf.Max(0, k - 1)];
            Vector2 above = rings[Mathf.Min(rings.Count - 1, k + 1)];
            // 輪郭の接線(高さ方向の変化, 半径方向の変化)を90度回すと外向きの法線になる
            float dh = above.x - below.x;
            float dr = above.y - below.y;
            var profileNormal = new Vector2(dh, -dr).normalized;

            float h = rings[k].x / 100f;
            float r = rings[k].y / 100f;
            for (int i = 0; i < Segments; i++)
            {
                vertices.Add(PointOnRing(r, h, i));
                float angle = 2f * Mathf.PI * i / Segments;
                normals.Add(new Vector3(Mathf.Cos(angle) * profileNormal.x, profileNormal.y, Mathf.Sin(angle) * profileNormal.x));
            }
        }

        private static Vector3 PointOnRing(float radius, float height, int index)
        {
            float angle = 2f * Mathf.PI * index / Segments;
            return new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        }

        /// <summary>輪郭を直線でつないだときの、その高さの半径(cm)。</summary>
        private static float RadiusAt(float height)
        {
            for (int i = 0; i < Profile.Length - 1; i++)
            {
                if (height <= Profile[i + 1].x)
                {
                    float t = Mathf.InverseLerp(Profile[i].x, Profile[i + 1].x, height);
                    return Mathf.Lerp(Profile[i].y, Profile[i + 1].y, t);
                }
            }

            return Profile[Profile.Length - 1].y;
        }

        private static bool IsStripe(float height)
        {
            foreach (Vector2 s in Stripes)
            {
                if (height > s.x && height < s.y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
