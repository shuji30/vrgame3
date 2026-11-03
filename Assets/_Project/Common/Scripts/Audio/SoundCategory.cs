using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Common
{
    /// <summary>
    /// 音源の種類(docs/plan.md M4「音量」)。BGM・環境音の音源に付け、設定の「BGM・かんきょう音」の音量と、
    /// シーン切り替えで下げる倍率(docs/art-direction.md 7.4)をかける。全体の音量は AudioListener でかける。
    /// AudioMixer は使わない。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundCategory : MonoBehaviour
    {
        private static readonly List<SoundCategory> Active = new List<SoundCategory>();
        private static float ambientFactor = 1f;
        private static float duck = 1f;

        private AudioSource source;
        private float baseVolume;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;
        }

        private void OnEnable()
        {
            Active.Add(this);
            Apply();
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Apply()
        {
            if (source != null)
            {
                source.volume = baseVolume * ambientFactor * duck;
            }
        }

        /// <summary>設定の音量を当てる(全体は AudioListener、BGM・環境音はこの部品の付いた音源)。</summary>
        public static void ApplySettings()
        {
            GameSettings settings = SaveStore.Settings;
            // 人の耳に合わせ、段の2乗で音量を変える(10 段で 1.0、5 段で 0.25)
            AudioListener.volume = Mathf.Pow(settings.masterVolume / 10f, 2f);
            ambientFactor = Mathf.Pow(settings.ambientVolume / 10f, 2f);
            Refresh();
        }

        /// <summary>シーン切り替えの間、BGM・環境音を下げる(0〜1)。</summary>
        public static void SetDuck(float value)
        {
            duck = Mathf.Clamp01(value);
            Refresh();
        }

        private static void Refresh()
        {
            foreach (SoundCategory category in Active)
            {
                category.Apply();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Active.Clear();
            ambientFactor = 1f;
            duck = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookSettings()
        {
            SaveStore.Changed += ApplySettings;
            ApplySettings();
        }
    }
}
