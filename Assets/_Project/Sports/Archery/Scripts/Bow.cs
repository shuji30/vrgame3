using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRGame3.Common;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// 弓。利き手でない方のコントローラーに付き(トリガーを引き続けなくてよい)、利き手で弦を引いて矢を放つ。
    /// docs/plan.md「アーチェリー」の操作:
    /// ・利き手を弦の中央(つがえる点)から nockDistance 以内に近づけてトリガーを引くと、矢が現れて弦を掴む
    /// ・トリガーを引いたまま後ろへ引く(最大 maxDraw)。離すと放つ。minDraw 未満なら矢は消え、本数は減らない
    /// ・矢の速さは引いた距離に比例(minSpeed〜maxSpeed)
    /// ・ねらいの補助がオンなら、引いている間だけ道筋の最初の数 m を点線で出す
    /// 弓のローカル座標:握りの中心が原点、+Y が上、+Z が的の方。
    /// </summary>
    public class Bow : MonoBehaviour
    {
        [Header("部品")]
        [Tooltip("弦(3点:上の先端 → つがえる点 → 下の先端)")]
        [SerializeField] private LineRenderer bowString;

        [Tooltip("上の腕の先端")]
        [SerializeField] private Transform topTip;

        [Tooltip("下の腕の先端")]
        [SerializeField] private Transform bottomTip;

        [Tooltip("休んでいるときのつがえる点(弦の中央)")]
        [SerializeField] private Transform restNock;

        [Tooltip("矢の見本(非表示にしておき、つがえるたびに複製する)")]
        [SerializeField] private Arrow arrowTemplate;

        [Tooltip("ねらいの補助の点線")]
        [SerializeField] private LineRenderer aimLine;

        [Header("手に付ける位置")]
        [Tooltip("コントローラーから見た弓の位置(m)")]
        [SerializeField] private Vector3 holdPosition = new Vector3(0f, 0f, 0.03f);

        [Tooltip("コントローラーから見た弓の向き(度)。コントローラーとの角度のずれをここで直す")]
        [SerializeField] private Vector3 holdRotation = new Vector3(0f, 0f, 0f);

        [Header("引く・放つ")]
        [Tooltip("つがえられる、手と弦の中央の距離(m)")]
        [SerializeField] private float nockDistance = 0.12f;

        [Tooltip("最大の引き(m)")]
        [SerializeField] private float maxDraw = 0.6f;

        [Tooltip("これより引かずに離すと空撃ち(矢は消え、本数は減らない)(m)")]
        [SerializeField] private float minDraw = 0.1f;

        [Tooltip("最小の引きで放ったときの矢の速さ(m/秒)")]
        [SerializeField] private float minSpeed = 15f;

        [Tooltip("最大まで引いて放ったときの矢の速さ(m/秒)")]
        [SerializeField] private float maxSpeed = 45f;

        [Tooltip("矢にかける重力の倍率")]
        [SerializeField] private float gravityScale = 1f;

        [Tooltip("矢が当たる物のレイヤー")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("矢の長さ(m)。先から筈(はず)まで")]
        [SerializeField] private float arrowLength = 0.8f;

        [Header("ねらいの補助")]
        [Tooltip("点線を出す道筋の長さ(m)")]
        [SerializeField] private float aimLength = 4f;

        [Tooltip("点線の点の数")]
        [SerializeField] private int aimPoints = 20;

        [Header("振動(docs/art-direction.md 8.8)")]
        [Tooltip("引いている間の振動の強さ(最小の引き〜最大の引き)")]
        [SerializeField] private Vector2 drawHaptic = new Vector2(0.1f, 0.4f);

        [Tooltip("引いている間に振動を送る間隔(秒)")]
        [SerializeField] private float drawHapticInterval = 0.08f;

        [Tooltip("放ったときの引く手の振動(強さ, 秒)")]
        [SerializeField] private Vector2 releaseHapticDrawHand = new Vector2(0.8f, 0.06f);

        [Tooltip("放ったときの弓の手の振動(強さ, 秒)")]
        [SerializeField] private Vector2 releaseHapticBowHand = new Vector2(0.3f, 0.04f);

        [Header("音")]
        [Tooltip("弦のきしみ(ループ)を鳴らす AudioSource")]
        [SerializeField] private AudioSource drawSource;

        [Tooltip("放つ音・風切りを鳴らす AudioSource")]
        [SerializeField] private AudioSource releaseSource;

        [SerializeField] private AudioClip[] releaseClips = new AudioClip[0];
        [SerializeField] private AudioClip whooshClip;

        [Range(0f, 1f)]
        [SerializeField] private float drawVolume = 0.16f;

        [Range(0f, 1f)]
        [SerializeField] private float releaseVolume = 0.32f;

        private Transform drawHand;
        private XRBaseInputInteractor bowInteractor;
        private XRBaseInputInteractor drawInteractor;
        private InputAction drawTrigger;
        private Arrow nockedArrow;
        private float drawDistance;
        private float nextHapticTime;
        private readonly Vector3[] stringPoints = new Vector3[3];
        private Vector3[] aimBuffer = new Vector3[0];

        /// <summary>矢を放ったときに呼ばれる。矢が止まったら onStopped(game が渡す)で知らせる。</summary>
        public event Action<Arrow> Fired;

        /// <summary>新しく矢をつがえてよいか(進行役が決める)。</summary>
        public bool CanShoot { get; set; }

        /// <summary>ねらいの補助を出すか。</summary>
        public bool AimAssist { get; set; } = true;

        /// <summary>矢が止まったときに呼ぶもの(進行役が設定する)。</summary>
        public Action<Arrow, Arrow.Hit> ArrowStopped { get; set; }

        /// <summary>いま弦を引いているか。</summary>
        public bool IsDrawing => nockedArrow != null;

        /// <summary>
        /// 弓を手に付ける。bowHand は弓を持つ手(利き手でない方)、hand は矢を引く手。
        /// drawHandIsLeft は矢を引く手が左か(左利き)。
        /// </summary>
        public void Equip(Transform bowHand, Transform hand, XRBaseInputInteractor bowHandInteractor,
            XRBaseInputInteractor handInteractor, bool drawHandIsLeft)
        {
            CancelDraw();
            transform.SetParent(bowHand, false);
            transform.localPosition = holdPosition;
            transform.localRotation = Quaternion.Euler(holdRotation);
            drawHand = hand;
            bowInteractor = bowHandInteractor;
            drawInteractor = handInteractor;

            drawTrigger?.Dispose();
            // OpenXR の HTC Vive Controller Profile では、トリガーの押し込みは TriggerButton の用途として公開される
            string usage = drawHandIsLeft ? "LeftHand" : "RightHand";
            drawTrigger = new InputAction("Draw Trigger", InputActionType.Button, $"<XRController>{{{usage}}}/{{TriggerButton}}");
            drawTrigger.Enable();
            gameObject.SetActive(true);
        }

        /// <summary>弓を手から外して隠す。</summary>
        public void Unequip()
        {
            CancelDraw();
            drawTrigger?.Dispose();
            drawTrigger = null;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            drawTrigger?.Dispose();
        }

        private void Update()
        {
            if (drawHand == null || drawTrigger == null)
            {
                UpdateString(restNock.position);
                return;
            }

            if (nockedArrow == null)
            {
                bool nearNock = Vector3.Distance(drawHand.position, restNock.position) <= nockDistance;
                if (CanShoot && !InputGate.GrabBlocked && nearNock && drawTrigger.WasPressedThisFrame())
                {
                    Nock();
                }

                UpdateString(restNock.position);
                return;
            }

            // 引いている間:引いた距離と矢の向きを手の位置から決める
            Vector3 pull = drawHand.position - restNock.position;
            drawDistance = Mathf.Min(pull.magnitude, maxDraw);
            Vector3 nockPoint = pull.sqrMagnitude > 0.000001f
                ? restNock.position + pull.normalized * drawDistance
                : restNock.position;
            Vector3 direction = transform.position - nockPoint;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            nockedArrow.transform.SetPositionAndRotation(nockPoint + direction * arrowLength,
                Quaternion.LookRotation(direction, transform.up));
            UpdateString(nockPoint);
            UpdateDrawFeedback();
            UpdateAimLine(nockedArrow.transform.position, direction * Speed());

            if (!drawTrigger.IsPressed())
            {
                Release(direction);
            }
        }

        private void Nock()
        {
            nockedArrow = Instantiate(arrowTemplate);
            nockedArrow.gameObject.SetActive(true);
            drawDistance = 0f;
            if (drawSource != null)
            {
                drawSource.volume = 0f;
                drawSource.Play();
            }
        }

        private void Release(Vector3 direction)
        {
            Arrow arrow = nockedArrow;
            nockedArrow = null;
            StopDrawFeedback();

            if (drawDistance < minDraw)
            {
                // 空撃ち:矢を消し、本数は減らさない
                Destroy(arrow.gameObject);
                return;
            }

            arrow.Launch(direction * Speed(), gravityScale, hitLayers, ArrowStopped);
            Haptic(drawInteractor, releaseHapticDrawHand);
            Haptic(bowInteractor, releaseHapticBowHand);
            if (releaseSource != null)
            {
                if (releaseClips.Length > 0 && releaseClips[0] != null)
                {
                    releaseSource.PlayOneShot(releaseClips[UnityEngine.Random.Range(0, releaseClips.Length)], releaseVolume);
                }

                if (whooshClip != null)
                {
                    releaseSource.PlayOneShot(whooshClip, releaseVolume * 0.6f);
                }
            }

            Fired?.Invoke(arrow);
        }

        /// <summary>引いている途中の矢を消す(メニューを開いたとき・ゲームの終わりなど)。</summary>
        public void CancelDraw()
        {
            if (nockedArrow != null)
            {
                Destroy(nockedArrow.gameObject);
                nockedArrow = null;
            }

            StopDrawFeedback();
            if (restNock != null)
            {
                UpdateString(restNock.position);
            }
        }

        private float Speed()
        {
            float t = Mathf.InverseLerp(minDraw, maxDraw, drawDistance);
            return Mathf.Lerp(minSpeed, maxSpeed, t);
        }

        private void UpdateString(Vector3 nockPoint)
        {
            if (bowString == null)
            {
                return;
            }

            stringPoints[0] = topTip.position;
            stringPoints[1] = nockPoint;
            stringPoints[2] = bottomTip.position;
            bowString.positionCount = 3;
            bowString.SetPositions(stringPoints);
        }

        /// <summary>引いた距離に合わせて、振動ときしみの音を変える。</summary>
        private void UpdateDrawFeedback()
        {
            float t = Mathf.InverseLerp(minDraw, maxDraw, drawDistance);
            if (drawDistance >= minDraw && Time.time >= nextHapticTime)
            {
                nextHapticTime = Time.time + drawHapticInterval;
                Haptic(drawInteractor, new Vector2(Mathf.Lerp(drawHaptic.x, drawHaptic.y, t), drawHapticInterval * 1.2f));
            }

            if (drawSource != null)
            {
                // 8.2:引いた距離 0.1〜0.6m で音量 30〜100%、ピッチ 0.9〜1.15
                drawSource.volume = drawDistance < minDraw ? 0f : drawVolume * Mathf.Lerp(0.3f, 1f, t);
                drawSource.pitch = Mathf.Lerp(0.9f, 1.15f, t);
                drawSource.transform.position = stringPoints[1];
            }
        }

        private void StopDrawFeedback()
        {
            if (drawSource != null)
            {
                drawSource.Stop();
            }

            if (aimLine != null)
            {
                aimLine.enabled = false;
            }
        }

        /// <summary>ねらいの補助:矢の先から aimLength 分の山なりの道筋を点線で出す。</summary>
        private void UpdateAimLine(Vector3 start, Vector3 velocity)
        {
            if (aimLine == null)
            {
                return;
            }

            bool show = AimAssist && drawDistance >= minDraw && aimPoints >= 2;
            aimLine.enabled = show;
            if (!show)
            {
                return;
            }

            if (aimBuffer.Length != aimPoints)
            {
                aimBuffer = new Vector3[aimPoints];
            }

            // 道筋の長さがおよそ aimLength になる時間刻みで点を置く
            float dt = aimLength / Mathf.Max(1f, velocity.magnitude) / (aimPoints - 1);
            Vector3 position = start;
            Vector3 v = velocity;
            Vector3 gravity = Physics.gravity * gravityScale;
            for (int i = 0; i < aimPoints; i++)
            {
                aimBuffer[i] = position;
                v += gravity * dt;
                position += v * dt;
            }

            aimLine.positionCount = aimPoints;
            aimLine.SetPositions(aimBuffer);
        }

        private static void Haptic(XRBaseInputInteractor interactor, Vector2 strengthAndDuration)
        {
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                interactor.SendHapticImpulse(strengthAndDuration.x, strengthAndDuration.y);
            }
        }
    }
}
