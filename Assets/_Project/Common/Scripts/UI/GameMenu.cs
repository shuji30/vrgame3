using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VRGame3.Common
{
    /// <summary>
    /// メニューボタン(左右どちらでも)で開く・閉じるメニュー。中身は つづける / せってい / ハブへもどる
    /// (docs/plan.md「シーンの行き来とメニュー」)。
    /// 開いた瞬間の頭の正面に置き、頭には追従させない。開いている間は物を新しく掴めない。ゲームの時間は止めない。
    /// この部品はパネル(panelRoot)とは別の、いつも有効な物に付ける(パネルを隠してもボタンを読み続けるため)。
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        [Tooltip("メニュー全体(表示・非表示を切り替える)")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("つづける / せってい / ハブへもどる のページ")]
        [SerializeField] private GameObject mainPage;

        [Tooltip("ゲームの途中でハブへもどるときの確認のページ")]
        [SerializeField] private GameObject confirmPage;

        [Tooltip("「ハブへもどる」ボタン(ハブでは隠す)")]
        [SerializeField] private GameObject hubButton;

        [Tooltip("共通の設定パネル")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Tooltip("頭(メインカメラ)。空なら探す")]
        [SerializeField] private Transform head;

        [Tooltip("頭からパネルまでの距離(m)")]
        [SerializeField] private float distance = 1f;

        [Tooltip("目の高さからパネルの中心までの下げ幅(m)")]
        [SerializeField] private float drop = 0.15f;

        private InputAction leftMenuButton;
        private InputAction rightMenuButton;

        /// <summary>メニューが開いているか。</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            // OpenXR の HTC Vive Controller Profile では、メニューボタンは MenuButton の用途として公開される
            leftMenuButton = new InputAction("Left Menu", InputActionType.Button, "<XRController>{LeftHand}/{MenuButton}");
            rightMenuButton = new InputAction("Right Menu", InputActionType.Button, "<XRController>{RightHand}/{MenuButton}");

            if (hubButton != null)
            {
                hubButton.SetActive(SceneManager.GetActiveScene().name != SceneTransition.HubScene);
            }

            SetOpen(false);
        }

        private void OnEnable()
        {
            leftMenuButton.Enable();
            rightMenuButton.Enable();
        }

        private void OnDisable()
        {
            leftMenuButton.Disable();
            rightMenuButton.Disable();
            InputGate.UnblockGrab(this);
        }

        private void OnDestroy()
        {
            leftMenuButton.Dispose();
            rightMenuButton.Dispose();
        }

        private void Update()
        {
            if (leftMenuButton.WasPressedThisFrame() || rightMenuButton.WasPressedThisFrame())
            {
                Toggle();
            }
        }

        /// <summary>開いていれば閉じ、閉じていれば開く。設定パネルが出ていればそれも閉じる。</summary>
        public void Toggle()
        {
            if (IsOpen || (settingsPanel != null && settingsPanel.IsOpen))
            {
                Close();
                if (settingsPanel != null)
                {
                    settingsPanel.Close();
                }

                return;
            }

            Open();
        }

        /// <summary>頭の正面にメニューを開く。</summary>
        [ContextMenu("開く")]
        public void Open()
        {
            if (SceneTransition.IsLoading)
            {
                return;
            }

            if (head == null)
            {
                head = PanelPlacement.FindHead();
            }

            if (head != null)
            {
                Pose pose = PanelPlacement.InFrontOfHead(head, distance, drop);
                panelRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }

            ShowPage(main: true);
            SetOpen(true);
        }

        /// <summary>メニューを閉じる(「つづける」ボタンからも呼ぶ)。</summary>
        public void Close()
        {
            SetOpen(false);
        }

        /// <summary>メニューを閉じて、同じ場所に設定パネルを出す(ボタンから呼ぶ)。</summary>
        public void OpenSettings()
        {
            Pose pose = new Pose(panelRoot.transform.position, panelRoot.transform.rotation);
            Close();
            if (settingsPanel != null)
            {
                settingsPanel.OpenAt(pose);
            }
        }

        /// <summary>ハブへもどる(ボタンから呼ぶ)。ゲームの途中なら確認を出す。</summary>
        public void RequestHub()
        {
            if (GameSession.InProgress)
            {
                ShowPage(main: false);
                return;
            }

            GoToHub();
        }

        /// <summary>確認で「もどる」を選んだ(ボタンから呼ぶ)。途中のゲームは記録しない。</summary>
        public void GoToHub()
        {
            Close();
            GameSession.InProgress = false;
            SceneTransition.Load(SceneTransition.HubScene);
        }

        /// <summary>確認で「やめる」を選んだ(ボタンから呼ぶ)。</summary>
        public void CancelHub()
        {
            ShowPage(main: true);
        }

        private void ShowPage(bool main)
        {
            if (mainPage != null)
            {
                mainPage.SetActive(main);
            }

            if (confirmPage != null)
            {
                confirmPage.SetActive(!main);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (panelRoot != null)
            {
                panelRoot.SetActive(open);
            }

            if (open)
            {
                InputGate.BlockGrab(this);
            }
            else
            {
                InputGate.UnblockGrab(this);
            }
        }
    }
}
