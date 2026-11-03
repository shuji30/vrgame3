using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRGame3.Common;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// アーチェリー1ゲームの進行(docs/plan.md「アーチェリー」)。3ラウンド × 6本、的は 10 / 20 / 30m。
    /// 「はじめに」パネルの「はじめる」で弓を利き手でない方に付け、1本ずつ放って点を数える。
    /// ラウンドの終わりに刺さった矢を消して的を次の距離へ置き直し、18本目のあとに記録して結果を出す。
    /// </summary>
    public class ArcheryGame : MonoBehaviour
    {
        private enum State
        {
            Waiting,
            Ready,
            Flying,
            RoundBreak,
            GameOver,
        }

        [Header("部品")]
        [Tooltip("弓")]
        [SerializeField] private Bow bow;

        [Tooltip("的")]
        [SerializeField] private ArcheryTarget target;

        [Tooltip("スコア板")]
        [SerializeField] private ArcheryScoreBoard scoreBoard;

        [Tooltip("結果の表示")]
        [SerializeField] private ResultPanel resultPanel;

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
        [Tooltip("ラウンドごとの的までの距離(m)")]
        [SerializeField] private float[] distances = { 10f, 20f, 30f };

        [Tooltip("的の中心の高さ = 身長 × この値")]
        [SerializeField] private float targetHeightRatio = 0.85f;

        [Tooltip("ラウンドの終わりに、次のラウンドの的を出すまでの秒数")]
        [SerializeField] private float roundBreakTime = 2f;

        [Tooltip("18本目のあと、結果を出すまでの秒数")]
        [SerializeField] private float resultDelay = 1.5f;

        private ArcheryScoreSheet sheet = new ArcheryScoreSheet();
        private State state = State.Waiting;

        /// <summary>1本の結果が出たとき(ラウンド, 何本目, 結果)。</summary>
        public event Action<int, int, Arrow.Hit> ArrowScored;

        /// <summary>ラウンドが終わったとき(終わったラウンド, ラウンドの合計)。</summary>
        public event Action<int, int> RoundFinished;

        /// <summary>的が次の距離に出たとき(ラウンド, 距離)。</summary>
        public event Action<int, float> TargetPlaced;

        /// <summary>18本を射ち終えて記録したとき。</summary>
        public event Action<GameRecordResult> GameFinished;

        /// <summary>「はじめる」を押すのを待っているとき。</summary>
        public event Action WaitingForStart;

        public ArcheryScoreSheet Sheet => sheet;

        /// <summary>ラウンドの距離(m)。</summary>
        public float DistanceOf(int round) => distances[Mathf.Clamp(round, 0, distances.Length - 1)];

        public XRBaseInputInteractor LeftInteractor => leftInteractor;
        public XRBaseInputInteractor RightInteractor => rightInteractor;

        private void OnEnable()
        {
            SaveStore.Changed += OnSettingsChanged;
            if (bow != null)
            {
                bow.Fired += OnFired;
                bow.ArrowStopped = OnArrowStopped;
            }

            if (resultPanel != null)
            {
                resultPanel.Retry.AddListener(BackToStart);
            }
        }

        private void OnDisable()
        {
            SaveStore.Changed -= OnSettingsChanged;
            if (bow != null)
            {
                bow.Fired -= OnFired;
            }

            if (resultPanel != null)
            {
                resultPanel.Retry.RemoveListener(BackToStart);
            }
        }

        private void Start()
        {
            bow.Unequip();
            target.PlaceAt(DistanceOf(0), TargetHeight());
            scoreBoard.Refresh(sheet, DistanceOf(0));
        }

        private void Update()
        {
            // メニューが出ている間は引いている矢を消す(メニューを押すトリガーで放たないように)
            if (InputGate.GrabBlocked && bow.IsDrawing)
            {
                bow.CancelDraw();
            }

            bow.CanShoot = state == State.Ready;
        }

        /// <summary>新しいゲームを始めて弓を手に付ける(「はじめに」パネルの「はじめる」から呼ぶ)。</summary>
        public void StartGame()
        {
            StopAllCoroutines();
            sheet = new ArcheryScoreSheet();
            GameSession.InProgress = false;
            target.StartCoroutine(target.ShrinkArrows(0.01f));
            target.PlaceAt(DistanceOf(0), TargetHeight());
            scoreBoard.Refresh(sheet, DistanceOf(0));
            EquipBow();
            state = State.Ready;
            TargetPlaced?.Invoke(0, DistanceOf(0));
        }

        /// <summary>結果から「もういちど」:はじめにパネルへ戻る。</summary>
        private void BackToStart()
        {
            state = State.Waiting;
            WaitingForStart?.Invoke();
        }

        private void EquipBow()
        {
            bool leftHanded = SaveStore.Settings.leftHanded;
            // 弓は利き手でない方、矢は利き手で引く
            Transform bowHand = leftHanded ? rightController : leftController;
            Transform drawHand = leftHanded ? leftController : rightController;
            XRBaseInputInteractor bowInteractor = leftHanded ? rightInteractor : leftInteractor;
            XRBaseInputInteractor drawInteractor = leftHanded ? leftInteractor : rightInteractor;
            bow.AimAssist = SaveStore.Settings.aimAssist;
            bow.Equip(bowHand, drawHand, bowInteractor, drawInteractor, leftHanded);
        }

        private void OnFired(Arrow arrow)
        {
            state = State.Flying;
            GameSession.InProgress = true;
        }

        private void OnArrowStopped(Arrow arrow, Arrow.Hit hit)
        {
            if (state != State.Flying)
            {
                return;
            }

            int round = sheet.CurrentRound;
            int index = sheet.ArrowsInCurrentRound;
            bool roundEnded = sheet.AddArrow(hit.OnTarget ? hit.Score : 0, hit.OnTarget && hit.IsX);
            // ラウンドが終わったときは、的を置き直すまで終わったラウンドの6本を見せておく
            if (roundEnded && !sheet.IsGameOver)
            {
                scoreBoard.RefreshRound(sheet, round, DistanceOf(round));
            }
            else
            {
                scoreBoard.Refresh(sheet, DistanceOf(sheet.CurrentRound));
            }
            ArrowScored?.Invoke(round, index, hit);

            if (!roundEnded)
            {
                state = State.Ready;
                return;
            }

            RoundFinished?.Invoke(round, sheet.GetRoundTotal(round));
            // ラウンドの終わりと結果は休憩案内を出してよい区切り
            RestReminder.AtBreak();
            if (sheet.IsGameOver)
            {
                StartCoroutine(FinishGame());
            }
            else
            {
                StartCoroutine(NextRound());
            }
        }

        private IEnumerator NextRound()
        {
            state = State.RoundBreak;
            yield return new WaitForSeconds(roundBreakTime);
            int round = sheet.CurrentRound;
            yield return target.MoveTo(DistanceOf(round), TargetHeight());
            scoreBoard.Refresh(sheet, DistanceOf(round));
            TargetPlaced?.Invoke(round, DistanceOf(round));
            // 区切りの間に利き手を変えていたら持ち替える
            EquipBow();
            state = State.Ready;
        }

        private IEnumerator FinishGame()
        {
            state = State.GameOver;
            GameSession.InProgress = false;
            GameRecordResult result = SaveStore.RecordFinishedGame(SportIds.Archery, sheet.GetTotal());
            yield return new WaitForSeconds(resultDelay);
            bow.Unequip();
            GameFinished?.Invoke(result);
            if (resultPanel != null)
            {
                resultPanel.Show(result);
            }
            else
            {
                BackToStart();
            }
        }

        private float TargetHeight()
        {
            return SaveStore.Settings.heightCm / 100f * targetHeightRatio;
        }

        private void OnSettingsChanged()
        {
            // 待っている間と構えている間だけ、身長に合わせて的の高さを直す
            if (state == State.Waiting || state == State.Ready)
            {
                target.PlaceAt(DistanceOf(sheet.CurrentRound), TargetHeight());
            }

            // 構えている間に利き手を変えたら、弓を持ち替える
            if (state == State.Ready)
            {
                EquipBow();
            }

            bow.AimAssist = SaveStore.Settings.aimAssist;
        }
    }
}
