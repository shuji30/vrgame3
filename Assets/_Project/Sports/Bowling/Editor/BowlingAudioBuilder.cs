using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRGame3.Common;

namespace VRGame3.Sports.Bowling.EditorTools
{
    /// <summary>
    /// ボウリング場の音を組み込むエディタ処理。ボール・ピン・ジングル・ボールの戻り・環境音(波・鳥・風)の
    /// AudioSource と部品を付け、Art/Audio/Placeholder の仮の音(合成音)をつなぐ。
    /// 音量は docs/art-direction.md 8.5 の目安(dB)を 0〜1 に直した値、空間音響は 8.4 に合わせる。
    /// 素材を差し替えるときは同じファイル名で置き換えるか、Inspector で付け替える。
    /// </summary>
    internal static class BowlingAudioBuilder
    {
        private const string AudioFolder = "Assets/_Project/Art/Audio/Placeholder";

        // 8.5 の目安:-6dB = 0.5、-8dB = 0.4、-12dB = 0.25、-16dB = 0.16、環境音の合計 -20dB = 0.1
        private const float PinVolume = 0.4f;
        private const float RollVolume = 0.25f;
        private const float GrabVolume = 0.16f;
        private const float WaveVolume = 0.06f;
        private const float BirdVolume = 0.08f;
        private const float WindVolume = 0.04f;

        // ピンの崩れる音(クラッシュ)は効果音でいちばん大きく -3dB ≈ 0.7
        private const float CrashVolume = 0.7f;
        private const float PinMinDistance = 8f;

        /// <summary>仮の音のファイルを読み込み直す(新しく置いた WAV を Unity に取り込ませる)。</summary>
        public static void ImportClips()
        {
            AssetDatabase.Refresh();
        }

