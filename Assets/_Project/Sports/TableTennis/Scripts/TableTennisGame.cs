using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRGame3.Common;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球1ゲームの進行(docs/plan.md「卓球」)。11点先取・デュースあり、相手は CPU。
    /// 「はじめに」パネルの「はじめる」でラケットを利き手に付け、サーブ → ラリー → 1点の決着を繰り返す。
    /// プレイヤーのサーブは、利き手でない方のトリガーでボールを持ち、離して投げ上げてラケットで打つ。
    /// メニューや休憩案内が開いたら、ラリー中の球を消してその1点はやり直す。
    /// </summary>
    public class TableTennisGame : MonoBehaviour
    {
        private enum State
        {
            Waiting,
            PlayerServe,
            CpuServe,
            Rally,
            PointOver,
            GameOver,
        }

        [Header("部品")]
        [SerializeField] private PingPongBall ball;
        [SerializeField] private Racket racket;
        [SerializeField] private TTCpuPlayer cpu;
        [SerializeField] private TTScoreBoard scoreBoard;
        [SerializeField] private ResultPanel resultPanel;

        [Tooltip("台の見た目と当たりの親(身長に合わせて高さを変える)。この物の位置が台の上面の中心")]
        [SerializeField] private Transform table;

        [Header("手")]
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private XRBaseInputInteractor leftInteractor;
        [SerializeField] private XRBaseInputInteractor rightInteractor;

        [Header("ルール")]
        [Tooltip("先取点")]
        [SerializeField] private int pointsToWin = 11;

        [Tooltip("台の高さの基準(身長 170cm のときの高さ, m)")]
        [SerializeField] private float baseTableHeight = 0.76f;

        [Tooltip("台の高さの範囲(最小, 最大)(m)")]
        [SerializeField] private Vector2 tableHeightRange = new Vector2(0.68f, 0.84f);

        [Tooltip("1点の決着からボールを消すまでの秒数")]
        [SerializeField] private float pointPause = 1f;

        [Tooltip("CPU のサーブまでの秒数")]
        [SerializeField] private float cpuServeDelay = 1.5f;

        [Tooltip("台からこれ以上離れたボールは迷子(m)")]
        [SerializeField] private float strayDistance = 8f;

        [Tooltip("これ以上決着しないボールは迷子(秒)")]
        [SerializeField] private float strayTime = 10f;

        [Header("入りやすさの補助")]
        [Tooltip("直す向きの最大(度)")]
        [SerializeField] private float assistMaxAngle = 6f;

        [Tooltip("直す速さの最大(割合)")]
        [SerializeField] private float assistMaxSpeedChange = 0.15f;

        [Tooltip("台の外に落ちる球のうち、縁からこの距離までを直す(m)")]
        [SerializeField] private float assistMargin = 0.3f;

        private TableTennisMatch match = new TableTennisMatch();
        private RallyJudge judge;
        private State state = State.Waiting;
        private InputAction serveTrigger;
        private Transform serveHand;
        private float pointTimer;
        private bool rallyStarted;

        /// <summary>1点が決まったとき(得点した側, 理由)。</summary>
        public event Action<TTSide, string> PointScored;

        /// <summary>ボールが台・ネット・床・ラケットに当たったとき(音のため)。</summary>
        public event Action<PingPongPhysics.Event> BallEvent;

        /// <summary>CPU が打ったとき(音のため)。</summary>
        public event Action<Vector3, float> CpuHit;

        /// <summary>「はじめる」を押すのを待っているとき。</summary>
        public event Action WaitingForStart;

        /// <summary>ゲームが終わって記録したとき(勝ったか)。</summary>
        public event Action<bool> GameFinished;

        public TableTennisMatch Match => match;

        private void OnEnable()
        {
            ball.Bounced += OnBallEvent;
            cpu.Hit += OnCpuHit;
            SaveStore.Changed += OnSettingsChanged;
            if (resultPanel != null)
            {
                resultPanel.Retry.AddListener(BackToStart);
            }
        }

        private void OnDisable()
        {
            ball.Bounced -= OnBallEvent;
            cpu.Hit -= OnCpuHit;
            SaveStore.Changed -= OnSettingsChanged;
            if (resultPanel != null)
            {
                resultPanel.Retry.RemoveListener(BackToStart);
            }

            serveTrigger?.Dispose();
            serveTrigger = null;
        }

        private void Start()
        {
            racket.Unequip();
            ball.Hide();
            ApplyTableHeight();
            cpu.ResetStance();
            scoreBoard.Refresh(match, cpu.Level.label);
        }

        /// <summary>新しいゲームを始める(「はじめに」パネルの「はじめる」から呼ぶ)。</summary>
        public void StartGame()
        {
            StopAllCoroutines();
            match = new TableTennisMatch(pointsToWin);
            cpu.SetLevel(SaveStore.Settings.cpuLevel);
            ApplyTableHeight();
            EquipHands();
            rallyStarted = false;
            GameSession.InProgress = false;
            scoreBoard.Refresh(match, cpu.Level.label);
            NextServe();
        }

        private void BackToStart()
        {
            state = State.Waiting;
            WaitingForStart?.Invoke();
        }

        private void EquipHands()
        {
            bool leftHanded = SaveStore.Settings.leftHanded;
            // ラケットは利き手、ボールは利き手でない方
            Transform racketHand = leftHanded ? leftController : rightController;
            serveHand = leftHanded ? rightController : leftController;
            racket.Equip(racketHand, leftHanded ? leftInteractor : rightInteractor);

            serveTrigger?.Dispose();
            string usage = leftHanded ? "RightHand" : "LeftHand";
            serveTrigger = new InputAction("Serve Trigger", InputActionType.Button, $"<XRController>{{{usage}}}/{{TriggerButton}}");
            serveTrigger.Enable();
        }

        private void NextServe()
        {
            ball.Hide();
            cpu.ResetStance();
            judge = new RallyJudge(match.Server);
            scoreBoard.Refresh(match, cpu.Level.label);
            if (match.Server == TTSide.Player)
            {
                state = State.PlayerServe;
            }
            else
            {
                state = State.CpuServe;
                StartCoroutine(CpuServeLater());
            }
        }

        private IEnumerator CpuServeLater()
        {
            yield return new WaitForSeconds(cpuServeDelay);
            if (state == State.CpuServe && !InputGate.GrabBlocked)
            {
                state = State.Rally;
                cpu.Serve();
            }
            else if (state == State.CpuServe)
            {
                // メニューが出ている間は待つ
                StartCoroutine(CpuServeLater());
            }
        }

        private void Update()
        {
            switch (state)
            {
                case State.PlayerServe:
                    UpdatePlayerServe();
                    break;
                case State.Rally:
                    UpdateRally();
                    break;
                case State.PointOver:
                    pointTimer -= Time.deltaTime;
                    if (pointTimer <= 0f)
                    {
                        AfterPoint();
                    }

                    break;
            }
        }

        private void UpdatePlayerServe()
        {
            if (serveTrigger == null || InputGate.GrabBlocked)
            {
                return;
            }

            if (!ball.IsHeld && serveTrigger.WasPressedThisFrame())
            {
                ball.Hold(serveHand);
            }
            else if (ball.IsHeld && serveTrigger.WasReleasedThisFrame())
            {
                ball.ReleaseFromHand();
                state = State.Rally;
            }
        }

        private void UpdateRally()
        {
            // メニューや休憩案内が開いたら、この1点はやり直す(時間は止めない決まりのため)
            if (InputGate.GrabBlocked)
            {
                cpu.Cancel();
                NextServe();
                return;
            }

            if (!ball.IsFlying)
            {
                return;
            }

            float far = Vector3.Distance(ball.Position, table.position);
            if (far > strayDistance || ball.FlightTime > strayTime)
            {
                RallyJudge.Point? point = judge.OnStray();
                if (point.HasValue)
                {
                    EndPoint(point.Value);
                }
                else
                {
                    NextServe();
                }
            }
        }

        private void OnBallEvent(PingPongPhysics.Event e)
        {
            BallEvent?.Invoke(e);
            if (state != State.Rally)
            {
                return;
            }

            RallyJudge.Point? point = null;
            switch (e.Kind)
            {
                case PingPongPhysics.EventKind.Racket:
                    point = judge.OnHit(TTSide.Player);
                    if (!point.HasValue)
                    {
                        rallyStarted = true;
                        GameSession.InProgress = true;
                        ApplyAssist();
                        cpu.PlanReturn();
                    }

                    break;
                case PingPongPhysics.EventKind.Table:
                    point = judge.OnBounce(e.Side);
                    break;
                case PingPongPhysics.EventKind.Floor:
                    point = judge.OnFloor();
                    if (!point.HasValue)
                    {
                        // 投げ上げたボールを打たずに落とした:失点にせず、もう一度サーブ
                        StartCoroutine(ReserveLater());
                        state = State.PointOver;
                        pointTimer = float.MaxValue;
                        return;
                    }

                    break;
            }

            if (point.HasValue)
            {
                EndPoint(point.Value);
            }
        }

        private IEnumerator ReserveLater()
        {
            yield return new WaitForSeconds(pointPause);
            NextServe();
        }

        private void OnCpuHit()
        {
            CpuHit?.Invoke(ball.Position, ball.Velocity.magnitude);
            if (state != State.Rally)
            {
                return;
            }

            RallyJudge.Point? point = judge.OnHit(TTSide.Cpu);
            if (point.HasValue)
            {
                EndPoint(point.Value);
            }
        }

        private void EndPoint(RallyJudge.Point point)
        {
            cpu.Cancel();
            match.AddPoint(point.Winner);
            cpu.React(point.Winner == TTSide.Cpu);
            scoreBoard.Refresh(match, cpu.Level.label);
            scoreBoard.ShowReason(point.Winner == TTSide.Player ? $"とくてん  {point.Reason}" : $"しってん  {point.Reason}");
            PointScored?.Invoke(point.Winner, point.Reason);
            state = State.PointOver;
            pointTimer = pointPause;
        }

        private void AfterPoint()
        {
            ball.Hide();
            if (match.IsOver)
            {
                Finish();
            }
            else
            {
                NextServe();
            }
        }

        private void Finish()
        {
            state = State.GameOver;
            GameSession.InProgress = false;
            racket.Unequip();
            serveTrigger?.Dispose();
            serveTrigger = null;

            bool won = match.Winner == TTSide.Player;
            int score = Score(won);
            GameRecordResult result = SaveStore.RecordFinishedGame(SportIds.TableTennis, score);
            scoreBoard.ShowReason(won ? "かち!" : "まけ");
            RestReminder.AtBreak();
            GameFinished?.Invoke(won);
            if (resultPanel != null)
            {
                resultPanel.Show(result);
            }
            else
            {
                BackToStart();
            }
        }

        /// <summary>
        /// 卓球スコア(docs/plan.md「腕前ポイントとランク」):強さの点(0/40/80)+ 自分の得点 × 4(11点まで)+ 勝てば 20。
        /// </summary>
        private int Score(bool won)
        {
            int levelPoints = Mathf.Clamp(SaveStore.Settings.cpuLevel, 0, 2) * 40;
            return levelPoints + Mathf.Min(match.PlayerPoints, 11) * 4 + (won ? 20 : 0);
        }

        /// <summary>
        /// 入りやすさの補助:もう少しで相手コートに入る球だけ、向きと速さを少し直して台の中に入れる。
        /// 大きく外れた球は直さない。
        /// </summary>
        private void ApplyAssist()
        {
            if (!SaveStore.Settings.hitAssist)
            {
                return;
            }

            PingPongPhysics.Settings s = ball.Physics;
            PingPongPhysics.Table t = ball.Table;
            bool landed = PingPongPhysics.PredictLanding(ball.Position, ball.Velocity, s, t, out Vector3 landing, out _, out bool net);
            if (landed && !net && t.SideOf(landing) == TTSide.Cpu)
            {
                return;
            }

            // 台の高さを通る点(ネットに掛かる球はネットの先)を、相手コートの中へ寄せた点をねらう
            Vector3 crossing = CrossTableHeight(ball.Position, ball.Velocity, s, t);
            float halfW = t.Width / 2f;
            float farEnd = t.Center.z + t.Length / 2f;
            bool nearMiss = net
                ? crossing.z < t.Center.z + 0.6f
                : Mathf.Abs(crossing.x - t.Center.x) <= halfW + assistMargin && crossing.z <= farEnd + assistMargin &&
                  crossing.z > t.Center.z;
            if (!nearMiss)
            {
                return;
            }

            Vector3 target = new Vector3(
                Mathf.Clamp(crossing.x, t.Center.x - halfW + 0.1f, t.Center.x + halfW - 0.1f),
                t.Top,
                Mathf.Clamp(crossing.z, t.Center.z + 0.3f, farEnd - 0.1f));
            Vector3 v = ball.Velocity;
            float horizontal = new Vector2(v.x, v.z).magnitude;
            if (!PingPongPhysics.SolveShot(ball.Position, target, horizontal, s, t, out Vector3 fixedVelocity))
            {
                return;
            }

            float angle = Vector3.Angle(v, fixedVelocity);
            float speedChange = Mathf.Abs(fixedVelocity.magnitude / Mathf.Max(0.01f, v.magnitude) - 1f);
            if (angle <= assistMaxAngle && speedChange <= assistMaxSpeedChange)
            {
                ball.SetVelocity(fixedVelocity);
            }
        }

        /// <summary>球が台の上面の高さまで落ちてくる点(台やネットは無いものとして重力と空気だけで求める)。</summary>
        private static Vector3 CrossTableHeight(Vector3 p, Vector3 v, PingPongPhysics.Settings s, PingPongPhysics.Table t)
        {
            for (float time = 0f; time < 3f; time += s.step)
            {
                v += Vector3.down * (s.gravity * s.step);
                v -= v * (s.drag * v.magnitude * s.step);
                p += v * s.step;
                if (v.y < 0f && p.y <= t.Top + s.ballRadius)
                {
                    return p;
                }
            }

            return p;
        }

        private void ApplyTableHeight()
        {
            float height = Mathf.Clamp(baseTableHeight * SaveStore.Settings.heightCm / 170f, tableHeightRange.x, tableHeightRange.y);
            Vector3 p = table.position;
            table.position = new Vector3(p.x, height, p.z);
            ball.Table = PingPongPhysics.Table.Standard(table.position);
        }

        private void OnSettingsChanged()
        {
            if (state == State.Waiting || state == State.GameOver)
            {
                ApplyTableHeight();
                cpu.SetLevel(SaveStore.Settings.cpuLevel);
                scoreBoard.Refresh(match, cpu.Level.label);
            }
        }
    }
}
