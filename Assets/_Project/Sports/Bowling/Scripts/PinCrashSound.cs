using UnityEngine;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// ボールがピンに当たった瞬間に、ピン群が崩れる「ドガシャーン」を1回だけ鳴らす部品(ボールに付ける)。
    /// ピン1本ずつの音(ImpactSound)は同時発音の上限で間引かれるため、迫力はこの1音で出す。
    /// 当たったときのボールの速さで音量を変える。
    /// </summary>
    public class PinCrashSound : MonoBehaviour
    {
        [Tooltip("鳴らす音。ランダムに1つ選ぶ")]
        [SerializeField] private AudioClip[] clips = new AudioClip[0];

        [Tooltip("鳴らす AudioSource(ボールとは別の物に付け、当たった位置へ動かして鳴らす)")]
        [SerializeField] private AudioSource source;

        [Tooltip("最大の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;

        [Tooltip("この速さ(m/秒)より遅く当たったときは鳴らさない")]
        [SerializeField] private float minSpeed = 0.5f;

        [Tooltip("この速さ(m/秒)以上で当たると最大の音量になる")]
        [SerializeField] private float fullVolumeSpeed = 6f;

        [Tooltip("一番遅く当たったときの音量の割合(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float minVolumeRatio = 0.35f;

        [Tooltip("一度鳴らしてから、次に鳴らせるまでの秒数(1投で何度も鳴らさない)")]
        [SerializeField] private float cooldown = 3f;

        private float lastPlayTime = -100f;

        private void OnCollisionEnter(Collision collision)
        {
            if (source == null || clips.Length == 0 || Time.time - lastPlayTime < cooldown)
            {
                return;
            }

            if (collision.collider.GetComponentInParent<BowlingPin>() == null)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            float strength = Mathf.InverseLerp(minSpeed, fullVolumeSpeed, speed);
            source.transform.position = collision.GetContact(0).point;
            source.pitch = 1f + Random.Range(-0.04f, 0.04f);
            source.PlayOneShot(clip, volume * Mathf.Lerp(minVolumeRatio, 1f, strength));
            lastPlayTime = Time.time;
        }
    }
}
