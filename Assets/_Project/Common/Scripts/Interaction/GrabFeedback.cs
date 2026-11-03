using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRGame3.Common
{
    /// <summary>
    /// 掴んだとき・離したときに、音とコントローラーの振動で手応えを返す部品。XRGrabInteractable と一緒に付ける。
    /// 強さと長さの目安は docs/art-direction.md 8.8。
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabFeedback : MonoBehaviour
    {
        [Header("音")]
        [Tooltip("掴んだときの音")]
        [SerializeField] private AudioClip grabClip;

        [Tooltip("離したときの音")]
        [SerializeField] private AudioClip releaseClip;

        [Tooltip("音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.16f;

        [Tooltip("音を鳴らす AudioSource。空なら同じ物に付いているものを使う")]
        [SerializeField] private AudioSource source;

        [Header("振動")]
        [Tooltip("掴んだときの振動の強さ(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float grabAmplitude = 0.3f;

        [Tooltip("掴んだときの振動の長さ(秒)")]
        [SerializeField] private float grabDuration = 0.03f;

        [Tooltip("離したときの振動の強さ(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseAmplitude = 0.4f;

        [Tooltip("離したときの振動の長さ(秒)")]
        [SerializeField] private float releaseDuration = 0.04f;

        private XRGrabInteractable grab;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Play(grabClip);
            Vibrate(args.interactorObject, grabAmplitude, grabDuration);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // 掴むのを取り消されたとき(物が消えたなど)は鳴らさない
            if (args.isCanceled)
            {
                return;
            }

            Play(releaseClip);
            Vibrate(args.interactorObject, releaseAmplitude, releaseDuration);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }

        private static void Vibrate(IXRSelectInteractor interactor, float amplitude, float duration)
        {
            if (amplitude > 0f && interactor is XRBaseInputInteractor input)
            {
                input.SendHapticImpulse(amplitude, duration);
            }
        }
    }
}
