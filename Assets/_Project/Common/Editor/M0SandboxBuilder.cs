using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
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
        private const string StarterAssetsFolder = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets";
        private const string XROriginPrefabPath = StarterAssetsFolder + "/Prefabs/XR Origin (XR Rig).prefab";
        private const string InputActionsPath = StarterAssetsFolder + "/XRI Default Input Actions.inputactions";

        // サンプルの XR Origin に入っている移動方式のうち、自前のトラックパッド操作とぶつかるもの
        private static readonly string[] LocomotionObjectsToDisable =
        {
            "Move", "Turn", "Teleportation", "Grab Move", "Climb", "Gravity", "Teleport Interactor",
        };

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

            EnsureFolder("Assets/_Project/Common/Scenes");
            EnsureFolder(MaterialFolder);

            // パレット(docs/art-direction.md の 2.3)から仮の色を選ぶ
            Material sand = GetOrCreateMaterial("M0_Sand", "#F2D49B", false);
            Material wood = GetOrCreateMaterial("M0_Wood", "#C98B55", false);
            Material woodLight = GetOrCreateMaterial("M0_WoodLight", "#F0C995", false);
            Material ball = GetOrCreateMaterial("M0_Ball", "#2FB5B0", false);
            Material target = GetOrCreateMaterial("M0_Target", "#FFD24A", true);
            Material hit = GetOrCreateMaterial("M0_Hit", "#3AA7FF", true);
            Material miss = GetOrCreateMaterial("M0_Miss", "#E0525E", true);
            Material arc = GetOrCreateArcMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 既定のカメラは XR Origin のカメラと重なるので消す
            GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            CreatePrimitive("床", PrimitiveType.Plane, new Vector3(0f, 0f, 0f), new Vector3(4f, 1f, 4f), sand, true);
            CreatePrimitive("奥の壁(すり抜け確認用)", PrimitiveType.Cube, new Vector3(0f, 1.5f, 14f),
                new Vector3(12f, 3f, 0.5f), wood, true);
            CreatePrimitive("ボール置き台", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.6f),
                new Vector3(1.2f, 0.8f, 0.5f), woodLight, true);

            // 目標:直径 = 許容幅×2 の円盤。円盤からはみ出さなければ命中の目安になる
            GameObject targetObject = CreatePrimitive("目標", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 8f),
                new Vector3(LateralTolerance * 2f, 0.005f, LateralTolerance * 2f), target, false);

            var throwables = new List<ThrowableObject>();
            for (int i = 0; i < 3; i++)
            {
                GameObject ballObject = CreatePrimitive($"ボール{i + 1}", PrimitiveType.Sphere,
                    new Vector3(-0.3f + 0.3f * i, 0.92f, 0.6f), Vector3.one * 0.22f, ball, true);
                ballObject.AddComponent<XRGrabInteractable>();
                ballObject.GetComponent<Rigidbody>().mass = 2f;
                throwables.Add(ballObject.AddComponent<ThrowableObject>());
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

            List<string> warnings = SetUpXROrigin(arc, hit);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

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

        /// <summary>
        /// サンプルの XR Origin を置き、トリガーで掴む設定とトラックパッドの移動に差し替える。
        /// うまくいかなかった点を返す。
        /// </summary>
        private static List<string> SetUpXROrigin(Material arcMaterial, Material reticleMaterial)
        {
            var warnings = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
            if (prefab == null)
            {
                warnings.Add($"XR Origin のプレハブが見つかりません({XROriginPrefabPath})。XRI の Starter Assets を入れ直してください");
                return warnings;
            }

            var origin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform[] all = origin.GetComponentsInChildren<Transform>(true);

            // 1. ぶつかる移動方式を止める
            foreach (Transform t in all.Where(t => LocomotionObjectsToDisable.Contains(t.name)))
            {
                t.gameObject.SetActive(false);
            }

            // 2. サンプルの入力切り替え(スティックでテレポートに入る仕組み)を止める
            foreach (MonoBehaviour behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "ControllerInputActionManager")
                {
                    behaviour.enabled = false;
                }
            }

            // 3. 掴む操作をグリップからトリガーへ差し替える
            InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>().ToArray();
            int remapped = 0;
            foreach (MonoBehaviour behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "NearFarInteractor")
                {
                    continue;
                }

                string map = IsUnder(behaviour.transform, "Left") ? "XRI Left Interaction" : "XRI Right Interaction";
                InputActionReference performed = FindReference(references, map, "Activate");
                InputActionReference value = FindReference(references, map, "Activate Value");
                var so = new SerializedObject(behaviour);
                SerializedProperty performedProperty = so.FindProperty("m_SelectInput.m_InputActionReferencePerformed");
                SerializedProperty valueProperty = so.FindProperty("m_SelectInput.m_InputActionReferenceValue");
                if (performed == null || value == null || performedProperty == null || valueProperty == null)
                {
                    continue;
                }

                performedProperty.objectReferenceValue = performed;
                valueProperty.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                remapped++;
            }

            if (remapped < 2)
            {
                warnings.Add("Near-Far Interactor の Select Input をトリガー(Activate / Activate Value)へ差し替えられませんでした。手順書の手動設定を行ってください");
            }

            // 4. 左右のコントローラーにトラックパッド操作を付ける
            var xrOrigin = origin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            foreach (string controllerName in new[] { "Left Controller", "Right Controller" })
            {
                Transform controller = all.FirstOrDefault(t => t.name == controllerName);
                if (controller == null)
                {
                    warnings.Add($"{controllerName} が見つかりません。TrackpadLocomotion を手で付けてください");
                    continue;
                }

                AddTrackpadLocomotion(controller, xrOrigin, controllerName.StartsWith("Left"), arcMaterial, reticleMaterial);
            }

            return warnings;
        }

        private static void AddTrackpadLocomotion(Transform controller, Unity.XR.CoreUtils.XROrigin xrOrigin,
            bool isLeft, Material arcMaterial, Material reticleMaterial)
        {
            var arcObject = new GameObject("テレポートの放物線");
            arcObject.transform.SetParent(controller, false);
            var line = arcObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.012f;
            line.positionCount = 0;
            line.sharedMaterial = arcMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "テレポートの着地点";
            Object.DestroyImmediate(reticle.GetComponent<Collider>());
            reticle.transform.SetParent(controller, false);
            reticle.transform.localScale = new Vector3(0.4f, 0.005f, 0.4f);
            var reticleRenderer = reticle.GetComponent<Renderer>();
            reticleRenderer.sharedMaterial = reticleMaterial;
            reticleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            reticle.SetActive(false);

            var locomotion = controller.gameObject.AddComponent<TrackpadLocomotion>();
            var so = new SerializedObject(locomotion);
            so.FindProperty("hand").enumValueIndex = isLeft ? 0 : 1;
            so.FindProperty("xrOrigin").objectReferenceValue = xrOrigin;
            so.FindProperty("arcLine").objectReferenceValue = line;
            so.FindProperty("reticle").objectReferenceValue = reticle.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputActionReference FindReference(InputActionReference[] references, string map, string action)
        {
            return references.FirstOrDefault(r =>
                r != null && r.action != null && r.action.name == action &&
                r.action.actionMap != null && r.action.actionMap.name == map);
        }

        private static bool IsUnder(Transform t, string keyword)
        {
            for (Transform current = t; current != null; current = current.parent)
            {
                if (current.name.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            return go;
        }

        private static Material GetOrCreateMaterial(string name, string hex, bool unlit)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var material = new Material(shader);
            ColorUtility.TryParseHtmlString(hex, out Color color);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 放物線用。LineRenderer の頂点色(移動できる/できないの色)が出るシェーダーを使う。
        /// </summary>
        private static Material GetOrCreateArcMaterial()
        {
            string path = $"{MaterialFolder}/M0_TeleportArc.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
            {
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
