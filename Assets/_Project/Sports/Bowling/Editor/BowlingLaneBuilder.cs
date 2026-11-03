using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRGame3.Common;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// メニュー「VRGame3 > ボウリング場を作る」で、M1 のボウリング小屋(室内・2レーン)を仮の形状で組み立てる。
    /// 自分のレーン(X=0)と CPU のレーン(X=1.8、M3 で使う)・小屋・外の砂地と海・ピン・ボール置き台と
    /// ボール3個・スコア板・音(BowlingAudioBuilder)・XR Origin を配置する。寸法はレーンを本物に合わせ(ファウルラインから先頭のピンまで 18.29m)、
    /// 小屋と色は docs/art-direction.md 第9章 M1「ボウリング小屋の仮組み」に合わせる。
    /// </summary>
    public static class BowlingLaneBuilder
    {
        private const string SceneFolder = "Assets/_Project/Sports/Bowling/Scenes";
        private const string ScenePath = SceneFolder + "/Bowling.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Bowling";
        private const string PhysicsFolder = "Assets/_Project/Sports/Bowling/Physics";
        private const string ModelFolder = "Assets/_Project/Sports/Bowling/Models";

        // レーンの寸法(m)。Z+ がピンの方向、ファウルラインが Z=0、レーンの上面が Y=0
        private const float PlayerLaneX = 0f;
        private const float CpuLaneX = 1.8f;
        private const float LaneWidth = 1.066f;
        private const float GutterWidth = 0.24f;
        private const float GutterDepth = 0.08f;
        private const float HeadPinDistance = 18.29f;
        private const float LaneEnd = 19.3f;
        private const float PitEnd = 21.5f;
        private const float PitDepth = 0.6f;
        private const float SideBoardThickness = 0.05f;
        private const float PinSpacing = 0.305f;
        private const float Bottom = -PitDepth - 0.1f;

        // 助走路の床の上面。レーン(上面 Y 0)と同じ高さで突き合わせると、転がるボールが
        // 継ぎ目でレーンの箱の角に引っかかる(見えない壁に当たったように止まる)ので、わずかに高くする
        private const float ApproachTop = 0.003f;
        private const float HalfLane = LaneWidth / 2f;
        private const float GutterOuter = HalfLane + GutterWidth;
        private const float LaneOuter = GutterOuter + SideBoardThickness;

        // 小屋の寸法(m)
        private const float HutMinX = -2.6f;
        private const float HutMaxX = 4.4f;
        private const float HutMinZ = -7f;
        private const float HutMaxZ = 22f;
        private const float WallThickness = 0.15f;
        private const float InnerMinX = HutMinX + WallThickness;
        private const float InnerMaxX = HutMaxX - WallThickness;
        private const float InnerMinZ = HutMinZ + WallThickness;
        private const float InnerMaxZ = HutMaxZ - WallThickness;
        private const float EaveHeight = 3.6f;
        private const float RidgeX = 0.9f;
        private const float RidgeHeight = 4.9f;
        private const float RoofPitch = 20f;

        // テレポートできる床(自分のレーンの助走路側)の右端
        private const float TeleportMaxX = 0.6f;

        // ピンの重さ(本物はおよそ 1.5kg)。形は BowlingPinMeshBuilder を参照
        private const float PinMass = 1.5f;

        // ボールの寸法(本物は直径 21.8cm)
        private const float BallDiameter = 0.218f;
        private const float BallMass = 6f;

        // プレイヤーとボール置き台の位置
        private static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, -3f);
        private static readonly Vector3 RackPosition = new Vector3(0.9f, 0f, -2.3f);
        private const float RackHeight = 0.8f;

        [MenuItem("VRGame3/ボウリング場を作る")]
        public static void Build()
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("ボウリング場",
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
                !EditorUtility.DisplayDialog("ボウリング場", $"{ScenePath} はすでにあります。作り直しますか?", "作り直す", "やめる"))
            {
                return;
            }

            SceneBuildUtility.EnsureFolder(SceneFolder);
            BowlingAudioBuilder.ImportClips();
            var palette = new BowlingBuildPalette(MaterialFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            SetUpSun();

            PhysicsMaterial lanePhysics = GetOrCreatePhysicsMaterial("Lane", 0.04f, 0f);
            PhysicsMaterial pinPhysics = GetOrCreatePhysicsMaterial("Pin", 0.35f, 0.3f);
            PhysicsMaterial ballPhysics = GetOrCreatePhysicsMaterial("Ball", 0.04f, 0.05f);
            PhysicsMaterial cushionPhysics = GetOrCreatePhysicsMaterial("Cushion", 0.6f, 0f);

            var root = new GameObject("ボウリング場");
            var building = new GameObject("小屋").transform;
            building.SetParent(root.transform, false);
            var outside = new GameObject("小屋の外").transform;
            outside.SetParent(root.transform, false);

            BuildLane(root.transform, "自分のレーン", PlayerLaneX, palette, lanePhysics, cushionPhysics);
            BuildLane(root.transform, "CPU のレーン", CpuLaneX, palette, lanePhysics, cushionPhysics);
            BuildLaneGap(root.transform, palette, lanePhysics);
            BuildFloor(building, palette);
            BuildHut(building, palette);
            List<Transform> palms = BuildOutside(outside, palette);
            // 波は海側の3か所(左右の浜と、入口の先の浜)から鳴らす
            BowlingAudioBuilder.BuildAmbient(outside, palms,
                new[] { new Vector3(-12f, 0f, 6f), new Vector3(16f, 0f, 10f), new Vector3(1f, 0f, -22f) });

            Transform foulLine = BuildFoulLine(root.transform, "ファウルライン(自分のレーン)", PlayerLaneX, palette);
            BuildFoulLine(root.transform, "ファウルライン(CPU のレーン)", CpuLaneX, palette);
            PinSetter pinSetter = BuildPins(root.transform, "ピンセッター(自分のレーン)", PlayerLaneX, palette, pinPhysics);
            BuildPins(root.transform, "ピンセッター(CPU のレーン)", CpuLaneX, palette, pinPhysics);
            List<ThrowableObject> balls = BuildBallRack(root.transform, palette, ballPhysics, foulLine);
            BowlingScoreBoard scoreBoard = BuildScoreBoards(root.transform, palette);

            var game = root.AddComponent<BowlingGame>();
            var gameSo = new SerializedObject(game);
            SerializedProperty ballList = gameSo.FindProperty("balls");
            ballList.arraySize = balls.Count;
            for (int i = 0; i < balls.Count; i++)
            {
                ballList.GetArrayElementAtIndex(i).objectReferenceValue = balls[i];
            }

            gameSo.FindProperty("pinSetter").objectReferenceValue = pinSetter;
            gameSo.FindProperty("scoreBoard").objectReferenceValue = scoreBoard;
            gameSo.FindProperty("foulLine").objectReferenceValue = foulLine;
            gameSo.FindProperty("pitDistance").floatValue = LaneEnd + 0.3f;
            // 終わったあとの再開は「はじめに」パネルから行う
            gameSo.FindProperty("restartOnGrab").boolValue = false;
            gameSo.ApplyModifiedPropertiesWithoutUndo();
            BowlingAudioBuilder.AddGameSounds(root, game, new Vector3(RidgeX, 2.85f, 2.5f),
                RackPosition + new Vector3(0f, RackHeight, 0f));
            BowlingAudioBuilder.AddPinCrash(root, balls, new Vector3(PlayerLaneX, 0.2f, HeadPinDistance + 0.5f));
            BowlingEffectsBuilder.AddEffects(root, game, pinSetter, palette.Pin, MaterialFolder, PlayerLaneX, HeadPinDistance + 0.5f);

            Material arc = SceneBuildUtility.GetOrCreateArcMaterial(MaterialFolder, "B_TeleportArc");
            List<string> warnings = SceneBuildUtility.SetUpXROrigin(arc, palette.Reticle, PlayerPosition, Quaternion.identity,
                requireTeleportArea: true);

            // 身長と利き手に合わせて動かす置き台と、投げ始める前の「はじめに」パネル
            BowlingRack rack = SetUpRack(root.transform, balls, scoreBoard.transform.Find("小型スコア板"));
            string panelWarning = BowlingStartPanelBuilder.AddStartPanel(root, game, rack, new Vector3(0f, 1.3f, -1.95f),
                PlayerPosition + new Vector3(0f, 1.6f, 0f));
            if (panelWarning != null)
            {
                warnings.Add(panelWarning);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuildSettings(ScenePath);

            string message = $"{ScenePath} を作りました。";
            if (warnings.Count > 0)
            {
                message += "\n\n手で確認が必要な点:\n- " + string.Join("\n- ", warnings);
                foreach (string warning in warnings)
                {
                    Debug.LogWarning($"[ボウリング場] {warning}");
                }
            }

            Debug.Log($"[ボウリング場] {message}");
            EditorUtility.DisplayDialog("ボウリング場", message, "OK");
        }

        /// <summary>
        /// 太陽(Directional Light)をプレイヤーの背中側の斜め上から当てる。
        /// </summary>
        private static void SetUpSun()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                }
            }
        }

        /// <summary>
        /// 1レーン分:レーン・ガター・横の板・キックバック・ピットの底・奥のクッション。
        /// レーンとガターは下まで厚みを持たせ、ピットの穴から見ても宙に浮いて見えないようにする。
        /// </summary>
        private static void BuildLane(Transform root, string name, float x, BowlingBuildPalette palette,
            PhysicsMaterial lanePhysics, PhysicsMaterial cushionPhysics)
        {
            var parent = new GameObject(name).transform;
            parent.SetParent(root, false);

            Block(parent, "レーン", new Vector3(x - HalfLane, Bottom, 0f), new Vector3(x + HalfLane, 0f, LaneEnd),
                palette.WoodLight, lanePhysics);

            for (int side = -1; side <= 1; side += 2)
            {
                string label = side < 0 ? "左" : "右";
                float inner = x + side * HalfLane;
                float gutter = x + side * GutterOuter;
                float outer = x + side * LaneOuter;

                Block(parent, $"ガター({label})", new Vector3(Mathf.Min(inner, gutter), Bottom, 0f),
                    new Vector3(Mathf.Max(inner, gutter), -GutterDepth, LaneEnd), palette.Stone, lanePhysics);

                // 横の板:ピンの手前までは低く、ピン置き場の横(キックバック)は高く
                Block(parent, $"横の板({label})", new Vector3(Mathf.Min(gutter, outer), Bottom, 0f),
                    new Vector3(Mathf.Max(gutter, outer), 0.15f, 17f), palette.Wood, lanePhysics);
                Block(parent, $"キックバック({label})", new Vector3(Mathf.Min(gutter, outer), Bottom, 17f),
                    new Vector3(Mathf.Max(gutter, outer), 0.7f, InnerMaxZ), palette.Wood, cushionPhysics);
            }

            // ピット:レーンの先の一段低い所。倒れたピンとボールがここに落ちる
            Block(parent, "ピットの底", new Vector3(x - GutterOuter, Bottom, LaneEnd), new Vector3(x + GutterOuter, -PitDepth, PitEnd),
                palette.Stone, cushionPhysics);
            Block(parent, "奥のクッション", new Vector3(x - LaneOuter, Bottom, PitEnd), new Vector3(x + LaneOuter, 1.2f, InnerMaxZ),
                palette.Accent, cushionPhysics);
        }

        /// <summary>2つのレーンの間のすき間を笠木でふさぐ。</summary>
        private static void BuildLaneGap(Transform root, BowlingBuildPalette palette, PhysicsMaterial lanePhysics)
        {
            Block(root, "レーンの間の笠木", new Vector3(PlayerLaneX + LaneOuter, Bottom, 0f),
                new Vector3(CpuLaneX - LaneOuter, 0.03f, InnerMaxZ), palette.Wood, lanePhysics);
        }

        /// <summary>
        /// 室内の床。自分のレーンの助走路側だけに TeleportArea の目印を付け、
        /// CPU のレーンの助走路・レーンの横・小屋の外へはテレポートできないようにする。
        /// </summary>
        private static void BuildFloor(Transform parent, BowlingBuildPalette palette)
        {
            GameObject playerFloor = Block(parent, "助走路(テレポートできる床)", new Vector3(InnerMinX, -0.1f, InnerMinZ),
                new Vector3(TeleportMaxX, ApproachTop, 0f), palette.WoodLight, null);
            playerFloor.AddComponent<TeleportArea>();

            Block(parent, "CPU 側の助走路", new Vector3(TeleportMaxX, -0.1f, InnerMinZ), new Vector3(InnerMaxX, ApproachTop, 0f),
                palette.WoodLight, null);
            Block(parent, "左の床", new Vector3(InnerMinX, -0.1f, 0f), new Vector3(PlayerLaneX - LaneOuter, 0f, InnerMaxZ),
                palette.Wood, null);
            Block(parent, "右の床", new Vector3(CpuLaneX + LaneOuter, -0.1f, 0f), new Vector3(InnerMaxX, 0f, InnerMaxZ),
                palette.Wood, null);
        }

        /// <summary>
        /// 小屋:側壁(腰壁・窓の開口・上の帯・柱)、前壁(入口と日よけ)、奥壁、切妻屋根、梁、ピン上の幕板。
        /// </summary>
        private static void BuildHut(Transform parent, BowlingBuildPalette palette)
        {
            const float windowBottom = 0.9f;
            const float windowTop = 2.4f;
            const float windowStartZ = -6f;
            const float windowEndZ = 17f;

            // 側壁
            for (int side = -1; side <= 1; side += 2)
            {
                string label = side < 0 ? "左" : "右";
                float minX = side < 0 ? HutMinX : InnerMaxX;
                float maxX = side < 0 ? InnerMinX : HutMaxX;

                Block(parent, $"腰壁({label})", new Vector3(minX, 0f, HutMinZ), new Vector3(maxX, windowBottom, HutMaxZ),
                    palette.Wood, null);
                Block(parent, $"上の帯({label})", new Vector3(minX, windowTop, HutMinZ), new Vector3(maxX, EaveHeight, HutMaxZ),
                    palette.WoodLight, null);
                Block(parent, $"窓の手前の壁({label})", new Vector3(minX, windowBottom, HutMinZ),
                    new Vector3(maxX, windowTop, windowStartZ), palette.WoodLight, null);
                Block(parent, $"窓の奥の壁({label})", new Vector3(minX, windowBottom, windowEndZ),
                    new Vector3(maxX, windowTop, HutMaxZ), palette.WoodLight, null);

                foreach (float z in new[] { -3f, 1f, 5f, 9f, 13f })
                {
                    Block(parent, $"柱({label} Z{z})", new Vector3(minX, windowBottom, z - 0.075f),
                        new Vector3(maxX, windowTop, z + 0.075f), palette.WoodDark, null);
                }
            }

            // 前壁(プレイヤーの背後):入口の開口から海と砂浜が見える
            const float doorMinX = -0.6f;
            const float doorMaxX = 2.4f;
            const float doorTop = 2.6f;
            Block(parent, "前壁(左)", new Vector3(HutMinX, 0f, HutMinZ), new Vector3(doorMinX, EaveHeight, InnerMinZ),
                palette.WoodLight, null);
            Block(parent, "前壁(右)", new Vector3(doorMaxX, 0f, HutMinZ), new Vector3(HutMaxX, EaveHeight, InnerMinZ),
                palette.WoodLight, null);
            Block(parent, "入口の上", new Vector3(doorMinX, doorTop, HutMinZ), new Vector3(doorMaxX, EaveHeight, InnerMinZ),
                palette.WoodLight, null);
            GameObject awning = Box(parent, "日よけ", Vector3.zero, new Vector3(3.6f, 0.08f, 1.2f), palette.Accent, null);
            awning.transform.SetPositionAndRotation(
                new Vector3((doorMinX + doorMaxX) / 2f, 2.8f, HutMinZ - 0.6f * Mathf.Cos(15f * Mathf.Deg2Rad)),
                Quaternion.Euler(-15f, 0f, 0f));

            // 奥壁:開口なし(ピンの背景を落ち着かせ、逆光を防ぐ)
            Block(parent, "奥壁", new Vector3(HutMinX, Bottom, InnerMaxZ), new Vector3(HutMaxX, EaveHeight, HutMaxZ),
                palette.WoodLight, null);

            // 切妻屋根:棟から軒へ下がる板2枚。前後の三角部分(妻)は開けたまま
            float slopeLength = 4.15f;
            float halfRun = slopeLength / 2f * Mathf.Cos(RoofPitch * Mathf.Deg2Rad);
            float halfRise = slopeLength / 2f * Mathf.Sin(RoofPitch * Mathf.Deg2Rad);
            float roofLength = HutMaxZ - HutMinZ + 1f;
            float roofCenterZ = (HutMinZ + HutMaxZ) / 2f;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject roof = Box(parent, side < 0 ? "屋根(左)" : "屋根(右)", Vector3.zero,
                    new Vector3(slopeLength, 0.1f, roofLength), palette.Roof, null);
                roof.transform.SetPositionAndRotation(
                    new Vector3(RidgeX + side * halfRun, RidgeHeight - halfRise, roofCenterZ),
                    Quaternion.Euler(0f, 0f, -side * RoofPitch));
            }

            Box(parent, "棟", new Vector3(RidgeX, RidgeHeight, roofCenterZ), new Vector3(0.15f, 0.15f, roofLength),
                palette.WoodDark, null);
            foreach (float z in new[] { 2.5f, 6.5f, 10.5f, 14.5f })
            {
                Block(parent, $"梁(Z{z})", new Vector3(InnerMinX, EaveHeight, z - 0.075f), new Vector3(InnerMaxX, EaveHeight + 0.2f, z + 0.075f),
                    palette.WoodDark, null);
            }

            // ピン上の幕板:ピンへの視線は幕板の位置で Y 約 0.45 なので、下端 1.2 ならピンは隠れない
            Block(parent, "幕板", new Vector3(InnerMinX, 1.2f, 18.8f), new Vector3(InnerMaxX, EaveHeight, 18.85f), palette.Accent, null);
        }

        /// <summary>
        /// 小屋の外:砂地(小屋の床の下には入れない)・海・ヤシの木。どこにも TeleportArea を付けない。
        /// </summary>
        private static List<Transform> BuildOutside(Transform parent, BowlingBuildPalette palette)
        {
            const float sandTop = -0.05f;
            const float sandBottom = -0.55f;
            Block(parent, "砂地(手前)", new Vector3(-10f, sandBottom, -20f), new Vector3(22f, sandTop, HutMinZ), palette.Sand, null);
            Block(parent, "砂地(奥)", new Vector3(-10f, sandBottom, HutMaxZ), new Vector3(22f, sandTop, 30f), palette.Sand, null);
            Block(parent, "砂地(左)", new Vector3(-10f, sandBottom, HutMinZ), new Vector3(HutMinX, sandTop, HutMaxZ), palette.Sand, null);
            Block(parent, "砂地(右)", new Vector3(HutMaxX, sandBottom, HutMinZ), new Vector3(22f, sandTop, HutMaxZ), palette.Sand, null);

            GameObject sea = SceneBuildUtility.CreatePrimitive("海", PrimitiveType.Plane, new Vector3(6f, -0.4f, 5f),
                new Vector3(30f, 1f, 30f), palette.Sea, false);
            MakeStatic(sea, parent);

            Vector3[] palms = { new Vector3(-6f, 0f, 2f), new Vector3(-7f, 0f, 10f), new Vector3(8f, 0f, -1f) };
            var leafTransforms = new List<Transform>();
            for (int i = 0; i < palms.Length; i++)
            {
                GameObject trunk = SceneBuildUtility.CreatePrimitive($"ヤシの幹{i + 1}", PrimitiveType.Cylinder,
                    palms[i] + new Vector3(0f, 2f, 0f), new Vector3(0.25f, 2f, 0.25f), palette.WoodDark, false);
                MakeStatic(trunk, parent);
                GameObject leaves = SceneBuildUtility.CreatePrimitive($"ヤシの葉{i + 1}", PrimitiveType.Sphere,
                    palms[i] + new Vector3(0f, 4.1f, 0f), new Vector3(2.8f, 0.5f, 2.8f), palette.Leaf, false);
                MakeStatic(leaves, parent);
                leafTransforms.Add(leaves.transform);
            }

            return leafTransforms;
        }

        private static Transform BuildFoulLine(Transform parent, string name, float x, BowlingBuildPalette palette)
        {
            // 助走路の床(わずかに高い)の上に、手前側へ寄せて置く
            GameObject line = SceneBuildUtility.CreatePrimitive(name, PrimitiveType.Cube, new Vector3(x, ApproachTop + 0.001f, -0.01f),
                new Vector3(LaneWidth + GutterWidth * 2f, 0.002f, 0.02f), palette.Line, false);
            MakeStatic(line, parent);
            return line.transform;
        }

        /// <summary>
        /// ピン10本を正三角形に並べる。先頭のピンがファウルラインから 18.29m、隣のピンとの間は 30.5cm。
        /// </summary>
        private static PinSetter BuildPins(Transform parent, string name, float laneX, BowlingBuildPalette palette,
            PhysicsMaterial pinPhysics)
        {
            var setterObject = new GameObject(name);
            setterObject.transform.SetParent(parent, false);
            setterObject.transform.position = new Vector3(laneX, 0f, HeadPinDistance);
            var setter = setterObject.AddComponent<PinSetter>();

            Mesh pinMesh = BowlingPinMeshBuilder.GetOrCreate(ModelFolder);
            float rowDepth = PinSpacing * Mathf.Sqrt(3f) / 2f;
            var pins = new List<BowlingPin>();
            int number = 1;
            for (int row = 0; row < 4; row++)
            {
                for (int i = 0; i <= row; i++)
                {
                    float x = laneX + (i - row / 2f) * PinSpacing;
                    var position = new Vector3(x, 0.001f, HeadPinDistance + row * rowDepth);
                    pins.Add(BuildPin(setterObject.transform, $"ピン{number}", position, pinMesh, palette, pinPhysics));
                    number++;
                }
            }

            var so = new SerializedObject(setter);
            SerializedProperty list = so.FindProperty("pins");
            list.arraySize = pins.Count;
            for (int i = 0; i < pins.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = pins[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return setter;
        }

        /// <summary>
        /// ピン1本。原点を底に置く。見た目は本物に近い輪郭の回転体メッシュ(首に赤い帯2本)、
        /// 当たり判定は同じメッシュの凸形状にする(底が平らなので立ったまま安定する)。
        /// </summary>
        private static BowlingPin BuildPin(Transform parent, string name, Vector3 position, Mesh mesh,
            BowlingBuildPalette palette, PhysicsMaterial physics)
        {
            var pin = new GameObject(name);
            pin.transform.SetParent(parent, false);
            pin.transform.position = position;

            var visual = new GameObject("見た目");
            visual.transform.SetParent(pin.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterials = new[] { palette.Pin, palette.PinStripe };

            var collider = pin.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            collider.sharedMaterial = physics;

            var body = pin.AddComponent<Rigidbody>();
            body.mass = PinMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            BowlingAudioBuilder.AddPinAudio(pin);
            return pin.AddComponent<BowlingPin>();
        }

        /// <summary>
        /// 2レーン共用のボール置き台(レーンの間)とボール3個。台の上の2本のレールの間にボールを置き、転がり落ちないようにする。
        /// </summary>
        private static List<ThrowableObject> BuildBallRack(Transform parent, BowlingBuildPalette palette, PhysicsMaterial ballPhysics,
            Transform foulLine)
        {
            var rack = new GameObject("ボール置き台");
            rack.transform.SetParent(parent, false);
            rack.transform.position = RackPosition;

            Box(rack.transform, "台", RackPosition + new Vector3(0f, RackHeight / 2f, 0f), new Vector3(0.3f, RackHeight, 1.0f),
                palette.Wood, null);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(rack.transform, side < 0 ? "レール(左)" : "レール(右)",
                    RackPosition + new Vector3(side * 0.08f, RackHeight + 0.02f, 0f), new Vector3(0.03f, 0.04f, 1.0f),
                    palette.WoodLight, null);
            }

            Material[] ballMaterials = { palette.BallA, palette.BallB, palette.BallC };
            var balls = new List<ThrowableObject>();
            for (int i = 0; i < ballMaterials.Length; i++)
            {
                Vector3 position = RackPosition + new Vector3(0f, RackHeight + BallDiameter / 2f + 0.02f, -0.3f + 0.3f * i);
                GameObject ball = SceneBuildUtility.CreatePrimitive($"ボール{i + 1}", PrimitiveType.Sphere, position,
                    Vector3.one * BallDiameter, ballMaterials[i], true);
                ball.transform.SetParent(rack.transform, true);
                ball.GetComponent<Collider>().sharedMaterial = ballPhysics;
                ball.AddComponent<Rigidbody>().mass = BallMass;
                ball.AddComponent<XRGrabInteractable>();

                var throwable = ball.AddComponent<ThrowableObject>();
                var throwSo = new SerializedObject(throwable);
                // レーンは油が引いてあり、ほとんど減速しない
                throwSo.FindProperty("rollingDeceleration").floatValue = 0.05f;
                throwSo.ApplyModifiedPropertiesWithoutUndo();

                // 上下の向きを水平に直し、左右のぶれをレーンの向きに寄せる
                ball.AddComponent<BowlingThrowAssist>().LaneDirection = foulLine;

                var returner = ball.AddComponent<ReturnToHome>();
                var returnSo = new SerializedObject(returner);
                // 投球ごとの戻しは BowlingGame が行う。ここは落ちたときと、止まったまま放置されたときの保険
                returnSo.FindProperty("maxAwayTime").floatValue = 0f;
                returnSo.FindProperty("stopDelay").floatValue = 5f;
                returnSo.ApplyModifiedPropertiesWithoutUndo();

                BowlingAudioBuilder.AddBallAudio(ball);

                balls.Add(throwable);
            }

            return balls;
        }

        /// <summary>
        /// ボール置き台に BowlingRack を付ける。設定に合わせて動かすので、置き台と小型スコア板は Static にしない。
        /// </summary>
        private static BowlingRack SetUpRack(Transform root, List<ThrowableObject> balls, Transform smallScoreBoard)
        {
            Transform rackTransform = root.Find("ボール置き台");
            SetStaticRecursively(rackTransform, false);
            if (smallScoreBoard != null)
            {
                SetStaticRecursively(smallScoreBoard, false);
            }

            var rack = rackTransform.gameObject.AddComponent<BowlingRack>();
            var so = new SerializedObject(rack);
            so.FindProperty("table").objectReferenceValue = rackTransform.Find("台");
            SerializedProperty rails = so.FindProperty("rails");
            rails.arraySize = 2;
            rails.GetArrayElementAtIndex(0).objectReferenceValue = rackTransform.Find("レール(左)");
            rails.GetArrayElementAtIndex(1).objectReferenceValue = rackTransform.Find("レール(右)");
            SerializedProperty ballList = so.FindProperty("balls");
            ballList.arraySize = balls.Count;
            for (int i = 0; i < balls.Count; i++)
            {
                ballList.GetArrayElementAtIndex(i).objectReferenceValue = balls[i];
            }

            so.FindProperty("smallScoreBoard").objectReferenceValue = smallScoreBoard;
            so.FindProperty("rightHandX").floatValue = RackPosition.x;
            so.FindProperty("leftHandX").floatValue = -RackPosition.x;
            so.FindProperty("ballDiameter").floatValue = BallDiameter;
            so.ApplyModifiedPropertiesWithoutUndo();
            return rack;
        }

        private static void SetStaticRecursively(Transform target, bool isStatic)
        {
            foreach (Transform t in target.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = isStatic;
            }
        }

        /// <summary>
        /// 大型スコア板(2レーン共用、上段が自分・下段が CPU)と小型スコア板(ボール置き台の横)、
        /// ストライクなどの大きな文字(自分のレーンのピンの手前)を作る。docs/art-direction.md の 6.1・6.2 と第9章 M1 に合わせる。
        /// </summary>
        private static BowlingScoreBoard BuildScoreBoards(Transform parent, BowlingBuildPalette palette)
        {
            var boardObject = new GameObject("スコア板");
            boardObject.transform.SetParent(parent, false);

            // 大型板:中心 (0.9, 2.85, 2.5)、幅 4.3 × 高さ 1.4。上端 3.55 は梁の下、下端 2.15 は視線より上
            const float centerX = RidgeX;
            const float centerY = 2.85f;
            const float boardZ = 2.5f;
            const float textZ = boardZ - 0.02f;
            const float boardWidth = 4.3f;
            const float labelWidth = 0.4f;
            const float cellWidth = 0.33f;
            const float totalWidth = 0.5f;
            Box(boardObject.transform, "大型スコア板", new Vector3(centerX, centerY, boardZ + 0.03f), new Vector3(boardWidth, 1.4f, 0.05f),
                palette.Board, null);

            float left = centerX - boardWidth / 2f + 0.05f;
            float labelX = left + labelWidth / 2f;
            float firstCellX = left + labelWidth + cellWidth / 2f;
            float totalX = left + labelWidth + cellWidth * BowlingScoreSheet.FrameCount + totalWidth / 2f;

            // 見出しの段:フレーム番号と TOTAL
            var frameNumbers = new TMP_Text[BowlingScoreSheet.FrameCount];
            for (int f = 0; f < BowlingScoreSheet.FrameCount; f++)
            {
                frameNumbers[f] = Text(boardObject.transform, $"フレーム番号{f + 1}", (f + 1).ToString(),
                    new Vector3(firstCellX + cellWidth * f, 3.42f, textZ), cellWidth, 1.3f, palette.TextColor);
            }

            Text(boardObject.transform, "合計の見出し", "TOTAL", new Vector3(totalX, 3.42f, textZ), totalWidth, 1.3f, palette.TextColor);

            // 上段(自分)と下段(CPU)
            var board = boardObject.AddComponent<BowlingScoreBoard>();
            ScoreRowTexts player = BuildScoreRow(boardObject.transform, "自分の段", "YOU", palette.TagPlayer, 3.17f, 2.93f,
                labelX, firstCellX, cellWidth, totalX, totalWidth, textZ, palette);
            var cpuRowObject = new GameObject("CPU の段(M3 で使う)");
            cpuRowObject.transform.SetParent(boardObject.transform, false);
            var cpuBoard = cpuRowObject.AddComponent<BowlingScoreBoard>();
            ScoreRowTexts cpu = BuildScoreRow(cpuRowObject.transform, "CPU の段", "CPU", palette.TagCpu, 2.62f, 2.38f,
                labelX, firstCellX, cellWidth, totalX, totalWidth, textZ, palette);
            cpu.Total.text = string.Empty;

            // 小型板:ボール置き台のプレイヤー側、胸の高さ。プレイヤーの方を向ける
            var smallPosition = new Vector3(0.55f, 1.15f, -1.9f);
            Vector3 toPlayer = PlayerPosition - smallPosition;
            toPlayer.y = 0f;
            Quaternion smallRotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
            var small = new GameObject("小型スコア板");
            small.transform.SetParent(boardObject.transform, false);
            small.transform.SetPositionAndRotation(smallPosition, smallRotation);
            GameObject smallBack = Box(small.transform, "板", smallPosition, new Vector3(0.62f, 0.3f, 0.03f), palette.Board, null);
            smallBack.transform.SetPositionAndRotation(smallPosition + smallRotation * new Vector3(0f, 0f, 0.02f), smallRotation);
            TMP_Text smallFrame = Text(small.transform, "現在のフレーム", string.Empty, Vector3.zero, 0.6f, 0.6f, palette.TextColor);
            smallFrame.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            smallFrame.transform.localRotation = Quaternion.identity;
            TMP_Text smallTotal = Text(small.transform, "合計", string.Empty, Vector3.zero, 0.6f, 0.6f, palette.HighlightColor);
            smallTotal.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            smallTotal.transform.localRotation = Quaternion.identity;

            // 大きな文字:自分のレーンのピンの手前の上空。プレイヤーから 10m 以上離し、幅は隣のレーンにはみ出さない
            TMP_Text message = Text(boardObject.transform, "大きな文字", "STRIKE!", new Vector3(PlayerLaneX, 1.6f, 14f), 3f, 6f,
                palette.HighlightColor);
            message.fontStyle = FontStyles.Bold;
            // 「GAME 300」のように長い文字でも隣のレーンにはみ出さないよう、幅に合わせて縮める
            message.enableAutoSizing = true;
            message.fontSizeMin = 3f;
            message.fontSizeMax = 6f;
            // 景色の上でも読めるよう縁取りを付ける(方針書 6.1)
            Material outline = GetOrCreateOutlineMaterial();
            if (outline != null)
            {
                message.fontSharedMaterial = outline;
            }
            message.gameObject.SetActive(false);

            var so = new SerializedObject(board);
            SetArray(so.FindProperty("frameNumberTexts"), frameNumbers);
            SetArray(so.FindProperty("rollTexts"), player.Rolls);
            SetArray(so.FindProperty("scoreTexts"), player.Scores);
            so.FindProperty("totalText").objectReferenceValue = player.Total;
            so.FindProperty("smallFrameText").objectReferenceValue = smallFrame;
            so.FindProperty("smallTotalText").objectReferenceValue = smallTotal;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();

            var cpuSo = new SerializedObject(cpuBoard);
            SetArray(cpuSo.FindProperty("frameNumberTexts"), new TMP_Text[0]);
            SetArray(cpuSo.FindProperty("rollTexts"), cpu.Rolls);
            SetArray(cpuSo.FindProperty("scoreTexts"), cpu.Scores);
            cpuSo.FindProperty("totalText").objectReferenceValue = cpu.Total;
            cpuSo.ApplyModifiedPropertiesWithoutUndo();
            return board;
        }

        /// <summary>
        /// 大型スコア板の1段:名前の札(色と文字)、各フレームの投球記号と累計、合計。
        /// </summary>
        private static ScoreRowTexts BuildScoreRow(Transform parent, string name, string label, Material tagMaterial, float rollY,
            float scoreY, float labelX, float firstCellX, float cellWidth, float totalX, float totalWidth, float textZ,
            BowlingBuildPalette palette)
        {
            float rowCenterY = (rollY + scoreY) / 2f;
            Box(parent, $"{name}の札", new Vector3(labelX, rowCenterY, textZ + 0.01f), new Vector3(0.36f, 0.44f, 0.01f),
                tagMaterial, null);
            Text(parent, $"{name}の名前", label, new Vector3(labelX, rowCenterY, textZ - 0.005f), 0.36f, 1.3f, palette.TextColor);

            var row = new ScoreRowTexts
            {
                Rolls = new TMP_Text[BowlingScoreSheet.FrameCount],
                Scores = new TMP_Text[BowlingScoreSheet.FrameCount],
            };
            for (int f = 0; f < BowlingScoreSheet.FrameCount; f++)
            {
                float x = firstCellX + cellWidth * f;
                row.Rolls[f] = Text(parent, $"{name}の投球{f + 1}", string.Empty, new Vector3(x, rollY, textZ), cellWidth, 1.7f,
                    palette.TextColor);
                row.Scores[f] = Text(parent, $"{name}の累計{f + 1}", string.Empty, new Vector3(x, scoreY, textZ), cellWidth, 1.7f,
                    palette.TextColor);
            }

            row.Total = Text(parent, $"{name}の合計", "0", new Vector3(totalX, rowCenterY, textZ), totalWidth, 2.2f,
                palette.HighlightColor);
            return row;
        }

        /// <summary>
        /// 最小の角と最大の角を指定して箱を作る。
        /// </summary>
        private static GameObject Block(Transform parent, string name, Vector3 min, Vector3 max, Material material,
            PhysicsMaterial physics)
        {
            return Box(parent, name, (min + max) / 2f, max - min, material, physics);
        }

        /// <summary>
        /// 動かない箱。Static にし、焼き込みまでの間は影を落とさない(影はボールとピンだけ)。
        /// </summary>
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material,
            PhysicsMaterial physics)
        {
            GameObject box = SceneBuildUtility.CreatePrimitive(name, PrimitiveType.Cube, position, size, material, true);
            if (physics != null)
            {
                box.GetComponent<Collider>().sharedMaterial = physics;
            }

            MakeStatic(box, parent);
            return box;
        }

        private static void MakeStatic(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, true);
            go.isStatic = true;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// 3D の文字(TextMeshPro)。fontSize はおよそ 10 で高さ 1m になる。
        /// </summary>
        private static TMP_Text Text(Transform parent, string name, string value, Vector3 position, float width,
            float fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var text = go.AddComponent<TextMeshPro>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(width, fontSize * 0.15f);
            return text;
        }

        private static void SetArray(SerializedProperty property, TMP_Text[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        /// <summary>
        /// 縁取り付きの文字のマテリアル。既定のフォントのマテリアルを複製し、縁取りを有効にする。
        /// </summary>
        private static Material GetOrCreateOutlineMaterial()
        {
            string path = $"{MaterialFolder}/B_TextOutline.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null || font.material == null)
            {
                return null;
            }

            var material = new Material(font.material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.2f);
            material.SetColor("_OutlineColor", new Color32(0x3A, 0x35, 0x50, 0xFF));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static PhysicsMaterial GetOrCreatePhysicsMaterial(string name, float friction, float bounciness)
        {
            SceneBuildUtility.EnsureFolder(PhysicsFolder);
            string path = $"{PhysicsFolder}/PM_{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new PhysicsMaterial(name)
            {
                staticFriction = friction,
                dynamicFriction = friction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
