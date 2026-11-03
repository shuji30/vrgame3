using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 人型のモデル(girl.vrm)の表情(docs/art-direction.md「M3 CPU の体」の反応)。モデルの顔のブレンドシェイプを、
    /// 名前の手がかり(Joy / Fun / Sorrow / Blink など。VRoid のモデルの名前にも合う)で探して動かす。
    /// ふだんは少し笑った顔でときどきまばたきし、得点で笑顔、失点で悲しい顔にする。モデルのファイルは変えない。
    /// UniVRM の表情の処理が同じ値を書くことがあるので、それより後(LateUpdate の遅い順)で書く。
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class HumanoidFace : MonoBehaviour
    {
        /// <summary>表情の種類。</summary>
        public enum Mood
        {
            Calm,
            Happy,
            Sad,
        }

        [Tooltip("ふだんの笑顔の強さ(0〜100)")]
        [Range(0f, 100f)]
        [SerializeField] private float calmSmile = 35f;

        [Tooltip("まばたきの間隔(最小, 最大)(秒)")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 5.5f);

        [Tooltip("得点・失点の表情を出しておく秒数")]
        [SerializeField] private float moodTime = 1.2f;

        private static readonly string[] HappyKeys = { "joy", "happy", "fun", "smile" };
        private static readonly string[] SadKeys = { "sorrow", "sad" };
        private static readonly string[] BlinkKeys = { "blink", "eye_close", "eyeclose", "close" };

        private readonly List<(SkinnedMeshRenderer mesh, int index)> happy = new List<(SkinnedMeshRenderer, int)>();
        private readonly List<(SkinnedMeshRenderer mesh, int index)> sad = new List<(SkinnedMeshRenderer, int)>();
        private readonly List<(SkinnedMeshRenderer mesh, int index)> blink = new List<(SkinnedMeshRenderer, int)>();

        private Mood mood = Mood.Calm;
        private float moodUntil;
        private float nextBlink;
        private float blinkStart = -1f;
        private float happyWeight;
        private float sadWeight;

        private void Awake()
        {
            foreach (SkinnedMeshRenderer mesh in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh shared = mesh.sharedMesh;
                if (shared == null)
                {
                    continue;
                }

                for (int i = 0; i < shared.blendShapeCount; i++)
                {
                    string name = shared.GetBlendShapeName(i).ToLowerInvariant();
                    // 口だけ・目だけの部分の形より、顔全体(ALL)の形を優先して使う
                    bool all = name.Contains("all") || !name.Contains("_");
                    if (all && Contains(name, HappyKeys) && happy.Count < 1)
                    {
                        happy.Add((mesh, i));
                    }
                    else if (all && Contains(name, SadKeys) && sad.Count < 1)
                    {
                        sad.Add((mesh, i));
                    }
                    else if (Contains(name, BlinkKeys) && blink.Count < 1)
                    {
                        blink.Add((mesh, i));
                    }
                }
            }

            nextBlink = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
        }

        /// <summary>表情を少しのあいだ変える(得点 Happy、失点 Sad)。</summary>
        public void Show(Mood value)
        {
            mood = value;
            moodUntil = Time.time + moodTime;
        }

        private void LateUpdate()
        {
            if (mood != Mood.Calm && Time.time >= moodUntil)
            {
                mood = Mood.Calm;
            }

            float dt = Time.deltaTime;
            float happyGoal = mood == Mood.Happy ? 100f : mood == Mood.Sad ? 0f : calmSmile;
            float sadGoal = mood == Mood.Sad ? 100f : 0f;
            happyWeight = Mathf.MoveTowards(happyWeight, happyGoal, dt * 400f);
            sadWeight = Mathf.MoveTowards(sadWeight, sadGoal, dt * 400f);
            Set(happy, happyWeight);
            Set(sad, sadWeight);

            // まばたき(0.15 秒で閉じて開く。笑顔のときは目が細いので浅く)
            if (Time.time >= nextBlink)
            {
                blinkStart = Time.time;
                nextBlink = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
            }

            float closed = 0f;
            if (blinkStart >= 0f)
            {
                float t = (Time.time - blinkStart) / 0.15f;
                closed = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * 100f * (1f - happyWeight / 150f);
                if (t >= 1f)
                {
                    blinkStart = -1f;
                }
            }

            Set(blink, closed);
        }

        private static void Set(List<(SkinnedMeshRenderer mesh, int index)> shapes, float weight)
        {
            foreach ((SkinnedMeshRenderer mesh, int index) in shapes)
            {
                if (mesh != null)
                {
                    mesh.SetBlendShapeWeight(index, weight);
                }
            }
        }

        private static bool Contains(string name, string[] keys)
        {
            foreach (string key in keys)
            {
                if (name.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
