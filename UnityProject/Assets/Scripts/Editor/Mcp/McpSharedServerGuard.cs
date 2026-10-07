using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;

namespace ZeldaDaughter.Editor.Mcp
{
    /// <summary>
    /// Zelda's side of the shared unity-mcp server on 127.0.0.1:6510 (R1-02, scheme B): the server belongs to
    /// The Chest editor; Zelda only connects to it and never starts or stops it.
    ///
    /// com.coplaydev.unity-mcp v10.2.0 keeps its settings in EditorPrefs, which on Windows are shared by every
    /// project of the user:
    /// - auto-connect on load is the global AutoStartOnLoad pref (off on the author's PC), so Zelda connects itself
    ///   when the server is already reachable — connect only, no server launch;
    /// - the "server I launched" handshake (pidfile + token) is global too, and on quit McpEditorShutdownCleanup stops
    ///   the server that handshake points to — The Chest's. Clearing it before quit makes that call a no-op.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpSharedServerGuard
    {
        // Keys from MCPForUnity.Editor.Constants.EditorPrefKeys (internal there).
        private const string PidFilePathKey = "MCPForUnity.LocalHttpServer.LastPidFilePath";
        private const string InstanceTokenKey = "MCPForUnity.LocalHttpServer.LastInstanceToken";
        private const string ConnectTriedKey = "ZD.Mcp.ConnectTried";

        static McpSharedServerGuard()
        {
            EditorApplication.wantsToQuit -= ForgetServerHandshake;
            EditorApplication.wantsToQuit += ForgetServerHandshake;
            EditorApplication.update -= ConnectWhenIdle;
            EditorApplication.update += ConnectWhenIdle;
        }

        private static void ConnectWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= ConnectWhenIdle;
            // Once per editor session; after a domain reload the package's own reload handler resumes the bridge.
            if (SessionState.GetBool(ConnectTriedKey, false)) return;
            SessionState.SetBool(ConnectTriedKey, true);
            _ = ConnectAsync();
        }

        private static async Task ConnectAsync()
        {
            if (!EditorConfigurationCache.Instance.UseHttpTransport || MCPServiceLocator.Bridge.IsRunning) return;
            if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
            {
                UnityEngine.Debug.Log("[ZD:Mcp] shared server not reachable; not starting one (it belongs to The Chest)");
                return;
            }
            bool started = await MCPServiceLocator.Bridge.StartAsync();
            UnityEngine.Debug.Log("[ZD:Mcp] connected to shared server: " + started);
        }

        private static bool ForgetServerHandshake()
        {
            EditorPrefs.DeleteKey(PidFilePathKey);
            EditorPrefs.DeleteKey(InstanceTokenKey);
            UnityEngine.Debug.Log("[ZD:Mcp] shared server handshake cleared before quit; server left running");
            return true;
        }
    }
}
