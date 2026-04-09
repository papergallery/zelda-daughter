using UnityEngine;

namespace ZeldaDaughter.Debugging
{
    /// <summary>
    /// Logs every Awake call to narrow down which component causes SIGSEGV.
    /// Uses PlayerLoop to detect when scene loading completes.
    /// </summary>
    public static class AwakeTracer
    {
        private static bool _sceneLoadComplete = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Debug.Log("[ZD:AwakeTrace] === BeforeSceneLoad — tracing will start ===");
            Application.logMessageReceived += OnLogMessage;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterScene()
        {
            _sceneLoadComplete = true;
            Debug.Log("[ZD:AwakeTrace] === AfterSceneLoad reached! Scene loaded without crash ===");

            // List all root objects
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            Debug.Log($"[ZD:AwakeTrace] Scene has {roots.Length} root objects, {Object.FindObjectsOfType<MonoBehaviour>().Length} MonoBehaviours");
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            // Only track errors/exceptions during scene load
            if (!_sceneLoadComplete && type == LogType.Exception)
            {
                Debug.Log($"[ZD:AwakeTrace] EXCEPTION during scene load: {condition}");
            }
        }
    }
}
