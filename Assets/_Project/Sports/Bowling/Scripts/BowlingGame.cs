using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボウリング1レーンの進行役。投げる → ボールがピットに着く(または止まる)→ ピンが落ち着くのを待つ →
    /// 倒れた本数をスコア表に記録 → ピンを片付けるか並べ直す → ボールを戻す、を繰り返す。
    /// 10フレームが終わったあと、ボールを掴むと新しいゲームが始まる。
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

        [Header("投球の終わりの判定")]
        [Tooltip("ファウルラインからこの距離(m)まで進んだら、ボールがピットに着いたとみなす")]
        [SerializeField] private float pitDistance = 19.6f;

        [Tooltip("ボールがこの速さ(m/秒)より遅い状態が続いたら、止まったとみなして投球を終える")]
        [SerializeField] private float stopSpeed = 0.05f;

        [Tooltip("止まったとみなすまでの秒数")]
        [SerializeField] private float stopTime = 1f;

        [Tooltip("投げてからこの秒数たったら、どこにあっても投球を終える")]
        [SerializeField] private float maxRollTime = 12f;

        [Tooltip("投球が終わってから、倒れた本数を数えるまで待つ秒数(ピンが落ち着くのを待つ)")]
        [SerializeField] private float settleTime = 2f;

        [Tooltip("記録してから、倒れたピンを片付けてボールを戻すまで待つ秒数")]
        [SerializeField] private float clearDelay = 1f;

        [Tooltip("ストライクのとき、片付けるまで待つ秒数(倒れたピンが光る演出を見せる時間)")]
        [SerializeField] private float strikeClearDelay = 2.2f;

        /// <summary>
        /// 1投の記録が終わったとき。引数はフレーム(0 始まり)、何投目か(0 始まり)、倒した本数、表示記号。
        /// 演出や効果音はここにつなぐ。この時点では倒れたピンはまだ片付けておらず、PinSetter から調べられる。
        /// </summary>
        public event Action<int, int, int, string> RollRecorded;

        /// <summary>10フレームが終わったとき。引数は合計点。</summary>
        public event Action<int> GameFinished;

        private BowlingScoreSheet sheet;
        private State state;
        private ThrowableObject currentBall;
        private bool crossedFoulLine;
        private float rollTimer;
        private float stoppedTimer;
        private float settleTimer;
        private float clearTimer;
        private bool resetRackOnClear;

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

        private void OnBallGrabbed(SelectEnterEventArgs args)
        {
            if (state == State.GameOver)
            {
                NewGame();
            }
        }

        private void OnBallReleased(ThrowableObject ball, Vector3 position)
        {
            if (state != State.Ready)
            {
                return;
            }

            currentBall = ball;
            crossedFoulLine = false;
            rollTimer = 0f;
            stoppedTimer = 0f;
            pinSetter.MarkStandingPins();
            state = State.Rolling;
        }

        private void Update()
        {
            switch (state)
            {
                case State.Rolling:
                    UpdateRolling();
                    break;
                case State.Settling:
                    settleTimer -= Time.deltaTime;
                    if (settleTimer <= 0f)
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
            stoppedTimer = body.linearVelocity.magnitude < stopSpeed ? stoppedTimer + Time.deltaTime : 0f;

            bool reachedPit = distance >= pitDistance;
            if (!reachedPit && rollTimer < maxRollTime && stoppedTimer < stopTime)
            {
                return;
            }

            if (!crossedFoulLine)
            {
                // ファウルラインより手前で落とした:投球に数えない(救済の自動転がしは投げ心地の調整で入れる)
                state = State.Ready;
                return;
            }

            settleTimer = settleTime;
            state = State.Settling;
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

            if (sheet.IsGameOver)
            {
                state = State.GameOver;
                int total = sheet.GetTotalScore();
                scoreBoard.ShowMessage($"GAME  {total}");
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
