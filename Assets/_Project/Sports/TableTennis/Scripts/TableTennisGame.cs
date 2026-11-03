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
        [Tooltip("ボール")]
        [SerializeField] private PingPongBall ball;

        [Tooltip("ラケット")]
        [SerializeField] private Racket racket;

        [Tooltip("CPU")]
        [SerializeField] private TTCpuPlayer cpu;

        [Tooltip("スコア板")]
        [SerializeField] private TTScoreBoard scoreBoard;

        [Tooltip("結果の表示")]
        [SerializeField] private ResultPanel resultPanel;

        [Tooltip("台の見た目と当たりの親(身長に合わせて高さを変える)。この物の位置が台の上面の中心")]
        [SerializeField] private Transform table;

        [Tooltip("台の脚(床から天板の下まで伸ばす)")]
        [SerializeField] private Transform[] tableLegs = new Transform[0];

        [Tooltip("脚の横木(床から 0.15m)")]
        [SerializeField] private Transform[] tableCrossbars = new Transform[0];

        [Tooltip("サーブの案内(プレイヤーのサーブでボールを出す前だけ出す)")]
        [SerializeField] private GameObject serveGuide;

        [Header("手")]
        [Tooltip("左のコントローラー")]
        [SerializeField] private Transform leftController;

        [Tooltip("右のコントローラー")]
        [SerializeField] private Transform rightController;

        [Tooltip("左手の Interactor(振動を送る)")]
        [SerializeField] private XRBaseInputInteractor leftInteractor;

        [Tooltip("右手の Interactor(振動を送る)")]
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

        [Header("モード(M3)")]
        [Tooltip("「はじめに」パネルのモードの行")]
        [SerializeField] private ModeSelector modeSelector;

        [Tooltip("チャレンジに使うパネル")]
        [SerializeField] private ChoicePanel choicePanel;

        [Tooltip("ねらい打ちの円(台の子。このチャレンジの間だけ出す)")]
        [SerializeField] private GameObject aimTargets;

        [Header("チャレンジ(docs/plan.md「チャレンジ」)")]
        [Tooltip("ラリー10回:続ける往復の数・試せる回数")]
        [SerializeField] private int rallyNeed = 10;
        [SerializeField] private int rallyAttempts = 3;

        [Tooltip("ねらい打ち:球の数・入れる数・円の半径(m)")]
        [SerializeField] private int aimBalls = 10;
        [SerializeField] private int aimNeed = 3;
        [SerializeField] private float aimRadius = 0.25f;

        [Tooltip("5点マッチ:先取点")]
        [SerializeField] private int shortMatchPoints = 5;

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
        private string challengeId;
        private int challengeCount;
        private int challengeTries;
        private bool aimChecked;

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
            cpu.SetLevel(SaveStore.Settings.cpuLevel);
            ApplyTableHeight();
            cpu.ResetStance();
            scoreBoard.Refresh(match, $"{cpu.Level.label} {cpu.Level.marks}");
        }

        /// <summary>新しいゲームを始める(「はじめに」パネルの「はじめる」から呼ぶ)。</summary>
        public void StartGame()
        {
            challengeId = null;
            BeginMatch(pointsToWin, true, SaveStore.Settings.cpuLevel);
        }

        private void BeginMatch(int points, bool deuce, int level)
        {
            StopAllCoroutines();
            match = new TableTennisMatch(points, TTSide.Player, deuce);
            cpu.SetLevel(level);
            cpu.SetNoMiss(challengeId == "tabletennis.rally");
            if (aimTargets != null)
            {
                aimTargets.SetActive(challengeId == "tabletennis.aim");
            }

            ApplyTableHeight();
            EquipHands();
            rallyStarted = false;
            GameSession.InProgress = false;
            RefreshBoard();
            NextServe();
        }

        /// <summary>チャレンジの一覧を出す(「はじめに」パネルの「かだいをえらぶ」から呼ぶ)。</summary>
        public void ShowChallengeList()
        {
            ModeFlow.ShowChallengeList(choicePanel, SportIds.TableTennis, StartChallenge, BackToStart);
        }

        /// <summary>チャレンジを始める。</summary>
        public void StartChallenge(string id)
        {
            challengeId = id;
            challengeCount = 0;
            challengeTries = 0;
            switch (id)
            {
                case "tabletennis.rally":
                    BeginMatch(999, true, 0);
                    break;
                case "tabletennis.aim":
                    BeginMatch(999, true, 0);
                    break;
                default:
                    BeginMatch(shortMatchPoints, false, 1);
                    break;
            }
        }

        private void RefreshBoard()
        {
            string label = $"{cpu.Level.label} {cpu.Level.marks}";
            switch (challengeId)
            {
                case "tabletennis.rally":
                    scoreBoard.ShowChallenge("ラリー10回", $"{challengeCount}/{rallyNeed}", $"のこり {rallyAttempts - challengeTries}回");
                    break;
                case "tabletennis.aim":
                    scoreBoard.ShowChallenge("ねらい打ち", $"{challengeCount}/{aimNeed}", $"のこり {aimBalls - challengeTries}球");
                    break;
                default:
                    scoreBoard.Refresh(match, challengeId == null ? label : $"5点マッチ  {label}");
                    break;
            }
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
            // ラリー10回はいつもプレイヤーのサーブ、ねらい打ちはいつも CPU の球出し
            TTSide server = challengeId == "tabletennis.rally" ? TTSide.Player
                : challengeId == "tabletennis.aim" ? TTSide.Cpu
                : match.Server;
            judge = new RallyJudge(server);
            aimChecked = false;
            RefreshBoard();
            if (server == TTSide.Player)
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
                cpu.Serve(challengeId == "tabletennis.aim");
            }
            else if (state == State.CpuServe)
            {
                // メニューが出ている間は待つ
                StartCoroutine(CpuServeLater());
            }
        }

        private void Update()
        {
            if (serveGuide != null)
            {
                bool show = state == State.PlayerServe && !ball.IsHeld && !InputGate.GrabBlocked;
                if (serveGuide.activeSelf != show)
                {
                    serveGuide.SetActive(show);
                }
            }

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
                        // ねらい打ちでは CPU は返さない(入ったかどうかだけを見る)
                        if (challengeId != "tabletennis.aim")
                        {
                            cpu.PlanReturn();
                        }
                    }

                    break;
                case PingPongPhysics.EventKind.Table:
                    bool firstLanding = judge.LastHitter == TTSide.Player && e.Side == TTSide.Cpu && !judge.LandedOnReceiver;
                    point = judge.OnBounce(e.Side);
                    if (firstLanding && OnPlayerShotLanded(e.Point))
                    {
                        return;
                    }

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

        /// <summary>
        /// プレイヤーの打った球が相手コートに入ったとき(チャレンジの数え方)。true ならこの1球の扱いをここで終える。
        /// </summary>
        private bool OnPlayerShotLanded(Vector3 at)
        {
            if (challengeId == "tabletennis.rally")
            {
                challengeCount++;
                RefreshBoard();
                if (challengeCount >= rallyNeed)
                {
                    cpu.Cancel();
                    StartCoroutine(FinishChallenge(true));
                    return true;
                }
            }
            else if (challengeId == "tabletennis.aim" && !aimChecked)
            {
                aimChecked = true;
                PingPongPhysics.Table t = ball.Table;
                float cornerX = t.Width / 2f - 0.3f;
                float cornerZ = t.Center.z + t.Length / 2f - 0.3f;
                bool inside = (new Vector2(at.x - (t.Center.x - cornerX), at.z - cornerZ).magnitude <= aimRadius) ||
                              (new Vector2(at.x - (t.Center.x + cornerX), at.z - cornerZ).magnitude <= aimRadius);
                challengeCount += inside ? 1 : 0;
                scoreBoard.ShowReason(inside ? "ナイス!" : "おしい");
                EndFeed();
                return true;
            }

            return false;
        }

        /// <summary>ねらい打ちの1球が終わった。</summary>
        private void EndFeed()
        {
            challengeTries++;
            RefreshBoard();
            RestReminder.AtBreak();
            if (challengeCount >= aimNeed || challengeTries >= aimBalls)
            {
                state = State.GameOver;
                StartCoroutine(FinishChallenge(challengeCount >= aimNeed));
                return;
            }

            state = State.PointOver;
            pointTimer = pointPause;
        }

        private IEnumerator FinishChallenge(bool cleared)
        {
            state = State.GameOver;
            string id = challengeId;
            string detail = id == "tabletennis.rally" ? $"ラリー {challengeCount}回"
                : id == "tabletennis.aim" ? $"{challengeCount}球 入った"
                : $"{match.PlayerPoints} - {match.CpuPoints}";
            yield return new WaitForSeconds(pointPause);
            ball.Hide();
            racket.Unequip();
            serveTrigger?.Dispose();
            serveTrigger = null;
            GameSession.InProgress = false;
            if (aimTargets != null)
            {
                aimTargets.SetActive(false);
            }

            ModeFlow.ShowChallengeResult(choicePanel, id, cleared, detail, () => StartChallenge(id), ShowChallengeList);
        }

        private void EndPoint(RallyJudge.Point point)
        {
            cpu.Cancel();
            if (challengeId == "tabletennis.aim")
            {
                // 入らなかった球
                scoreBoard.ShowReason(point.Reason);
                EndFeed();
                return;
            }

            if (challengeId == "tabletennis.rally")
            {
                // ラリーが切れた:回数を数えて、のこりがあれば最初から
                challengeTries++;
                scoreBoard.ShowReason($"{challengeCount}回で きれた");
                RestReminder.AtBreak();
                if (challengeTries >= rallyAttempts)
                {
                    StartCoroutine(FinishChallenge(false));
                    return;
                }

                challengeCount = 0;
                state = State.PointOver;
                pointTimer = pointPause;
                return;
            }

            match.AddPoint(point.Winner);
            cpu.React(point.Winner == TTSide.Cpu);
            RefreshBoard();
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
            if (challengeId != null)
            {
                // 5点マッチ:勝てばクリア(記録とポイントは動かさない)
                StartCoroutine(FinishChallenge(won));
                return;
            }

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

            // 脚は床から天板の下(上面から 0.03 下)まで、横木は床から 0.15m
            foreach (Transform leg in tableLegs)
            {
                if (leg != null)
                {
                    leg.localScale = new Vector3(leg.localScale.x, height - 0.03f, leg.localScale.z);
                    leg.localPosition = new Vector3(leg.localPosition.x, (-height - 0.03f) / 2f, leg.localPosition.z);
                }
            }

            foreach (Transform bar in tableCrossbars)
            {
                if (bar != null)
                {
                    bar.localPosition = new Vector3(bar.localPosition.x, 0.15f - height, bar.localPosition.z);
                }
            }
        }

        private void OnSettingsChanged()
        {
            if (state == State.Waiting || state == State.GameOver)
            {
                ApplyTableHeight();
                cpu.SetLevel(SaveStore.Settings.cpuLevel);
                RefreshBoard();
            }
        }
    }
}
