using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRGame3.Common
{
    /// <summary>
    /// SceneTransition が作る、切り替え1回分の進行役。暗くする → 読み込む → 終わったら自分を消す。
    /// 明るくするのは新しいシーンの ScreenFader。
    /// </summary>
    public class SceneTransitionRunner : MonoBehaviour
    {
        public void Run(string sceneName)
        {
            StartCoroutine(Transition(sceneName));
        }

        private IEnumerator Transition(string sceneName)
        {
            InputGate.BlockGrab(this);
            ScreenFader fader = ScreenFader.Current;
            if (fader != null)
            {
                yield return fader.FadeTo(1f);
            }

            SceneTransition.PreviousScene = SceneManager.GetActiveScene().name;
            SceneTransition.Arriving = true;
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError($"[シーン切り替え] シーン「{sceneName}」を読み込めません。Build Settings に入っているか確かめてください");
                SceneTransition.Arriving = false;
                if (fader != null)
                {
                    yield return fader.FadeTo(0f);
                }
            }
            else
            {
                while (!load.isDone)
                {
                    yield return null;
                }
            }

            InputGate.UnblockGrab(this);
            SceneTransition.IsLoading = false;
            Destroy(gameObject);
        }
    }
}
