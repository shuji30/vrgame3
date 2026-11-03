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

namespace VRGame3.Sports.TableTennis.EditorTools
{
    /// <summary>
    /// 卓球場の仮組みシーンを作るエディタ処理。docs/art-direction.md「M3 卓球場の仮組み」「M3 ラケットとボール」
    /// 「M3 CPU の体」、ルールは docs/plan.md「卓球」。メニュー「VRGame3/卓球場を作る」。
    /// 座標:台の中心の真下の床が原点、プレイヤー側が -Z、ネットが Z=0。
    /// </summary>
    public static class TableTennisCourtBuilder
    {
        public const string SceneName = "TableTennis";
        private const string SceneFolder = "Assets/_Project/Sports/TableTennis/Scenes";
        private const string ScenePath = SceneFolder + "/" + SceneName + ".unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/TableTennis";
        private const string AudioFolder = "Assets/_Project/Art/Audio/Placeholder";

        private const float TableHeight = 0.76f;
        private const float TableLength = 2.74f;
        private const float TableWidth = 1.525f;
        private static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, -2.07f);
        private static readonly Vector3 Eye = new Vector3(0f, 1.6f, -2.07f);

        [MenuItem("VRGame3/卓球場を作る")]
        public static void Build()
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("卓球場",
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
                !EditorUtility.DisplayDialog("卓球場", $"{ScenePath} はすでにあります。作り直しますか?", "作り直す", "やめる"))
            {
                return;
            }

            TMP_FontAsset font = UIBuildUtility.GetOrCreateFontAsset();
            if (font == null)
            {
                EditorUtility.DisplayDialog("卓球場", $"日本語フォントが見つかりません({UIBuildUtility.FontPath})。", "OK");
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

            var root = new GameObject("卓球場");
            Renderer mat = BuildGround(root.transform);
            BuildScenery(root.transform);
            Transform table = BuildTable(root.transform, out List<Transform> legs, out List<Transform> crossbars);

            var warnings = new List<string>();
            Material arc = SceneBuildUtility.GetOrCreateArcMaterial(MaterialFolder, "T_TeleportArc");
            Material reticle = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "T_Reticle", "#3AA7FF", true);
            warnings.AddRange(SceneBuildUtility.SetUpXROrigin(arc, reticle, PlayerPosition, Quaternion.identity,
                requireTeleportArea: true));

            Racket racket = BuildRacket(root.transform);
            PingPongBall ball = BuildBall(root.transform, racket);
            TTCpuPlayer cpu = BuildCpu(root.transform, ball, mat, font);
            TTScoreBoard scoreBoard = BuildScoreBoard(root.transform, font);
            GameObject serveGuide = BuildServeGuide(root.transform, font);
            ResultPanel resultPanel = CommonSceneBuilder.AddResultPanel(root.transform, new Vector3(0f, 1.55f, 1.0f), Eye);

            var game = root.AddComponent<TableTennisGame>();
            var so = new SerializedObject(game);
            so.FindProperty("ball").objectReferenceValue = ball;
            so.FindProperty("racket").objectReferenceValue = racket;
            so.FindProperty("cpu").objectReferenceValue = cpu;
            so.FindProperty("scoreBoard").objectReferenceValue = scoreBoard;
            so.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            so.FindProperty("table").objectReferenceValue = table;
            SetArray(so.FindProperty("tableLegs"), legs.Cast<Object>().ToArray());
            SetArray(so.FindProperty("tableCrossbars"), crossbars.Cast<Object>().ToArray());
            so.FindProperty("serveGuide").objectReferenceValue = serveGuide;
            SetControllers(so, warnings);
            so.ApplyModifiedPropertiesWithoutUndo();

            ChoicePanel choicePanel = CommonSceneBuilder.AddChoicePanel(root.transform, new Vector3(0f, 1.4f, -0.95f), Eye);
            GameObject aimTargets = BuildAimTargets(table);
            BuildStartPanel(root.transform, font, game, choicePanel, aimTargets);
            // 卓球場は狭いので、生け垣の上(開始位置から約 7.6m)から 80個だけ降らせる
            CommonSceneBuilder.AddCelebration(root.transform, new Vector3(0f, 2.6f, 5.4f), 80, resultPanel, choicePanel);
            BuildFeedback(root.transform, game);
            BuildAmbient(root.transform);