        /// <summary>ボール:当たる音(床・ピン)、転がる音、掴む・離す音と振動。</summary>
        public static void AddBallAudio(GameObject ball)
        {
            AudioSource source = AddSource(ball, 1f, 30f);

            // 当たる音はピッチを毎回変えるので、掴む・離す音とは別の音源にする
            var impactObject = new GameObject("当たる音");
            impactObject.transform.SetParent(ball.transform, false);
            AudioSource impactSource = AddSource(impactObject, 1f, 30f);

            var impact = ball.AddComponent<ImpactSound>();
            var impactSo = new SerializedObject(impact);
            SetClips(impactSo.FindProperty("clipsAgainstSounding"), Clips("SFX_PinHit_", 4));
            SetClips(impactSo.FindProperty("clipsAgainstOther"), new[] { Clip("SFX_BallDrop") });
            impactSo.FindProperty("volume").floatValue = PinVolume;
            impactSo.FindProperty("source").objectReferenceValue = impactSource;
            impactSo.ApplyModifiedPropertiesWithoutUndo();

            var loopObject = new GameObject("転がる音");
            loopObject.transform.SetParent(ball.transform, false);
            AudioSource loopSource = AddSource(loopObject, 1f, 30f);
            var rolling = ball.AddComponent<RollingSound>();
            var rollingSo = new SerializedObject(rolling);
            rollingSo.FindProperty("loop").objectReferenceValue = Clip("SFX_BallRoll_Loop");
            rollingSo.FindProperty("loopSource").objectReferenceValue = loopSource;
            rollingSo.FindProperty("maxVolume").floatValue = RollVolume;
            rollingSo.ApplyModifiedPropertiesWithoutUndo();

            var feedback = ball.AddComponent<GrabFeedback>();
            var feedbackSo = new SerializedObject(feedback);
            feedbackSo.FindProperty("grabClip").objectReferenceValue = Clip("SFX_Grab");
            feedbackSo.FindProperty("releaseClip").objectReferenceValue = Clip("SFX_Release");
            feedbackSo.FindProperty("volume").floatValue = GrabVolume;
            feedbackSo.FindProperty("source").objectReferenceValue = source;
            feedbackSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>ピン:ピン同士(ボールも含む)と、床などに当たる音の2系統。18m 先でも届くよう減衰を緩める。</summary>
        public static void AddPinAudio(GameObject pin)
        {
            // 助走路(約 18m 先)から聞いても小さくなりすぎないよう、減衰の始まりを遠くする
            AudioSource source = AddSource(pin, PinMinDistance, 45f);
            var impact = pin.AddComponent<ImpactSound>();
            var so = new SerializedObject(impact);
            SetClips(so.FindProperty("clipsAgainstSounding"), Clips("SFX_PinHit_", 4));
            SetClips(so.FindProperty("clipsAgainstOther"), Clips("SFX_PinFloor_", 4));
            so.FindProperty("volume").floatValue = PinVolume;
            so.FindProperty("source").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// ジングル(スコア板から)とボールの戻り(ボール置き台から)の音源を置き、BowlingSounds でゲームにつなぐ。
        /// </summary>
        public static void AddGameSounds(GameObject root, BowlingGame game, Vector3 boardPosition, Vector3 rackPosition)
        {
            var jingleObject = new GameObject("ジングルの音源");
            jingleObject.transform.SetParent(root.transform, false);
            jingleObject.transform.position = boardPosition;
            AudioSource jingle = AddSource(jingleObject, 5f, 40f);

            var returnObject = new GameObject("ボールの戻りの音源");
            returnObject.transform.SetParent(root.transform, false);
            returnObject.transform.position = rackPosition;
            AudioSource returnSource = AddSource(returnObject, 1f, 20f);

            var sounds = root.AddComponent<BowlingSounds>();
            var so = new SerializedObject(sounds);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("jingleSource").objectReferenceValue = jingle;
            so.FindProperty("strikeClip").objectReferenceValue = Clip("SFX_Strike");
            so.FindProperty("spareClip").objectReferenceValue = Clip("SFX_Spare");
            so.FindProperty("gutterClip").objectReferenceValue = Clip("SFX_Gutter");
            so.FindProperty("returnSource").objectReferenceValue = returnSource;
            so.FindProperty("returnClip").objectReferenceValue = Clip("SFX_BallReturn");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// ボールがピンに当たった瞬間の崩れる音。音源を1つ置き、各ボールの PinCrashSound から当たった位置で鳴らす。
        /// </summary>
        public static void AddPinCrash(GameObject root, IEnumerable<ThrowableObject> balls, Vector3 pinDeckPosition)
        {
            var crashObject = new GameObject("ピンが崩れる音の音源");
            crashObject.transform.SetParent(root.transform, false);
            crashObject.transform.position = pinDeckPosition;
            AudioSource crashSource = AddSource(crashObject, PinMinDistance, 45f);

            AudioClip[] clips = Clips("SFX_PinCrash_", 3);
            foreach (ThrowableObject ball in balls)
            {
                var crash = ball.gameObject.AddComponent<PinCrashSound>();
                var so = new SerializedObject(crash);
                SetClips(so.FindProperty("clips"), clips);
                so.FindProperty("source").objectReferenceValue = crashSource;
                so.FindProperty("volume").floatValue = CrashVolume;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 環境音:波(海側に 3D のループを3か所)、鳥(ヤシの木の位置からランダムに単発)、風(2D の弱いループ)。
        /// </summary>
        public static void BuildAmbient(Transform parent, IReadOnlyList<Transform> birdPoints, Vector3[] wavePositions)
        {
            var ambient = new GameObject("環境音");
            ambient.transform.SetParent(parent, false);

            for (int i = 0; i < wavePositions.Length; i++)
            {
                var wave = new GameObject($"波{i + 1}");
                wave.transform.SetParent(ambient.transform, false);
                wave.transform.position = wavePositions[i];
                AudioSource source = AddSource(wave, 4f, 45f);
                source.clip = Clip("AMB_Wave_Loop");
                source.loop = true;
                source.playOnAwake = true;
                source.volume = WaveVolume;
                // 3か所が同じ波の音でそろわないよう、ピッチを少しずつずらす
                source.pitch = 0.95f + 0.05f * i;
            }

            var birds = new GameObject("鳥");
            birds.transform.SetParent(ambient.transform, false);
            AddSource(birds, 3f, 40f);
            var oneShot = birds.AddComponent<RandomOneShot>();
            var so = new SerializedObject(oneShot);
            SetClips(so.FindProperty("clips"), Clips("AMB_Bird_", 4));
            SerializedProperty points = so.FindProperty("points");
            points.arraySize = birdPoints.Count;
            for (int i = 0; i < birdPoints.Count; i++)
            {
                points.GetArrayElementAtIndex(i).objectReferenceValue = birdPoints[i];
            }

            so.FindProperty("volume").floatValue = BirdVolume;
            so.ApplyModifiedPropertiesWithoutUndo();

            var wind = new GameObject("風");
            wind.transform.SetParent(ambient.transform, false);
            AudioSource windSource = wind.AddComponent<AudioSource>();
            windSource.spatialBlend = 0f;
            windSource.clip = Clip("AMB_Wind_Loop");
            windSource.loop = true;
            windSource.playOnAwake = true;
            windSource.volume = WindVolume;
        }

        /// <summary>3D の AudioSource(距離減衰は Logarithmic)。</summary>
        private static AudioSource AddSource(GameObject go, float minDistance, float maxDistance)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.wav");
            if (clip == null)
            {
                Debug.LogWarning($"[ボウリング場] 音のファイルが見つかりません: {AudioFolder}/{name}.wav");
            }

            return clip;
        }

        private static AudioClip[] Clips(string prefix, int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                clips[i] = Clip($"{prefix}{i + 1}");
            }

            return clips;
        }

        private static void SetClips(SerializedProperty property, AudioClip[] clips)
        {
            property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }
    }
}
