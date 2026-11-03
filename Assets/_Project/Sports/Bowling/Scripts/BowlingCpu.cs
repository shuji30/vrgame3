using System;
using System.Collections;
using UnityEngine;
using VRGame3.Common;
using Random = UnityEngine.Random;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボウリングの CPU(docs/plan.md M3「ボウリングの CPU 対戦」、見た目は docs/art-direction.md「M3 ボウリングの CPU 対戦の見た目」)。
    /// 隣のレーンで、プレイヤーと交互に1フレームずつ投げる。球は物理で実際に転がす。
    /// 助走路を滑るように進み、手を後ろへ引いてから振り、振り子の一番下で放す。
    /// ねらいは1投目がポケット、2投目が残ったピンの真ん中。強さで速さとねらいのばらつきが変わる。
    /// </summary>
    public class BowlingCpu : MonoBehaviour
    {
        /// <summary>強さ1段分。</summary>
        [Serializable]
        public class Level
        {
            [Tooltip("強さの名前")]
            public string label = "ふつう";

            [Tooltip("強さの印")]
            public string marks = "▲▲";

            [Tooltip("強さの色")]
            public Color color = new Color32(0x3A, 0xA7, 0xFF, 0xFF);

            [Tooltip("球の速さ(m/秒)")]
            public float speed = 7f;

            [Tooltip("ねらいの左右のばらつき(m)")]
            public float scatter = 0.08f;
        }

        [Tooltip("CPU のボール")]
        [SerializeField] private Rigidbody ball;

        [Tooltip("CPU のレーンのピン")]
        [SerializeField] private PinSetter pinSetter;

        [Tooltip("CPU のレーンのファウルライン(青い矢印がピンの方)")]
        [SerializeField] private Transform foulLine;

        [Tooltip("大型スコア板の CPU の段")]
        [SerializeField] private BowlingScoreBoard scoreBoard;

        [Tooltip("体の根元(足元)")]
        [SerializeField] private Transform body;

        [Tooltip("ボールを投げる手")]
        [SerializeField] private Transform throwHand;

        [Tooltip("名札の背景(強さの色)")]
        [SerializeField] private UnityEngine.UI.Image nameTagBack;

        [Tooltip("名札の文字")]
        [SerializeField] private TMPro.TMP_Text nameTagText;

        [Tooltip("強さの色に塗る物(足元の敷物)")]
        [SerializeField] private Renderer[] levelColorRenderers = new Renderer[0];

        [Tooltip("強さ3段階(やさしい / ふつう / つよい)")]
        [SerializeField] private Level[] levels =
        {
            new Level { label = "やさしい", marks = "▲", color = new Color32(0x6C, 0xC3, 0x6A, 0xFF), speed = 7f, scatter = 0.15f },
            new Level { label = "ふつう", marks = "▲▲", color = new Color32(0x3A, 0xA7, 0xFF, 0xFF), speed = 8f, scatter = 0.08f },
            new Level { label = "つよい", marks = "▲▲▲", color = new Color32(0xFF, 0x8A, 0x3D, 0xFF), speed = 9f, scatter = 0.04f },
        };

        [Header("動き")]
        [Tooltip("待つ位置(ファウルラインからの手前の距離, m)")]
        [SerializeField] private float waitDistance = 3.2f;

        [Tooltip("投げるときに進んで止まる位置(ファウルラインからの手前の距離, m)")]
        [SerializeField] private float throwDistance = 0.7f;

        [Tooltip("ポケットのねらい(ピンの中心から右へ, m)")]
        [SerializeField] private float pocketOffset = 0.06f;

        [Tooltip("ピットに着いたとみなす距離(ファウルラインから, m)")]
        [SerializeField] private float pitDistance = 19.6f;

        [Tooltip("投げてからこの秒数で、どこにあっても投球を終える")]
        [SerializeField] private float maxRollTime = 8f;

        [Tooltip("ピンが倒れきるのを待つ秒数")]
        [SerializeField] private float settleTime = 2.5f;

        [Header("確認用")]
        [Tooltip("オンにすると、プレイを始めたときに CPU だけで10ゲーム投げ、平均点を Console に出す(強さの合わせ込み用)")]
        [SerializeField] private bool simulateTenGames;

        [Tooltip("試験で投げる強さ(0 やさしい / 1 ふつう / 2 つよい)")]
        [Range(0, 2)]
        [SerializeField] private int simulateLevel = 1;

        private BowlingScoreSheet sheet = new BowlingScoreSheet();
        private int level = 1;
        private Vector3 waitPosition;
        private Vector3 handRest;
        private bool playing;

        /// <summary>CPU の1投が記録されたとき(倒した本数, 記号)。</summary>
        public event Action<int, string> RollRecorded;

        public BowlingScoreSheet Sheet => sheet;

        public Level CurrentLevel => levels[Mathf.Clamp(level, 0, levels.Length - 1)];

        /// <summary>CPU が投げている途中か。</summary>
        public bool IsPlaying => playing;

        private void Awake()
        {
            waitPosition = body.position;
            handRest = throwHand.localPosition;
            ball.gameObject.SetActive(false);
            // 回転の上限(既定 7 ラジアン/秒)のままだと、転がる回転に合わせて球が約 0.8m/秒まで減速してしまう
            ball.maxAngularVelocity = 100f;
        }

        /// <summary>新しいゲーム。強さを決め、スコア板の CPU の段を空にする。</summary>
        public void NewGame(int levelIndex)
        {
            StopAllCoroutines();
            playing = false;
            level = Mathf.Clamp(levelIndex, 0, levels.Length - 1);
            sheet = new BowlingScoreSheet();
            pinSetter.ResetRack();
            scoreBoard.Refresh(sheet);
            body.position = waitPosition;
            ball.gameObject.SetActive(false);
            ApplyLevelLook();
            Chatter.Say("よろしくね!", "しょうぶだよ!");
        }

        /// <summary>スコア板の CPU の段を空にして隠す(ひとりで遊ぶとき)。</summary>
        public void Clear()
        {
            StopAllCoroutines();
            playing = false;
            sheet = new BowlingScoreSheet();
            scoreBoard.Refresh(sheet);
            ball.gameObject.SetActive(false);
            pinSetter.ResetRack();
            body.position = waitPosition;
            throwHand.localPosition = handRest;
        }

        private IEnumerator Start()
        {
            if (!simulateTenGames)
            {
                yield break;
            }

            int total = 0;
            for (int game = 0; game < 10; game++)
            {
                NewGame(simulateLevel);
                while (!sheet.IsGameOver)
                {
                    bool done = false;
                    PlayFrame(() => done = true);
                    while (!done)
                    {
                        yield return null;
                    }
                }

                total += sheet.GetTotalScore();
                Debug.Log($"[CPU の試験] {CurrentLevel.label} {game + 1}ゲーム目 {sheet.GetTotalScore()}点");
            }

            Debug.Log($"[CPU の試験] {CurrentLevel.label} 10ゲームの平均 {total / 10f:0.0}点");
        }

        /// <summary>おしゃべり(無ければ付ける)。</summary>
        public CpuChatter Chatter
        {
            get
            {
                if (chatter == null)
                {
                    // Unity の物は ?? で null を正しく判定できないので、明示して確かめる
                    chatter = body.GetComponent<CpuChatter>();
                    if (chatter == null)
                    {
                        chatter = body.gameObject.AddComponent<CpuChatter>();
                    }
                }

                return chatter;
            }
        }

        private CpuChatter chatter;

        /// <summary>プレイヤーの投球に反応する(CPU 対戦のとき)。</summary>
        public void OnPlayerRoll(string symbol)
        {
            if (symbol == "X")
            {
                Chatter.Say("すごい!", "ナイスストライク!");
            }
            else if (symbol == "/")
            {
                Chatter.MaybeSay("ナイススペア!");
            }
        }

        /// <summary>1フレームを投げる。終わったら onDone。</summary>
        public void PlayFrame(Action onDone)
        {
            StartCoroutine(Frame(onDone));
        }

        private IEnumerator Frame(Action onDone)
        {
            playing = true;
            int frame = sheet.CurrentFrame;
            pinSetter.ResetRack();
            yield return new WaitForSeconds(0.8f);
            while (!sheet.IsGameOver && sheet.CurrentFrame == frame)
            {
                yield return Throw(sheet.GetRolls(frame).Count == 0 || sheet.PinsStandingBefore(frame, sheet.GetRolls(frame).Count) == 10);
                yield return new WaitForSeconds(settleTime);
                int rollIndex = sheet.GetRolls(frame).Count;
                int knocked = pinSetter.CountKnockedSinceMark();
                bool reset = sheet.AddRoll(knocked);
                string symbol = sheet.GetRollSymbol(frame, rollIndex);
                scoreBoard.Refresh(sheet);
                RollRecorded?.Invoke(knocked, symbol);
                if (symbol == "X")
                {
                    Chatter.Say("ストライク!", "やったー!");
                }
                else if (symbol == "/")
                {
                    Chatter.MaybeSay("スペア!", "セーフ!");
                }
                else if (knocked == 0)
                {
                    Chatter.Say("あれ〜?", "ガター…");
                }

                HumanoidFace face = body.GetComponentInChildren<HumanoidFace>();
                if (face != null)
                {
                    face.Show(symbol == "X" || symbol == "/" ? HumanoidFace.Mood.Happy : knocked == 0 ? HumanoidFace.Mood.Sad : HumanoidFace.Mood.Calm);
                }

                if (symbol == "X" || symbol == "/")
                {
                    // 小さく祝う:その場で2回跳ねる(docs/art-direction.md 7.6)
                    yield return Hop(2);
                }
                ball.gameObject.SetActive(false);
                yield return new WaitForSeconds(1f);
                if (reset)
                {
                    pinSetter.ResetRack();
                }
                else
                {
                    pinSetter.SweepKnockedPins();
                }
            }

            // 待つ位置へ戻る
            yield return Slide(body.position, waitPosition, 1f);
            playing = false;
            onDone?.Invoke();
        }

        /// <summary>1投:進んで、振って、放す。ピットに着くか止まるまで待つ。</summary>
        private IEnumerator Throw(bool fullRack)
        {
            Level l = CurrentLevel;
            Vector3 forward = foulLine.forward;
            Vector3 start = foulLine.position - forward * waitDistance;
            Vector3 end = foulLine.position - forward * throwDistance;
            body.rotation = Quaternion.LookRotation(forward, Vector3.up);
            yield return Slide(start, end, 0.8f);

            // ボールを手の下に出し、後ろへ引いてから前へ振る
            ball.gameObject.SetActive(true);
            ball.isKinematic = true;
            Vector3 back = handRest + new Vector3(0f, -0.1f, -0.7f);
            Vector3 bottom = new Vector3(handRest.x, 0.25f, 0.1f);
            yield return MoveHand(handRest, back, 0.4f);
            yield return MoveHand(back, bottom, 0.3f);

            // ねらう点:1投目はポケット、2投目は残ったピンの真ん中
            pinSetter.MarkStandingPins();
            Vector3 aim = fullRack ? PinCenter(true) + foulLine.right * pocketOffset : PinCenter(false);
            aim += foulLine.right * Random.Range(-l.scatter, l.scatter);
            Vector3 from = throwHand.position + Vector3.down * 0.12f;
            // 放す位置は、真下の床にちょうど載る高さにする(浮いた所から落とすと跳ねる)
            float radius = ball.transform.lossyScale.x * 0.5f;
            if (Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                from.y = floor.point.y + radius + 0.002f;
            }

            Vector3 direction = aim - from;
            direction.y = 0f;
            direction.Normalize();

            ball.transform.position = from;
            ball.position = from;
            ball.isKinematic = false;
            ball.linearVelocity = direction * l.speed;
            // 滑らずに転がる回転(速さ ÷ 半径)。回転の向きは進む向きと上向きの外積
            ball.angularVelocity = Vector3.Cross(Vector3.up, direction) * (l.speed / Mathf.Max(0.05f, radius));

            // 振り抜いて手を上げる
            yield return MoveHand(bottom, handRest + new Vector3(0f, 0.2f, 0.3f), 0.3f);

            float t = 0f;
            while (t < maxRollTime && Vector3.Dot(ball.position - foulLine.position, forward) < pitDistance)
            {
                t += Time.deltaTime;
                yield return null;
            }

            throwHand.localPosition = handRest;
        }

        /// <summary>立っているピンの真ん中(fullRack なら1番ピンの位置)。</summary>
        private Vector3 PinCenter(bool headPin)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (BowlingPin pin in pinSetter.Pins)
            {
                if (!pin.gameObject.activeSelf || pin.IsKnockedDown)
                {
                    continue;
                }

                if (headPin)
                {
                    return pin.transform.position;
                }

                sum += pin.transform.position;
                count++;
            }

            return count > 0 ? sum / count : foulLine.position + foulLine.forward * 18.3f;
        }

        private IEnumerator Hop(int times)
        {
            Vector3 start = body.position;
            for (int i = 0; i < times; i++)
            {
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    body.position = start + Vector3.up * (Mathf.Sin(t / 0.3f * Mathf.PI) * 0.12f);
                    yield return null;
                }
            }

            body.position = start;
        }

        private IEnumerator Slide(Vector3 from, Vector3 to, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                body.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
                yield return null;
            }

            body.position = to;
        }

        private IEnumerator MoveHand(Vector3 from, Vector3 to, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                throwHand.localPosition = Vector3.Lerp(from, to, t / time);
                if (ball.isKinematic)
                {
                    ball.transform.position = throwHand.position + Vector3.down * 0.12f;
                }

                yield return null;
            }

            throwHand.localPosition = to;
        }

        private void ApplyLevelLook()
        {
            Level l = CurrentLevel;
            if (nameTagBack != null)
            {
                nameTagBack.color = l.color;
            }

            if (nameTagText != null)
            {
                nameTagText.text = $"{l.label} {l.marks}";
            }

            var block = new MaterialPropertyBlock();
            foreach (Renderer r in levelColorRenderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", l.color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
