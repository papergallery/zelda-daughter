using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZeldaDaughter.Debugging
{
    public static class DebugSceneLogger
    {
#if ZD_DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            string version = Application.version;
            string platform = Application.platform.ToString();
            ZDLog.Log("Scene", $"GameStarted version={version} platform={platform}");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ZDLog.Log("Scene", $"Loaded scene={scene.name}");
        }
#endif
    }
}
