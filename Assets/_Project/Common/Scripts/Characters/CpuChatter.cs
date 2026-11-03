using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame3.Common
{
    /// <summary>
    /// CPU(仮人形や girl.vrm)のおしゃべり。頭の上に短いセリフの吹き出しを出し、文字に合わせて
    /// 「ポポポ」という鳴き声のような声(その場で合成するオリジナルの音)を鳴らし、人型のモデルなら口を動かす。
    /// 吹き出しは名札(強さの札)を複製して作るので、シーンを作り直さなくても使える。
    /// 音声合成の声(Tools/make_voices.ps1 で Resources/Voice に書き出した WAV)があれば、セリフごとにそれを鳴らす。
    /// 無いセリフは合成した鳴き声で鳴らす。
    /// </summary>
    public class CpuChatter : MonoBehaviour
    {
        [Tooltip("名札(吹き出しの見本にする。空なら子の「名札」を探す)")]
        [SerializeField] private Transform nameTag;

        [Tooltip("名札から吹き出しまでの高さ(m)")]
        [SerializeField] private float bubbleOffset = 0.35f;

        [Tooltip("吹き出しを出しておく最短の秒数")]
        [SerializeField] private float minShowTime = 1.6f;

        [Tooltip("1文字ごとの間隔(秒)")]
        [SerializeField] private float charInterval = 0.07f;

        [Tooltip("声の高さ(ピッチの倍率)")]
        [SerializeField] private float voicePitch = 1.35f;

        [Tooltip("声の音量(0〜1)")]
        [Range(0f, 1f)]
        [SerializeField] private float voiceVolume = 0.35f;

        [Tooltip("録音した声(入れるとこちらを使う。空なら合成した声)")]
        [SerializeField] private AudioClip[] voiceClips = new AudioClip[0];

        [Tooltip("話す頻度(0〜1)。点が入るたびに必ず話すとうるさいので、この割合で話す")]
        [Range(0f, 1f)]
        [SerializeField] private float chattiness = 0.6f;

        private GameObject bubble;
        private TMP_Text bubbleText;
        private AudioSource voice;
        private AudioClip[] syllables;
        private HumanoidFace face;
        private Coroutine speaking;
        private float busyUntil;
        private string[] voiceLines = new string[0];

        private void Awake()
        {
            if (nameTag == null)
            {
                nameTag = transform.Find("名札");
            }

            if (nameTag != null)
            {
                // 名札を複製して、白い吹き出しにする
                bubble = Instantiate(nameTag.gameObject, nameTag.parent);
                bubble.name = "吹き出し";
                bubble.transform.position = nameTag.position + Vector3.up * bubbleOffset;
                var image = bubble.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
                }

                bubbleText = bubble.GetComponentInChildren<TMP_Text>();
                if (bubbleText != null)
                {
                    bubbleText.color = new Color32(0x3A, 0x35, 0x50, 0xFF);
                    bubbleText.enableAutoSizing = true;
                    bubbleText.fontSizeMin = 60f;
                    bubbleText.fontSizeMax = 130f;
                }

                bubble.SetActive(false);
            }

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.minDistance = 2f;
            voice.maxDistance = 25f;
            voice.rolloffMode = AudioRolloffMode.Logarithmic;
            voice.dopplerLevel = 0f;
            syllables = voiceClips.Length > 0 ? voiceClips : MakeSyllables();

            // 音声合成の声の一覧(lines.txt の行の順が voice_00, voice_01 ... の番号)
            var list = Resources.Load<TextAsset>("Voice/lines");
            if (list != null)
            {
                voiceLines = list.text.Replace("", string.Empty).Split('
');
            }
        }

        /// <summary>たまに話す(chattiness の割合で)。話している途中なら話さない。</summary>
        public void MaybeSay(params string[] lines)
        {
            if (Random.value <= chattiness)
            {
                Say(lines);
            }
        }

        /// <summary>候補の中から1つを話す。</summary>
        public void Say(params string[] lines)
        {
            if (lines.Length == 0 || Time.time < busyUntil)
            {
                return;
            }

            if (face == null)
            {
                face = GetComponentInChildren<HumanoidFace>();
            }

            string line = lines[Random.Range(0, lines.Length)];
            if (speaking != null)
            {
                StopCoroutine(speaking);
            }

            speaking = StartCoroutine(Speak(line));
        }

        private IEnumerator Speak(string line)
        {
            busyUntil = Time.time + 0.6f;
            if (bubble != null)
            {
                bubbleText.text = line;
                bubble.SetActive(true);
            }

            float start = Time.time;
            AudioClip spoken = SpokenClip(line);
            if (spoken != null)
            {
                // 音声合成の声:鳴っている間、口をパクパクさせる
                voice.pitch = 1f;
                voice.PlayOneShot(spoken, Mathf.Min(1f, voiceVolume * 2.5f));
                float end = Time.time + spoken.length;
                while (Time.time < end)
                {
                    face?.SetMouth(Random.Range(0.3f, 1f));
                    yield return new WaitForSeconds(0.08f);
                }

                face?.SetMouth(0f);
                float wait = minShowTime - (Time.time - start);
                if (wait > 0f)
                {
                    yield return new WaitForSeconds(wait);
                }

                if (bubble != null)
                {
                    bubble.SetActive(false);
                }

                speaking = null;
                yield break;
            }

            foreach (char c in line)
            {
                if (char.IsWhiteSpace(c) || c == '!' || c == '!' || c == '?' || c == '?' || c == '…' || c == '〜' || c == 'ー')
                {
                    face?.SetMouth(0f);
                    yield return new WaitForSeconds(charInterval);
                    continue;
                }

                AudioClip clip = syllables[Random.Range(0, syllables.Length)];
                voice.pitch = voicePitch * Random.Range(0.92f, 1.08f);
                voice.PlayOneShot(clip, voiceVolume);
                face?.SetMouth(Random.Range(0.5f, 1f));
                yield return new WaitForSeconds(charInterval * 0.6f);
                face?.SetMouth(0.15f);
                yield return new WaitForSeconds(charInterval * 0.4f);
            }

            face?.SetMouth(0f);
            float rest = minShowTime - (Time.time - start);
            if (rest > 0f)
            {
                yield return new WaitForSeconds(rest);
            }

            if (bubble != null)
            {
                bubble.SetActive(false);
            }

            speaking = null;
        }

        /// <summary>セリフの音声合成の声(無ければ null)。</summary>
        private AudioClip SpokenClip(string line)
        {
            for (int i = 0; i < voiceLines.Length; i++)
            {
                if (voiceLines[i].Trim() == line)
                {
                    return Resources.Load<AudioClip>($"Voice/voice_{i:D2}");
                }
            }

            return null;
        }

        private void LateUpdate()
        {
            // 吹き出しはいつもプレイヤーの方を向ける(左右の向きだけ)
            if (bubble != null && bubble.activeSelf && Camera.main != null)
            {
                Vector3 away = bubble.transform.position - Camera.main.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f)
                {
                    bubble.transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                }
            }
        }

        /// <summary>「ポ」「ピ」「プ」のような短い声を、母音の響きを変えて6つ作る(オリジナルの合成音)。</summary>
        private static AudioClip[] MakeSyllables()
        {
            const int rate = 44100;
            float[] bases = { 330f, 370f, 392f, 440f, 349f, 415f };
            float[][] formants = { new[] { 800f, 1200f }, new[] { 300f, 2300f }, new[] { 350f, 800f }, new[] { 500f, 1800f }, new[] { 700f, 1100f }, new[] { 400f, 2000f } };
            var clips = new AudioClip[bases.Length];
            for (int k = 0; k < bases.Length; k++)
            {
                int n = (int)(rate * 0.075f);
                var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / rate;
                    float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t * 28f);
                    float f0 = bases[k] * (1f + 0.15f * Mathf.Exp(-t * 60f));
                    float v = Mathf.Sin(2f * Mathf.PI * f0 * t);
                    v += 0.45f * Mathf.Sin(2f * Mathf.PI * formants[k][0] * t) * Mathf.Sin(2f * Mathf.PI * f0 * t);
                    v += 0.25f * Mathf.Sin(2f * Mathf.PI * formants[k][1] * t) * Mathf.Sin(2f * Mathf.PI * f0 * t);
                    data[i] = 0.4f * v * env;
                }

                clips[k] = AudioClip.Create($"声{k + 1}", n, 1, rate, false);
                clips[k].SetData(data, 0);
            }

            return clips;
        }
    }
}
