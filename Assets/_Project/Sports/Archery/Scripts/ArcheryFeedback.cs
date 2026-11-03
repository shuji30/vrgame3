using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRGame3.Sports.Archery
{
    /// <summary>
    /// アーチェリーの結果に合わせた音・演出・振動(docs/art-direction.md 7.3、8.2、8.8)。
    /// 刺さる音・外れの音は当たった点から、10点のチャイム・X のジングル・ラウンドの終わりの音は的のまわりから鳴らす。
    /// カメラは動かさず、演出は的のまわり(10m 以上先)で起こす。
    /// </summary>
    public class ArcheryFeedback : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private ArcheryGame game;

        [Tooltip("的")]
        [SerializeField] private ArcheryTarget target;

        [Tooltip("スコア板")]
        [SerializeField] private ArcheryScoreBoard scoreBoard;

        [Header("音")]
        [Tooltip("当たった点で鳴らす AudioSource(鳴らす前に当たった点へ動かす)")]
        [SerializeField] private AudioSource hitSource;

        [Tooltip("的のまわりで鳴らす AudioSource(チャイム・ジングル)")]
        [SerializeField] private AudioSource targetSource;

        [Tooltip("的に刺さる音(ランダムに1つ)")]
        [SerializeField] private AudioClip[] targetHitClips = new AudioClip[0];

        [Tooltip("外れて地面に落ちる音(ランダムに1つ)")]
        [SerializeField] private AudioClip[] groundHitClips = new AudioClip[0];

        [Tooltip("10点のチャイム")]
        [SerializeField] private AudioClip tenClip;

        [Tooltip("X のジングル")]
        [SerializeField] private AudioClip xClip;

        [Tooltip("ラウンドの終わりの音")]
        [SerializeField] private AudioClip roundEndClip;

        [Tooltip("的が出る音")]
        [SerializeField] private AudioClip targetAppearClip;

        [Tooltip("音量(刺さる / 外れ / 10点 / X / ラウンドの終わり・的が出る)。8.5 の目安")]
        [SerializeField] private float hitVolume = 0.4f;

        [Tooltip("外れの音の音量(0〜1)")]
        [SerializeField] private float groundVolume = 0.2f;

        [Tooltip("10点のチャイムの音量(0〜1)")]
        [SerializeField] private float tenVolume = 0.32f;

        [Tooltip("X のジングルの音量(0〜1)")]
        [SerializeField] private float xVolume = 0.5f;

        [Tooltip("ラウンドの終わり・的が出る音の音量(0〜1)")]
        [SerializeField] private float roundVolume = 0.3f;

        [Header("演出")]
        [Tooltip("的に刺さったときの砂ぼこり")]
        [SerializeField] private ParticleSystem dust;

        [Tooltip("10点のときの光の粒")]
        [SerializeField] private ParticleSystem sparkle;

        [Tooltip("X のときの紙吹雪")]
        [SerializeField] private ParticleSystem confetti;

        [Tooltip("外れたときの草のかけら")]
        [SerializeField] private ParticleSystem grass;

        [Header("X の祝福の振動")]
        [Tooltip("X の祝福の振動の強さ(0〜1)")]
        [SerializeField] private float xHapticAmplitude = 0.4f;

        [Tooltip("X の祝福の振動の長さ(秒)")]
        [SerializeField] private float xHapticDuration = 0.15f;

        private void OnEnable()
        {
            if (game == null)
            {
                return;
            }

            game.ArrowScored += OnArrowScored;
            game.RoundFinished += OnRoundFinished;
            game.TargetPlaced += OnTargetPlaced;
        }

        private void OnDisable()
        {
            if (game == null)
            {
                return;
            }

            game.ArrowScored -= OnArrowScored;
            game.RoundFinished -= OnRoundFinished;
            game.TargetPlaced -= OnTargetPlaced;
        }

        private void OnArrowScored(int round, int index, Arrow.Hit hit)
        {
            scoreBoard.ShowHit(target, hit.Score, hit.IsX, hit.OnTarget);
            if (!hit.OnTarget || hit.Score == 0)
            {
                PlayAt(hitSource, hit.Point, groundHitClips, groundVolume);
                Burst(grass, hit.Point);
                return;
            }

            PlayAt(hitSource, hit.Point, targetHitClips, hitVolume);
            Burst(dust, hit.Point);
            if (hit.IsX)
            {
                Play(targetSource, xClip, xVolume);
                Burst(confetti, target.transform.position + Vector3.up * 1.2f);
                Burst(sparkle, hit.Point);
                Haptic(game.LeftInteractor);
                Haptic(game.RightInteractor);
            }
            else if (hit.Score == 10)
            {
                Play(targetSource, tenClip, tenVolume);
                Burst(sparkle, hit.Point);
            }
        }

        private void OnRoundFinished(int round, int total)
        {
            Play(targetSource, roundEndClip, roundVolume);
        }

        private void OnTargetPlaced(int round, float distance)
        {
            scoreBoard.ShowBanner(round, distance);
            if (targetSource != null)
            {
                targetSource.transform.position = target.transform.position;
            }

            Play(targetSource, targetAppearClip, roundVolume);
        }

        private static void PlayAt(AudioSource source, Vector3 point, AudioClip[] clips, float volume)
        {
            if (source == null || clips.Length == 0)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            source.transform.position = point;
            source.pitch = 1f + Random.Range(-0.05f, 0.05f);
            source.PlayOneShot(clip, volume);
        }

        private static void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }

        private static void Burst(ParticleSystem particles, Vector3 point)
        {
            if (particles == null)
            {
                return;
            }

            particles.transform.position = point;
            particles.Play();
        }

        private void Haptic(XRBaseInputInteractor interactor)
        {
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                interactor.SendHapticImpulse(xHapticAmplitude, xHapticDuration);
            }
        }
    }
}
