using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 人型のモデル(girl.vrm など)の腕を、手の目標(仮人形の丸い手)へ向けて動かす(docs/plan.md M3「CPU の体」)。
    /// アニメーション素材は使わず、Animator の人型のボーンを2本の骨の IK で毎フレーム曲げるだけ。
    /// モデルのファイルやマテリアルは変えない(改変禁止のため。動かすのは実行中のボーンの向きだけ)。
    /// 体の移動・跳ねは、モデルを仮人形の体の子にすることで付いて行く。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HumanoidPuppet : MonoBehaviour
    {
        [Tooltip("モデルの Animator(人型のアバターが入っているもの)")]
        [SerializeField] private Animator animator;

        [Tooltip("右手の目標")]
        [SerializeField] private Transform rightTarget;

        [Tooltip("左手の目標")]
        [SerializeField] private Transform leftTarget;

        [Tooltip("ひじを曲げる向きの目安(体から見て後ろ・下)")]
        [SerializeField] private Vector3 elbowHint = new Vector3(0f, -1f, -1f);

        [Tooltip("頭(失点でうつむく動きを写す。空なら写さない)")]
        [SerializeField] private Transform headSource;

        private Transform rightUpper;
        private Transform rightLower;
        private Transform rightHand;
        private Transform leftUpper;
        private Transform leftLower;
        private Transform leftHand;
        private Transform head;
        private Quaternion headRest;
        private Quaternion headSourceRest;

        private void Awake()
        {
            if (animator == null || !animator.isHuman)
            {
                enabled = false;
                return;
            }

            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                headRest = head.localRotation;
            }

            if (headSource != null)
            {
                headSourceRest = headSource.localRotation;
            }
        }

        private void LateUpdate()
        {
            Solve(rightUpper, rightLower, rightHand, rightTarget);
            Solve(leftUpper, leftLower, leftHand, leftTarget);
            if (head != null && headSource != null)
            {
                head.localRotation = headRest * (Quaternion.Inverse(headSourceRest) * headSource.localRotation);
            }
        }

        /// <summary>2本の骨(上腕・前腕)を、手先が目標に届くように曲げる。届かなければまっすぐ伸ばして向ける。</summary>
        private void Solve(Transform upper, Transform lower, Transform end, Transform target)
        {
            if (upper == null || lower == null || end == null || target == null)
            {
                return;
            }

            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            Vector3 t = target.position;
            float ab = Vector3.Distance(a, b);
            float bc = Vector3.Distance(b, c);
            float at = Mathf.Clamp(Vector3.Distance(a, t), 0.01f, ab + bc - 0.001f);

            // ひじの曲がる面を決める向き
            Vector3 toTarget = (t - a).normalized;
            Vector3 hint = transform.TransformDirection(elbowHint).normalized;
            Vector3 bendNormal = Vector3.Cross(toTarget, hint);
            if (bendNormal.sqrMagnitude < 0.0001f)
            {
                bendNormal = Vector3.Cross(toTarget, transform.up);
            }

            bendNormal.Normalize();

            // 余弦定理で上腕の角度を求め、ひじの位置を決める
            float cosA = Mathf.Clamp((ab * ab + at * at - bc * bc) / (2f * ab * at), -1f, 1f);
            float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;
            // bendNormal = toTarget × hint のまわりに +角度で回すと、ひじはヒントの側(下・後ろ)へ出る
            Vector3 elbowDir = Quaternion.AngleAxis(angleA, bendNormal) * toTarget;
            Vector3 elbow = a + elbowDir * ab;

            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            b = lower.position;
            c = end.position;
            lower.rotation = Quaternion.FromToRotation(c - b, t - b) * lower.rotation;
        }
    }
}
