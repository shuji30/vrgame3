using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// UI のボタンの手応え(docs/art-direction.md 6.4)。
    /// ・レイが当たっている間:白い枠を付けて 0.01m 手前に出す(色は変えない)
    /// ・押したとき:0.1 秒だけ 0.95 倍に縮めて戻し、UI の音(決める / 戻る)と、押した手に小さな振動
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("レイが当たっている間に出す枠")]
        [SerializeField] private Outline outline;

        [Tooltip("レイが当たっている間に手前へ出す距離(Canvas の単位。1 = 1mm)")]
        [SerializeField] private float hoverLift = 10f;

        [Tooltip("押したときの音(決める・戻る)")]
        [SerializeField] private AudioClip clickClip;

        [Tooltip("押したときの音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.25f;

        [Tooltip("押したときの振動(強さ, 秒)")]
        [SerializeField] private Vector2 haptic = new Vector2(0.15f, 0.02f);

        private static AudioSource sharedSource;

        private Button button;
        private Vector3 restPosition;
        private bool hovering;

        private void Awake()
        {
            button = GetComponent<Button>();
            restPosition = transform.localPosition;
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private void OnDisable()
        {
            SetHover(false);
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(button.interactable);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!button.interactable)
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                StartCoroutine(Press());
            }

            PlaySound();
            if (eventData is TrackedDeviceEventData tracked && tracked.interactor is XRBaseInputInteractor interactor)
            {
                interactor.SendHapticImpulse(haptic.x, haptic.y);
            }
        }

        private void SetHover(bool on)
        {
            hovering = on;
            if (outline != null)
            {
                outline.enabled = on;
            }

            transform.localPosition = restPosition + (on ? Vector3.back * hoverLift : Vector3.zero);
        }

        private IEnumerator Press()
        {
            transform.localScale = Vector3.one * 0.95f;
            yield return new WaitForSeconds(0.1f);
            transform.localScale = Vector3.one;
        }

        private void PlaySound()
        {
            if (clickClip == null)
            {
                return;
            }

            // UI の音は 2D。シーンに1つだけ音源を作って使い回す
            if (sharedSource == null)
            {
                var go = new GameObject("UI の音");
                sharedSource = go.AddComponent<AudioSource>();
                sharedSource.playOnAwake = false;
                sharedSource.spatialBlend = 0f;
            }

            sharedSource.PlayOneShot(clickClip, volume);
        }
    }
}
