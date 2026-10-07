#if ZD_DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;

namespace ZeldaDaughter.Debugging
{
    public static class DebugBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var go = new GameObject("[DebugLoggers]");
            Object.DontDestroyOnLoad(go);

            go.AddComponent<DebugEventLogger>();
            go.AddComponent<DebugPerformanceLogger>();

            ZDLog.Log("Debug", "DebugLoggers initialized");

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerObj.AddComponent<DebugPositionLogger>();
                ZDLog.Log("Debug", "DebugPositionLogger attached to Player");
            }
            else
            {
                ZDLog.LogWarning("Debug", "Player not found — DebugPositionLogger not attached");
            }
        }
    }
}
#endif
