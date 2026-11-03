using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRGame3.Common
{
    /// <summary>
    /// Vive コントローラーのトラックパッド1つで、テレポートとスナップターンを振り分ける。
    /// 押し込んだ瞬間のタッチ位置 X が中央ならテレポート(押している間は放物線を表示し、離した地点へ移動)、
    /// 左右ならスナップターン(1回の押し込みで1回だけ回る)。
    /// 左右のコントローラーそれぞれに1つずつ付ける。
    /// </summary>
    public class TrackpadLocomotion : MonoBehaviour
    {
        /// <summary>どちらの手のコントローラーか。</summary>
        public enum Hand
        {
            Left,
            Right,
        }

        private enum Mode
        {
            None,
            Teleport,
        }

        [Header("基本")]
        [Tooltip("このコンポーネントを付けたコントローラーが左右どちらの手か")]
        [SerializeField] private Hand hand = Hand.Right;

        [Tooltip("移動・回転させる XR Origin。空なら親から探す")]
        [SerializeField] private XROrigin xrOrigin;

        [Tooltip("放物線を出す向きの基準。空ならこのオブジェクト自身")]
        [SerializeField] private Transform aimSource;

        [Header("振り分け")]
        [Tooltip("押し込んだ瞬間のタッチ位置 X の絶対値がこの値未満ならテレポート、以上ならスナップターン(0〜1)")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float centerThreshold = 0.5f;

        [Header("スナップターン")]
        [Tooltip("1回の押し込みで回る角度(度)")]
        [Range(5f, 90f)]
        [SerializeField] private float snapTurnAngle = 45f;

        [Header("テレポート")]
        [Tooltip("放物線の初速(m/秒)。大きいほど遠くまで届く")]
        [SerializeField] private float arcLaunchSpeed = 8f;

        [Tooltip("放物線の計算に使う重力(m/秒²)")]
        [SerializeField] private float arcGravity = 9.81f;

        [Tooltip("放物線を計算する最大時間(秒)。長いほど遠くまで探す")]
        [SerializeField] private float arcMaxTime = 2f;

        [Tooltip("放物線の1区間の時間(秒)。小さいほど滑らかだが計算が増える")]
        [SerializeField] private float arcTimeStep = 0.04f;

        [Tooltip("テレポート先として当たり判定をとるレイヤー")]
        [SerializeField] private LayerMask teleportLayers = ~0;

        [Tooltip("着地点の面の傾きがこの角度(度)以下なら移動できる")]
        [Range(0f, 60f)]
        [SerializeField] private float maxSlopeAngle = 30f;

        [Tooltip("放物線を描く LineRenderer。空なら線は出さない")]
        [SerializeField] private LineRenderer arcLine;

        [Tooltip("着地点に出す目印。空なら出さない")]
        [SerializeField] private Transform reticle;

        [Tooltip("移動できる場所を指しているときの線の色")]
        [SerializeField] private Color validColor = new Color(0.23f, 0.65f, 1f);

        [Tooltip("移動できない場所を指しているときの線の色")]
        [SerializeField] private Color invalidColor = new Color(1f, 0.32f, 0.37f);

        private InputAction trackpadPosition;
        private InputAction trackpadClick;
        private Mode mode = Mode.None;
        private bool hasValidTarget;
        private Vector3 targetPoint;
        private readonly List<Vector3> arcPoints = new List<Vector3>();

        private void Awake()
        {
            if (xrOrigin == null)
            {
                xrOrigin = GetComponentInParent<XROrigin>();
            }

            if (aimSource == null)
            {
                aimSource = transform;
            }

            // OpenXR の HTC Vive Controller Profile では、トラックパッドの位置が Primary2DAxis、
            // 押し込みが Primary2DAxisClick の用途(usage)として公開される
            string handUsage = hand == Hand.Left ? "LeftHand" : "RightHand";
            trackpadPosition = new InputAction(
                $"{hand} Trackpad Position",
                InputActionType.Value,
                $"<XRController>{{{handUsage}}}/{{Primary2DAxis}}",
                expectedControlType: "Vector2");
            trackpadClick = new InputAction(
                $"{hand} Trackpad Click",
                InputActionType.Button,
                $"<XRController>{{{handUsage}}}/{{Primary2DAxisClick}}");

            SetArcVisible(false);
        }

        private void OnEnable()
        {
            trackpadPosition.Enable();
            trackpadClick.Enable();
        }

        private void OnDisable()
        {
            trackpadPosition.Disable();
            trackpadClick.Disable();
            mode = Mode.None;
            SetArcVisible(false);
        }

        private void OnDestroy()
        {
            trackpadPosition?.Dispose();
            trackpadClick?.Dispose();
        }

        private void Update()
        {
            if (xrOrigin == null)
            {
                return;
            }

            if (trackpadClick.WasPressedThisFrame())
            {
                float x = trackpadPosition.ReadValue<Vector2>().x;
                if (Mathf.Abs(x) < centerThreshold)
                {
                    mode = Mode.Teleport;
                }
                else
                {
                    // 右を押したら右回り(Y 軸の正方向)、左なら左回り
                    xrOrigin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * snapTurnAngle);
                }
            }

            if (mode != Mode.Teleport)
            {
                return;
            }

            UpdateArc();

            if (trackpadClick.WasReleasedThisFrame())
            {
                if (hasValidTarget)
                {
                    TeleportTo(targetPoint);
                }

                mode = Mode.None;
                SetArcVisible(false);
            }
        }

        /// <summary>
        /// 放物線を少しずつ進めながら当たり判定をとり、着地点と線の形を更新する。
        /// </summary>
        private void UpdateArc()
        {
            arcPoints.Clear();
            hasValidTarget = false;

            Vector3 position = aimSource.position;
            Vector3 velocity = aimSource.forward * arcLaunchSpeed;
            Vector3 up = xrOrigin.Origin.transform.up;
            arcPoints.Add(position);

            float step = Mathf.Max(0.005f, arcTimeStep);
            for (float t = 0f; t < arcMaxTime; t += step)
            {
                Vector3 next = position + velocity * step;
                velocity += -up * arcGravity * step;
                Vector3 segment = next - position;

                if (Physics.Raycast(position, segment.normalized, out RaycastHit hit, segment.magnitude,
                        teleportLayers, QueryTriggerInteraction.Ignore))
                {
                    arcPoints.Add(hit.point);
                    hasValidTarget = Vector3.Angle(hit.normal, up) <= maxSlopeAngle;
                    targetPoint = hit.point;
                    break;
                }

                arcPoints.Add(next);
                position = next;
            }

            if (arcLine != null)
            {
                arcLine.enabled = true;
                arcLine.positionCount = arcPoints.Count;
                arcLine.SetPositions(arcPoints.ToArray());
                Color color = hasValidTarget ? validColor : invalidColor;
                arcLine.startColor = color;
                arcLine.endColor = color;
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(hasValidTarget);
                if (hasValidTarget)
                {
                    reticle.position = targetPoint;
                    reticle.rotation = Quaternion.FromToRotation(Vector3.up, up);
                }
            }
        }

        /// <summary>
        /// 頭(カメラ)の真下が着地点に来るように XR Origin を動かす。向きと身長はそのまま。
        /// </summary>
        private void TeleportTo(Vector3 point)
        {
            Transform origin = xrOrigin.Origin.transform;
            Vector3 head = xrOrigin.Camera.transform.position;
            Vector3 up = origin.up;

            // 頭の位置を床(Origin の高さ)まで下ろした点
            Vector3 headOnFloor = head - up * Vector3.Dot(head - origin.position, up);
            origin.position += point - headOnFloor;
        }

        private void SetArcVisible(bool visible)
        {
            if (arcLine != null)
            {
                arcLine.enabled = visible;
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(visible && hasValidTarget);
            }
        }
    }
}
