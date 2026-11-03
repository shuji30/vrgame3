using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// CPU の体を、ユーザーが用意した人型のモデル(girl.vrm)に差し替えるエディタ処理(docs/plan.md M3「CPU の体」)。
    /// 卓球場・ボウリング場を作るときに、モデルがあれば自動で差し替える。メニュー「VRGame3/CPU の体を VRM にする」でも、
    /// 今開いているシーンの「CPU」に対して行える。
    /// 前提:UniVRM を入れ、girl.vrm を Assets/ThirdParty/UserModels/girl/ に置いて取り込んである。
    /// モデルは改変しない。仮人形の見た目を隠し、モデルを体の子にして、HumanoidPuppet で腕を手の目標へ動かすだけ。
    /// 名札・ラケット・強さの色は仮人形のものをそのまま使う(モデルの外で見分ける)。
    /// </summary>
    public static class VrmBodyInstaller
    {
        private const string ModelFolder = "Assets/ThirdParty/UserModels/girl";

        [MenuItem("VRGame3/CPU の体を VRM にする")]
        public static void Install()
        {
            GameObject cpu = GameObject.Find("CPU");
            if (cpu == null)
            {
                EditorUtility.DisplayDialog("CPU の体", "開いているシーンに「CPU」がありません。卓球場かボウリング場のシーンを開いてください。", "OK");
                return;
            }

            if (!InstallOn(cpu))
            {
                EditorUtility.DisplayDialog("CPU の体",
                    $"{ModelFolder} に人型のモデル(取り込み済みの .vrm)が見つかりません。\n" +
                    "UniVRM を入れてから girl.vrm をこのフォルダに置き、取り込みが終わってからもう一度実行してください。", "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(cpu.scene);
            EditorUtility.DisplayDialog("CPU の体",
                "CPU の体をモデルに差し替えました。シーンを保存してください。\n" +
                "VR で両目に正しく映るか、fps が足りるかを確かめ、だめなら「〜を作る」で作り直すと仮人形に戻ります" +
                "(モデルを別のフォルダへ移してから作り直してください)。", "OK");
        }

        /// <summary>モデルがあれば cpu の体を差し替えて true。無ければ何もせず false(仮人形のまま)。</summary>
        public static bool InstallOn(GameObject cpu)
        {
            GameObject model = FindModel();
            if (model == null)
            {
                return false;
            }

            Transform old = cpu.transform.Find("VRM の体");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
            {
                instance = Object.Instantiate(model);
            }

            instance.name = "VRM の体";
            instance.transform.SetParent(cpu.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // 仮人形の見た目を隠す(名札・ラケットは残す)
            foreach (string part in new[] { "胴", "胴の帯", "頭", "右手", "左手" })
            {
                Transform t = cpu.transform.Find(part);
                if (t == null)
                {
                    continue;
                }

                foreach (Renderer r in t.GetComponents<Renderer>())
                {
                    r.enabled = false;
                }

                if (part == "頭")
                {
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                    {
                        r.enabled = false;
                    }
                }
            }

            Animator animator = instance.GetComponentInChildren<Animator>();
            var puppet = instance.AddComponent<HumanoidPuppet>();
            var so = new SerializedObject(puppet);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("rightTarget").objectReferenceValue = cpu.transform.Find("右手");
            so.FindProperty("leftTarget").objectReferenceValue = cpu.transform.Find("左手");
            so.FindProperty("headSource").objectReferenceValue = cpu.transform.Find("頭");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 手に持つ物(卓球の CPU のラケット)を、女の子の右手の骨に持たせる(仮人形の手は IK の目標として隠したまま)
            Transform dollHand = cpu.transform.Find("右手");
            Transform handBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (dollHand != null && handBone != null)
            {
                foreach (Transform held in dollHand.Cast<Transform>().ToArray())
                {
                    Quaternion rotation = held.rotation;
                    held.SetParent(handBone, false);
                    held.position = handBone.position;
                    held.rotation = rotation;
                    float s = Mathf.Max(0.0001f, handBone.lossyScale.x);
                    held.localScale = Vector3.one / s;
                }
            }

            // 名札を頭のボーンの上 0.3m へ
            Transform tag = cpu.transform.Find("名札");
            if (animator != null && animator.isHuman && tag != null)
            {
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    tag.position = new Vector3(tag.position.x, headBone.position.y + 0.3f, tag.position.z);
                }
            }

            return true;
        }

        /// <summary>フォルダの中の、取り込み済みの人型のモデル(.vrm などのメインのアセット)。</summary>
        private static GameObject FindModel()
        {
            if (!AssetDatabase.IsValidFolder(ModelFolder))
            {
                return null;
            }

            // .vrm は ScriptedImporter で取り込まれ、メインのアセットが GameObject になる。
            // FindAssets の型の絞り込みに頼らず、フォルダの中のファイルを全部見て、Animator の付いたものを使う
            return AssetDatabase.FindAssets(string.Empty, new[] { ModelFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Select(path => AssetDatabase.LoadMainAssetAtPath(path) as GameObject)
                .FirstOrDefault(go => go != null && go.GetComponentInChildren<Animator>(true) != null);
        }
    }
}
