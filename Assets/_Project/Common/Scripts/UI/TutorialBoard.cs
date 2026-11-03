using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// てびき(チュートリアル)の手順の板(docs/plan.md M4「チュートリアル」、見た目は docs/art-direction.md 6.14)。
    /// 短い文と Vive ワンドの簡単な図を出し、その手順で使うボタンに色を付ける。実際のコントローラーのボタンにも光る輪を出す。
    /// 出した瞬間の頭の前(利き手と反対側へ少しずらす)に置き、頭には追従させない。文字は手元に出さない。
    /// </summary>
    public class TutorialBoard : MonoBehaviour
    {
        /// <summary>教えるボタン。</summary>
        public enum Button
        {
            None,
            Trigger,
            PadCenter,
            PadSides,
            Menu,
        }

        [Tooltip("板全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("見出し(「1 / 3」など)")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("本文(1手順 2行まで)")]
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("ワンドの図のトリガー")]
        [SerializeField] private Image triggerImage;

        [Tooltip("ワンドの図のトラックパッド")]
        [SerializeField] private Image padImage;

        [Tooltip("ワンドの図のメニューボタン")]
        [SerializeField] private Image menuImage;

        [Tooltip("ワンドの図で、教えるボタンのまわりに広がる輪")]
        [SerializeField] private RectTransform ring;

        [Tooltip("「わかった」ボタン(ハブのはじめての案内だけで使う)")]
        [SerializeField] private GameObject okButton;

        [Tooltip("実際のコントローラーの光る輪(左のトリガー・左のパッド・右のトリガー・右のパッドの順)")]
        [SerializeField] private GameObject[] controllerRings = new GameObject[4];

        [Tooltip("頭からの距離(m)")]
        [SerializeField] private float distance = 1.0f;

        [Tooltip("利き手と反対側へずらす距離(m)")]
        [SerializeField] private float sideOffset = 0.55f;

        [Tooltip("目の高さからの下げ幅(m)")]
        [SerializeField] private float drop = 0.2f;

        [Tooltip("光る輪の周期(秒)")]
        [SerializeField] private float pulsePeriod = 1.2f;

        [Tooltip("次の手順へ進んだときの音")]
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip nextClip;
        [SerializeField] private AudioClip completeClip;

        [Tooltip("音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.25f;

        private static readonly Color Normal = new Color32(0xB8, 0xB4, 0xC8, 0xFF);
        private static readonly Color Lit = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        private Action onOk;
        private bool shown;
        private Button lit;
        private bool litLeftHand;

        /// <summary>板が出ているか。</summary>
        public bool IsOpen => shown;

        /// <summary>いまのシーンのてびきの板(無ければ null)。</summary>
        public static TutorialBoard Current { get; private set; }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>いまのシーンの板で手順を始める(板が無ければ何もしない)。</summary>
        public static void Begin(string id, string[] texts, Button[] buttons, bool[] leftHand)
        {
            Current?.BeginSteps(id, texts, buttons, leftHand);
        }

        /// <summary>いまのシーンの板の手順を進める。</summary>
        public static void Next(int from)
        {
            Current?.Advance(from);
        }

        /// <summary>いまのシーンの板の最後の手順を、少しして消す。</summary>
        public static void Finish(int lastStep, float seconds = 4f)
        {
            Current?.CompleteAfter(lastStep, seconds);
        }

        private Vector3[] ringBaseScales = new Vector3[0];

        private void Awake()
        {
            Current = this;
            // 光る輪の元の大きさ(直径 5cm ほどの薄い円盤)を覚えておき、倍率だけをかける
            ringBaseScales = new Vector3[controllerRings.Length];
            for (int i = 0; i < controllerRings.Length; i++)
            {
                ringBaseScales[i] = controllerRings[i] != null ? controllerRings[i].transform.localScale : Vector3.one;
            }

            panelRoot.SetActive(false);
            SetRings(false, false, false, false);
        }

        /// <summary>
        /// 手順を出す。leftHand は教えるボタンがどちらの手か。ok を渡すと「わかった」ボタンを出す(押すと呼ぶ)。
        /// placeNew が true なら、いまの頭の前に置き直す。
        /// </summary>
        public void Show(string title, string body, Button button, bool leftHand, bool placeNew, Action ok = null)
        {
            if (placeNew || !shown)
            {
                Place();
            }

            if (shown && source != null && nextClip != null)
            {
                source.PlayOneShot(nextClip, volume);
            }

            titleText.text = title;
            bodyText.text = body;
            onOk = ok;
            if (okButton != null)
            {
                okButton.SetActive(ok != null);
            }

            lit = button;
            litLeftHand = leftHand;
            shown = true;
            panelRoot.SetActive(true);
            UpdateDiagram();
        }

        // ---- 3段の手順(各種目)
        private string stepsId;
        private string[] stepTexts;
        private Button[] stepButtons;
        private bool[] stepLeftHand;
        private int step = -1;

        /// <summary>いま何段目か(手順を出していなければ -1)。</summary>
        public int Step => step;

        /// <summary>
        /// 種目の手順を始める。見た印があれば何もしない(交代プレイのときは印に関係なく出す)。
        /// leftHand は各段で光らせるボタンが左手か。
        /// </summary>
        public void BeginSteps(string id, string[] texts, Button[] buttons, bool[] leftHand)
        {
            StopAllCoroutines();
            if (!TurnSession.Active && SaveStore.HasSeenTutorial(id))
            {
                step = -1;
                return;
            }

            stepsId = id;
            stepTexts = texts;
            stepButtons = buttons;
            stepLeftHand = leftHand;
            step = 0;
            ShowStep(true);
        }

        /// <summary>from 段目が終わったら次へ進む(違う段なら何もしない)。</summary>
        public void Advance(int from)
        {
            if (step != from || stepTexts == null || step + 1 >= stepTexts.Length)
            {
                return;
            }

            step++;
            ShowStep(false);
        }

        /// <summary>最後の段を seconds 秒後に消し、見た印を付ける(交代プレイでは付けない)。</summary>
        public void CompleteAfter(int lastStep, float seconds)
        {
            if (step != lastStep)
            {
                return;
            }

            step = -2;
            StartCoroutine(CompleteLater(seconds));
        }

        /// <summary>手順をやめる(印は付けない。次にまた最初から出す)。</summary>
        public void CancelSteps()
        {
            StopAllCoroutines();
            step = -1;
            Hide();
        }

        private System.Collections.IEnumerator CompleteLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!TurnSession.Active && stepsId != null)
            {
                SaveStore.MarkTutorialSeen(stepsId);
            }

            step = -1;
            Hide(true);
        }

        private void ShowStep(bool placeNew)
        {
            Show($"あそびかた  {step + 1} / {stepTexts.Length}", stepTexts[step], stepButtons[step], stepLeftHand[step], placeNew);
        }

        /// <summary>板を消す。complete なら完了の音を鳴らす。</summary>
        public void Hide(bool complete = false)
        {
            if (shown && complete && source != null && completeClip != null)
            {
                source.PlayOneShot(completeClip, volume);
            }

            shown = false;
            panelRoot.SetActive(false);
            SetRings(false, false, false, false);
        }

        /// <summary>「わかった」(ボタンから呼ぶ)。</summary>
        public void OnOk()
        {
            Action action = onOk;
            Hide(true);
            action?.Invoke();
        }

        private void Update()
        {
            if (!shown)
            {
                return;
            }

            // メニューや休憩案内が出ている間は隠す
            bool visible = !InputGate.GrabBlocked;
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }

            // 教えるボタンのまわりの輪を 1.0 → 1.2 倍にゆっくり広げる(点滅はしない)
            float k = 1f + 0.2f * (0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / pulsePeriod));
            if (ring != null)
            {
                ring.localScale = Vector3.one * k;
            }

            for (int i = 0; i < controllerRings.Length; i++)
            {
                GameObject r = controllerRings[i];
                if (r != null && r.activeSelf)
                {
                    Vector3 b = ringBaseScales[i];
                    // 厚みはそのまま、輪の広がりだけを変える
                    r.transform.localScale = new Vector3(b.x * k, b.y, b.z * k);
                }
            }
        }

        private void Place()
        {
            Transform head = PanelPlacement.FindHead();
            if (head == null)
            {
                return;
            }

            Pose pose = PanelPlacement.InFrontOfHead(head, distance, drop);
            // 利き手と反対側へずらす(振る手の邪魔をしない)
            Vector3 right = Vector3.Cross(Vector3.up, pose.rotation * Vector3.forward).normalized;
            float side = SaveStore.Settings.leftHanded ? 1f : -1f;
            Vector3 position = pose.position + right * (side * sideOffset);
            panelRoot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, Vector3.up));
        }

        private void UpdateDiagram()
        {
            SetColor(triggerImage, lit == Button.Trigger);
            SetColor(padImage, lit == Button.PadCenter || lit == Button.PadSides);
            SetColor(menuImage, lit == Button.Menu);
            Image target = lit == Button.Trigger ? triggerImage : lit == Button.Menu ? menuImage :
                lit == Button.None ? null : padImage;
            if (ring != null)
            {
                ring.gameObject.SetActive(target != null);
                if (target != null)
                {
                    ring.anchoredPosition = ((RectTransform)target.transform).anchoredPosition;
                }
            }

            bool trigger = lit == Button.Trigger;
            bool pad = lit == Button.PadCenter || lit == Button.PadSides;
            SetRings(trigger && litLeftHand, pad && litLeftHand, trigger && !litLeftHand, pad && !litLeftHand);
        }

        private static void SetColor(Image image, bool on)
        {
            if (image != null)
            {
                image.color = on ? Lit : Normal;
            }
        }

        private void SetRings(bool leftTrigger, bool leftPad, bool rightTrigger, bool rightPad)
        {
            bool[] on = { leftTrigger, leftPad, rightTrigger, rightPad };
            for (int i = 0; i < controllerRings.Length && i < on.Length; i++)
            {
                if (controllerRings[i] != null)
                {
                    controllerRings[i].SetActive(on[i]);
                }
            }
        }
    }
}
