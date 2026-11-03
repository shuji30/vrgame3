using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 転がっている間だけループ音を鳴らす部品。床などに触れているときの速さに合わせて、音量とピッチを変える。
    /// docs/art-direction.md 8.2「転がる」。
    /// 音量を絶えず変えるので、ほかの音(掴む・当たる)とは別の AudioSource を使う(子の物に付けて指定する)。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RollingSound : MonoBehaviour
    {
        [Tooltip("転がる音のループ")]
        [SerializeField] private AudioClip loop;

        [Tooltip("転がる音専用の AudioSource。ほかの音と共用しないこと")]
        [SerializeField] private AudioSource loopSource;

        [Tooltip("この速さ(m/秒)より遅いときは鳴らさない")]
        [SerializeField] private float minSpeed = 0.2f;

        [Tooltip("この速さ(m/秒)で最大の音量とピッチになる")]
        [SerializeField] private float fullSpeed = 8f;

        [Tooltip("最大の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float maxVolume = 0.25f;

        [Tooltip("遅いときのピッチ")]
        [SerializeField] private float minPitch = 0.7f;

        [Tooltip("速いときのピッチ")]
        [SerializeField] private float maxPitch = 1.3f;

        [Tooltip("音量の変わる速さ。大きいほど素早く追いつく")]
        [SerializeField] private float smoothing = 10f;

        private Rigidbody body;
        private bool isTouching;
        private bool wasTouching;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (loopSource != null)
            {
                loopSource.playOnAwake = false;
                loopSource.loop = true;
                loopSource.clip = loop;
                loopSource.volume = 0f;
            }
        }

        private void FixedUpdate()
        {
            // OnCollisionStay は FixedUpdate のあとに呼ばれるので、前のステップの結果を使う
            wasTouching = isTouching;
            isTouching = false;
        }

        private void OnCollisionStay(Collision collision)
        {
            isTouching = true;
        }

        private void Update()
        {
            if (loop == null || loopSource == null)
            {
                return;
            }

            float speed = body.isKinematic ? 0f : body.linearVelocity.magnitude;
            float t = Mathf.InverseLerp(minSpeed, fullSpeed, speed);
            float targetVolume = wasTouching && speed >= minSpeed ? Mathf.Lerp(0.15f, 1f, t) * maxVolume : 0f;

            loopSource.volume = Mathf.Lerp(loopSource.volume, targetVolume, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            loopSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);

            if (loopSource.volume > 0.001f && !loopSource.isPlaying)
            {
                loopSource.Play();
            }
            else if (loopSource.volume <= 0.001f && loopSource.isPlaying)
            {
                loopSource.Stop();
            }
        }
    }
}
