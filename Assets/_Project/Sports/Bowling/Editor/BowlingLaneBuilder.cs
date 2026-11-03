using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRGame3.Common;
using VRGame3.Common.EditorTools;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// メニュー「VRGame3 > ボウリング場を作る」で、M1 の1レーンを仮の形状で組み立てる。
    /// レーン・ガター・ピット・ピン10本・ボール置き台とボール3個・スコア板・XR Origin を配置する。
    /// 寸法は本物のレーンに合わせる(ファウルラインから先頭のピンまで 18.29m)。
    /// 見た目は docs/art-direction.md のパレットから選んだ仮の色で、モデルは後で差し替える。
    /// </summary>
    public static class BowlingLaneBuilder
    {
        private const string SceneFolder = "Assets/_Project/Sports/Bowling/Scenes";
        private const string ScenePath = SceneFolder + "/Bowling.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Bowling";
        private const string PhysicsFolder = "Assets/_Project/Sports/Bowling/Physics";

        // レーンの寸法(m)。Z+ がピンの方向、ファウルラインが Z=0
        private const float LaneWidth = 1.066f;
        private const float GutterWidth = 0.24f;
        private const float GutterDepth = 0.08f;
        private const float HeadPinDistance = 18.29f;
        private const float LaneEnd = 19.3f;
        private const float PitEnd = 21.5f;
        private const float PitDepth = 0.6f;
        private const float SideBoardThickness = 0.05f;
        private const float PinSpacing = 0.305f;

        // ピンの寸法(本物はおよそ高さ 38cm、一番太い所で直径 12cm、1.5kg)
        private const float PinHeight = 0.38f;
        private const float PinRadius = 0.06f;
        private const float PinMass = 1.5f;

        // ボールの寸法(本物は直径 21.8cm)
        private const float BallDiameter = 0.218f;
        private const float BallMass = 6f;

        // プレイヤーとボール置き台の位置
        private static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, -3f);
        private static readonly Vector3 RackPosition = new Vector3(0.75f, 0f, -2.3f);
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
            var palette = new BowlingBuildPalette(MaterialFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            PhysicsMaterial lanePhysics = GetOrCreatePhysicsMaterial("Lane", 0.04f, 0f);
            PhysicsMaterial pinPhysics = GetOrCreatePhysicsMaterial("Pin", 0.35f, 0.3f);
            PhysicsMaterial ballPhysics = GetOrCreatePhysicsMaterial("Ball", 0.04f, 0.05f);
            PhysicsMaterial cushionPhysics = GetOrCreatePhysicsMaterial("Cushion", 0.6f, 0f);

            var root = new GameObject("ボウリング場");
            BuildLane(root.transform, palette, lanePhysics, cushionPhysics);
            Transform foulLine = BuildFoulLine(root.transform, palette);
            PinSetter pinSetter = BuildPins(root.transform, palette, pinPhysics);
            List<ThrowableObject> balls = BuildBallRack(root.transform, palette, ballPhysics);
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
            gameSo.ApplyModifiedPropertiesWithoutUndo();

            Material arc = SceneBuildUtility.GetOrCreateArcMaterial(MaterialFolder, "B_TeleportArc");
            List<string> warnings = SceneBuildUtility.SetUpXROrigin(arc, palette.Reticle, PlayerPosition, Quaternion.identity);

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
        /// 床・レーン・ガター・横の板・ピット・奥のクッションを作る。
        /// レーンとガターは下まで厚みを持たせ、ピットの穴が見えても宙に浮いて見えないようにする。
        /// </summary>
        private static void BuildLane(Transform parent, BowlingBuildPalette palette, PhysicsMaterial lanePhysics,
            PhysicsMaterial cushionPhysics)
        {
            float halfLane = LaneWidth / 2f;
            float gutterOuter = halfLane + GutterWidth;
            float boardCenter = gutterOuter + SideBoardThickness / 2f;
            float sideFloorInner = gutterOuter + SideBoardThickness;
            const float sideFloorWidth = 6f;
            const float bottom = -PitDepth - 0.1f;

            // 投げる側の床(ファウルラインより手前)
            Box(parent, "助走路", new Vector3(0f, -0.05f, -3f), new Vector3(sideFloorInner * 2f + sideFloorWidth * 2f, 0.1f, 6f),
                palette.WoodLight, lanePhysics);

            // 周りの砂浜(見た目だけの広い床)
            Box(parent, "砂浜", new Vector3(0f, -0.15f, -16f), new Vector3(40f, 0.1f, 20f), palette.Sand, null);

            // レーン本体
            Box(parent, "レーン", new Vector3(0f, bottom / 2f, LaneEnd / 2f), new Vector3(LaneWidth, -bottom, LaneEnd),
                palette.WoodLight, lanePhysics);

            for (int side = -1; side <= 1; side += 2)
            {
                string label = side < 0 ? "左" : "右";
                float gutterTop = -GutterDepth;
                Box(parent, $"ガター({label})", new Vector3(side * (halfLane + GutterWidth / 2f), (bottom + gutterTop) / 2f, LaneEnd / 2f),
                    new Vector3(GutterWidth, gutterTop - bottom, LaneEnd), palette.Stone, lanePhysics);

                // 横の板:ピンの手前までは低く、ピン置き場の横(キックバック)は高く
                Box(parent, $"横の板({label})", new Vector3(side * boardCenter, (bottom + 0.15f) / 2f, 17f / 2f),
                    new Vector3(SideBoardThickness, 0.15f - bottom, 17f), palette.Wood, lanePhysics);
                Box(parent, $"キックバック({label})", new Vector3(side * boardCenter, (bottom + 0.7f) / 2f, (17f + PitEnd) / 2f),
                    new Vector3(SideBoardThickness, 0.7f - bottom, PitEnd - 17f), palette.Wood, cushionPhysics);

                // レーンの両脇の床
                Box(parent, $"レーン脇の床({label})", new Vector3(side * (sideFloorInner + sideFloorWidth / 2f), -0.05f, PitEnd / 2f),
                    new Vector3(sideFloorWidth, 0.1f, PitEnd), palette.Sand, null);
            }

            // ピット:レーンの先の一段低い所。倒れたピンとボールがここに落ちる
            Box(parent, "ピットの底", new Vector3(0f, -PitDepth - 0.05f, (LaneEnd + PitEnd) / 2f),
                new Vector3(gutterOuter * 2f, 0.1f, PitEnd - LaneEnd), palette.Stone, cushionPhysics);
            Box(parent, "奥のクッション", new Vector3(0f, (bottom + 1.2f) / 2f, PitEnd + 0.05f),
                new Vector3(sideFloorInner * 2f, 1.2f - bottom, 0.1f), palette.Accent, cushionPhysics);

            // 奥の壁と屋根の代わりの梁(レーンの終わりを分かりやすくする)
            Box(parent, "奥の壁", new Vector3(0f, 1.5f, PitEnd + 0.3f), new Vector3(sideFloorInner * 2f + 4f, 4.4f, 0.4f),
                palette.Wood, null);
        }

        private static Transform BuildFoulLine(Transform parent, BowlingBuildPalette palette)
        {
            GameObject line = SceneBuildUtility.CreatePrimitive("ファウルライン", PrimitiveType.Cube, new Vector3(0f, 0.001f, 0f),
                new Vector3(LaneWidth + GutterWidth * 2f, 0.002f, 0.02f), palette.Line, false);
            line.transform.SetParent(parent, true);
            return line.transform;
        }

        /// <summary>
        /// ピン10本を正三角形に並べる。先頭のピンがファウルラインから 18.29m、隣のピンとの間は 30.5cm。
        /// </summary>
        private static PinSetter BuildPins(Transform parent, BowlingBuildPalette palette, PhysicsMaterial pinPhysics)
        {
            var setterObject = new GameObject("ピンセッター");
            setterObject.transform.SetParent(parent, false);
            setterObject.transform.position = new Vector3(0f, 0f, HeadPinDistance);
            var setter = setterObject.AddComponent<PinSetter>();

            float rowDepth = PinSpacing * Mathf.Sqrt(3f) / 2f;
            var pins = new List<BowlingPin>();
            int number = 1;
            for (int row = 0; row < 4; row++)
            {
                for (int i = 0; i <= row; i++)
                {
                    float x = (i - row / 2f) * PinSpacing;
                    Vector3 position = new Vector3(x, 0.001f, HeadPinDistance + row * rowDepth);
                    pins.Add(BuildPin(setterObject.transform, $"ピン{number}", position, palette.Pin, pinPhysics));
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
        /// ピン1本。原点を底に置く。カプセルの当たり判定だけだと底が丸くて立たないので、
        /// 底に平らな箱の当たり判定を足す。
        /// </summary>
        private static BowlingPin BuildPin(Transform parent, string name, Vector3 position, Material material,
            PhysicsMaterial physics)
        {
            var pin = new GameObject(name);
            pin.transform.SetParent(parent, false);
            pin.transform.position = position;

            GameObject visual = SceneBuildUtility.CreatePrimitive("見た目", PrimitiveType.Capsule, Vector3.zero,
                new Vector3(PinRadius * 2f, PinHeight / 2f, PinRadius * 2f), material, false);
            visual.transform.SetParent(pin.transform, false);
            visual.transform.localPosition = new Vector3(0f, PinHeight / 2f, 0f);

            var capsule = pin.AddComponent<CapsuleCollider>();
            capsule.radius = PinRadius;
            capsule.height = PinHeight;
            capsule.center = new Vector3(0f, PinHeight / 2f, 0f);
            capsule.sharedMaterial = physics;

            var foot = pin.AddComponent<BoxCollider>();
            foot.size = new Vector3(0.06f, 0.02f, 0.06f);
            foot.center = new Vector3(0f, 0.01f, 0f);
            foot.sharedMaterial = physics;

            var body = pin.AddComponent<Rigidbody>();
            body.mass = PinMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            return pin.AddComponent<BowlingPin>();
        }

        /// <summary>
        /// ボール置き台とボール3個。台の上の2本のレールの間にボールを置いて転がり落ちないようにする。
        /// </summary>
        private static List<ThrowableObject> BuildBallRack(Transform parent, BowlingBuildPalette palette, PhysicsMaterial ballPhysics)
        {
            var rack = new GameObject("ボール置き台");
            rack.transform.SetParent(parent, false);
            rack.transform.position = RackPosition;

            Box(rack.transform, "台", RackPosition + new Vector3(0f, RackHeight / 2f, 0f), new Vector3(0.4f, RackHeight, 1.0f),
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

                var returner = ball.AddComponent<ReturnToHome>();
                var returnSo = new SerializedObject(returner);
                // 投球ごとの戻しは BowlingGame が行う。ここは落ちたときと、止まったまま放置されたときの保険
                returnSo.FindProperty("maxAwayTime").floatValue = 0f;
                returnSo.FindProperty("stopDelay").floatValue = 5f;
                returnSo.ApplyModifiedPropertiesWithoutUndo();

                balls.Add(throwable);
            }

            return balls;
        }

        /// <summary>
        /// 大型スコア板(レーンの上、目の高さより少し上)と小型スコア板(ボール置き台の横)、
        /// ストライクなどの大きな文字(ピンの手前)を作る。docs/art-direction.md の 6.1 と 6.2 に合わせる。
        /// </summary>
        private static BowlingScoreBoard BuildScoreBoards(Transform parent, BowlingBuildPalette palette)
        {
            var boardObject = new GameObject("スコア板");
            boardObject.transform.SetParent(parent, false);
            var board = boardObject.AddComponent<BowlingScoreBoard>();

            // 大型板:プレイヤーから約 5.5m 先、高さ 2.7m(仰角およそ 11°)
            const float largeZ = 2.5f;
            const float largeY = 2.7f;
            const float cellWidth = 0.36f;
            const float totalWidth = 0.6f;
            float boardWidth = cellWidth * BowlingScoreSheet.FrameCount + totalWidth + 0.1f;
            Box(boardObject.transform, "大型スコア板", new Vector3(0f, largeY, largeZ + 0.03f), new Vector3(boardWidth, 0.95f, 0.05f),
                palette.Board, null);

            var frameNumbers = new TMP_Text[BowlingScoreSheet.FrameCount];
            var rolls = new TMP_Text[BowlingScoreSheet.FrameCount];
            var scores = new TMP_Text[BowlingScoreSheet.FrameCount];
            float left = -boardWidth / 2f + 0.05f;
            for (int f = 0; f < BowlingScoreSheet.FrameCount; f++)
            {
                float x = left + cellWidth * (f + 0.5f);
                frameNumbers[f] = Text(boardObject.transform, $"フレーム番号{f + 1}", (f + 1).ToString(),
                    new Vector3(x, largeY + 0.32f, largeZ), cellWidth, 1.2f, palette.TextColor);
                rolls[f] = Text(boardObject.transform, $"投球{f + 1}", string.Empty,
                    new Vector3(x, largeY + 0.06f, largeZ), cellWidth, 1.6f, palette.TextColor);
                scores[f] = Text(boardObject.transform, $"累計{f + 1}", string.Empty,
                    new Vector3(x, largeY - 0.26f, largeZ), cellWidth, 1.8f, palette.TextColor);
            }

            float totalX = left + cellWidth * BowlingScoreSheet.FrameCount + totalWidth / 2f;
            Text(boardObject.transform, "合計の見出し", "TOTAL", new Vector3(totalX, largeY + 0.32f, largeZ), totalWidth, 1.2f,
                palette.TextColor);
            TMP_Text total = Text(boardObject.transform, "合計", "0", new Vector3(totalX, largeY - 0.1f, largeZ), totalWidth, 2.6f,
                palette.HighlightColor);

            // 小型板:ボール置き台の横、胸の高さ。プレイヤーの方を向ける
            Vector3 smallPosition = RackPosition + new Vector3(0.45f, 1.15f, 0.2f);
            Vector3 eye = PlayerPosition + new Vector3(0f, 1.6f, 0f);
            Vector3 toPlayer = eye - smallPosition;
            toPlayer.y = 0f;
            Quaternion smallRotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
            var small = new GameObject("小型スコア板");
            small.transform.SetParent(boardObject.transform, false);
            small.transform.SetPositionAndRotation(smallPosition, smallRotation);
            GameObject smallBack = Box(small.transform, "板", smallPosition, new Vector3(0.62f, 0.3f, 0.03f), palette.Board, null);
            smallBack.transform.rotation = smallRotation;
            smallBack.transform.position = smallPosition + smallRotation * new Vector3(0f, 0f, 0.02f);
            TMP_Text smallFrame = Text(small.transform, "現在のフレーム", string.Empty, Vector3.zero, 0.6f, 0.6f, palette.TextColor);
            smallFrame.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            smallFrame.transform.localRotation = Quaternion.identity;
            TMP_Text smallTotal = Text(small.transform, "合計", string.Empty, Vector3.zero, 0.6f, 0.6f, palette.HighlightColor);
            smallTotal.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            smallTotal.transform.localRotation = Quaternion.identity;

            // 大きな文字:ピンの手前の上空。プレイヤーから 10m 以上離す
            TMP_Text message = Text(boardObject.transform, "大きな文字", "STRIKE!", new Vector3(0f, 1.6f, 14f), 8f, 7f,
                palette.HighlightColor);
            message.fontStyle = FontStyles.Bold;
            message.gameObject.SetActive(false);

            var so = new SerializedObject(board);
            SetArray(so.FindProperty("frameNumberTexts"), frameNumbers);
            SetArray(so.FindProperty("rollTexts"), rolls);
            SetArray(so.FindProperty("scoreTexts"), scores);
            so.FindProperty("totalText").objectReferenceValue = total;
            so.FindProperty("smallFrameText").objectReferenceValue = smallFrame;
            so.FindProperty("smallTotalText").objectReferenceValue = smallTotal;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();
            return board;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material,
            PhysicsMaterial physics)
        {
            GameObject box = SceneBuildUtility.CreatePrimitive(name, PrimitiveType.Cube, position, size, material, true);
            box.transform.SetParent(parent, true);
            if (physics != null)
            {
                box.GetComponent<Collider>().sharedMaterial = physics;
            }

            return box;
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
