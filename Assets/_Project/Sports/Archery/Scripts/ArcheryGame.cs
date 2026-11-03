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

        [Header("モード(M3)")]
        [Tooltip("「はじめに」パネルのモードの行")]
        [SerializeField] private ModeSelector modeSelector;

        [Tooltip("交代の案内・順位表・チャレンジに使うパネル")]
        [SerializeField] private ChoicePanel choicePanel;

        [Header("チャレンジ(docs/plan.md「チャレンジ」)")]
        [Tooltip("ねらいを保て:距離(m)・本数・続けて何本・何点以上")]
        [SerializeField] private float keepDistance = 10f;
        [SerializeField] private int keepArrows = 6;
        [SerializeField] private int keepNeed = 3;
        [SerializeField] private int keepMinScore = 8;

        [Tooltip("とおくの的:距離(m)・本数・当てる本数")]
        [SerializeField] private float farDistance = 30f;
        [SerializeField] private int farArrows = 6;
        [SerializeField] private int farNeed = 3;

        [Tooltip("6本で45点:距離(m)・本数・合計の点")]
        [SerializeField] private float sumDistance = 20f;
        [SerializeField] private int sumArrows = 6;
        [SerializeField] private int sumNeed = 45;

        private string challengeId;
        private int challengeShots;
        private int challengeStreak;
        private int challengeHits;
        private int challengeSum;

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
                bow.Nocked += OnNocked;
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
                bow.Nocked -= OnNocked;
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
            challengeId = null;
            SportMode mode = modeSelector != null ? modeSelector.Mode : SportMode.Solo;
            if (mode == SportMode.Turns && !TurnSession.Active)
            {
                TurnSession.Begin(modeSelector.Choice.PlayerCount);
            }

            sheet = new ArcheryScoreSheet();
            GameSession.InProgress = false;
            BeginTutorial();
            target.StartCoroutine(target.ShrinkArrows(0.01f));
            target.PlaceAt(DistanceOf(0), TargetHeight());
            scoreBoard.Refresh(sheet, DistanceOf(0));
            EquipBow();
            state = State.Ready;
            TargetPlaced?.Invoke(0, DistanceOf(0));
        }

        /// <summary>チャレンジの一覧を出す(「はじめに」パネルの「かだいをえらぶ」から呼ぶ)。</summary>
        public void ShowChallengeList()
        {
            ModeFlow.ShowChallengeList(choicePanel, SportIds.Archery, StartChallenge, BackToStart);
        }

        /// <summary>チャレンジを始める。</summary>
        public void StartChallenge(string id)
        {
            StopAllCoroutines();
            challengeId = id;
            challengeShots = 0;
            challengeStreak = 0;
            challengeHits = 0;
            challengeSum = 0;
            GameSession.InProgress = false;
            BeginTutorial();
            target.StartCoroutine(target.ShrinkArrows(0.01f));
            target.PlaceAt(CurrentDistance, TargetHeight());
            EquipBow();
            state = State.Ready;
            UpdateChallengeBoard();
            TargetPlaced?.Invoke(0, CurrentDistance);
        }

        private int ChallengeArrows => challengeId == "archery.keep" ? keepArrows : challengeId == "archery.far" ? farArrows : sumArrows;

        /// <summary>いまの的の距離(チャレンジなら課題の距離)。</summary>
        private float CurrentDistance =>
            challengeId == null ? DistanceOf(sheet.CurrentRound)
            : challengeId == "archery.keep" ? keepDistance
            : challengeId == "archery.far" ? farDistance
            : sumDistance;

        private void UpdateChallengeBoard()
        {
            ChallengeCatalog.Entry entry = ChallengeCatalog.Find(challengeId);
            string progress = challengeId == "archery.keep" ? $"つづけて {challengeStreak}/{keepNeed}"
                : challengeId == "archery.far" ? $"当たり {challengeHits}/{farNeed}"
                : $"合計 {challengeSum}/{sumNeed}";
            scoreBoard.ShowChallenge($"{entry?.Name}  {CurrentDistance:0}m", progress, ChallengeArrows - challengeShots);
        }

        /// <summary>チャレンジの1本。クリアか失敗が決まったら結果を出す。</summary>
        private void OnChallengeArrow(Arrow.Hit hit)
        {
            int score = hit.OnTarget ? hit.Score : 0;
            challengeShots++;
            challengeStreak = score >= keepMinScore ? challengeStreak + 1 : 0;
            challengeHits += score > 0 ? 1 : 0;
            challengeSum += score;
            ArrowScored?.Invoke(0, challengeShots - 1, hit);
            UpdateChallengeBoard();
            RestReminder.AtBreak();

            int left = ChallengeArrows - challengeShots;
            bool cleared = challengeId == "archery.keep" ? challengeStreak >= keepNeed
                : challengeId == "archery.far" ? challengeHits >= farNeed
                : challengeSum >= sumNeed;
            bool failed = !cleared && (left <= 0 ||
                (challengeId == "archery.keep" && challengeStreak + left < keepNeed) ||
                (challengeId == "archery.far" && challengeHits + left < farNeed) ||
                (challengeId == "archery.45" && challengeSum + left * 10 < sumNeed));
            if (!cleared && !failed)
            {
                state = State.Ready;
                return;
            }

            state = State.GameOver;
            StartCoroutine(FinishChallenge(cleared));
        }

        private IEnumerator FinishChallenge(bool cleared)
        {
            string id = challengeId;
            string detail = id == "archery.keep" ? $"つづけて {challengeStreak}本"
                : id == "archery.far" ? $"当たり {challengeHits}本"
                : $"合計 {challengeSum}点";
            yield return new WaitForSeconds(resultDelay);
            bow.Unequip();
            ModeFlow.ShowChallengeResult(choicePanel, id, cleared, detail, () => StartChallenge(id), ShowChallengeList);
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
            scoreBoard.PlaceForHand(leftHanded);
            bow.Equip(bowHand, drawHand, bowInteractor, drawInteractor, leftHanded);
        }

        private void OnNocked()
        {
            TutorialBoard.Next(0);
        }

        /// <summary>てびき(初めてのときだけ):1 つがえる → 2 引いて放つ → 3 ねらい方。</summary>
        private static void BeginTutorial()
        {
            bool left = SaveStore.Settings.leftHanded;
            TutorialBoard.Begin("archery",
                new[]
                {
                    "弓は自動で もちます。反対の手を\n弦のまんなかに近づけて トリガー",
                    "トリガーを引いたまま うしろへ引いて、\nはなすと 矢がとぶ",
                    "点線は 矢のとぶ向き。\n的のまんなかを ねらおう",
                },
                new[] { TutorialBoard.Button.Trigger, TutorialBoard.Button.Trigger, TutorialBoard.Button.None },
                new[] { left, left, left });
        }

        private void OnFired(Arrow arrow)
        {
            TutorialBoard.Next(1);
            state = State.Flying;
            GameSession.InProgress = true;
        }

        private void OnArrowStopped(Arrow arrow, Arrow.Hit hit)
        {
            TutorialBoard.Finish(2);
            if (state != State.Flying)
            {
                return;
            }

            if (challengeId != null)
            {
                OnChallengeArrow(hit);
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
            if (TurnSession.Active)
            {
                // 交代プレイは記録しない。スコアを出して次の人へ
                yield return new WaitForSeconds(resultDelay);
                bow.Unequip();
                ModeFlow.FinishTurn(choicePanel, sheet.GetTotal(), "点", BackToStart, BackToStart);
                yield break;
            }

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
                target.PlaceAt(CurrentDistance, TargetHeight());
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
