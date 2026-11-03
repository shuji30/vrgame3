using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// メニュー「VRGame3 > M0 テスト場を作る」で、M0 のゲート確認用のシーンを組み立てる。
    /// 床・壁・目標・台・ボール3個と、設定を済ませた XR Origin を配置する。
    /// </summary>
    public static class M0SandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Common/Scenes/M0_Sandbox.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/M0";

        // 目標の左右の許容幅(m)。ThrowAccuracyTest の既定値と合わせる
        private const float LateralTolerance = 1f;

        [MenuItem("VRGame3/M0 テスト場を作る")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("M0 テスト場", $"{ScenePath} はすでにあります。作り直しますか?", "作り直す", "やめる"))
            {
                return;
            }

            SceneBuildUtility.EnsureFolder("Assets/_Project/Common/Scenes");
            SceneBuildUtility.EnsureFolder(MaterialFolder);

            // パレット(docs/art-direction.md の 2.3)から仮の色を選ぶ
            Material sand = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_Sand", "#F2D49B", false);
            Material wood = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_Wood", "#C98B55", false);
            Material woodLight = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_WoodLight", "#F0C995", false);
            Material ball = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_Ball", "#2FB5B0", false);
            Material target = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_Target", "#FFD24A", true);
            Material hit = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_Hit", "#3AA7FF", true);
            Material miss = SceneBuildUtility.GetOrCreateMaterial(MaterialFolder, "M0_Miss", "#E0525E", true);
            Material arc = SceneBuildUtility.GetOrCreateArcMaterial(MaterialFolder, "M0_TeleportArc");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 既定のカメラは XR Origin のカメラと重なるので消す
            GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            SceneBuildUtility.CreatePrimitive("床", PrimitiveType.Plane, new Vector3(0f, 0f, 0f), new Vector3(4f, 1f, 4f), sand, true);
            SceneBuildUtility.CreatePrimitive("奥の壁(すり抜け確認用)", PrimitiveType.Cube, new Vector3(0f, 1.5f, 14f),
                new Vector3(12f, 3f, 0.5f), wood, true);
            SceneBuildUtility.CreatePrimitive("ボール置き台", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.6f),
                new Vector3(1.2f, 0.8f, 0.5f), woodLight, true);

            // 目標:直径 = 許容幅×2 の円盤。円盤からはみ出さなければ命中の目安になる
            GameObject targetObject = SceneBuildUtility.CreatePrimitive("目標", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 8f),
                new Vector3(LateralTolerance * 2f, 0.005f, LateralTolerance * 2f), target, false);

            var throwables = new List<ThrowableObject>();
            for (int i = 0; i < 3; i++)
            {
                GameObject ballObject = SceneBuildUtility.CreatePrimitive($"ボール{i + 1}", PrimitiveType.Sphere,
                    new Vector3(-0.3f + 0.3f * i, 0.92f, 0.6f), Vector3.one * 0.22f, ball, true);
                ballObject.AddComponent<XRGrabInteractable>();
                ballObject.GetComponent<Rigidbody>().mass = 2f;
                throwables.Add(ballObject.AddComponent<ThrowableObject>());
                ballObject.AddComponent<ReturnToHome>();
            }

            var test = targetObject.AddComponent<ThrowAccuracyTest>();
            var testSo = new SerializedObject(test);
            SerializedProperty list = testSo.FindProperty("throwables");
            list.arraySize = throwables.Count;
            for (int i = 0; i < throwables.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = throwables[i];
            }

            testSo.FindProperty("lateralTolerance").floatValue = LateralTolerance;
            testSo.FindProperty("targetRenderer").objectReferenceValue = targetObject.GetComponent<Renderer>();
            testSo.FindProperty("hitMaterial").objectReferenceValue = hit;
            testSo.FindProperty("missMaterial").objectReferenceValue = miss;
            testSo.ApplyModifiedPropertiesWithoutUndo();

            List<string> warnings = SceneBuildUtility.SetUpXROrigin(arc, hit, Vector3.zero, Quaternion.identity);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuildSettings(ScenePath);

            string message = $"{ScenePath} を作りました。";
            if (warnings.Count > 0)
            {
                message += "\n\n手で確認が必要な点:\n- " + string.Join("\n- ", warnings);
                foreach (string warning in warnings)
                {
                    Debug.LogWarning($"[M0 テスト場] {warning}");
                }
            }

            Debug.Log($"[M0 テスト場] {message}");
            EditorUtility.DisplayDialog("M0 テスト場", message, "OK");
        }
    }
}
