using System.Diagnostics;
using UnityEngine;

namespace ZeldaDaughter.Debugging
{
    public static class ZDLog
    {
        [Conditional("ZD_DEBUG")]
        [Conditional("DEVELOPMENT_BUILD")]
        [Conditional("UNITY_EDITOR")]
        public static void Log(string category, string message)
        {
            UnityEngine.Debug.Log($"[ZD:{category}] {message}");
        }

        [Conditional("ZD_DEBUG")]
        [Conditional("DEVELOPMENT_BUILD")]
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(string category, string message)
        {
            UnityEngine.Debug.LogWarning($"[ZD:{category}] {message}");
        }

        [Conditional("ZD_DEBUG")]
        [Conditional("DEVELOPMENT_BUILD")]
        [Conditional("UNITY_EDITOR")]
        public static void LogError(string category, string message)
        {
            UnityEngine.Debug.LogError($"[ZD:{category}] {message}");
        }
    }
}
