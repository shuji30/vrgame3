using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRGame3.Common;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Archery.EditorTools
{
    /// <summary>
    /// アーチェリー場の仮組みシーンを作るエディタ処理。docs/art-direction.md「M2 アーチェリー場の仮組み」、
    /// ルールは docs/plan.md「アーチェリー」。メニュー「VRGame3/アーチェリー場を作る」。
    /// 座標:射線が Z=0、的の方が +Z、地面の上面 Y=0。
    /// </summary>
    public static class ArcheryRangeBuilder
    {
        private const string SceneName = "Archery";
        private const string SceneFolder = "Assets/_Project/Sports/Archery/Scenes";
        private const string ScenePath = SceneFolder + "/" + SceneName + ".unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Archery";
        private const string TextureFolder = "Assets/_Project/Sports/Archery/Textures";
        private const string AudioFolder = "Assets/_Project/Art/Audio/Placeholder";

        private const float TargetDiameter = 1.2f;
        private const float TargetFaceSize = 1.35f;
        private static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, -0.75f);
        private static readonly Vector3 Eye = new Vector3(0f, 1.6f, -0.75f);

        [MenuItem("VRGame3/アーチェリー場を作る")]
        public static void Build()
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("アーチェリー場",
                    "TextMeshPro の基本素材がまだ入っていません。\n" +
                    "メニュー「Window > TextMeshPro > Import TMP Essential Resources」を実行してから、もう一度作ってください。",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("アーチェリー場", $"{ScenePath} はすでにあります。作り直しますか?", "作り直す", "やめる"))
            {
                return;
            }

            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                EditorUtility.DisplayDialog("アーチェリー場", $"日本語フォントが見つかりません({UIBuildUtility.FontPath})。", "OK");
                return;
            }

            AssetDatabase.Refresh();
            SceneBuildUtility.EnsureFolder(SceneFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                }
            }

            var root = new GameObject("アーチェリー場");
            BuildGround(root.transform, font);
            BuildBackdrop(root.transform);
            ArcheryTarget target = BuildTarget(root.transform);

            var warnings = new List<string>();
            Material arc = SceneBuildUtility.GetOrCreateArcMaterial(MaterialFolder, "A_TeleportArc");
            Material reticle = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "A_Reticle", "#3AA7FF", true);
            warnings.AddRange(SceneBuildUtility.SetUpXROrigin(arc, reticle, PlayerPosition, Quaternion.identity,
                requireTeleportArea: true));

            Bow bow = BuildBow(root.transform);
            ArcheryScoreBoard scoreBoard = BuildScoreBoard(root.transform, font);
            ResultPanel resultPanel = CommonSceneBuilder.AddResultPanel(root.transform, new Vector3(0f, 1.5f, 2.5f), Eye);

            var game = root.AddComponent<ArcheryGame>();
            var gameSo = new SerializedObject(game);
            gameSo.FindProperty("bow").objectReferenceValue = bow;
            gameSo.FindProperty("target").objectReferenceValue = target;
            gameSo.FindProperty("scoreBoard").objectReferenceValue = scoreBoard;
            gameSo.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            SetControllers(gameSo, warnings);
            gameSo.ApplyModifiedPropertiesWithoutUndo();

            ChoicePanel choicePanel = CommonSceneBuilder.AddChoicePanel(root.transform, new Vector3(0f, 1.3f, 0.35f), Eye);
            BuildStartPanel(root.transform, font, game, choicePanel);
            BuildFeedback(root.transform, game, target, scoreBoard);
            BuildAmbient(root.transform);

            AddWarning(warnings, CommonSceneBuilder.AddFader());
            AddWarning(warnings, CommonSceneBuilder.AddMenuAndSettings(root.transform, false, out _));
            CommonSceneBuilder.EnsureSkillPointRules();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuildSettings(ScenePath);

            string message = $"{ScenePath} を作りました。";
            if (warnings.Count > 0)
            {
                message += "\n\n手で確認が必要な点:\n- " + string.Join("\n- ", warnings);
                foreach (string warning in warnings)
                {
                    Debug.LogWarning($"[アーチェリー場] {warning}");
                }
            }

            Debug.Log($"[アーチェリー場] {message}");
            EditorUtility.DisplayDialog("アーチェリー場", message, "OK");
        }

        private static void AddWarning(List<string> warnings, string warning)
        {
            if (warning != null)
            {
                warnings.Add(warning);
            }
        }

        // ---------------------------------------------------------------- 地面と射つ位置

        private static void BuildGround(Transform root, TMP_FontAsset font)
        {
            var parent = new GameObject("地面と射つ位置").transform;
            parent.SetParent(root, false);

            Block(parent, "台", new Vector3(-1.5f, -0.1f, -2.5f), new Vector3(1.5f, 0f, 0.3f), Mat("A_Wood", "#C98B55"), true);
            GameObject area = Block(parent, "射つ範囲(テレポートできる)", new Vector3(-0.75f, -0.01f, -1.5f),
                new Vector3(0.75f, 0.01f, 0f), Mat("A_WoodLight", "#F0C995"), true);
            area.AddComponent<TeleportArea>();
            Block(parent, "射線", new Vector3(-0.75f, 0.005f, -0.025f), new Vector3(0.75f, 0.015f, 0.025f),
                Mat("A_White", "#FFFDF7"), false);
            Block(parent, "地面", new Vector3(-20f, -0.55f, -12f), new Vector3(20f, -0.05f, 45f), Mat("A_Grass", "#6CC36A"), true);
            Block(parent, "射場の芝", new Vector3(-3f, -0.05f, 0.3f), new Vector3(3f, -0.04f, 34f), Mat("A_Lawn", "#B9E27A"), true);

            Material outline = GetOrCreateOutlineMaterial(font);
            foreach (int distance in new[] { 10, 20, 30 })
            {
                Block(parent, $"距離の札の柱 {distance}m", new Vector3(-3.24f, -0.05f, distance - 0.04f),
                    new Vector3(-3.16f, 0.6f, distance + 0.04f), Mat("A_White", "#FFFDF7"), false);
                TextMeshPro label = Text3D(parent, $"距離の札 {distance}m", $"{distance}m", font, outline,
                    new Vector3(-3.2f, 0.85f, distance), 0.4f, CommonSceneBuilder.Hex("#FFFDF7"));
                Vector3 toEye = Eye - label.transform.position;
                toEye.y = 0f;
                label.transform.rotation = Quaternion.LookRotation(-toEye.normalized, Vector3.up);
            }
        }

        /// <summary>安全のための幕と土手、横の生け垣、森。</summary>
        private static void BuildBackdrop(Transform root)
        {
            var parent = new GameObject("背景").transform;
            parent.SetParent(root, false);
            Material dark = Mat("A_LeafDark", "#2F8A5B");
            Block(parent, "幕", new Vector3(-5f, -0.05f, 33.45f), new Vector3(5f, 3.5f, 33.55f), dark, true);
            Block(parent, "土手", new Vector3(-10f, -0.05f, 34f), new Vector3(10f, 3.5f, 37f), Mat("A_Earth", "#C9A26B"), true);
            Block(parent, "土手の上の草", new Vector3(-10f, 3.5f, 34f), new Vector3(10f, 3.7f, 37f), Mat("A_Grass", "#6CC36A"), true);
            for (int side = -1; side <= 1; side += 2)
            {
                Block(parent, side < 0 ? "生け垣(左)" : "生け垣(右)", new Vector3(side * 6f - 0.3f, -0.05f, 1f),
                    new Vector3(side * 6f + 0.3f, 1f, 33f), dark, true);
            }

            var trees = new List<Vector3>();
            int n = 0;
            for (float z = -4f; z <= 32f; z += 6f)
            {
                trees.Add(new Vector3(n % 2 == 0 ? -8f : -11f, 0f, z));
                trees.Add(new Vector3(n % 2 == 0 ? 11f : 8f, 0f, z));
                n++;
            }

            trees.AddRange(new[] { new Vector3(-7f, 0f, 40f), new Vector3(-2.5f, 0f, 40f), new Vector3(2.5f, 0f, 40f), new Vector3(7f, 0f, 40f) });
            for (int i = 0; i < trees.Count; i++)
            {
                Shape(parent, $"木の幹{i + 1}", PrimitiveType.Cylinder, trees[i] + new Vector3(0f, 2.5f, 0f),
                    new Vector3(0.35f, 2.5f, 0.35f), Mat("A_WoodDark", "#8A5A3B"));
                Shape(parent, $"木の葉{i + 1}", PrimitiveType.Sphere, trees[i] + new Vector3(0f, 5.5f, 0f),
                    new Vector3(3.5f, 3f, 3.5f), i % 2 == 0 ? Mat("A_Grass", "#6CC36A") : dark);
            }
        }

        // ---------------------------------------------------------------- 的

        private static ArcheryTarget BuildTarget(Transform root)
        {
            var targetObject = new GameObject("的");
            targetObject.transform.SetParent(root, false);
            targetObject.transform.position = new Vector3(0f, 1.45f, 10f);

            var visual = new GameObject("見た目");
            visual.transform.SetParent(targetObject.transform, false);

            // 畳(矢が刺さる当たり判定はこれ1つ)。前面が的の面(親の Z)
            GameObject board = SceneBuildUtility.CreatePrimitive("畳", PrimitiveType.Cube, Vector3.zero,
                Vector3.one, Mat("A_Straw", "#F2D49B"), true);
            Local(board, visual.transform, new Vector3(0f, 0f, 0.15f), new Vector3(TargetFaceSize, TargetFaceSize, 0.3f));

            GameObject face = SceneBuildUtility.CreatePrimitive("的の面", PrimitiveType.Quad, Vector3.zero, Vector3.one,
                GetOrCreateTargetMaterial(), false);
            Local(face, visual.transform, new Vector3(0f, 0f, -0.002f), new Vector3(TargetFaceSize, TargetFaceSize, 1f));

            Material wood = Mat("A_WoodDark", "#8A5A3B");
            var legs = new List<Transform>();
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leg = SceneBuildUtility.CreatePrimitive("脚", PrimitiveType.Cube, Vector3.zero, Vector3.one, wood, false);
                Local(leg, visual.transform, new Vector3(side * 0.45f, -0.725f, 0.35f), new Vector3(0.08f, 1.45f, 0.08f));
                legs.Add(leg.transform);
            }

            GameObject crossbar = SceneBuildUtility.CreatePrimitive("横木", PrimitiveType.Cube, Vector3.zero, Vector3.one, wood, false);
            Local(crossbar, visual.transform, new Vector3(0f, -1.35f, 0.35f), new Vector3(1.2f, 0.08f, 0.08f));

            var target = targetObject.AddComponent<ArcheryTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("diameter").floatValue = TargetDiameter;
            SerializedProperty legList = so.FindProperty("legs");
            legList.arraySize = legs.Count;
            for (int i = 0; i < legs.Count; i++)
            {
                legList.GetArrayElementAtIndex(i).objectReferenceValue = legs[i];
            }

            so.FindProperty("crossbar").objectReferenceValue = crossbar.transform;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        /// <summary>
        /// 的の面のテクスチャ(T_ArcheryTarget)を書き出してマテリアルを作る。
        /// 1024px で 1.35m 四方。直径 1.2m の円の外は畳の色、10リング、中心 6cm の X に細い輪と「+」。
        /// </summary>
        private static Material GetOrCreateTargetMaterial()
        {
            string materialPath = $"{MaterialFolder}/A_TargetFace.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null)
            {
                return existing;
            }

            SceneBuildUtility.EnsureFolder(TextureFolder);
            string texturePath = $"{TextureFolder}/T_ArcheryTarget.png";
            const int size = 1024;
            float metersPerPixel = TargetFaceSize / size;
            float ring = TargetDiameter / 2f / ArcheryScoring.RingCount;
            float line = 3f * metersPerPixel;
            Color straw = CommonSceneBuilder.Hex("#F2D49B");
            Color white = CommonSceneBuilder.Hex("#FFFDF7");
            Color dark = CommonSceneBuilder.Hex("#4A4660");
            Color blue = CommonSceneBuilder.Hex("#3AA7FF");
            Color red = CommonSceneBuilder.Hex("#FF7F6B");
            Color gold = CommonSceneBuilder.Hex("#FFD24A");
            Color xLine = CommonSceneBuilder.Hex("#FF8A3D");
            Color[] bands = { gold, red, blue, dark, white };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f - size / 2f) * metersPerPixel;
                    float py = (y + 0.5f - size / 2f) * metersPerPixel;
                    float r = Mathf.Sqrt(px * px + py * py);
                    Color c;
                    if (r >= TargetDiameter / 2f)
                    {
                        c = straw;
                    }
                    else
                    {
                        int ringIndex = Mathf.FloorToInt(r / ring);
                        Color band = bands[Mathf.Clamp(ringIndex / 2, 0, 4)];
                        c = band;
                        // リングの境目の線(明るい帯の中は暗い線、暗い帯の中は白い線)
                        float toEdge = Mathf.Min(r - ringIndex * ring, (ringIndex + 1) * ring - r);
                        if (toEdge < line / 2f && ringIndex > 0)
                        {
                            c = band == dark ? white : dark;
                        }

                        // X:中心 6cm の細い輪と「+」
                        float xRadius = ring / 2f;
                        if (Mathf.Abs(r - xRadius) < line / 2f ||
                            (r < xRadius * 0.6f && (Mathf.Abs(px) < line / 2f || Mathf.Abs(py) < line / 2f)))
                        {
                            c = xLine;
                        }
                    }

                    pixels[y * size + x] = c;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            // 30m 先でちらつかないように(docs/art-direction.md の取り込み設定)
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            SceneBuildUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        // ---------------------------------------------------------------- 弓と矢

        private static Bow BuildBow(Transform root)
        {
            var bowObject = new GameObject("弓");
            bowObject.transform.SetParent(root, false);
            bowObject.transform.position = new Vector3(0.4f, 1.1f, -0.5f);
            Material dark = Mat("A_WoodDark", "#8A5A3B");
            Material wood = Mat("A_Wood", "#C98B55");
            Material accent = Mat("A_Coral", "#FF7F6B");

            Part(bowObject.transform, "握り", PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.035f, 0.07f, 0.035f), dark);
            Part(bowObject.transform, "胴", PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.04f, 0.30f, 0.03f), wood);
            float tilt = Mathf.Atan2(0.15f, 0.40f) * Mathf.Rad2Deg;
            Part(bowObject.transform, "上の腕", PrimitiveType.Cube, new Vector3(0f, 0.35f, -0.075f), new Vector3(-tilt, 0f, 0f),
                new Vector3(0.035f, 0.43f, 0.015f), wood);
            Part(bowObject.transform, "下の腕", PrimitiveType.Cube, new Vector3(0f, -0.35f, -0.075f), new Vector3(tilt, 0f, 0f),
                new Vector3(0.035f, 0.43f, 0.015f), wood);
            Transform topTip = Part(bowObject.transform, "上の先", PrimitiveType.Sphere, new Vector3(0f, 0.55f, -0.15f), Vector3.zero,
                Vector3.one * 0.03f, accent).transform;
            Transform bottomTip = Part(bowObject.transform, "下の先", PrimitiveType.Sphere, new Vector3(0f, -0.55f, -0.15f), Vector3.zero,
                Vector3.one * 0.03f, accent).transform;

            var nock = new GameObject("つがえる点");
            nock.transform.SetParent(bowObject.transform, false);
            nock.transform.localPosition = new Vector3(0f, 0f, -0.15f);

            var stringObject = new GameObject("弦");
            stringObject.transform.SetParent(bowObject.transform, false);
            LineRenderer bowString = Line(stringObject, SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "A_String", "#FFFDF7", true),
                0.004f);
            bowString.positionCount = 3;

            Arrow arrowTemplate = BuildArrow(bowObject.transform);

            var aimObject = new GameObject("ねらいの補助");
            aimObject.transform.SetParent(bowObject.transform, false);
            LineRenderer aim = Line(aimObject, GetOrCreateDotMaterial(), 0.01f);
            aim.widthCurve = AnimationCurve.Linear(0f, 0.010f, 1f, 0.006f);
            aim.textureMode = LineTextureMode.Tile;
            aim.textureScale = new Vector2(10f, 1f);
            aim.enabled = false;

            AudioSource drawSource = Source(bowObject, "弦の音", 1f, 10f);
            drawSource.clip = Clip("SFX_BowDraw_Loop");
            drawSource.loop = true;
            AudioSource releaseSource = Source(bowObject, "放つ音", 1f, 15f);

            var bow = bowObject.AddComponent<Bow>();
            var so = new SerializedObject(bow);
            so.FindProperty("bowString").objectReferenceValue = bowString;
            so.FindProperty("topTip").objectReferenceValue = topTip;
            so.FindProperty("bottomTip").objectReferenceValue = bottomTip;
            so.FindProperty("restNock").objectReferenceValue = nock.transform;
            so.FindProperty("arrowTemplate").objectReferenceValue = arrowTemplate;
            so.FindProperty("aimLine").objectReferenceValue = aim;
            so.FindProperty("drawSource").objectReferenceValue = drawSource;
            so.FindProperty("releaseSource").objectReferenceValue = releaseSource;
            SetClips(so.FindProperty("releaseClips"), Clips("SFX_BowRelease_", 3));
            so.FindProperty("whooshClip").objectReferenceValue = Clip("SFX_ArrowWhoosh");
            so.ApplyModifiedPropertiesWithoutUndo();
            return bow;
        }

        /// <summary>矢の見本。原点が矢の先、+Z が飛ぶ向き。当たり判定は付けない(レイで取る)。</summary>
        private static Arrow BuildArrow(Transform parent)
        {
            var arrowObject = new GameObject("矢の見本");
            arrowObject.transform.SetParent(parent, false);
            Part(arrowObject.transform, "軸", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.4f), new Vector3(90f, 0f, 0f),
                new Vector3(0.008f, 0.4f, 0.008f), Mat("A_WoodLight", "#F0C995"));
            Part(arrowObject.transform, "矢じり", PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f), new Vector3(0f, 0f, 45f),
                new Vector3(0.012f, 0.012f, 0.04f), Mat("A_Stone", "#B8B4C8"));
            Material feather = Mat("A_Feather", "#FF7F6B");
            Part(arrowObject.transform, "羽根1", PrimitiveType.Cube, new Vector3(0f, 0f, -0.73f), Vector3.zero,
                new Vector3(0.002f, 0.025f, 0.08f), feather);
            Part(arrowObject.transform, "羽根2", PrimitiveType.Cube, new Vector3(0f, 0f, -0.73f), new Vector3(0f, 0f, 90f),
                new Vector3(0.002f, 0.025f, 0.08f), feather);
            Part(arrowObject.transform, "筈", PrimitiveType.Cube, new Vector3(0f, 0f, -0.7925f), Vector3.zero,
                new Vector3(0.012f, 0.012f, 0.015f), Mat("A_Yellow", "#FFD24A"));
            var arrow = arrowObject.AddComponent<Arrow>();
            arrowObject.SetActive(false);
            return arrow;
        }

        /// <summary>点線のマテリアル(丸い点を 0.1m おきに並べる。Alpha Clip で半透明にしない)。</summary>
        private static Material GetOrCreateDotMaterial()
        {
            string materialPath = $"{MaterialFolder}/A_AimDots.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null)
            {
                return existing;
            }

            SceneBuildUtility.EnsureFolder(TextureFolder);
            string texturePath = $"{TextureFolder}/T_AimDot.png";
            var texture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x + 0.5f - 8f) / 7f;
                    float dy = (y + 0.5f - 8f) / 7f;
                    bool inside = dx * dx + dy * dy <= 1f;
                    texture.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", CommonSceneBuilder.Hex("#FFFDF7"));
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            SceneBuildUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static void SetControllers(SerializedObject gameSo, List<string> warnings)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform left = all.FirstOrDefault(t => t.name == "Left Controller");
            Transform right = all.FirstOrDefault(t => t.name == "Right Controller");
            if (left == null || right == null)
            {
                warnings.Add("Left Controller / Right Controller が見つかりません。ArcheryGame の手の欄を手で設定してください");
                return;
            }

            gameSo.FindProperty("leftController").objectReferenceValue = left;
            gameSo.FindProperty("rightController").objectReferenceValue = right;
            gameSo.FindProperty("leftInteractor").objectReferenceValue = left.GetComponentInChildren<NearFarInteractor>(true);
            gameSo.FindProperty("rightInteractor").objectReferenceValue = right.GetComponentInChildren<NearFarInteractor>(true);
        }

        // ---------------------------------------------------------------- 表示

        private static ArcheryScoreBoard BuildScoreBoard(Transform root, TMP_FontAsset font)
        {
            var holder = new GameObject("スコア板");
            holder.transform.SetParent(root, false);
            var board = holder.AddComponent<ArcheryScoreBoard>();

            // 小型スコア板:射つ位置の右前(左利きでも弦を引く手の届かない所。M2 では右に固定)
            Vector3 center = new Vector3(1f, 1f, 1f);
            var small = new GameObject("小型スコア板");
            small.transform.SetParent(holder.transform, false);
            small.transform.SetPositionAndRotation(center, Quaternion.LookRotation(center - Eye, Vector3.up));
            GameObject frame = SceneBuildUtility.CreatePrimitive("枠", PrimitiveType.Cube, Vector3.zero, Vector3.one,
                Mat("A_Wood", "#C98B55"), false);
            Local(frame, small.transform, new Vector3(0f, 0f, 0.03f), new Vector3(1.1f, 0.7f, 0.05f));
            GameObject post = SceneBuildUtility.CreatePrimitive("柱", PrimitiveType.Cylinder, Vector3.zero, Vector3.one,
                Mat("A_WoodDark", "#8A5A3B"), false);
            Local(post, small.transform, new Vector3(0f, -0.675f, 0.06f), new Vector3(0.08f, 0.325f, 0.08f));

            GameObject canvas = UIBuildUtility.CreateCanvas(small.transform, "文字", new Vector2(1000f, 600f), UIBuildUtility.Background);
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            Transform c = canvas.transform;
            Color light = UIBuildUtility.TextLight;
            TMP_Text round = UIBuildUtility.Label(c, "ラウンド", "ラウンド1  10m", font, 60, new Vector2(0f, 235f), new Vector2(960f, 80f), light);
            var slotTexts = new TMP_Text[ArcheryScoreSheet.ArrowsPerRound];
            var slotImages = new Image[ArcheryScoreSheet.ArrowsPerRound];
            for (int i = 0; i < slotTexts.Length; i++)
            {
                (Image image, TMP_Text text) = UIBuildUtility.Button(c, $"{i + 1}本目", string.Empty, font, 76,
                    new Vector2(-375f + i * 150f, 110f), new Vector2(130f, 120f), UIBuildUtility.ButtonColor, light, null);
                Object.DestroyImmediate(image.GetComponent<Button>());
                image.raycastTarget = false;
                slotTexts[i] = text;
                slotImages[i] = image;
            }

            TMP_Text rounds = UIBuildUtility.Label(c, "ラウンドの合計", "R1 —   R2 —   R3 —", font, 60, new Vector2(0f, -20f),
                new Vector2(960f, 80f), light);
            TMP_Text total = UIBuildUtility.Label(c, "ごうけい", "ごうけい 0", font, 72, new Vector2(-220f, -140f), new Vector2(500f, 90f), light);
            TMP_Text left = UIBuildUtility.Label(c, "のこり", "のこり 18本", font, 60, new Vector2(260f, -140f), new Vector2(400f, 90f), light);

            Material outline = GetOrCreateOutlineMaterial(font);
            TextMeshPro hit = Text3D(holder.transform, "的の横の点数", "10", font, outline, new Vector3(1.5f, 1.45f, 10f), 0.4f,
                light);
            TextMeshPro banner = Text3D(holder.transform, "ラウンドの見出し", "ラウンド1  10m", font, outline, new Vector3(0f, 2.4f, 6f),
                0.25f, light);

            var so = new SerializedObject(board);
            so.FindProperty("roundText").objectReferenceValue = round;
            SetArray(so.FindProperty("slotTexts"), slotTexts);
            SetArray(so.FindProperty("slotImages"), slotImages);
            so.FindProperty("roundsText").objectReferenceValue = rounds;
            so.FindProperty("totalText").objectReferenceValue = total;
            so.FindProperty("leftText").objectReferenceValue = left;
            so.FindProperty("hitText").objectReferenceValue = hit;
            so.FindProperty("bannerText").objectReferenceValue = banner;
            so.ApplyModifiedPropertiesWithoutUndo();
            return board;
        }

        /// <summary>「はじめに」パネル(docs/art-direction.md 6.6 の最後。800 × 720px)。</summary>
        private static void BuildStartPanel(Transform root, TMP_FontAsset font, ArcheryGame game, ChoicePanel choicePanel)
        {
            UIBuildUtility.EnsureEventSystem();
            var holder = new GameObject("はじめに");
            holder.transform.SetParent(root, false);
            var panel = holder.AddComponent<ArcheryStartPanel>();

            // docs/art-direction.md 6.10 のアーチェリーの行(1000 × 940)
            Vector3 position = new Vector3(0f, 1.3f, 0.35f);
            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "はじめにパネル", new Vector2(1000f, 940f),
                UIBuildUtility.Background);
            canvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - Eye, Vector3.up));
            Transform c = canvas.transform;
            Color light = UIBuildUtility.TextLight;

            TMP_Text title = UIBuildUtility.Label(c, "見出し", "はじめに", font, 52, new Vector2(0f, 410f), new Vector2(960f, 70f), light);
            UIBuildUtility.Label(c, "案内1", "まわりに人や物がないか確かめてください", font, 30, new Vector2(0f, 340f), new Vector2(960f, 44f), light);
            UIBuildUtility.Label(c, "案内2", "コントローラーのストラップを手首に通してください", font, 30, new Vector2(0f, 293f),
                new Vector2(960f, 44f), light);

            Vector2 wide = new Vector2(220f, 76f);
            UIBuildUtility.Label(c, "補助の見出し", "ねらいの補助", font, 34, new Vector2(-360f, -60f), new Vector2(260f, 60f), light);
            (Image onImage, TMP_Text onText) = UIBuildUtility.NormalButton(c, "補助オン", "オン", font, 38, new Vector2(-110f, -60f),
                wide, panel.AimAssistOn);
            (Image offImage, TMP_Text offText) = UIBuildUtility.NormalButton(c, "補助オフ", "オフ", font, 38, new Vector2(125f, -60f),
                wide, panel.AimAssistOff);

            UIBuildUtility.Label(c, "身長の見出し", "身長", font, 36, new Vector2(-360f, -165f), new Vector2(260f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を下げる", "-", font, 48, new Vector2(-110f, -165f), new Vector2(100f, 76f), panel.DecreaseHeight);
            TMP_Text height = UIBuildUtility.Label(c, "身長の数字", "170 cm", font, 40, new Vector2(70f, -165f), new Vector2(200f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を上げる", "+", font, 48, new Vector2(250f, -165f), new Vector2(100f, 76f), panel.IncreaseHeight);

            UIBuildUtility.Label(c, "利き手の見出し", "利き手", font, 36, new Vector2(-360f, -270f), new Vector2(260f, 60f), light);
            (Image leftImage, TMP_Text leftText) = UIBuildUtility.NormalButton(c, "左利き", "左", font, 38, new Vector2(-110f, -270f),
                wide, panel.SelectLeftHand);
            (Image rightImage, TMP_Text rightText) = UIBuildUtility.NormalButton(c, "右利き", "右", font, 38, new Vector2(125f, -270f),
                wide, panel.SelectRightHand);

            (_, TMP_Text startLabel) = UIBuildUtility.PrimaryButton(c, "はじめる", "はじめる", font, 46, new Vector2(0f, -390f),
                new Vector2(340f, 96f), panel.StartGame);

            ModeSelector selector = CommonSceneBuilder.AddModeRows(holder, c, font, SportIds.Archery,
                new[] { SportMode.Solo, SportMode.Turns, SportMode.Challenge }, 150f, 45f, startLabel, title);

            var so = new SerializedObject(panel);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("modeSelector").objectReferenceValue = selector;
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            so.FindProperty("heightText").objectReferenceValue = height;
            SetArray(so.FindProperty("choiceImages"), new[] { leftImage, rightImage, onImage, offImage });
            SetArray(so.FindProperty("choiceTexts"), new[] { leftText, rightText, onText, offText });
            so.ApplyModifiedPropertiesWithoutUndo();

            var gameSo = new SerializedObject(game);
            gameSo.FindProperty("modeSelector").objectReferenceValue = selector;
            gameSo.FindProperty("choicePanel").objectReferenceValue = choicePanel;
            gameSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 音と演出

        private static void BuildFeedback(Transform root, ArcheryGame game, ArcheryTarget target, ArcheryScoreBoard scoreBoard)
        {
            var holder = new GameObject("音と演出");
            holder.transform.SetParent(root, false);
            var feedback = holder.AddComponent<ArcheryFeedback>();

            // 的に刺さる音は 18m 以上先からも届くよう減衰を緩める(docs/art-direction.md 8.4)
            AudioSource hitSource = Source(holder, "当たった点の音", 10f, 60f);
            AudioSource targetSource = Source(holder, "的のまわりの音", 10f, 60f);
            targetSource.transform.position = target.transform.position;

            Vector3 at = target.transform.position;
            ParticleSystem dust = ParticleBuildUtility.Burst(holder.transform, "砂ぼこり", at, 12, new[] { "#F2D49B", "#F0C995" },
                new Vector2(0.3f, 0.4f), new Vector2(0.4f, 1.2f), new Vector2(0.02f, 0.04f), 0.5f, 0.03f, false, true, false);
            ParticleSystem sparkle = ParticleBuildUtility.Burst(holder.transform, "光の粒", at, 12, new[] { "#FFD24A", "#FFFDF7" },
                new Vector2(0.5f, 0.6f), new Vector2(0.8f, 1.2f), new Vector2(0.03f, 0.05f), 0f, 0.05f, false, true, false);
            ParticleSystem confetti = ParticleBuildUtility.Burst(holder.transform, "X の紙吹雪", at + Vector3.up * 1.2f, 120,
                new[] { "#FFD24A", "#FF7F6B", "#3AA7FF", "#6CC36A", "#FFFDF7" }, new Vector2(1.2f, 1.5f), new Vector2(1.5f, 3.5f),
                new Vector2(0.05f, 0.09f), 0.35f, 0.4f, true, false, true);
            ParticleSystem grass = ParticleBuildUtility.Burst(holder.transform, "草のかけら", at, 6, new[] { "#6CC36A", "#B9E27A" },
                new Vector2(0.25f, 0.3f), new Vector2(0.5f, 1.2f), new Vector2(0.03f, 0.05f), 0.8f, 0.05f, true, true, false);

            var so = new SerializedObject(feedback);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("scoreBoard").objectReferenceValue = scoreBoard;
            so.FindProperty("hitSource").objectReferenceValue = hitSource;
            so.FindProperty("targetSource").objectReferenceValue = targetSource;
            SetClips(so.FindProperty("targetHitClips"), Clips("SFX_ArrowHitTarget_", 3));
            SetClips(so.FindProperty("groundHitClips"), Clips("SFX_ArrowHitGround_", 2));
            so.FindProperty("tenClip").objectReferenceValue = Clip("SFX_ArcheryTen");
            so.FindProperty("xClip").objectReferenceValue = Clip("SFX_ArcheryX");
            so.FindProperty("roundEndClip").objectReferenceValue = Clip("SFX_ArcheryRoundEnd");
            so.FindProperty("targetAppearClip").objectReferenceValue = Clip("SFX_TargetAppear");
            so.FindProperty("dust").objectReferenceValue = dust;
            so.FindProperty("sparkle").objectReferenceValue = sparkle;
            so.FindProperty("confetti").objectReferenceValue = confetti;
            so.FindProperty("grass").objectReferenceValue = grass;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>環境音:左右の森から葉のざわめき(3D のループ)。</summary>
        private static void BuildAmbient(Transform root)
        {
            var holder = new GameObject("環境音");
            holder.transform.SetParent(root, false);
            Vector3[] points = { new Vector3(-9f, 3f, 6f), new Vector3(9f, 3f, 12f) };
            for (int i = 0; i < points.Length; i++)
            {
                var go = new GameObject($"葉のざわめき{i + 1}");
                go.transform.SetParent(holder.transform, false);
                go.transform.position = points[i];
                AudioSource source = Source(go, "", 4f, 40f, go);
                source.clip = Clip("AMB_Leaves_Loop");
                source.loop = true;
                source.playOnAwake = true;
                source.volume = 0.06f;
                source.pitch = 0.95f + 0.07f * i;
            }
        }

        // ---------------------------------------------------------------- 小さな部品

        private static Material Mat(string name, string hex)
        {
            return SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, name, hex, false);
        }

        /// <summary>最小と最大の角で決める動かない箱(Static、影なし)。</summary>
        private static GameObject Block(Transform parent, string name, Vector3 min, Vector3 max, Material material, bool collider)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, PrimitiveType.Cube, (min + max) / 2f, max - min, material, collider);
            go.transform.SetParent(parent, true);
            go.isStatic = true;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, position, scale, material, false);
            go.transform.SetParent(parent, true);
            go.isStatic = true;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>親の中に置く部品(当たり判定なし、影なし)。</summary>
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localEuler,
            Vector3 scale, Material material)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, Vector3.zero, Vector3.one, material, false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void Local(GameObject go, Transform parent, Vector3 localPosition, Vector3 scale)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static LineRenderer Line(GameObject go, Material material, float width)
        {
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            return line;
        }

        private static AudioSource Source(GameObject owner, string childName, float minDistance, float maxDistance,
            GameObject target = null)
        {
            GameObject go = target;
            if (go == null)
            {
                go = new GameObject(childName);
                go.transform.SetParent(owner.transform, false);
            }

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        private static TextMeshPro Text3D(Transform parent, string name, string value, TMP_FontAsset font, Material material,
            Vector3 position, float height, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            if (material != null)
            {
                text.fontSharedMaterial = material;
            }

            text.text = value;
            // TextMeshPro はフォントサイズ 10 でおよそ高さ 1m
            text.fontSize = height * 10f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(height * 12f, height * 1.5f);
            return text;
        }

        /// <summary>日本語フォントの縁取り付きマテリアル(縁 #3A3550)。</summary>
        private static Material GetOrCreateOutlineMaterial(TMP_FontAsset font)
        {
            string path = $"{MaterialFolder}/A_TextOutline.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(font.material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.2f);
            material.SetColor("_OutlineColor", CommonSceneBuilder.Hex("#3A3550"));
            SceneBuildUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.wav");
            if (clip == null)
            {
                Debug.LogWarning($"[アーチェリー場] 音のファイルが見つかりません: {AudioFolder}/{name}.wav");
            }

            return clip;
        }

        private static AudioClip[] Clips(string prefix, int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                clips[i] = Clip($"{prefix}{i + 1}");
            }

            return clips;
        }

        private static void SetClips(SerializedProperty property, AudioClip[] clips)
        {
            property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
