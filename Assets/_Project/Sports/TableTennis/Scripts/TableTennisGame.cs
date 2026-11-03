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

        [Header("確認用")]
        [Tooltip("オンにすると、「はじめる」で練習用の球出しになる(CPU がやさしい球を出し続け、点は数えない)。" +
                 "すり抜けの確認(企画書のゲート)に使う。打った数と台に入った数を Console に出す")]
        [SerializeField] private bool practiceFeed;

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

        [Header("やさしさ(試遊で「難しすぎる」と出たので足した。2026-11-04)")]
        [Tooltip("補助オンのとき、打った球を向きに合わせて必ず相手コートへ届くように直す(オフなら惜しい球だけ直す)")]
        [SerializeField] private bool strongAssist = true;

        [Tooltip("強い補助で直すときの、返す球の水平の速さ(最小, 最大)(m/秒)")]
        [SerializeField] private Vector2 assistedSpeed = new Vector2(4.5f, 9f);

        [Tooltip("補助オンのときのラケットの当たりの半径(m)。見た目は 0.0775")]
        [SerializeField] private float assistRacketRadius = 0.12f;

        [Tooltip("補助オフのときのラケットの当たりの半径(m)")]
        [SerializeField] private float normalRacketRadius = 0.085f;

        [Tooltip("サーブで、トリガーを離したときに真上へ投げ上げる速さ(m/秒)。手の動きは使わない")]
        [SerializeField] private float tossSpeed = 2.4f;

        private TableTennisMatch match = new TableTennisMatch();
        private RallyJudge judge;
        private State state = State.Waiting;
        private InputAction serveTrigger;
        private Transform serveHand;
        private float pointTimer;
        private bool lastWasNet;
        private int practiceHits;
        private int practiceLanded;
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
            TrackpadLocomotion.Suspended = false;
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
            cpu.ResetStance(true);
            scoreBoard.Refresh(match, $"{cpu.Level.label} {cpu.Level.marks}");
        }

        /// <summary>新しいゲームを始める(「はじめに」パネルの「はじめる」から呼ぶ)。</summary>
        public void StartGame()
        {
            if (practiceFeed)
            {
                StartChallenge("practice");
                return;
            }

            challengeId = null;
            BeginMatch(pointsToWin, true, SaveStore.Settings.cpuLevel);
        }

        private void BeginMatch(int points, bool deuce, int level)
        {
            // 補助オンのときは、ラケットの当たりを広くする
            ball.Physics.racketRadius = SaveStore.Settings.hitAssist ? assistRacketRadius : normalRacketRadius;
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
            GameSession.InProgress = false;
            RefreshBoard();
            cpu.Chatter.Say("よろしくね!", "いくよ〜!", "まけないよ!");
            // ねらい打ちと練習は CPU が球を出すので、投げ上げから始まるてびきは出さない
            if (challengeId != "tabletennis.aim" && challengeId != "practice")
            {
                BeginTutorial();
            }

            NextServe();
        }

        /// <summary>てびき(初めてのときだけ):1 ボールを出して投げ上げる → 2 打つ → 3 ルール。</summary>
        private static void BeginTutorial()
        {
            bool left = SaveStore.Settings.leftHanded;
            TutorialBoard.Begin("tabletennis",
                new[]
                {
                    "ボールの手の トリガーで ボールが出る。\nはなすと上に投げる",
                    "ラケットを ふって ボールに当てる。\nボタンは いらない",
                    "相手のコートに 入れよう。\nおちた球は ひろわなくてよい",
                },
                new[] { TutorialBoard.Button.Trigger, TutorialBoard.Button.None, TutorialBoard.Button.None },
                new[] { !left, left, left });
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
                case "practice":
                    practiceHits = 0;
                    practiceLanded = 0;
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
                case "practice":
                    scoreBoard.ShowChallenge("れんしゅう", $"{practiceLanded}", $"打った {practiceHits}");
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
            lastWasNet = false;
            ball.Hide();
            cpu.ResetStance();
            // ラリー10回はいつもプレイヤーのサーブ、ねらい打ちはいつも CPU の球出し
            TTSide server = challengeId == "tabletennis.rally" ? TTSide.Player
                : challengeId == "tabletennis.aim" || challengeId == "practice" ? TTSide.Cpu
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
                cpu.Serve(challengeId == "tabletennis.aim" || challengeId == "practice");
            }
            else if (state == State.CpuServe)
            {
                // メニューが出ている間は待つ
                StartCoroutine(CpuServeLater());
            }
        }

        private void Update()
        {
            // ラケットを振っている間に親指でトラックパッドを押して、回ったり移動したりしないようにする
            TrackpadLocomotion.Suspended = state == State.PlayerServe || state == State.CpuServe || state == State.Rally ||
                                           state == State.PointOver;

            if (serveGuide != null)
            {
                // てびきの板が出ている間は、サーブの案内は出さない(板と重ねない)
                bool tutorial = TutorialBoard.Current != null && TutorialBoard.Current.IsOpen;
                bool show = state == State.PlayerServe && !ball.IsHeld && !InputGate.GrabBlocked && !tutorial;
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
                // 手の動きに関係なく、まっすぐ上へ投げ上げる(サーブをやさしくする)
                ball.Launch(ball.Position, Vector3.up * tossSpeed);
                state = State.Rally;
                TutorialBoard.Next(0);
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
            if (e.Kind == PingPongPhysics.EventKind.Table && judge.LastHitter == null)
            {
                // 投げ上げた球を打たずに台に落とした:失点にせず、もう一度サーブ
                StartCoroutine(ReserveLater());
                state = State.PointOver;
                pointTimer = float.MaxValue;
                return;
            }

            if (e.Kind != PingPongPhysics.EventKind.Net && e.Kind != PingPongPhysics.EventKind.None)
            {
                lastWasNet = false;
            }

            switch (e.Kind)
            {
                case PingPongPhysics.EventKind.Net:
                    lastWasNet = true;
                    break;
                case PingPongPhysics.EventKind.Racket:
                    TutorialBoard.Next(1);
                    point = judge.OnHit(TTSide.Player);
                    if (!point.HasValue)
                    {
                        GameSession.InProgress = true;
                        ApplyAssist();
                        // ねらい打ちでは CPU は返さない(入ったかどうかだけを見る)
                        if (challengeId == "practice")
                        {
                            practiceHits++;
                        }

                        if (challengeId != "tabletennis.aim" && challengeId != "practice")
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
            else if (challengeId == "practice")
            {
                practiceLanded++;
                Debug.Log($"[卓球の練習] 打った {practiceHits} / 台に入った {practiceLanded}");
                cpu.Cancel();
                state = State.PointOver;
                pointTimer = pointPause;
                return true;
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
            TutorialBoard.Finish(2);
            cpu.Cancel();
            if (challengeId == "practice")
            {
                // 練習:点は数えず、次の球を出す
                Debug.Log($"[卓球の練習] 打った {practiceHits} / 台に入った {practiceLanded}({point.Reason})");
                state = State.PointOver;
                pointTimer = pointPause;
                return;
            }

            if (lastWasNet && point.Reason != "2バウンド")
            {
                point.Reason = "ネット";
            }

            if (challengeId == "tabletennis.aim")
            {
                if (judge.LastHitter != TTSide.Player)
                {
                    // CPU の球出しのミス(プレイヤーがまだ打っていない):数えずに出し直す
                    state = State.PointOver;
                    pointTimer = pointPause;
                    return;
                }

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
            scoreBoard.ShowReason(point.Winner == TTSide.Player ? $"{point.Reason} → あなたの点" : $"{point.Reason} → CPU の点");
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
            if (won)
            {
                cpu.Chatter.Say("まけちゃった…つよいね!", "くやしい!もういっかい!");
            }
            else
            {
                cpu.Chatter.Say("かったよ!たのしかった!", "やったー!またあそぼ!");
            }
            if (resultPanel != null)
            {
                // 勝敗と点数を見出しに出す(docs/art-direction.md 6.13)
                resultPanel.Show(result, $"{(won ? "かち!" : "まけ")}  {match.PlayerPoints} ― {match.CpuPoints}");
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
            if (strongAssist)
            {
                AssistStrong(s, t);
                return;
            }

            bool landed = PingPongPhysics.PredictLanding(ball.Position, ball.Velocity, s, t, out Vector3 landing, out _, out bool net);
            if (landed && !net && t.SideOf(landing) == TTSide.Cpu)
            {
                return;
            }

            if (net)
            {
                AssistOverNet(s, t);
                return;
            }

            // 台の高さを通る点を、相手コートの中へ寄せた点をねらう
            Vector3 crossing = CrossTableHeight(ball.Position, ball.Velocity, s, t);
            float halfW = t.Width / 2f;
            float farEnd = t.Center.z + t.Length / 2f;
            bool nearMiss = Mathf.Abs(crossing.x - t.Center.x) <= halfW + assistMargin && crossing.z <= farEnd + assistMargin &&
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

        /// <summary>
        /// 強い補助:打った球を、打った向き(左右)に合わせて相手コートの中ほどへ必ず届くように直す。
        /// 速さは打った強さを目安に assistedSpeed の範囲に収める。初めての人でもラリーが続くようにするため。
        /// </summary>
        private void AssistStrong(PingPongPhysics.Settings s, PingPongPhysics.Table t)
        {
            Vector3 v = ball.Velocity;
            float halfW = t.Width / 2f - 0.15f;
            float far = t.Center.z + t.Length / 2f;
            // 左右は打った向きから決め(少し真ん中へ寄せる)、奥行きは相手コートの中ほど
            float x = t.Center.x + Mathf.Clamp(v.z > 0.5f ? v.x / v.z * (far - ball.Position.z) * 0.6f : 0f, -halfW, halfW);
            float z = Mathf.Lerp(t.Center.z + 0.5f, far - 0.25f, Mathf.InverseLerp(assistedSpeed.x, assistedSpeed.y, v.magnitude));
            var target = new Vector3(x, t.Top, z);
            float speed = Mathf.Clamp(new Vector2(v.x, v.z).magnitude, assistedSpeed.x, assistedSpeed.y);
            if (PingPongPhysics.SolveShot(ball.Position, target, speed, s, t, out Vector3 fixedVelocity))
            {
                ball.SetVelocity(fixedVelocity);
            }
        }

        /// <summary>
        /// ネットを 0.1m 以内の差で越えられない球だけ、同じ向きのまま上向きの速さを上げて、ネットの上 0.05m を越えさせる。
        /// </summary>
        private void AssistOverNet(PingPongPhysics.Settings s, PingPongPhysics.Table t)
        {
            Vector3 v = ball.Velocity;
            float netTop = t.Top + t.NetHeight + s.ballRadius;
            float shortfall = netTop - HeightAtNet(ball.Position, v, s, t);
            if (shortfall <= 0f || shortfall > 0.1f)
            {
                return;
            }

            for (float add = 0.1f; add <= 3f; add += 0.1f)
            {
                Vector3 candidate = v + Vector3.up * add;
                if (Vector3.Angle(v, candidate) > assistMaxAngle ||
                    Mathf.Abs(candidate.magnitude / Mathf.Max(0.01f, v.magnitude) - 1f) > assistMaxSpeedChange)
                {
                    return;
                }

                if (HeightAtNet(ball.Position, candidate, s, t) < netTop + 0.05f)
                {
                    continue;
                }

                bool landed = PingPongPhysics.PredictLanding(ball.Position, candidate, s, t, out Vector3 landing, out _, out bool net);
                if (landed && !net && t.SideOf(landing) == TTSide.Cpu)
                {
                    ball.SetVelocity(candidate);
                }

                return;
            }
        }

        /// <summary>球がネットの面(台の中心の Z)を通るときの高さ(重力と空気だけで求める)。</summary>
        private static float HeightAtNet(Vector3 p, Vector3 v, PingPongPhysics.Settings s, PingPongPhysics.Table t)
        {
            float side = Mathf.Sign(p.z - t.Center.z);
            for (float time = 0f; time < 2f; time += s.step)
            {
                v += Vector3.down * (s.gravity * s.step);
                v -= v * (s.drag * v.magnitude * s.step);
                p += v * s.step;
                if (Mathf.Sign(p.z - t.Center.z) != side)
                {
                    return p.y;
                }
            }

            return float.MinValue;
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
