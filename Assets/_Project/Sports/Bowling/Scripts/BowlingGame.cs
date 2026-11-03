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
        public event Action<int> GameFinished;

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

        /// <summary>投げるボール。</summary>
        public IReadOnlyList<ThrowableObject> Balls => balls;

        /// <summary>10フレームを投げ終えたか。</summary>
        public bool IsGameOver => state == State.GameOver;

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
            sheet = new BowlingScoreSheet();
            pinSetter.ResetRack();
            state = State.Ready;
            currentBall = null;
            scoreBoard.Refresh(sheet);
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
            bool allowed = !inputLocked && waiting && !anyHeld;
            foreach (ThrowableObject ball in balls)
            {
                if (ball != null && ball.TryGetComponent(out XRGrabInteractable grab))
                {
                    // ファウルラインを越える前なら、投げたボールを拾い直せる
                    bool canRetake = state == State.Rolling && ball == currentBall && !crossedFoulLine && !inputLocked;
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

            resetRackOnClear = sheet.AddRoll(knocked);
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

            clearTimer = symbol == "X" ? strikeClearDelay : clearDelay;
            state = State.Clearing;
        }

        /// <summary>
        /// 倒れたピンを片付けるか10本を並べ直し、ボールを戻して次の投球を待つ。
        /// </summary>
        private void ClearPins()
        {
            if (resetRackOnClear)
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

            if (sheet.IsGameOver)
            {
                state = State.GameOver;
                int total = sheet.GetTotalScore();
                // 最後まで遊んだゲームだけ記録する(途中でやめたゲームは記録しない)
                bool newBest = SaveStore.RecordFinishedGame(SportIds.Bowling, total);
                scoreBoard.ShowMessage(newBest ? $"GAME  {total}\nBEST!" : $"GAME  {total}");
                GameFinished?.Invoke(total);
            }
            else
            {
                state = State.Ready;
            }
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
