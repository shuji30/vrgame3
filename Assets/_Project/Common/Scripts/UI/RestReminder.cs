using TMPro;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 休憩案内(docs/plan.md「休憩案内」、見た目は docs/art-direction.md 6.7)。
    /// 起動してからの実時間で数え、間隔を過ぎたあとの最初の区切りで、頭の正面にパネルを1回だけ置く(追従させない)。
    /// 区切りは各種目が AtBreak() で知らせる(ボウリングの1フレームの終わり、アーチェリーのラウンドの終わり、結果)。
    /// ハブではいつでも区切りとみなす。「とじる」で数え直し、「今後は出さない」で設定をオフにする。
    /// 出ている間はゲームを止めず、物は掴めない。
    /// </summary>
    public class RestReminder : MonoBehaviour
    {
        [Tooltip("パネル全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("「20分あそびました。」の文字")]
        [SerializeField] private TMP_Text messageText;

        [Tooltip("案内を出す間隔(分)。確かめるときは 1 にする")]
        [SerializeField] private float intervalMinutes = 20f;

        [Tooltip("オンにすると、いつでも区切りとみなす(ハブ)")]
        [SerializeField] private bool alwaysAtBreak;

        [Tooltip("頭からパネルまでの距離(m)")]
        [SerializeField] private float distance = 1.2f;

        [Tooltip("目の高さからパネルの中心までの下げ幅(m)")]
        [SerializeField] private float drop = 0.15f;

        [Tooltip("知らせの音を鳴らす AudioSource")]
        [SerializeField] private AudioSource source;

        [Tooltip("知らせの音")]
        [SerializeField] private AudioClip noticeClip;

        [Tooltip("知らせの音の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.2f;

        // 数え始めた時刻(起動してからの実時間)。シーンをまたいで続ける
        private static float countStart;
        private static bool breakRequested;

        private bool shown;

        /// <summary>いまが区切りであることを知らせる(各種目から呼ぶ)。</summary>
        public static void AtBreak()
        {
            breakRequested = true;
        }

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            InputGate.UnblockGrab(this);
        }

        private void Update()
        {
            if (shown)
            {
                return;
            }

            if (alwaysAtBreak)
            {
                breakRequested = true;
            }

            if (!SaveStore.Settings.restReminder)
            {
                breakRequested = false;
                return;
            }

            bool due = Time.realtimeSinceStartup - countStart >= intervalMinutes * 60f;
            if (!breakRequested)
            {
                return;
            }

            // 時間がまだなら区切りは使わない。メニューや切り替えの途中なら、終わるまで待つ
            if (!due)
            {
                breakRequested = false;
                return;
            }

            // メニューや切り替えの途中、シーンに入った直後(出現位置へ動かしてフェードが明けるまで)は待つ
            if (InputGate.GrabBlocked || SceneTransition.IsLoading || Time.timeSinceLevelLoad < 1f)
            {
                return;
            }

            Show();
        }

        private void Show()
        {
            breakRequested = false;
            shown = true;
            Transform head = PanelPlacement.FindHead();
            if (head != null)
            {
                Pose pose = PanelPlacement.InFrontOfHead(head, distance, drop);
                panelRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }

            if (messageText != null)
            {
                messageText.text = $"{Mathf.RoundToInt(intervalMinutes)}分あそびました。";
            }

            panelRoot.SetActive(true);
            InputGate.BlockGrab(this);
            if (source != null && noticeClip != null)
            {
                source.PlayOneShot(noticeClip, volume);
            }
        }

        /// <summary>「とじる」(ボタンから呼ぶ)。次の間隔を数え直す。</summary>
        public void Close()
        {
            countStart = Time.realtimeSinceStartup;
            shown = false;
            panelRoot.SetActive(false);
            InputGate.UnblockGrab(this);
        }

        /// <summary>「今後は出さない」(ボタンから呼ぶ)。設定の休憩案内をオフにする。</summary>
        public void NeverShow()
        {
            SaveStore.Settings.restReminder = false;
            SaveStore.SaveSettings();
            Close();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // エディタでは起動時刻がエディタを開いた時刻になるので、プレイの始めから数える
            countStart = Time.realtimeSinceStartup;
            breakRequested = false;
        }
    }
}