            AddWarning(warnings, CommonSceneBuilder.AddFader());
            AddWarning(warnings, CommonSceneBuilder.AddMenuAndSettings(root.transform, false, out _));
            CommonSceneBuilder.EnsureSkillPointRules();
            TimeOfDayApplier time = Object.FindAnyObjectByType<TimeOfDayApplier>();
            if (time != null)
            {
                TimeOfDayPresets.AdjustForCourt(time);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuildSettings(ScenePath);

            string message = $"{ScenePath} を作りました。";
            if (warnings.Count > 0)
            {
                message += "\n\n手で確認が必要な点:\n- " + string.Join("\n- ", warnings);
                foreach (string warning in warnings)
                {
                    Debug.LogWarning($"[卓球場] {warning}");
                }
            }

            Debug.Log($"[卓球場] {message}");
            EditorUtility.DisplayDialog("卓球場", message, "OK");
        }

        private static void AddWarning(List<string> warnings, string warning)
        {
            if (warning != null)
            {
                warnings.Add(warning);
            }
        }

        // ---------------------------------------------------------------- 床と景色

        /// <summary>床・テレポートできる範囲・CPU の敷物・砂地・海。CPU の敷物(強さの色に塗る)を返す。</summary>
        private static Renderer BuildGround(Transform root)
        {
            var parent = new GameObject("床と範囲").transform;
            parent.SetParent(root, false);
            Block(parent, "床", new Vector3(-4.5f, -0.1f, -5f), new Vector3(4.5f, 0f, 5.5f), Mat("T_Wood", "#C98B55"), true, true);
            GameObject area = Block(parent, "テレポートできる範囲", new Vector3(-1.5f, -0.01f, -3.87f), new Vector3(1.5f, 0.01f, -1.67f),
                Mat("T_WoodLight", "#F0C995"), true, true);
            area.AddComponent<TeleportArea>();
            GameObject mat = Block(parent, "CPU の敷物", new Vector3(-2f, 0f, 1.7f), new Vector3(2f, 0.005f, 3f),
                Mat("T_White", "#FFFDF7"), false, false);
            Block(parent, "砂地", new Vector3(-20f, -0.55f, -20f), new Vector3(20f, -0.05f, 20f), Mat("T_Sand", "#F2D49B"), true, true);
            GameObject sea = SceneBuildUtility.CreatePrimitive("海", PrimitiveType.Plane, new Vector3(0f, -0.4f, 0f),
                new Vector3(30f, 1f, 30f), Mat("T_Sea", "#2FB5B0"), false);
            Static(sea, parent);
            return mat.GetComponent<Renderer>();
        }

        private static void BuildScenery(Transform root)
        {
            var parent = new GameObject("景色").transform;
            parent.SetParent(root, false);
            Material dark = Mat("T_LeafDark", "#2F8A5B");
            Block(parent, "仕切り", new Vector3(-3.5f, 0f, 4.575f), new Vector3(3.5f, 0.7f, 4.625f), Mat("T_Ink", "#4A4660"), true, true);
            Block(parent, "生け垣", new Vector3(-4.5f, 0f, 5f), new Vector3(4.5f, 2f, 5.8f), dark, true, true);
            Vector3[] palms =
            {
                new Vector3(-4f, 0f, -4.2f), new Vector3(4.2f, 0f, -4f), new Vector3(-4.6f, 0f, 1.2f),
                new Vector3(4.6f, 0f, 0.6f), new Vector3(-3f, 0f, 7f), new Vector3(3.5f, 0f, 7.5f),
            };
            for (int i = 0; i < palms.Length; i++)
            {
                Shape(parent, $"ヤシの幹{i + 1}", PrimitiveType.Cylinder, palms[i] + new Vector3(0f, 2f, 0f), new Vector3(0.25f, 2f, 0.25f),
                    Mat("T_Trunk", "#C98B55"));
                Shape(parent, $"ヤシの葉{i + 1}", PrimitiveType.Sphere, palms[i] + new Vector3(0f, 4.1f, 0f), new Vector3(2.6f, 0.5f, 2.6f),
                    i % 2 == 0 ? Mat("T_Leaf", "#6CC36A") : dark);
            }
        }

