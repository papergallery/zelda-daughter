using UnityEngine;

namespace ZeldaDaughter
{
    /// <summary>
    /// One output for game logs: "[ZD:&lt;system&gt;] message" (T-06). Proof lines for done-criteria are grepped from
    /// Player.log / logcat by this prefix.
    /// </summary>
    public static class ZdLog
    {
        public static void Info(string system, string message) => Debug.Log($"[ZD:{system}] {message}");
        public static void Warn(string system, string message) => Debug.LogWarning($"[ZD:{system}] {message}");
        public static void Error(string system, string message) => Debug.LogError($"[ZD:{system}] {message}");
    }
}
