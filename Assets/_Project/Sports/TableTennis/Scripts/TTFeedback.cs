using UnityEngine;

namespace VRGame3.Sports.TableTennis
{
    /// <summary>
    /// 卓球の音(docs/art-direction.md 8.2・8.5・7.5)。打球・台・ネット・床の音は当たった点から 3D で鳴らし、
    /// ラケットの音は打ったあとのボールの速さで3段階(6m/秒未満 / 6〜12 / 12 以上)に分ける。
    /// 得点・失点・デュース・勝敗の音は 2D で小さく鳴らす。
    /// </summary>
    public class TTFeedback : MonoBehaviour
    {
        [Tooltip("進行役")]
        [SerializeField] private TableTennisGame game;

        [Tooltip("当たった点で鳴らす AudioSource(鳴らす前に当たった点へ動かす。続けて鳴っても前の音が動かないよう、順番に使う)")]
        [SerializeField] private AudioSource[] hitSources = new AudioSource[0];

        [Tooltip("得点・勝敗を鳴らす AudioSource(2D)")]
        [SerializeField] private AudioSource uiSource;

        [Tooltip("ラケットの音(弱)")]
        [SerializeField] private AudioClip[] paddleSoft = new AudioClip[0];

        [Tooltip("ラケットの音(中)")]
        [SerializeField] private AudioClip[] paddleMid = new AudioClip[0];

        [Tooltip("ラケットの音(強)")]
        [SerializeField] private AudioClip[] paddleHard = new AudioClip[0];

        [Tooltip("台に弾む音")]
        [SerializeField] private AudioClip[] tableClips = new AudioClip[0];

        [Tooltip("ネットに当たる音")]
        [SerializeField] private AudioClip[] netClips = new AudioClip[0];

        [Tooltip("床に落ちる音")]
        [SerializeField] private AudioClip[] floorClips = new AudioClip[0];

        [Tooltip("自分の得点の音")]
        [SerializeField] private AudioClip pointWin;

        [Tooltip("CPU の得点の音")]
        [SerializeField] private AudioClip pointLose;

        [Tooltip("デュースの音")]
        [SerializeField] private AudioClip deuce;

        [Tooltip("勝ちの音")]
        [SerializeField] private AudioClip matchWin;

        [Tooltip("負けの音")]
        [SerializeField] private AudioClip matchLose;

        [Tooltip("ラケットの音量(弱・中・強。8.5:-15 / -12 / -10 dB)")]
        [SerializeField] private Vector3 paddleVolume = new Vector3(0.18f, 0.25f, 0.32f);

        [Tooltip("台に弾む音の音量(-14 dB)")]
        [SerializeField] private float tableVolume = 0.2f;

        [Tooltip("ネットの音の音量(-16 dB)")]
        [SerializeField] private float netVolume = 0.16f;

        [Tooltip("床の音の音量(-18 dB)")]
        [SerializeField] private float floorVolume = 0.13f;

        [Tooltip("得点・勝敗の音の音量")]
        [SerializeField] private float uiVolume = 0.25f;

        private int nextSource;

        private void OnEnable()
        {
            game.BallEvent += OnBallEvent;
            game.CpuHit += OnCpuHit;
            game.PointScored += OnPoint;
            game.GameFinished += OnFinished;
        }

        private void OnDisable()
        {
            game.BallEvent -= OnBallEvent;
            game.CpuHit -= OnCpuHit;
            game.PointScored -= OnPoint;
            game.GameFinished -= OnFinished;
        }

        private void OnBallEvent(PingPongPhysics.Event e)
        {
            switch (e.Kind)
            {
                case PingPongPhysics.EventKind.Racket:
                    PlayPaddle(e.Point, e.Speed);
                    break;
                case PingPongPhysics.EventKind.Table:
                    PlayAt(e.Point, tableClips, tableVolume);
                    break;
                case PingPongPhysics.EventKind.Net:
                    PlayAt(e.Point, netClips, netVolume);
                    break;
                case PingPongPhysics.EventKind.Floor:
                    PlayAt(e.Point, floorClips, floorVolume);
                    break;
            }
        }

        private void OnCpuHit(Vector3 point, float speed)
        {
            PlayPaddle(point, speed);
        }

        private void OnPoint(TTSide winner, string reason)
        {
            Play2D(winner == TTSide.Player ? pointWin : pointLose);
            if (game.Match.IsDeuce && !game.Match.IsOver && game.Match.PlayerPoints == game.Match.CpuPoints)
            {
                Play2D(deuce);
            }
        }

        private void OnFinished(bool won)
        {
            Play2D(won ? matchWin : matchLose);
        }

        private void PlayPaddle(Vector3 point, float speed)
        {
            if (speed < 6f)
            {
                PlayAt(point, paddleSoft, paddleVolume.x);
            }
            else if (speed < 12f)
            {
                PlayAt(point, paddleMid, paddleVolume.y);
            }
            else
            {
                PlayAt(point, paddleHard, paddleVolume.z);
            }
        }

        private void PlayAt(Vector3 point, AudioClip[] clips, float volume)
        {
            if (hitSources.Length == 0 || clips.Length == 0)
            {
                return;
            }

            AudioSource hitSource = hitSources[nextSource];
            nextSource = (nextSource + 1) % hitSources.Length;
            if (hitSource == null)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            hitSource.transform.position = point;
            hitSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
            hitSource.PlayOneShot(clip, volume);
        }

        private void Play2D(AudioClip clip)
        {
            if (uiSource != null && clip != null)
            {
                uiSource.PlayOneShot(clip, uiVolume);
            }
        }
    }
}
