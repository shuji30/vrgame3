using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRGame3.Common;
using VRGame3.Common.EditorTools;

namespace VRGame3.Hub.EditorTools
{
    /// <summary>
    /// 島のハブ(広場)の仮組みシーンを作るエディタ処理。docs/art-direction.md「M2 ハブ(島の広場)の仮組み」、
    /// ルールは docs/plan.md「島のハブ」。プリミティブで組み、正式なモデルに後で差し替える。
    /// メニュー「VRGame3/ハブを作る」。作ったシーンは Build Settings の先頭(起動して最初に入るシーン)にする。
    /// </summary>
    public static class HubBuilder
    {
        private const string SceneFolder = "Assets/_Project/Hub/Scenes";
        private const string ScenePath = SceneFolder + "/" + SceneTransition.HubScene + ".unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Hub";

        private const float PlazaRadius = 10f;
        private const float SignX = 2.6f;
        private static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);

        [MenuItem("VRGame3/ハブを作る")]
        public static void Build()
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("ハブ",
                    "TextMeshPro の基本素材がまだ入っていません。\n" +
                    "メニュー「Window > TextMeshPro > Import TMP Essential Resources」を実行してから、もう一度作ってください。",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("ハブ", $"{ScenePath} はすでにあります。作り直しますか?", "作り直す", "やめる"))
            {
                return;
            }

            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                EditorUtility.DisplayDialog("ハブ", $"日本語フォントが見つかりません({UIBuildUtility.FontPath})。", "OK");
                return;
            }

            SceneBuildUtility.EnsureFolder(SceneFolder);
            AssetDatabase.Refresh();
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

            var root = new GameObject("ハブ");
            BuildGround(root.transform);
            List<Transform> palms = BuildPalms(root.transform);
            BuildPathsAndGates(root.transform);
            BuildDistantVenues(root.transform);
            BuildAmbient(root.transform, palms);

            UIBuildUtility.EnsureEventSystem();
            BuildSportSign(root.transform, font, "ボウリングの看板", "ボウリング", SportIds.Bowling, "Bowling",
                new Vector3(-SignX, 1.65f, 5f), "#FF7F6B");
            BuildSportSign(root.transform, font, "アーチェリーの看板", "アーチェリー", SportIds.Archery, "Archery",
                new Vector3(SignX, 1.65f, 5f), "#6CC36A");

            Material arc = SceneBuildUtility.GetOrCreateArcMaterial(MaterialFolder, "H_TeleportArc");
            Material reticle = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "H_Reticle", "#3AA7FF", true);
            List<string> warnings = SceneBuildUtility.SetUpXROrigin(arc, reticle, Vector3.zero, Quaternion.identity,
                requireTeleportArea: true);

            // 会場から戻ってきたときは、その会場の看板の前に出す
            AddArrivalPoint(root.transform, "ボウリングから戻ったとき", "Bowling", new Vector3(-SignX, 0f, 2.2f));
            AddArrivalPoint(root.transform, "アーチェリーから戻ったとき", "Archery", new Vector3(SignX, 0f, 2.2f));

            AddWarning(warnings, CommonSceneBuilder.AddFader());
            AddWarning(warnings, CommonSceneBuilder.AddMenuAndSettings(root.transform, true, out SettingsPanel settings));
            if (settings != null)
            {
                BuildSettingsSign(root.transform, font, settings);
            }

            CommonSceneBuilder.EnsureSkillPointRules();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuildSettings(ScenePath, makeFirst: true);

            string message = $"{ScenePath} を作り、Build Settings の先頭にしました。";
            if (warnings.Count > 0)
            {
                message += "\n\n手で確認が必要な点:\n- " + string.Join("\n- ", warnings);
                foreach (string warning in warnings)
                {
                    Debug.LogWarning($"[ハブ] {warning}");
                }
            }

            Debug.Log($"[ハブ] {message}");
            EditorUtility.DisplayDialog("ハブ", message, "OK");
        }

        private static void AddWarning(List<string> warnings, string warning)
        {
            if (warning != null)
            {
                warnings.Add(warning);
            }
        }

        /// <summary>広場の床(全面テレポート可)、中央の印、縁の石、砂浜、海。</summary>
        private static void BuildGround(Transform root)
        {
            var parent = new GameObject("地面").transform;
            parent.SetParent(root, false);

            // Cylinder の標準の当たり判定はカプセルで床が丸く盛り上がるので、メッシュの当たり判定に替える
            GameObject floor = Shape(parent, "広場の床", PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 0f),
                new Vector3(PlazaRadius * 2f, 0.1f, PlazaRadius * 2f), Mat("H_Floor", "#FFF1CF"), false);
            floor.AddComponent<MeshCollider>();
            floor.AddComponent<TeleportArea>();

            Shape(parent, "中央の印", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(1.2f, 0.01f, 1.2f),
                Mat("H_Mark", "#F0C995"), false);

            // 縁の石:半径 10.3 に 30° おき。道の位置(X ±2.6 付近、奥側)にかかる石は抜く(30° おきの配置ではかからない)
            Material rock = Mat("H_Rock", "#B8B4C8");
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (PlazaRadius + 0.3f);
                if (position.z > 0f && Mathf.Abs(Mathf.Abs(position.x) - SignX) < 1.6f)
                {
                    continue;
                }

                GameObject stone = Shape(parent, $"縁の石{i + 1}", PrimitiveType.Sphere, position + new Vector3(0f, 0.1f, 0f),
                    new Vector3(0.8f, 0.4f, 0.6f), rock, false);
                stone.transform.rotation = Quaternion.Euler(0f, i * 30f, 0f);
            }

            // 砂浜(テレポート不可。当たり判定は残して物が落ちないようにする)
            GameObject sand = Shape(parent, "砂浜", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f),
                new Vector3(48f, 0.1f, 48f), Mat("H_Sand", "#F2D49B"), false);
            sand.AddComponent<MeshCollider>();

            Shape(parent, "海", PrimitiveType.Plane, new Vector3(0f, -0.4f, 0f), new Vector3(30f, 1f, 30f),
                Mat("H_Sea", "#2FB5B0"), false);
        }

        private static List<Transform> BuildPalms(Transform root)
        {
            var leaves = new List<Transform>();
            var parent = new GameObject("ヤシ").transform;
            parent.SetParent(root, false);
            Vector3[] positions =
            {
                new Vector3(-11f, 0f, -4f), new Vector3(-12f, 0f, 5f), new Vector3(11f, 0f, -3f),
                new Vector3(12f, 0f, 6f), new Vector3(-6f, 0f, -13f), new Vector3(7f, 0f, -12f),
            };
            for (int i = 0; i < positions.Length; i++)
            {
                Shape(parent, $"ヤシの幹{i + 1}", PrimitiveType.Cylinder, positions[i] + new Vector3(0f, 2f, 0f),
                    new Vector3(0.25f, 2f, 0.25f), Mat("H_Trunk", "#C98B55"), false);
                leaves.Add(Shape(parent, $"ヤシの葉{i + 1}", PrimitiveType.Sphere, positions[i] + new Vector3(0f, 4.1f, 0f),
                    new Vector3(2.2f, 0.5f, 2.2f), Mat("H_Leaf", "#6CC36A"), false).transform);
            }

            return leaves;
        }

        /// <summary>
        /// 環境音と BGM(docs/art-direction.md 8.1・8.3):波は砂浜の縁の3か所から 3D、鳥はヤシから、風と BGM は 2D で小さく。
        /// </summary>
        private static void BuildAmbient(Transform root, List<Transform> birdPoints)
        {
            var parent = new GameObject("環境音と BGM").transform;
            parent.SetParent(root, false);

            Vector3[] waves = { new Vector3(0f, 0f, 20f), new Vector3(-14f, 0f, -14f), new Vector3(14f, 0f, -14f) };
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = new GameObject($"波{i + 1}");
                wave.transform.SetParent(parent, false);
                wave.transform.position = waves[i];
                AudioSource source = Source(wave, true, 5f, 60f);
                source.clip = Clip("AMB_Wave_Loop");
                source.loop = true;
                source.playOnAwake = true;
                source.volume = 0.06f;
                source.pitch = 0.95f + 0.05f * i;
            }

            var birds = new GameObject("鳥");
            birds.transform.SetParent(parent, false);
            Source(birds, true, 1f, 30f);
            var oneShot = birds.AddComponent<RandomOneShot>();
            var so = new SerializedObject(oneShot);
            SerializedProperty clips = so.FindProperty("clips");
            clips.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                clips.GetArrayElementAtIndex(i).objectReferenceValue = Clip($"AMB_Bird_{i + 1}");
            }

            SerializedProperty points = so.FindProperty("points");
            points.arraySize = birdPoints.Count;
            for (int i = 0; i < birdPoints.Count; i++)
            {
                points.GetArrayElementAtIndex(i).objectReferenceValue = birdPoints[i];
            }

            so.FindProperty("volume").floatValue = 0.08f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var wind = new GameObject("風");
            wind.transform.SetParent(parent, false);
            AudioSource windSource = Source(wind, false, 1f, 30f);
            windSource.clip = Clip("AMB_Wind_Loop");
            windSource.loop = true;
            windSource.playOnAwake = true;
            windSource.volume = 0.04f;

            var bgm = new GameObject("BGM");
            bgm.transform.SetParent(parent, false);
            AudioSource bgmSource = Source(bgm, false, 1f, 30f);
            bgmSource.clip = Clip("BGM_Hub_Loop");
            bgmSource.loop = true;
            bgmSource.playOnAwake = true;
            // 8.5 の目安 -22dB
            bgmSource.volume = 0.08f;
        }

        private static AudioSource Source(GameObject go, bool spatial, float minDistance, float maxDistance)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Art/Audio/Placeholder/{name}.wav");
            if (clip == null)
            {
                Debug.LogWarning($"[ハブ] 音のファイルが見つかりません: {name}.wav");
            }

            return clip;
        }

        /// <summary>会場への道と入口の門(景色だけ。中は歩けない)。</summary>
        private static void BuildPathsAndGates(Transform root)
        {
            var parent = new GameObject("道と門").transform;
            parent.SetParent(root, false);
            Material path = Mat("H_Path", "#C9A26B");
            Material wood = Mat("H_WoodDark", "#8A5A3B");
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * SignX;
                string label = side < 0 ? "ボウリング" : "アーチェリー";
                Shape(parent, $"道({label})", PrimitiveType.Cube, new Vector3(x, -0.055f, 13.5f), new Vector3(1.6f, 0.05f, 8f),
                    path, false);
                for (int post = -1; post <= 1; post += 2)
                {
                    Shape(parent, $"門の柱({label})", PrimitiveType.Cylinder, new Vector3(x + post * 0.9f, 1.3f, 9.8f),
                        new Vector3(0.15f, 1.3f, 0.15f), wood, false);
                }

                Shape(parent, $"門の横木({label})", PrimitiveType.Cube, new Vector3(x, 2.6f, 9.8f),
                    new Vector3(2f, 0.15f, 0.15f), wood, false);
                Shape(parent, $"門の布({label})", PrimitiveType.Cube, new Vector3(x, 2.35f, 9.8f),
                    new Vector3(1.6f, 0.3f, 0.03f), side < 0 ? Mat("H_Coral", "#FF7F6B") : Mat("H_Leaf", "#6CC36A"), false);
            }
        }

        /// <summary>道の先の会場の遠景(ボウリング小屋、アーチェリー場の木と的)。</summary>
        private static void BuildDistantVenues(Transform root)
        {
            var parent = new GameObject("会場の遠景").transform;
            parent.SetParent(root, false);

            Vector3 hut = new Vector3(-SignX, 0f, 20.5f);
            Shape(parent, "小屋の壁", PrimitiveType.Cube, hut + new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 6f),
                Mat("H_WoodLight", "#F0C995"), false);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject roof = Shape(parent, "小屋の屋根", PrimitiveType.Cube,
                    hut + new Vector3(side * 1.05f, 2.75f, 0f), new Vector3(2.4f, 0.12f, 6.4f), Mat("H_Floor", "#FFF1CF"), false);
                roof.transform.rotation = Quaternion.Euler(0f, 0f, -side * 20f);
            }

            Shape(parent, "小屋の日よけ", PrimitiveType.Cube, hut + new Vector3(0f, 2.1f, -3.4f), new Vector3(2f, 0.06f, 0.8f),
                Mat("H_Coral", "#FF7F6B"), false);

            Vector3 range = new Vector3(SignX, 0f, 20f);
            Vector3[] trees =
            {
                new Vector3(-2.5f, 0f, -2f), new Vector3(2.5f, 0f, -1f), new Vector3(-2.8f, 0f, 2f),
                new Vector3(2.8f, 0f, 3f), new Vector3(0f, 0f, 5f),
            };
            for (int i = 0; i < trees.Length; i++)
            {
                Shape(parent, $"木の幹{i + 1}", PrimitiveType.Cylinder, range + trees[i] + new Vector3(0f, 2.5f, 0f),
                    new Vector3(0.35f, 2.5f, 0.35f), Mat("H_Trunk", "#C98B55"), false);
                Shape(parent, $"木の葉{i + 1}", PrimitiveType.Sphere, range + trees[i] + new Vector3(0f, 5.5f, 0f),
                    new Vector3(3.5f, 3f, 3.5f), i % 2 == 0 ? Mat("H_LeafDark", "#2F8A5B") : Mat("H_Leaf", "#6CC36A"), false);
            }

            Shape(parent, "的の台", PrimitiveType.Cube, range + new Vector3(0f, 0.9f, 1f), new Vector3(0.8f, 0.8f, 0.2f),
                Mat("H_Sand", "#F2D49B"), false);
            GameObject bull = Shape(parent, "的の中心", PrimitiveType.Cylinder, range + new Vector3(0f, 0.9f, 0.88f),
                new Vector3(0.4f, 0.01f, 0.4f), Mat("H_Yellow", "#FFD24A"), false);
            bull.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>種目の看板:柱2本・板・種目の色の帯と、種目名・ランク・ポイント・ベスト・「あそぶ」の Canvas。</summary>
        private static void BuildSportSign(Transform root, TMP_FontAsset font, string name, string title, string sportId,
            string sceneName, Vector3 center, string bandHex)
        {
            var sign = new GameObject(name);
            sign.transform.SetParent(root, false);
            sign.transform.position = center;
            Vector3 toEye = Eye - center;
            toEye.y = 0f;
            // 看板の表(-Z)を目の方へ向ける
            sign.transform.rotation = Quaternion.LookRotation(-toEye.normalized, Vector3.up);

            Material wood = Mat("H_Wood", "#C98B55");
            Material post = Mat("H_WoodDark", "#8A5A3B");
            for (int side = -1; side <= 1; side += 2)
            {
                LocalShape(sign.transform, "柱", PrimitiveType.Cylinder, new Vector3(side * 1.0f, -center.y + 1.3f, 0.06f),
                    new Vector3(0.12f, 1.3f, 0.12f), post);
            }

            LocalShape(sign.transform, "板", PrimitiveType.Cube, Vector3.zero, new Vector3(2.3f, 1.8f, 0.08f), wood);
            LocalShape(sign.transform, "帯", PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), new Vector3(2.3f, 0.15f, 0.09f),
                Mat(bandHex == "#FF7F6B" ? "H_Coral" : "H_Leaf", bandHex));

            GameObject canvas = UIBuildUtility.CreateCanvas(sign.transform, "文字", new Vector2(2200f, 1700f),
                UIBuildUtility.Background);
            // 帯(前面 z -0.045)と同じ面にするとちらつくので、少し手前に出す
            canvas.transform.localPosition = new Vector3(0f, 0f, -0.047f);
            canvas.transform.localRotation = Quaternion.identity;
            Transform c = canvas.transform;

            var component = sign.AddComponent<SportSign>();
            UIBuildUtility.Label(c, "種目名", title, font, 240, new Vector2(0f, 640f), new Vector2(2100f, 280f),
                UIBuildUtility.TextLight);
            RankMedal medal = CommonSceneBuilder.AddRankMedal(c, font, new Vector2(-500f, 360f), 200f, 200f);
            TMP_Text points = UIBuildUtility.Label(c, "ポイント", "ポイント 0", font, 190, new Vector2(0f, 130f),
                new Vector2(2100f, 230f), UIBuildUtility.TextLight);
            TMP_Text best = UIBuildUtility.Label(c, "ベスト", "ベスト —", font, 190, new Vector2(0f, -100f),
                new Vector2(2100f, 230f), UIBuildUtility.TextLight);
            (Image playImage, TMP_Text playLabel) = UIBuildUtility.PrimaryButton(c, "あそぶ", "あそぶ", font, 220,
                new Vector2(0f, -520f), new Vector2(900f, 300f), component.Play);

            var so = new SerializedObject(component);
            so.FindProperty("sportId").stringValue = sportId;
            so.FindProperty("sceneName").stringValue = sceneName;
            so.FindProperty("medal").objectReferenceValue = medal;
            so.FindProperty("pointsText").objectReferenceValue = points;
            so.FindProperty("bestText").objectReferenceValue = best;
            so.FindProperty("playButton").objectReferenceValue = playImage.GetComponent<Button>();
            so.FindProperty("playLabel").objectReferenceValue = playLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>「せってい」看板。押すと共通の設定パネルを頭の前に出す。</summary>
        private static void BuildSettingsSign(Transform root, TMP_FontAsset font, SettingsPanel settings)
        {
            Vector3 center = new Vector3(0f, 1.2f, 6f);
            var sign = new GameObject("せってい看板");
            sign.transform.SetParent(root, false);
            sign.transform.position = center;
            sign.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            LocalShape(sign.transform, "柱", PrimitiveType.Cylinder, new Vector3(0f, -center.y + 0.75f, 0.05f),
                new Vector3(0.1f, 0.75f, 0.1f), Mat("H_WoodDark", "#8A5A3B"));
            LocalShape(sign.transform, "板", PrimitiveType.Cube, Vector3.zero, new Vector3(1.3f, 0.6f, 0.06f),
                Mat("H_Wood", "#C98B55"));

            GameObject canvas = UIBuildUtility.CreateCanvas(sign.transform, "文字", new Vector2(1200f, 500f), Color.clear);
            canvas.transform.localPosition = new Vector3(0f, 0f, -0.035f);
            canvas.transform.localRotation = Quaternion.identity;
            UIBuildUtility.NormalButton(canvas.transform, "せってい", "せってい", font, 200, Vector2.zero,
                new Vector2(1100f, 400f), settings.Open);
        }

        private static void AddArrivalPoint(Transform root, string name, string fromScene, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            var point = go.AddComponent<ArrivalPoint>();
            var so = new SerializedObject(point);
            so.FindProperty("fromScene").stringValue = fromScene;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Mat(string name, string hex)
        {
            return SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, name, hex, false);
        }

        /// <summary>動かない形。Static にし、影は落とさない(ハブには動く物がない)。</summary>
        private static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, bool keepCollider)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, position, scale, material, keepCollider);
            go.transform.SetParent(parent, true);
            go.isStatic = true;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>親の向きに合わせて置く形(看板の部品)。当たり判定は付けない。</summary>
        private static GameObject LocalShape(Transform parent, string name, PrimitiveType type, Vector3 localPosition,
            Vector3 scale, Material material)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, Vector3.zero, scale, material, false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.isStatic = true;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
