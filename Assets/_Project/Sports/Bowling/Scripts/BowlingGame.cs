using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボウリング1レーンの進行役。投げる → ボールがピットに着く(または止まる)→ ピンが落ち着くのを待つ →
    /// 倒れた本数をスコア表に記録 → ピンを片付けるか並べ直す → ボールを戻す、を繰り返す。
    /// 10フレームが終わったあとは「はじめに」パネル(BowlingStartPanel)の「はじめる」で新しいゲームを始める。
    /// パネルがないシーンでは、restartOnGrab がオンならボールを掴むと新しいゲームが始まる。
    /// </summary>
    public class BowlingGame : MonoBehaviour
    {
        private enum State
        {
            Ready,
            Rolling,
            Settling,
            Clearing,
            GameOver,
        }

        [Header("つなぐ物")]
        [Tooltip("投げるボール")]
        [SerializeField] private List<ThrowableObject> balls = new List<ThrowableObject>();

        [Tooltip("ピンを並べる装置")]
        [SerializeField] private PinSetter pinSetter;

        [Tooltip("スコア板")]
        [SerializeField] private BowlingScoreBoard scoreBoard;

        [Tooltip("ファウルライン。位置が線の中央、青い矢印(Z+)がピンの方向を向くように置く")]
        [SerializeField] private Transform foulLine;

        [Tooltip("10フレームが終わったあと、ボールを掴むと新しいゲームを始めるか。「はじめに」パネルがあるシーンではオフにする")]
        [SerializeField] private bool restartOnGrab = true;

        [Header("投球の終わりの判定")]
        [Tooltip("ファウルラインからこの距離(m)まで進んだら、ボールがピットに着いたとみなす")]
        [SerializeField] private float pitDistance = 19.6f;

        [Tooltip("ボールがこの速さ(m/秒)より遅い状態が続いたら、止まったとみなして投球を終える")]
        [SerializeField] private float stopSpeed = 0.05f;

        [Tooltip("止まったとみなすまでの秒数")]
        [SerializeField] private float stopTime = 1f;

        [Tooltip("投げてからこの秒数たったら、どこにあっても投球を終える")]
        [SerializeField] private float maxRollTime = 12f;

        [Tooltip("投球が終わってから、倒れた本数を数えるまで最低限待つ秒数(このあと立っているピンが止まるのを待つ)")]
        [SerializeField] private float settleTime = 2f;

        [Tooltip("ピンが揺れ続けていても、この秒数たったら数える")]
        [SerializeField] private float maxSettleTime = 5f;

        [Header("投げ損ねの救済")]
        [Tooltip("ファウルラインの手前で止まったボールを、自動で転がし直すか")]
        [SerializeField] private bool rescueEnabled = true;

        [Tooltip("前への速さ(m/秒)がこれ以上で離したボールだけを救済する(そっと置いただけのときは救済しない)")]
        [SerializeField] private float rescueMinForwardSpeed = 0.5f;

        [Tooltip("救済で転がし直す速さ(m/秒)")]
        [SerializeField] private float rescueSpeed = 4f;

        [Tooltip("救済するとき、レーンの中心から左右にこの距離(m)より外にあれば、内側へ寄せてから転がす")]
        [SerializeField] private float rescueMaxOffset = 0.35f;

        [Tooltip("ボールの中心が床からこの高さ(m)より上にあるときは救済しない(置き台の上など)")]
        [SerializeField] private float rescueMaxHeight = 0.25f;

        [Tooltip("レーンの中心から左右にこの距離(m)より離れているときは救済しない")]
        [SerializeField] private float rescueMaxLateral = 0.7f;

        [Header("モード(M3)")]
        [Tooltip("「はじめに」パネルのモードの行")]
        [SerializeField] private ModeSelector modeSelector;

        [Tooltip("交代の案内・順位表・チャレンジに使うパネル")]
        [SerializeField] private ChoicePanel choicePanel;

        [Tooltip("10フレームのあと、交代の案内やチャレンジの結果を出すまでの秒数")]
        [SerializeField] private float modeResultDelay = 3f;

        [Header("チャレンジ(docs/plan.md「チャレンジ」)")]
        [Tooltip("ぴったり7本:倒す本数・投げられる回数")]
        [SerializeField] private int sevenTarget = 7;
        [SerializeField] private int sevenThrows = 5;

        [Tooltip("スペアをねらえ:フレームの数")]
        [SerializeField] private int spareFrames = 3;

        [Tooltip("はしっこの1本:投げられる回数")]
        [SerializeField] private int cornerThrows = 3;

        [Header("片付け")]
        [Tooltip("記録してから、倒れたピンを片付けてボールを戻すまで待つ秒数")]
        [SerializeField] private float clearDelay = 1f;

        [Tooltip("ストライクのとき、片付けるまで待つ秒数(倒れたピンが光る演出を見せる時間)")]
        [SerializeField] private float strikeClearDelay = 2.2f;

        /// <summary>
        /// 1投の記録が終わったとき。引数はフレーム(0 始まり)、何投目か(0 始まり)、倒した本数、表示記号。
        /// 演出や効果音はここにつなぐ。この時点では倒れたピンはまだ片付けておらず、PinSetter から調べられる。
        /// </summary>
        public event Action<int, int, int, string> RollRecorded;

        /// <summary>投球のあと、ボールを置き台へ戻したとき(戻りの音などをつなぐ)。</summary>
        public event Action BallReturned;

        /// <summary>10フレームが終わったとき。引数は合計点。</summary>
        public event Action<GameRecordResult> GameFinished;

        /// <summary>「はじめに」パネルを出してほしいとき(交代の案内・チャレンジの「もどる」から)。</summary>
        public event Action WaitingForStart;

        private BowlingScoreSheet sheet;
        private State state;
        private ThrowableObject currentBall;
        private bool crossedFoulLine;
        private float rollTimer;
        private float stoppedTimer;
        private float settleTimer;
        private float maxForwardSpeed;
        private bool rescued;
        private float clearTimer;
        private bool resetRackOnClear;
        private bool inputLocked;
        private string challengeId;
        private int challengeThrows;
        private int challengeFrames;

        /// <summary>投げるボール。</summary>
        public IReadOnlyList<ThrowableObject> Balls => balls;

        /// <summary>10フレームを投げ終えたか。</summary>
        public bool IsGameOver => state == State.GameOver;

        /// <summary>
        /// ボール置き台を動かしてよいか(待っている状態で、どのボールも掴まれていないとき)。
        /// </summary>
        public bool CanMoveRack
        {
            get
            {
                if (state != State.Ready && state != State.GameOver)
                {
                    return false;
                }

                foreach (ThrowableObject ball in balls)
                {
                    if (ball != null && ball.IsHeld)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>いまのスコア表。</summary>
        public BowlingScoreSheet Sheet => sheet;

        private void OnEnable()
        {
            foreach (ThrowableObject ball in balls)
            {
                if (ball == null)
                {
                    continue;
                }

                ball.Released += OnBallReleased;
                ball.GetComponent<XRGrabInteractable>().selectEntered.AddListener(OnBallGrabbed);
            }
        }

        private void OnDisable()
        {
            foreach (ThrowableObject ball in balls)
            {
                if (ball == null)
                {
                    continue;
                }

                ball.Released -= OnBallReleased;
                ball.GetComponent<XRGrabInteractable>().selectEntered.RemoveListener(OnBallGrabbed);
            }
        }

        private void Start()
        {
            NewGame();
        }

        /// <summary>
        /// 新しいゲームを始める。Inspector の右上メニュー(⋮)からも呼べる。
        /// </summary>
        [ContextMenu("新しいゲーム")]
        public void NewGame()
        {
            challengeId = null;
            sheet = new BowlingScoreSheet();
            pinSetter.ResetRack();
            state = State.Ready;
            currentBall = null;
            GameSession.InProgress = false;
            scoreBoard.Refresh(sheet);
        }

        /// <summary>
        /// 「はじめに」パネルの「はじめる」から、選んだモードでゲームを始める(チャレンジは課題の一覧から)。
        /// </summary>
        public void StartSelectedMode()
        {
            SportMode mode = modeSelector != null ? modeSelector.Mode : SportMode.Solo;
            if (mode == SportMode.Turns && !TurnSession.Active)
            {
                TurnSession.Begin(modeSelector.Choice.PlayerCount);
            }

            NewGame();
        }

        /// <summary>チャレンジの一覧を出す。</summary>
        public void ShowChallengeList()
        {
            ModeFlow.ShowChallengeList(choicePanel, SportIds.Bowling, StartChallenge, () => WaitingForStart?.Invoke());
        }

        /// <summary>チャレンジを始める。</summary>
        public void StartChallenge(string id)
        {
            NewGame();
            challengeId = id;
            challengeThrows = 0;
            challengeFrames = 0;
            if (id == "bowling.corner")
            {
                pinSetter.SetUpSingle(CornerPinIndex);
            }

            SetInputLocked(false);
            ShowChallengeBoard();
        }

        // 10番ピン(利き手が左なら7番)
        private static int CornerPinIndex => SaveStore.Settings.leftHanded ? 6 : 9;

        private void ShowChallengeBoard()
        {
            ChallengeCatalog.Entry entry = ChallengeCatalog.Find(challengeId);
            string left = challengeId == "bowling.seven" ? $"のこり {sevenThrows - challengeThrows}投"
                : challengeId == "bowling.spare" ? $"のこり {spareFrames - challengeFrames}フレーム"
                : $"のこり {cornerThrows - challengeThrows}投";
            scoreBoard.ShowChallenge(entry?.Name, left);
        }

        /// <summary>
        /// ボールを掴めないようにする(「はじめに」パネルが出ている間など)。掴んでいるボールは離すまでそのまま。
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            UpdateGrabbable();
        }

        /// <summary>
        /// ボールを掴めるのは、ロックされておらず次の投球を待っている(Ready)間だけにする。
        /// 投球中・数えている間・片付けの間に別のボールを投げたり、転がっているボールを掴み直したりできないようにする。
        /// 掴んでいる最中のボールは無効にしない(XRI が掴むのを取り消して、ボールが勝手に飛ぶのを防ぐ)。
        /// </summary>
        private void UpdateGrabbable()
        {
            // 1個掴んでいる間は、ほかのボールを掴めない(両手で2個持って続けて投げるのを防ぐ)
            bool anyHeld = false;
            foreach (ThrowableObject ball in balls)
            {
                anyHeld |= ball != null && ball.IsHeld;
            }

            bool waiting = state == State.Ready || (state == State.GameOver && restartOnGrab);
            // メニューや設定パネルが出ている間も掴めない
            bool locked = inputLocked || InputGate.GrabBlocked;
            bool allowed = !locked && waiting && !anyHeld;
            foreach (ThrowableObject ball in balls)
            {
                if (ball != null && ball.TryGetComponent(out XRGrabInteractable grab))
                {
                    // ファウルラインを越える前なら、投げたボールを拾い直せる
                    bool canRetake = state == State.Rolling && ball == currentBall && !crossedFoulLine && !locked;
                    bool enable = allowed || grab.isSelected || canRetake;
                    if (grab.enabled != enable)
                    {
                        grab.enabled = enable;
                    }
                }
            }
        }

        private void OnBallGrabbed(SelectEnterEventArgs args)
        {
            // 「はじめに」パネルがないシーン向け:終わったあとにボールを掴むと新しいゲーム
            if (state == State.GameOver && restartOnGrab && !inputLocked)
            {
                NewGame();
            }
        }

        private void OnBallReleased(ThrowableObject ball, Vector3 position)
        {
            if (state != State.Ready)
            {
                // 投球中など、数えない間に離されたボールはすぐ置き台へ戻す(ピンを倒させない)
                if (ball != currentBall)
                {
                    ReturnBall(ball);
                }

                return;
            }

            currentBall = ball;
            crossedFoulLine = false;
            rollTimer = 0f;
            stoppedTimer = 0f;
            maxForwardSpeed = 0f;
            rescued = false;
            pinSetter.MarkStandingPins();
            state = State.Rolling;
            // 投げた時点でゲームの途中(メニューからハブへもどるときに確認を出す)
            GameSession.InProgress = true;
        }

        private void Update()
        {
            UpdateGrabbable();

            switch (state)
            {
                case State.Rolling:
                    UpdateRolling();
                    break;
                case State.Settling:
                    // 最短の待ち時間がたち、立っているピンの揺れが収まってから数える。収まらなくても上限で数える
                    settleTimer += Time.deltaTime;
                    if (settleTimer >= settleTime && (pinSetter.AreStandingPinsSettled() || settleTimer >= maxSettleTime))
                    {
                        RecordRoll();
                    }

                    break;
                case State.Clearing:
                    clearTimer -= Time.deltaTime;
                    if (clearTimer <= 0f)
                    {
                        ClearPins();
                    }

                    break;
            }
        }

        private void UpdateRolling()
        {
            // ファウルラインを越える前に拾い直したら、投げていないことにする
            if (currentBall.IsHeld)
            {
                state = State.Ready;
                return;
            }

            float distance = Vector3.Dot(currentBall.transform.position - foulLine.position, foulLine.forward);
            if (distance > 0f)
            {
                crossedFoulLine = true;
            }

            rollTimer += Time.deltaTime;
            Rigidbody body = currentBall.GetComponent<Rigidbody>();
            maxForwardSpeed = Mathf.Max(maxForwardSpeed, Vector3.Dot(body.linearVelocity, foulLine.forward));
            stoppedTimer = body.linearVelocity.magnitude < stopSpeed ? stoppedTimer + Time.deltaTime : 0f;

            bool reachedPit = distance >= pitDistance;
            if (!reachedPit && rollTimer < maxRollTime && stoppedTimer < stopTime)
            {
                return;
            }

            if (!crossedFoulLine && stoppedTimer >= stopTime && TryRescue(body))
            {
                return;
            }

            if (!crossedFoulLine)
            {
                // ファウルラインより手前で落とした(救済もしない場所・速さだった):投球に数えない
                state = State.Ready;
                return;
            }

            settleTimer = 0f;
            state = State.Settling;
        }

        /// <summary>
        /// 投げたつもりのボールがファウルラインの手前で止まったら、レーンの向きに転がし直す(1投につき1回)。
        /// </summary>
        private bool TryRescue(Rigidbody body)
        {
            if (!rescueEnabled || rescued || maxForwardSpeed < rescueMinForwardSpeed)
            {
                return false;
            }

            // 置き台に戻した・手前の別の場所に置いたときは救済しない(床の上で、レーンの近くにあるときだけ)
            float height = body.position.y - foulLine.position.y;
            float side = Mathf.Abs(Vector3.Dot(body.position - foulLine.position, foulLine.right));
            if (height > rescueMaxHeight || side > rescueMaxLateral)
            {
                return false;
            }

            // レーンの外にずれていたら内側へ寄せる(ファウルラインからの前後の位置と高さはそのまま)。
            // ファウルラインの物は拡大縮小されているので、ローカル座標ではなく向きで左右の距離を測る
            float lateral = Vector3.Dot(body.position - foulLine.position, foulLine.right);
            float clamped = Mathf.Clamp(lateral, -rescueMaxOffset, rescueMaxOffset);
            Vector3 position = body.position - foulLine.right * (lateral - clamped);

            body.position = position;
            currentBall.transform.position = position;
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = foulLine.forward * rescueSpeed;

            rescued = true;
            stoppedTimer = 0f;
            rollTimer = 0f;
            return true;
        }

        /// <summary>
        /// 倒れた本数を記録してスコア板を更新し、演出(RollRecorded)に知らせる。
        /// ピンはまだ倒れたまま残しておき、clearDelay 秒後に ClearPins で片付ける。
        /// </summary>
        private void RecordRoll()
        {
            int frame = sheet.CurrentFrame;
            int rollIndex = sheet.GetRolls(frame).Count;
            int knocked = pinSetter.CountKnockedSinceMark();

            if (challengeId != null)
            {
                RecordChallengeRoll(frame, rollIndex, knocked);
                return;
            }

            resetRackOnClear = sheet.AddRoll(knocked);
            // 1投でもしたらゲームの途中(メニューからハブへもどるときに確認を出す)
            GameSession.InProgress = !sheet.IsGameOver;
            string symbol = sheet.GetRollSymbol(frame, rollIndex);
            scoreBoard.Refresh(sheet);

            if (symbol == "X")
            {
                scoreBoard.ShowMessage("STRIKE!");
            }
            else if (symbol == "/")
            {
                scoreBoard.ShowMessage("SPARE");
            }

            RollRecorded?.Invoke(frame, rollIndex, knocked, symbol);

            // フレームの終わりは休憩案内を出してよい区切り
            if (sheet.CurrentFrame != frame || sheet.IsGameOver)
            {
                RestReminder.AtBreak();
            }

            clearTimer = symbol == "X" ? strikeClearDelay : clearDelay;
            state = State.Clearing;
        }

        /// <summary>チャレンジの1投。クリアか失敗が決まったら結果を出す。</summary>
        private void RecordChallengeRoll(int frame, int rollIndex, int knocked)
        {
            challengeThrows++;
            bool cleared = false;
            bool failed = false;
            string symbol = knocked == 0 ? "-" : knocked.ToString();
            switch (challengeId)
            {
                case "bowling.seven":
                    cleared = knocked == sevenTarget;
                    failed = !cleared && challengeThrows >= sevenThrows;
                    resetRackOnClear = true;
                    break;
                case "bowling.spare":
                    resetRackOnClear = sheet.AddRoll(knocked);
                    symbol = sheet.GetRollSymbol(frame, rollIndex);
                    cleared = symbol == "X" || symbol == "/";
                    if (sheet.CurrentFrame != frame)
                    {
                        challengeFrames++;
                    }

                    failed = !cleared && challengeFrames >= spareFrames;
                    break;
                default:
                    cleared = knocked >= 1;
                    failed = !cleared && challengeThrows >= cornerThrows;
                    resetRackOnClear = true;
                    break;
            }

            RollRecorded?.Invoke(frame, rollIndex, knocked, symbol);
            scoreBoard.ShowMessage(cleared ? "クリア!" : knocked.ToString());
            RestReminder.AtBreak();
            ShowChallengeBoard();
            challengeResult = cleared ? 1 : failed ? -1 : 0;
            clearTimer = clearDelay;
            state = State.Clearing;
        }

        // チャレンジの結果(1 クリア / -1 失敗 / 0 続ける)。片付けたあとに使う
        private int challengeResult;

        /// <summary>
        /// 倒れたピンを片付けるか10本を並べ直し、ボールを戻して次の投球を待つ。
        /// </summary>
        private void ClearPins()
        {
            if (resetRackOnClear && challengeId == "bowling.corner")
            {
                pinSetter.SetUpSingle(CornerPinIndex);
            }
            else if (resetRackOnClear)
            {
                pinSetter.ResetRack();
            }
            else
            {
                pinSetter.SweepKnockedPins();
            }

            ReturnBall(currentBall);
            currentBall = null;
            BallReturned?.Invoke();

            if (challengeId != null)
            {
                if (challengeResult == 0)
                {
                    state = State.Ready;
                    return;
                }

                state = State.GameOver;
                GameSession.InProgress = false;
                string id = challengeId;
                bool cleared = challengeResult > 0;
                string detail = id == "bowling.spare" ? $"{challengeFrames}フレーム目" : $"{challengeThrows}投目";
                SetInputLocked(true);
                ModeFlow.ShowChallengeResult(choicePanel, id, cleared, cleared ? $"{detail}でクリア" : detail,
                    () => StartChallenge(id), ShowChallengeList);
                return;
            }

            if (sheet.IsGameOver && TurnSession.Active)
            {
                // 交代プレイは記録しない。スコアを出してから次の人へ
                state = State.GameOver;
                GameSession.InProgress = false;
                int turnTotal = sheet.GetTotalScore();
                scoreBoard.ShowMessage($"{TurnSession.Label(TurnSession.Current)}\n{turnTotal}");
                SetInputLocked(true);
                StartCoroutine(FinishTurnLater(turnTotal));
                return;
            }

            if (sheet.IsGameOver)
            {
                state = State.GameOver;
                int total = sheet.GetTotalScore();
                // 最後まで遊んだゲームだけ記録する(途中でやめたゲームは記録しない)
                GameRecordResult result = SaveStore.RecordFinishedGame(SportIds.Bowling, total);
                scoreBoard.ShowMessage(result.NewBest ? $"GAME  {total}\nBEST!" : $"GAME  {total}");
                GameFinished?.Invoke(result);
            }
            else
            {
                state = State.Ready;
            }
        }

        private System.Collections.IEnumerator FinishTurnLater(int total)
        {
            yield return new WaitForSeconds(modeResultDelay);
            ModeFlow.FinishTurn(choicePanel, total, "点", () => WaitingForStart?.Invoke(), () => WaitingForStart?.Invoke());
        }

        private static void ReturnBall(ThrowableObject ball)
        {
            if (ball == null || ball.IsHeld)
            {
                return;
            }

            var returner = ball.GetComponent<ReturnToHome>();
            if (returner != null)
            {
                returner.ReturnNow();
            }
        }
    }
}
