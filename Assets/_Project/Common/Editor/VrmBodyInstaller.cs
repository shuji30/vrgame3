using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRGame3.Common.EditorTools
{
    /// <summary>
    /// CPU の体を、ユーザーが用意した人型のモデル(girl.vrm)に差し替えるエディタ処理(docs/plan.md M3「CPU の体」)。
    /// メニュー「VRGame3/CPU の体を VRM にする」。今開いているシーン(卓球場かボウリング場)の「CPU」に対して行う。
    /// 前提:UniVRM を入れ、girl.vrm を Assets/ThirdParty/UserModels/girl/ に置いて取り込んである(UniVRM がプレハブを作る)。
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

            GameObject model = FindModel();
            if (model == null)
            {
                EditorUtility.DisplayDialog("CPU の体",
                    $"{ModelFolder} に人型のモデル(Animator 付きのプレハブ)が見つかりません。\n" +
                    "UniVRM を入れてから girl.vrm をこのフォルダに置き、取り込みが終わってからもう一度実行してください。", "OK");
                return;
            }

            Transform old = cpu.transform.Find("VRM の体");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
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

            var puppet = instance.AddComponent<HumanoidPuppet>();
            var so = new SerializedObject(puppet);
            so.FindProperty("animator").objectReferenceValue = instance.GetComponentInChildren<Animator>();
            so.FindProperty("rightTarget").objectReferenceValue = cpu.transform.Find("右手");
            so.FindProperty("leftTarget").objectReferenceValue = cpu.transform.Find("左手");
            so.FindProperty("headSource").objectReferenceValue = cpu.transform.Find("頭");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 名札を頭のボーンの上 0.3m へ
            Animator animator = instance.GetComponentInChildren<Animator>();
            Transform tag = cpu.transform.Find("名札");
            if (animator != null && animator.isHuman && tag != null)
            {
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    tag.position = new Vector3(tag.position.x, headBone.position.y + 0.3f, tag.position.z);
                }
            }

            EditorSceneManager.MarkSceneDirty(cpu.scene);
            EditorUtility.DisplayDialog("CPU の体",
                "CPU の体をモデルに差し替えました。シーンを保存してください。\n" +
                "VR で両目に正しく映るか、fps が足りるかを確かめ、だめなら「〜を作る」で作り直すと仮人形に戻ります。", "OK");
        }

        private static GameObject FindModel()
        {
            if (!AssetDatabase.IsValidFolder(ModelFolder))
            {
                return null;
            }

            return AssetDatabase.FindAssets("t:GameObject", new[] { ModelFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(go => go != null && go.GetComponentInChildren<Animator>() != null);
        }
    }
}
