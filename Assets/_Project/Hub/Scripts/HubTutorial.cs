using UnityEngine;
using VRGame3.Common;

namespace VRGame3.Hub
{
    /// <summary>
    /// ハブのてびき(docs/plan.md M4「チュートリアル」)。初めてハブに入ったとき、頭の前に「ようこそ」と操作の4行を出し、
    /// 「わかった」で閉じたあとは、どの種目にも入るまで看板の「あそぶ」の下に「ゆびさして トリガー」の札を出す。
    /// 種目に入ったら見た印(hub)を付ける(SportSign)。
    /// </summary>
    public class HubTutorial : MonoBehaviour
    {
        public const string Id = "hub";

        [Tooltip("てびきの板")]
        [SerializeField] private TutorialBoard board;

        [Tooltip("看板の「ゆびさして トリガー」の札")]
        [SerializeField] private GameObject[] signHints = new GameObject[0];

        [Tooltip("確認用:オンにすると、見た印に関係なく毎回出す")]
        [SerializeField] private bool alwaysShow;

        private bool hintsOn;

        private void Start()
        {
            SetHints(false);
            if (board == null || (!alwaysShow && SaveStore.HasSeenTutorial(Id)))
            {
                return;
            }

            string move = SaveStore.Settings.continuousMove
                ? "パッドにふれた方へ = いどう"
                : "パッドのまんなかを押す = いどう(はなした所へ)";
            board.Show("ようこそ",
                $"トリガー = えらぶ・つかむ\n{move}\nパッドの左右を押す = むきをかえる\nメニューボタン = メニュー",
                TutorialBoard.Button.Trigger, SaveStore.Settings.leftHanded, true, () => SetHints(true));
        }

        private void Update()
        {
            // 「あそびかたを さいしょから」を押したら、またはじめから出す
            if (!hintsOn && !board.IsOpen && !SaveStore.HasSeenTutorial(Id) && Time.timeSinceLevelLoad > 2f)
            {
                SetHints(true);
            }

            if (hintsOn && SaveStore.HasSeenTutorial(Id) && !alwaysShow)
            {
                SetHints(false);
            }
        }

        private void SetHints(bool on)
        {
            hintsOn = on;
            foreach (GameObject hint in signHints)
            {
                if (hint != null)
                {
                    hint.SetActive(on);
                }
            }
        }
    }
}