        // ---------------------------------------------------------------- 台とネット

        /// <summary>台のまとまり。親の位置が台の上面の中心(身長で高さが変わるので Static にしない)。</summary>
        private static Transform BuildTable(Transform root, out List<Transform> legs, out List<Transform> crossbars)
        {
            var table = new GameObject("台");
            table.transform.SetParent(root, false);
            table.transform.position = new Vector3(0f, TableHeight, 0f);
            Transform t = table.transform;

            Material top = Mat("T_TableTop", "#1A7A8C");
            Material white = Mat("T_White", "#FFFDF7");
            Material wood = Mat("T_WoodDark", "#8A5A3B");
            Material ink = Mat("T_Ink", "#4A4660");

            // 天板。当たりは計算で取るが、テレポートの線が台を突き抜けないようにコライダーを残す
            GameObject board = Part(t, "天板", PrimitiveType.Cube, new Vector3(0f, -0.015f, 0f), new Vector3(TableWidth, 0.03f, TableLength), top);
            board.AddComponent<BoxCollider>();
            Part(t, "横の線(左)", PrimitiveType.Cube, new Vector3(-0.7525f, 0.001f, 0f), new Vector3(0.02f, 0.002f, TableLength), white);
            Part(t, "横の線(右)", PrimitiveType.Cube, new Vector3(0.7525f, 0.001f, 0f), new Vector3(0.02f, 0.002f, TableLength), white);
            Part(t, "端の線(手前)", PrimitiveType.Cube, new Vector3(0f, 0.001f, -1.36f), new Vector3(TableWidth, 0.002f, 0.02f), white);
            Part(t, "端の線(奥)", PrimitiveType.Cube, new Vector3(0f, 0.001f, 1.36f), new Vector3(TableWidth, 0.002f, 0.02f), white);
            Part(t, "中央線", PrimitiveType.Cube, new Vector3(0f, 0.001f, 0f), new Vector3(0.006f, 0.002f, TableLength), white);

            legs = new List<Transform>();
            foreach (float x in new[] { -0.65f, 0.65f })
            {
                foreach (float z in new[] { -1.15f, 1.15f })
                {
                    legs.Add(Part(t, "脚", PrimitiveType.Cube, new Vector3(x, -(TableHeight + 0.03f) / 2f, z),
                        new Vector3(0.06f, TableHeight - 0.03f, 0.06f), wood).transform);
                }
            }

            crossbars = new List<Transform>();
            foreach (float z in new[] { -1.15f, 1.15f })
            {
                crossbars.Add(Part(t, "脚の横木", PrimitiveType.Cube, new Vector3(0f, 0.15f - TableHeight, z), new Vector3(1.36f, 0.05f, 0.05f),
                    wood).transform);
            }

            Part(t, "ネット", PrimitiveType.Cube, new Vector3(0f, 0.076f, 0f), new Vector3(1.83f, 0.1525f, 0.008f), ink);
            Part(t, "ネットの白帯", PrimitiveType.Cube, new Vector3(0f, 0.1525f - 0.0075f, 0f), new Vector3(1.83f, 0.015f, 0.012f), white);
            foreach (float x in new[] { -0.93f, 0.93f })
            {
                Part(t, "ネットの支柱", PrimitiveType.Cube, new Vector3(x, 0.09f, 0f), new Vector3(0.03f, 0.18f, 0.05f), wood);
                Part(t, "留め具", PrimitiveType.Cube, new Vector3(x * 0.968f, -0.015f, 0f), new Vector3(0.05f, 0.03f, 0.08f), wood);
            }

            return t;
        }

        // ---------------------------------------------------------------- ラケットとボール

