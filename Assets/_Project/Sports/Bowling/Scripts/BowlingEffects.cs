using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// 投球の結果に合わせた見た目の演出。docs/art-direction.md 7.2。
    /// ・ストライク:ピン置き場から紙吹雪、倒れたピンが順に光る(0.5秒で上げて1秒で戻す)、レーン脇の明かりが奥から手前へ流れる
    /// ・スペア:紙吹雪を少なめ・色違いで
    /// ・ガター(1本も倒れない):ピットで小さな煙
    /// カメラは動かさず、演出はすべてプレイヤーから 10m 以上離れたピン置き場で起こす。
    /// </summary>
    public class BowlingEffects : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private BowlingGame game;

        [Tooltip("ピンを並べる装置(倒れたピンを光らせるため)")]
        [SerializeField] private PinSetter pinSetter;

        [Tooltip("ストライクの紙吹雪")]
        [SerializeField] private ParticleSystem strikeConfetti;

        [Tooltip("スペアの紙吹雪")]
        [SerializeField] private ParticleSystem spareConfetti;

        [Tooltip("ガターの小さな煙")]
        [SerializeField] private ParticleSystem gutterPuff;

        [Tooltip("ストライクでレーン脇の明かりを奥から手前へ流す演出")]
        [SerializeField] private LaneLightSweep lightSweep;

        [Header("ピンの光")]
        [Tooltip("光が最大になるまでの秒数")]
        [SerializeField] private float glowRiseTime = 0.5f;

        [Tooltip("光が消えるまでの秒数")]
        [SerializeField] private float glowFallTime = 1f;

        [Tooltip("次のピンが光り始めるまでの間隔(秒)。手前のピンから順に光る")]
        [SerializeField] private float glowStagger = 0.06f;

        [Tooltip("光の強さ")]
        [Range(0f, 3f)]
        [SerializeField] private float glowIntensity = 1.2f;

        private readonly List<BowlingPin> knockedPins = new List<BowlingPin>();

        private void OnEnable()
        {
            if (game != null)
            {
                game.RollRecorded += OnRollRecorded;
            }
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.RollRecorded -= OnRollRecorded;
            }
        }

        private void OnRollRecorded(int frame, int rollIndex, int pins, string symbol)
        {
            if (symbol == "X")
            {
                Play(strikeConfetti);
                GlowKnockedPins();
                if (lightSweep != null)
                {
                    lightSweep.Play();
                }
            }
            else if (symbol == "/")
            {
                Play(spareConfetti);
            }
            else if (pins == 0)
            {
                Play(gutterPuff);
            }
        }

        private void GlowKnockedPins()
        {
            if (pinSetter == null)
            {
                return;
            }

            pinSetter.GetKnockedSinceMark(knockedPins);
            // 手前(プレイヤー側)のピンから順に光らせる
            knockedPins.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            for (int i = 0; i < knockedPins.Count; i++)
            {
                StartCoroutine(Glow(knockedPins[i], i * glowStagger));
            }
        }

        /// <summary>なめらかに光を上げ下げする(点滅させない)。</summary>
        private IEnumerator Glow(BowlingPin pin, float delay)
        {
            int resetCount = pin.ResetCount;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            for (float t = 0f; t < glowRiseTime; t += Time.deltaTime)
            {
                if (pin == null || !pin.gameObject.activeInHierarchy || pin.ResetCount != resetCount)
                {
                    yield break;
                }

                pin.SetGlow(Mathf.SmoothStep(0f, glowIntensity, t / glowRiseTime));
                yield return null;
            }

            for (float t = 0f; t < glowFallTime; t += Time.deltaTime)
            {
                if (pin == null || !pin.gameObject.activeInHierarchy || pin.ResetCount != resetCount)
                {
                    yield break;
                }

                pin.SetGlow(Mathf.SmoothStep(glowIntensity, 0f, t / glowFallTime));
                yield return null;
            }

            if (pin != null && pin.ResetCount == resetCount)
            {
                pin.SetGlow(0f);
            }
        }

        private static void Play(ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Play();
            }
        }
    }
}
