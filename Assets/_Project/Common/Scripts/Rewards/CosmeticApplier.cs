using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 選んだ見た目のご褒美をシーンに当てる(docs/art-direction.md「M4 見た目のご褒美」)。
    /// アセットのマテリアルは書き換えず、実行時に複製して色や柄を替える。標準のときは何もしない。
    /// リストバンドは両手のコントローラーの子として実行時に作る(全シーン)。
    /// ボール・弓・ラケットはシーンに入ったときに替わる(持っている物をその場では変えない)。
    /// </summary>
    public class CosmeticApplier : MonoBehaviour
    {
        [Header("ボウリング")]
        [Tooltip("プレイヤーのボール(柄を付ける)")]
        [SerializeField] private Renderer[] balls = new Renderer[0];

        [Header("アーチェリー")]
        [Tooltip("弓の上下の腕")]
        [SerializeField] private Renderer[] bowArms = new Renderer[0];

        [Tooltip("弓の胴")]
        [SerializeField] private Renderer bowRiser;

        [Tooltip("弓の握り")]
        [SerializeField] private Renderer bowGrip;

        [Tooltip("弓の腕の先")]
        [SerializeField] private Renderer[] bowTips = new Renderer[0];

        [Tooltip("矢の羽根(矢の見本の中)")]
        [SerializeField] private Renderer[] arrowFeathers = new Renderer[0];

        [Header("卓球")]
        [Tooltip("ラケットのラバー(表)")]
        [SerializeField] private Renderer racketRubber;

        [Tooltip("ラケットの柄")]
        [SerializeField] private Renderer racketHandle;

        [Tooltip("ラケットの柄の飾り")]
        [SerializeField] private Renderer racketOrnament;

        [Header("リストバンド")]
        [Tooltip("左のコントローラー")]
        [SerializeField] private Transform leftController;

        [Tooltip("右のコントローラー")]
        [SerializeField] private Transform rightController;

        [Tooltip("コントローラーから見たリストバンドの位置(m)")]
        [SerializeField] private Vector3 wristPosition = new Vector3(0f, -0.01f, -0.075f);

        [Tooltip("コントローラーから見たリストバンドの向き(度)")]
        [SerializeField] private Vector3 wristRotation = new Vector3(90f, 0f, 0f);

        [Tooltip("確認用:オンにすると全部解放する")]
        [SerializeField] private bool unlockAllForTest;

        private readonly List<Object> created = new List<Object>();

        private static readonly string[][] BowColors =
        {
            null,
            new[] { "#2FB5B0", "#7FE3D6", "#4A4660", "#FFFDF7" },
            new[] { "#E0525E", "#FFB4A2", "#8A5A3B", "#FFD24A" },
            new[] { "#FFD24A", "#F0C995", "#8A5A3B", "#FF8A3D" },
        };

        private static readonly string[][] RacketColors =
        {
            null,
            new[] { "#2FB5B0", "#1A7A8C", "#FFFDF7" },
            new[] { "#FFB4A2", "#E0525E", "#FFD24A" },
            new[] { "#FFD24A", "#8A5A3B", "#FF7F6B" },
        };

        private void Start()
        {
            if (unlockAllForTest)
            {
                RewardCatalog.UnlockAllForTest = true;
            }

            ApplyBall(RewardCatalog.Selected(RewardCatalog.Item.Ball));
            ApplyBow(RewardCatalog.Selected(RewardCatalog.Item.Bow));
            ApplyRacket(RewardCatalog.Selected(RewardCatalog.Item.Racket));
            ApplyWristbands(RewardCatalog.Selected(RewardCatalog.Item.Wristband));
        }

        private void OnDestroy()
        {
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Destroy(o);
                }
            }
        }

        // ---------------------------------------------------------------- ボール

        private void ApplyBall(int style)
        {
            if (style <= 0)
            {
                return;
            }

            foreach (Renderer ball in balls)
            {
                if (ball == null)
                {
                    continue;
                }

                Color ground = ball.sharedMaterial.HasProperty("_BaseColor") ? ball.sharedMaterial.GetColor("_BaseColor") : Color.white;
                Texture2D texture = BallPattern(style, ground);
                Material material = Copy(ball.sharedMaterial);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                ball.sharedMaterial = material;
            }
        }

        /// <summary>柄のテクスチャ(経度・緯度の正距円筒。地の色はボールの元の色)。</summary>
        private Texture2D BallPattern(int style, Color ground)
        {
            const int width = 512;
            const int height = 256;
            Color white = Hex("#FFFDF7");
            Color yellow = Hex("#FFD24A");
            var pixels = new Color[width * height];
            float phi = 1.618f;
            Vector3[] dots =
            {
                new Vector3(0, 1, phi), new Vector3(0, 1, -phi), new Vector3(0, -1, phi), new Vector3(0, -1, -phi),
                new Vector3(1, phi, 0), new Vector3(1, -phi, 0), new Vector3(-1, phi, 0), new Vector3(-1, -phi, 0),
                new Vector3(phi, 0, 1), new Vector3(phi, 0, -1), new Vector3(-phi, 0, 1), new Vector3(-phi, 0, -1),
            };
            Vector3[] stars = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int y = 0; y < height; y++)
            {
                float lat = Mathf.PI * ((y + 0.5f) / height - 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float lon = 2f * Mathf.PI * (x + 0.5f) / width;
                    var dir = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    Color c = ground;
                    float latDeg = Mathf.Abs(lat * Mathf.Rad2Deg);
                    if (style == 1)
                    {
                        if (latDeg < 6f)
                        {
                            c = white;
                        }
                        else if (latDeg > 16f && latDeg < 22f)
                        {
                            c = yellow;
                        }
                    }
                    else if (style == 2)
                    {
                        foreach (Vector3 d in dots)
                        {
                            if (Vector3.Angle(dir, d) < 13f)
                            {
                                c = white;
                                break;
                            }
                        }
                    }
                    else
                    {
                        foreach (Vector3 s in stars)
                        {
                            if (InStar(dir, s))
                            {
                                c = yellow;
                                break;
                            }
                        }
                    }

                    pixels[y * width + x] = c;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texture.SetPixels(pixels);
            texture.wrapModeU = TextureWrapMode.Repeat;
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 2;
            texture.Apply();
            created.Add(texture);
            return texture;
        }

        /// <summary>方向 dir が、center を中心にした5つの角の星(外 20°・内 8°)の中か。</summary>
        private static bool InStar(Vector3 dir, Vector3 center)
        {
            float angle = Vector3.Angle(dir, center);
            if (angle > 20f)
            {
                return false;
            }

            // center に垂直な面の上で角度を測る
            Vector3 up = Mathf.Abs(Vector3.Dot(center, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 a = Vector3.Cross(center, up).normalized;
            Vector3 b = Vector3.Cross(center, a);
            Vector3 flat = Vector3.ProjectOnPlane(dir, center);
            float theta = Mathf.Atan2(Vector3.Dot(flat, b), Vector3.Dot(flat, a));
            // 72° ごとの山で、外の半径 20°・内の半径 8° を結ぶ
            float t = Mathf.Repeat(theta * Mathf.Rad2Deg + 90f, 72f) / 72f;
            float edge = Mathf.Lerp(8f, 20f, Mathf.Abs(t - 0.5f) * 2f);
            return angle < edge;
        }

        // ---------------------------------------------------------------- 弓・ラケット

        private void ApplyBow(int style)
        {
            if (style <= 0 || style >= BowColors.Length)
            {
                return;
            }

            string[] c = BowColors[style];
            Tint(bowArms, c[0]);
            Tint(bowRiser, c[1]);
            Tint(bowGrip, c[2]);
            Tint(bowTips, c[3]);
            Tint(arrowFeathers, c[3]);
        }

        private void ApplyRacket(int style)
        {
            if (style <= 0 || style >= RacketColors.Length)
            {
                return;
            }

            string[] c = RacketColors[style];
            Tint(racketRubber, c[0]);
            Tint(racketHandle, c[1]);
            Tint(racketOrnament, c[2]);
        }

        private void Tint(Renderer[] renderers, string hex)
        {
            foreach (Renderer r in renderers)
            {
                Tint(r, hex);
            }
        }

        private void Tint(Renderer renderer, string hex)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = Copy(renderer.sharedMaterial);
            material.SetColor("_BaseColor", Hex(hex));
            renderer.sharedMaterial = material;
        }

        // ---------------------------------------------------------------- リストバンド

        private void ApplyWristbands(int style)
        {
            if (style <= 0)
            {
                return;
            }

            foreach (Transform controller in new[] { leftController, rightController })
            {
                if (controller == null)
                {
                    continue;
                }

                var band = new GameObject("リストバンド");
                band.transform.SetParent(controller, false);
                band.transform.localPosition = wristPosition;
                band.transform.localRotation = Quaternion.Euler(wristRotation);
                switch (style)
                {
                    case 1:
                        Ring(band.transform, 0f, 0.028f, 0.062f, "#FF7F6B");
                        Ring(band.transform, 0.008f, 0.006f, 0.064f, "#FFD24A");
                        Ring(band.transform, -0.008f, 0.006f, 0.064f, "#FFD24A");
                        break;
                    case 2:
                        Ring(band.transform, 0f, 0.028f, 0.062f, "#2FB5B0");
                        Ring(band.transform, 0f, 0.006f, 0.064f, "#FFFDF7");
                        break;
                    default:
                        string[] colors = { "#FF7F6B", "#FFD24A", "#6CC36A", "#3AA7FF" };
                        for (int i = 0; i < colors.Length; i++)
                        {
                            Ring(band.transform, 0.0105f - 0.007f * i, 0.007f, 0.062f, colors[i]);
                        }

                        break;
                }
            }
        }

        private void Ring(Transform parent, float y, float height, float diameter, string hex)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "帯";
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, y, 0f);
            ring.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            var renderer = ring.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                var material = new Material(lit);
                material.SetColor("_BaseColor", Hex(hex));
                renderer.sharedMaterial = material;
                created.Add(material);
            }
        }

        private Material Copy(Material source)
        {
            var material = new Material(source);
            created.Add(material);
            return material;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