        /// <summary>プレイヤーのラケット。原点がコントローラーの付く点、+Z が指す向き、面の向きは ±X。</summary>
        private static Racket BuildRacket(Transform root)
        {
            var racketObject = new GameObject("ラケット");
            racketObject.transform.SetParent(root, false);
            racketObject.transform.position = new Vector3(0.3f, 1f, -1.8f);
            BuildRacketVisual(racketObject.transform, Mat("T_RubberRed", "#E0525E"));

            // 当たりの計算に使う面(この物の Z が面の向き。見た目の面は ±X)
            var face = new GameObject("面");
            face.transform.SetParent(racketObject.transform, false);
            face.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            face.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var racket = racketObject.AddComponent<Racket>();
            var so = new SerializedObject(racket);
            so.FindProperty("face").objectReferenceValue = face.transform;
            so.FindProperty("holdPosition").vector3Value = Vector3.zero;
            so.ApplyModifiedPropertiesWithoutUndo();
            return racket;
        }

        /// <summary>ラケットの見た目(芯・表裏のラバー・柄・柄の飾り)。表のラバーを返す。</summary>
        private static Renderer BuildRacketVisual(Transform parent, Material frontRubber)
        {
            Part(parent, "芯", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.12f), new Vector3(0f, 0f, 90f), new Vector3(0.155f, 0.003f, 0.155f),
                Mat("T_WoodLight", "#F0C995"));
            GameObject front = Part(parent, "ラバー(表)", PrimitiveType.Cylinder, new Vector3(0.0045f, 0f, 0.12f), new Vector3(0f, 0f, 90f),
                new Vector3(0.15f, 0.0015f, 0.15f), frontRubber);
            Part(parent, "ラバー(裏)", PrimitiveType.Cylinder, new Vector3(-0.0045f, 0f, 0.12f), new Vector3(0f, 0f, 90f),
                new Vector3(0.15f, 0.0015f, 0.15f), Mat("T_Ink", "#4A4660"));
            Part(parent, "柄", PrimitiveType.Cube, new Vector3(0f, 0f, 0.012f), Vector3.zero, new Vector3(0.026f, 0.03f, 0.07f),
                Mat("T_Wood", "#C98B55"));
            Part(parent, "柄の飾り", PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f), Vector3.zero, new Vector3(0.03f, 0.034f, 0.01f),
                Mat("T_Yellow", "#FFD24A"));
            return front.GetComponent<Renderer>();
        }

        private static PingPongBall BuildBall(Transform root, Racket racket)
        {
            // ボールのマテリアルは他と共有しない(見えにくければ色だけ替えられるように)
            Material material = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "T_Ball", "#FFFDF7", false);
            material.SetFloat("_Smoothness", 0.3f);
            GameObject ballObject = SceneBuildUtility.CreatePrimitive("ボール", PrimitiveType.Sphere, new Vector3(0f, 1.2f, -1f),
                Vector3.one * 0.04f, material, false);
            ballObject.transform.SetParent(root, true);
            ballObject.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            var ball = ballObject.AddComponent<PingPongBall>();
            var so = new SerializedObject(ball);
            so.FindProperty("racket").objectReferenceValue = racket;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ball;
        }

        // ---------------------------------------------------------------- CPU の仮人形

        /// <summary>こけし風の仮人形(身長 約 1.5m)。体の原点は足元、プレイヤーの方(-Z)を向く。</summary>
        private static TTCpuPlayer BuildCpu(Transform root, PingPongBall ball, Renderer mat, TMP_FontAsset font)
        {
            CpuDollBuilder.Parts doll = CpuDollBuilder.Build(root, "CPU", new Vector3(0f, 0f, 1.92f), Quaternion.Euler(0f, 180f, 0f), font);
            GameObject cpuObject = doll.Body.gameObject;
            Transform body = doll.Body;

            // CPU のラケット(ラバーの表は強さの色)
            var cpuRacket = new GameObject("CPU のラケット");
            cpuRacket.transform.SetParent(doll.RightHand, false);
            cpuRacket.transform.localScale = Vector3.one / 0.11f;
            // 柄の軸のまわりに 90° 回してから傾け、面をプレイヤーの方へ向ける
            cpuRacket.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f) * Quaternion.Euler(0f, 0f, 90f);
            Renderer rubber = BuildRacketVisual(cpuRacket.transform, Mat("T_White", "#FFFDF7"));

            var cpu = cpuObject.AddComponent<TTCpuPlayer>();
            var so = new SerializedObject(cpu);
            so.FindProperty("ball").objectReferenceValue = ball;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("racketHand").objectReferenceValue = doll.RightHand;
            so.FindProperty("tossHand").objectReferenceValue = doll.LeftHand;
            SetArray(so.FindProperty("levelColorRenderers"), new Object[] { mat, rubber });
            so.FindProperty("nameTagBack").objectReferenceValue = doll.NameTagBack;
            so.FindProperty("nameTagText").objectReferenceValue = doll.NameTagText;
            so.FindProperty("head").objectReferenceValue = doll.Head;
            SetArray(so.FindProperty("eyes"), doll.Eyes.Cast<Object>().ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            return cpu;
        }

        // ---------------------------------------------------------------- 表示

        private static TTScoreBoard BuildScoreBoard(Transform root, TMP_FontAsset font)
        {
            var holder = new GameObject("スコア板");
            holder.transform.SetParent(root, false);
            Vector3 center = new Vector3(2.8f, 1.9f, 3f);
            Vector3 toEye = center - Eye;
            toEye.y = 0f;
            holder.transform.SetPositionAndRotation(center, Quaternion.LookRotation(toEye.normalized, Vector3.up));

            GameObject frame = Part(holder.transform, "枠", PrimitiveType.Cube, new Vector3(0f, 0f, 0.035f), Vector3.zero,
                new Vector3(2.2f, 1.3f, 0.06f), Mat("T_Wood", "#C98B55"));
            frame.isStatic = true;
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                // 地面から枠の下端(中心から 0.65 下)まで。Cylinder の高さは scale.y の2倍
                Part(holder.transform, "柱", PrimitiveType.Cylinder, new Vector3(x, -(center.y + 0.65f) / 2f, 0.07f), Vector3.zero,
                    new Vector3(0.1f, (center.y - 0.65f) / 2f, 0.1f), Mat("T_WoodDark", "#8A5A3B"));
            }

            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "文字", new Vector2(2000f, 1100f), UIBuildUtility.Background);
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            Transform c = canvas.transform;
            Color light = UIBuildUtility.TextLight;

            TMP_Text level = UIBuildUtility.Label(c, "CPU の強さ", "CPU  ふつう ▲▲", font, 190, new Vector2(0f, 400f), new Vector2(1900f, 220f), light);
            UIBuildUtility.Label(c, "あなた", "あなた", font, 190, new Vector2(-700f, 80f), new Vector2(500f, 230f), light);
            TMP_Text player = UIBuildUtility.Label(c, "自分の点", "0", font, 320, new Vector2(-230f, 80f), new Vector2(400f, 360f), light);
            UIBuildUtility.Label(c, "あいだ", "-", font, 200, new Vector2(0f, 80f), new Vector2(120f, 230f), light);
            TMP_Text cpu = UIBuildUtility.Label(c, "CPU の点", "0", font, 320, new Vector2(230f, 80f), new Vector2(400f, 360f), light);
            UIBuildUtility.Label(c, "CPU", "CPU", font, 190, new Vector2(700f, 80f), new Vector2(500f, 230f), light);
            TMP_Text playerServe = UIBuildUtility.Label(c, "あなたのサーブ", "● サーブ", font, 190, new Vector2(-470f, -200f),
                new Vector2(800f, 230f), light);
            TMP_Text cpuServe = UIBuildUtility.Label(c, "CPU のサーブ", "● サーブ", font, 190, new Vector2(470f, -200f),
                new Vector2(800f, 230f), light);
            TMP_Text reason = UIBuildUtility.Label(c, "決着の理由", "アウト", font, 200, new Vector2(0f, -420f), new Vector2(1900f, 240f),
                UIBuildUtility.PrimaryColor);
            reason.gameObject.SetActive(false);

            var board = holder.AddComponent<TTScoreBoard>();
            var so = new SerializedObject(board);
            so.FindProperty("playerPoints").objectReferenceValue = player;
            so.FindProperty("cpuPoints").objectReferenceValue = cpu;
            so.FindProperty("playerServeMark").objectReferenceValue = playerServe.gameObject;
            so.FindProperty("cpuServeMark").objectReferenceValue = cpuServe.gameObject;
            so.FindProperty("levelText").objectReferenceValue = level;
            so.FindProperty("reasonText").objectReferenceValue = reason;
            so.ApplyModifiedPropertiesWithoutUndo();
            return board;
        }

        /// <summary>サーブの案内(ネットの上)。</summary>
        private static GameObject BuildServeGuide(Transform root, TMP_FontAsset font)
        {
            Vector3 position = new Vector3(0f, 1.22f, 0f);
            GameObject canvas = UIBuildUtility.CreateCanvas(root, "サーブの案内", new Vector2(1300f, 240f), UIBuildUtility.Background);
            canvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - Eye, Vector3.up));
            canvas.transform.localScale = Vector3.one * UIBuildUtility.PixelToMeter;
            UIBuildUtility.Label(canvas.transform, "見出し", "あなたのサーブ", font, 80, new Vector2(0f, 50f), new Vector2(1250f, 100f),
                UIBuildUtility.TextLight);
            UIBuildUtility.Label(canvas.transform, "説明", "ボールの手のトリガーでボールを出す", font, 70, new Vector2(0f, -55f),
                new Vector2(1250f, 90f), UIBuildUtility.TextLight);
            canvas.SetActive(false);
            return canvas;
        }

        /// <summary>チャレンジ「ねらい打ち」の円(CPU 側のコートの左右の隅、直径 0.5m)。台の子にして高さに付いて行く。</summary>
        private static GameObject BuildAimTargets(Transform table)
        {
            var group = new GameObject("ねらい打ちの円");
            group.transform.SetParent(table, false);
            float x = TableWidth / 2f - 0.3f;
            float z = TableLength / 2f - 0.3f;
            foreach (float side in new[] { -1f, 1f })
            {
                Part(group.transform, "円の縁", PrimitiveType.Cylinder, new Vector3(side * x, 0.002f, z), new Vector3(0.53f, 0.001f, 0.53f),
                    Mat("T_Orange", "#FF8A3D"));
                Part(group.transform, "円", PrimitiveType.Cylinder, new Vector3(side * x, 0.003f, z), new Vector3(0.5f, 0.001f, 0.5f),
                    Mat("T_Yellow", "#FFD24A"));
            }

            group.SetActive(false);
            return group.gameObject;
        }

        /// <summary>「はじめに」パネル(docs/art-direction.md 6.10 の卓球の行。1000 × 940)。</summary>
        private static void BuildStartPanel(Transform root, TMP_FontAsset font, TableTennisGame game, ChoicePanel choicePanel,
            GameObject aimTargets)
        {
            UIBuildUtility.EnsureEventSystem();
            var holder = new GameObject("はじめに");
            holder.transform.SetParent(root, false);
            var panel = holder.AddComponent<TTStartPanel>();

            Vector3 position = new Vector3(0f, 1.4f, -0.95f);
            GameObject canvas = UIBuildUtility.CreateCanvas(holder.transform, "はじめにパネル", new Vector2(1000f, 940f), UIBuildUtility.Background);
            canvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - Eye, Vector3.up));
            Transform c = canvas.transform;
            Color light = UIBuildUtility.TextLight;

            TMP_Text title = UIBuildUtility.Label(c, "見出し", "はじめに", font, 52, new Vector2(0f, 410f), new Vector2(960f, 70f), light);
            UIBuildUtility.Label(c, "案内1", "まわりに人や物がないか確かめてください", font, 30, new Vector2(0f, 340f), new Vector2(960f, 44f), light);
            UIBuildUtility.Label(c, "案内2", "コントローラーのストラップを手首に通してください", font, 30, new Vector2(0f, 293f), new Vector2(960f, 44f), light);
            UIBuildUtility.Label(c, "案内3", "台はほんものではありません。手をついたり、もたれたりしないでください", font, 26,
                new Vector2(0f, 246f), new Vector2(960f, 44f), UIBuildUtility.PrimaryColor);

            var images = new Image[7];
            var texts = new TMP_Text[7];

            void Choice(TTStartPanel.Choice choice, string label, float x, float y, Vector2 size, float fontSize,
                UnityEngine.Events.UnityAction action)
            {
                (Image image, TMP_Text text) = UIBuildUtility.NormalButton(c, choice.ToString(), label, font, fontSize, new Vector2(x, y), size, action);
                images[(int)choice] = image;
                texts[(int)choice] = text;
            }

            Vector2 wide = new Vector2(220f, 76f);

            UIBuildUtility.Label(c, "補助の見出し", "入りやすさの補助", font, 28, new Vector2(-360f, -60f), new Vector2(260f, 60f), light);
            Choice(TTStartPanel.Choice.AssistOn, "オン", -110f, -60f, wide, 34, panel.AssistOn);
            Choice(TTStartPanel.Choice.AssistOff, "オフ", 125f, -60f, wide, 34, panel.AssistOff);

            UIBuildUtility.Label(c, "身長の見出し", "身長", font, 36, new Vector2(-360f, -165f), new Vector2(260f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を下げる", "-", font, 48, new Vector2(-110f, -165f), new Vector2(100f, 76f), panel.DecreaseHeight);
            TMP_Text height = UIBuildUtility.Label(c, "身長の数字", "170 cm", font, 40, new Vector2(70f, -165f), new Vector2(200f, 60f), light);
            UIBuildUtility.NormalButton(c, "身長を上げる", "+", font, 48, new Vector2(250f, -165f), new Vector2(100f, 76f), panel.IncreaseHeight);

            UIBuildUtility.Label(c, "利き手の見出し", "利き手", font, 36, new Vector2(-360f, -270f), new Vector2(260f, 60f), light);
            Choice(TTStartPanel.Choice.LeftHand, "左", -110f, -270f, wide, 38, panel.SelectLeftHand);
            Choice(TTStartPanel.Choice.RightHand, "右", 125f, -270f, wide, 38, panel.SelectRightHand);

            (_, TMP_Text startLabel) = UIBuildUtility.PrimaryButton(c, "はじめる", "はじめる", font, 46, new Vector2(0f, -390f),
                new Vector2(340f, 96f), panel.StartGame);

            // モード(CPU と / チャレンジ)と強さの行(6.10)
            ModeSelector selector = CommonSceneBuilder.AddModeRows(holder, c, font, SportIds.TableTennis,
                new[] { SportMode.Cpu, SportMode.Challenge }, 150f, 45f, startLabel, title);

            var so = new SerializedObject(panel);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("modeSelector").objectReferenceValue = selector;
            so.FindProperty("panelRoot").objectReferenceValue = canvas;
            so.FindProperty("heightText").objectReferenceValue = height;
            SetArray(so.FindProperty("choiceImages"), images);
            SetArray(so.FindProperty("choiceTexts"), texts);
            so.ApplyModifiedPropertiesWithoutUndo();

            var gameSo = new SerializedObject(game);
            gameSo.FindProperty("modeSelector").objectReferenceValue = selector;
            gameSo.FindProperty("choicePanel").objectReferenceValue = choicePanel;
            gameSo.FindProperty("aimTargets").objectReferenceValue = aimTargets;
            gameSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 音

        private static void BuildFeedback(Transform root, TableTennisGame game)
        {
            var holder = new GameObject("音");
            holder.transform.SetParent(root, false);
            var feedback = holder.AddComponent<TTFeedback>();
            var hits = new Object[3];
            for (int i = 0; i < hits.Length; i++)
            {
                hits[i] = Source(holder, $"当たった点の音{i + 1}", true, 2f, 25f);
            }

            AudioSource ui = Source(holder, "得点の音", false, 1f, 30f);

            var so = new SerializedObject(feedback);
            so.FindProperty("game").objectReferenceValue = game;
            SetArray(so.FindProperty("hitSources"), hits);
            so.FindProperty("uiSource").objectReferenceValue = ui;
            SetArray(so.FindProperty("paddleSoft"), Clips("SFX_TT_Paddle_Soft_", 2));
            SetArray(so.FindProperty("paddleMid"), Clips("SFX_TT_Paddle_Mid_", 2));
            SetArray(so.FindProperty("paddleHard"), Clips("SFX_TT_Paddle_Hard_", 2));
            SetArray(so.FindProperty("tableClips"), Clips("SFX_TT_Table_", 3));
            SetArray(so.FindProperty("netClips"), Clips("SFX_TT_Net_", 2));
            SetArray(so.FindProperty("floorClips"), Clips("SFX_TT_Floor_", 2));
            so.FindProperty("pointWin").objectReferenceValue = Clip("SFX_TT_PointWin");
            so.FindProperty("pointLose").objectReferenceValue = Clip("SFX_TT_PointLose");
            so.FindProperty("deuce").objectReferenceValue = Clip("SFX_TT_Deuce");
            so.FindProperty("matchWin").objectReferenceValue = Clip("SFX_MatchWin");
            so.FindProperty("matchLose").objectReferenceValue = Clip("SFX_MatchLose");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>環境音:波と葉のざわめき(合計 -24dB 程度に小さく)。</summary>
        private static void BuildAmbient(Transform root)
        {
            var holder = new GameObject("環境音");
            holder.transform.SetParent(root, false);
            (string clip, Vector3 at, float volume)[] loops =
            {
                ("AMB_Wave_Loop", new Vector3(0f, 0f, -14f), 0.05f),
                ("AMB_Leaves_Loop", new Vector3(-4f, 3f, 2f), 0.04f),
                ("AMB_Leaves_Loop", new Vector3(4f, 3f, 6f), 0.04f),
            };
            for (int i = 0; i < loops.Length; i++)
            {
                AudioSource source = Source(holder, $"環境音{i + 1}", true, 4f, 40f);
                source.transform.position = loops[i].at;
                source.clip = Clip(loops[i].clip);
                source.loop = true;
                source.playOnAwake = true;
                source.volume = loops[i].volume;
                source.pitch = 0.95f + 0.05f * i;
                source.gameObject.AddComponent<SoundCategory>();
            }
        }

        // ---------------------------------------------------------------- 小さな部品

        private static void SetControllers(SerializedObject so, List<string> warnings)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform left = all.FirstOrDefault(t => t.name == "Left Controller");
            Transform right = all.FirstOrDefault(t => t.name == "Right Controller");
            if (left == null || right == null)
            {
                warnings.Add("Left Controller / Right Controller が見つかりません。TableTennisGame の手の欄を手で設定してください");
                return;
            }

            so.FindProperty("leftController").objectReferenceValue = left;
            so.FindProperty("rightController").objectReferenceValue = right;
            so.FindProperty("leftInteractor").objectReferenceValue = left.GetComponentInChildren<NearFarInteractor>(true);
            so.FindProperty("rightInteractor").objectReferenceValue = right.GetComponentInChildren<NearFarInteractor>(true);
        }

        private static Material Mat(string name, string hex)
        {
            return SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, name, hex, false);
        }

        private static GameObject Block(Transform parent, string name, Vector3 min, Vector3 max, Material material, bool collider,
            bool isStatic)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, PrimitiveType.Cube, (min + max) / 2f, max - min, material, collider);
            go.transform.SetParent(parent, true);
            go.isStatic = isStatic;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void Static(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, true);
            go.isStatic = true;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, position, scale, material, false);
            Static(go, parent);
        }

        /// <summary>親の中に置く部品(当たり判定なし、影なし)。</summary>
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale,
            Material material)
        {
            return Part(parent, name, type, localPosition, Vector3.zero, scale, material);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 euler,
            Vector3 scale, Material material)
        {
            GameObject go = SceneBuildUtility.CreatePrimitive(name, type, Vector3.zero, Vector3.one, material, false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static AudioSource Source(GameObject owner, string name, bool spatial, float minDistance, float maxDistance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(owner.transform, false);
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
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.wav");
            if (clip == null)
            {
                Debug.LogWarning($"[卓球場] 音のファイルが見つかりません: {AudioFolder}/{name}.wav");
            }

            return clip;
        }

        private static Object[] Clips(string prefix, int count)
        {
            var clips = new Object[count];
            for (int i = 0; i < count; i++)
            {
                clips[i] = Clip($"{prefix}{i + 1}");
            }

            return clips;
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
