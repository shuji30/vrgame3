using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// ランダムな間隔で、ランダムな場所から単発の音を鳴らす部品(鳥の声など)。
    /// 同じ音が続かないよう、音は順番に回す。docs/art-direction.md 8.1「鳥」。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RandomOneShot : MonoBehaviour
    {
        [Tooltip("鳴らす音。順番に回す")]
        [SerializeField] private AudioClip[] clips = new AudioClip[0];

        [Tooltip("鳴らす場所の候補。空ならこの物の位置で鳴らす")]
        [SerializeField] private Transform[] points = new Transform[0];

        [Tooltip("次に鳴らすまでの最短の秒数")]
        [SerializeField] private float minInterval = 8f;

        [Tooltip("次に鳴らすまでの最長の秒数")]
        [SerializeField] private float maxInterval = 20f;

        [Tooltip("音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.1f;

        [Tooltip("ピッチのばらつき(±、1 = 100%)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float pitchVariation = 0.08f;

        private AudioSource source;
        private float nextTime;
        private int nextClip;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            ScheduleNext();
        }

        private void Update()
        {
            if (Time.time < nextTime || clips.Length == 0)
            {
                return;
            }

            if (points.Length > 0)
            {
                Transform point = points[Random.Range(0, points.Length)];
                if (point != null)
                {
                    transform.position = point.position;
                }
            }

            AudioClip clip = clips[nextClip];
            nextClip = (nextClip + 1) % clips.Length;
            if (clip != null)
            {
                source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                source.PlayOneShot(clip, volume);
            }

            ScheduleNext();
        }

        private void ScheduleNext()
        {
            nextTime = Time.time + Random.Range(minInterval, maxInterval);
        }
    }
}
