using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// 投球の結果に合わせて、ジングル(ストライク・スペア・ガター)とボールの戻りの音を鳴らし、
    /// ストライクのときは両手のコントローラーを振動させて祝う。docs/art-direction.md 7.2・8.2・8.8。
    /// </summary>
    public class BowlingSounds : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private BowlingGame game;

        [Header("ジングル(スコア板から鳴らす)")]
        [Tooltip("ジングルを鳴らす AudioSource(スコア板に付ける)")]
        [SerializeField] private AudioSource jingleSource;

        [SerializeField] private AudioClip strikeClip;
        [SerializeField] private AudioClip spareClip;

        [Tooltip("1本も倒れなかったときの音")]
        [SerializeField] private AudioClip gutterClip;

        [Tooltip("ストライクのジングルの音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float strikeVolume = 0.5f;

        [Tooltip("スペアのジングルの音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float spareVolume = 0.4f;

        [Tooltip("ガターの音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float gutterVolume = 0.25f;

        [Header("ボールの戻り(ボール置き台から鳴らす)")]
        [Tooltip("ボールの戻りの音を鳴らす AudioSource(ボール置き台に付ける)")]
        [SerializeField] private AudioSource returnSource;

        [SerializeField] private AudioClip returnClip;

        [Range(0f, 1f)]
        [SerializeField] private float returnVolume = 0.25f;

        [Header("ストライクの振動")]
        [Range(0f, 1f)]
        [SerializeField] private float strikeHapticAmplitude = 0.5f;

        [SerializeField] private float strikeHapticDuration = 0.25f;

        private XRBaseInputInteractor[] hands = new XRBaseInputInteractor[0];

        private void Start()
        {
            // 握っていなくても祝福として両手を振動させるため、左右の手の Interactor を1つずつ集めておく
            // (1つの手に Near-Far や Poke など複数あると、同じ振動を重ねて送ってしまうため)
            XRBaseInputInteractor left = null;
            XRBaseInputInteractor right = null;
            foreach (XRBaseInputInteractor interactor in FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
            {
                if (interactor.handedness == InteractorHandedness.Left && left == null)
                {
                    left = interactor;
                }
                else if (interactor.handedness == InteractorHandedness.Right && right == null)
                {
                    right = interactor;
                }
            }

            hands = new[] { left, right };
        }

        private void OnEnable()
        {
            if (game != null)
            {
                game.RollRecorded += OnRollRecorded;
                game.BallReturned += OnBallReturned;
            }
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.RollRecorded -= OnRollRecorded;
                game.BallReturned -= OnBallReturned;
            }
        }

        private void OnRollRecorded(int frame, int rollIndex, int pins, string symbol)
        {
            if (symbol == "X")
            {
                PlayJingle(strikeClip, strikeVolume);
                foreach (XRBaseInputInteractor hand in hands)
                {
                    if (hand != null && hand.isActiveAndEnabled)
                    {
                        hand.SendHapticImpulse(strikeHapticAmplitude, strikeHapticDuration);
                    }
                }
            }
            else if (symbol == "/")
            {
                PlayJingle(spareClip, spareVolume);
            }
            else if (pins == 0)
            {
                PlayJingle(gutterClip, gutterVolume);
            }
        }

        private void OnBallReturned()
        {
            if (returnSource != null && returnClip != null)
            {
                returnSource.PlayOneShot(returnClip, returnVolume);
            }
        }

        private void PlayJingle(AudioClip clip, float volume)
        {
            if (jingleSource != null && clip != null)
            {
                jingleSource.PlayOneShot(clip, volume);
            }
        }
    }
}
