using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 物が何かに当たったときに音を鳴らす部品。当たった速さで音量を変え、ピッチを少しばらつかせる。
    /// 相手も ImpactSound を持っているとき(ピン同士など)と、それ以外(床・壁など)で鳴らす音を分けられる。
    /// docs/art-direction.md 8.2:同時発音は全体で 8 まで、同じ物は 0.03秒あける、ピッチ ±5%。
    /// </summary>
    public class ImpactSound : MonoBehaviour
    {
        [Tooltip("相手も ImpactSound を持っているときに鳴らす音(ピン同士など)。ランダムに1つ選ぶ")]
        [SerializeField] private AudioClip[] clipsAgainstSounding = new AudioClip[0];

        [Tooltip("床や壁など、それ以外に当たったときに鳴らす音。ランダムに1つ選ぶ")]
        [SerializeField] private AudioClip[] clipsAgainstOther = new AudioClip[0];

        [Tooltip("この速さ(m/秒)より弱い当たりでは鳴らさない(転がっている間の小さな接触を無視する)")]
        [SerializeField] private float minSpeed = 0.4f;

        [Tooltip("この速さ(m/秒)以上の当たりで最大の音量になる")]
        [SerializeField] private float fullVolumeSpeed = 5f;

        [Tooltip("最大の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.4f;

        [Tooltip("ピッチのばらつき(±、1 = 100%)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float pitchVariation = 0.05f;

        [Tooltip("同じ物が続けて鳴らすまでの最短の間隔(秒)")]
        [SerializeField] private float minInterval = 0.03f;

        [Tooltip("音を鳴らす AudioSource。空なら同じ物に付いているものを使う")]
        [SerializeField] private AudioSource source;

        // すべての ImpactSound で同時に鳴っている音の終わる時刻
        private static readonly List<float> PlayingUntil = new List<float>();
        private const int MaxVoices = 8;

        // 両方が ImpactSound を持つとき、どちらが鳴らすかを決める番号
        private static int nextOrder;
        private int order;

        private float lastPlayTime = -1f;

        private void Awake()
        {
            order = nextOrder++;
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed || Time.time - lastPlayTime < minInterval)
            {
                return;
            }

            // 両方が ImpactSound を持つときは、片方だけが鳴らす(二重に鳴らさない)
            var other = collision.collider.GetComponentInParent<ImpactSound>();
            if (other != null && other.order < order)
            {
                return;
            }

            AudioClip[] clips = other != null ? clipsAgainstSounding : clipsAgainstOther;
            if (source == null || clips.Length == 0 || !TryTakeVoice())
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            float strength = Mathf.InverseLerp(minSpeed, fullVolumeSpeed, speed);
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, volume * Mathf.Lerp(0.2f, 1f, strength));
            lastPlayTime = Time.time;
            PlayingUntil.Add(Time.time + clip.length);
        }

        /// <summary>同時発音の上限に空きがあれば1つ使う。</summary>
        private static bool TryTakeVoice()
        {
            float now = Time.time;
            PlayingUntil.RemoveAll(end => end <= now);
            return PlayingUntil.Count < MaxVoices;
        }
    }
}
