using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// シーンを自動で組み立てるエディタ処理の共通部品。
    /// XR Origin の配置と設定、仮の形状・マテリアルの作成、ビルド設定への登録をまとめる。
    /// </summary>
    public static class SceneBuildUtility
    {
        public const string StarterAssetsFolder = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets";
        public const string XROriginPrefabPath = StarterAssetsFolder + "/Prefabs/XR Origin (XR Rig).prefab";
        public const string InputActionsPath = StarterAssetsFolder + "/XRI Default Input Actions.inputactions";

        // サンプルの XR Origin に入っている移動方式のうち、自前のトラックパッド操作とぶつかるもの
        private static readonly string[] LocomotionObjectsToDisable =
        {
            "Move", "Turn", "Teleportation", "Grab Move", "Climb", "Gravity", "Teleport Interactor",
        };

        /// <summary>
        /// サンプルの XR Origin を置き、トリガーで掴む設定とトラックパッドの移動に差し替える。
        /// うまくいかなかった点を返す。
        /// </summary>
        public static List<string> SetUpXROrigin(Material arcMaterial, Material reticleMaterial, Vector3 position, Quaternion rotation,
            bool requireTeleportArea = false)
        {
            var warnings = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
            if (prefab == null)
            {
                warnings.Add($"XR Origin のプレハブが見つかりません({XROriginPrefabPath})。XRI の Starter Assets を入れ直してください");
                return warnings;
            }

            var origin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            origin.transform.SetPositionAndRotation(position, rotation);

            // サンプルの根元の CharacterController は XRI の移動部品を止めると頭に付いて来ず、
            // プレイ範囲の中心に立つ見えない柱になる(矢や連続移動の壁の判定に当たる)ので外す
            foreach (CharacterController controller in origin.GetComponentsInChildren<CharacterController>(true))
            {
                Object.DestroyImmediate(controller);
            }
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

                AddTrackpadLocomotion(controller, xrOrigin, controllerName.StartsWith("Left"), arcMaterial, reticleMaterial,
                    requireTeleportArea);
            }

            return warnings;
        }

        private static void AddTrackpadLocomotion(Transform controller, Unity.XR.CoreUtils.XROrigin xrOrigin,
            bool isLeft, Material arcMaterial, Material reticleMaterial, bool requireTeleportArea)
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
            so.FindProperty("requireTeleportArea").boolValue = requireTeleportArea;
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

        public static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
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

        public static Material GetOrCreateMaterial(string folder, string name, string hex, bool unlit)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";
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
        public static Material GetOrCreateArcMaterial(string folder, string name)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";
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

        public static void EnsureFolder(string path)
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

        /// <summary>
        /// シーンを Build Settings に入れる。makeFirst が true なら先頭(起動して最初に入るシーン)にする。
        /// </summary>
        public static void AddSceneToBuildSettings(string path, bool makeFirst = false)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == path);
            if (index >= 0 && !makeFirst)
            {
                return;
            }

            if (index >= 0)
            {
                scenes.RemoveAt(index);
            }

            // 先頭はハブにするので、ハブ以外は2番目以降へ足す
            int insertAt = makeFirst || scenes.Count == 0 ? 0 : scenes.Count;
            scenes.Insert(insertAt, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
