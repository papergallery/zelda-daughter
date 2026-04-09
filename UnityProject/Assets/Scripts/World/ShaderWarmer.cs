using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Warms up all shaders at startup to prevent pink-screen artifacts on mobile.
    /// Attach to the bootstrap GameObject or add via GameBootstrap.
    /// </summary>
    public class ShaderWarmer : MonoBehaviour
    {
        private void Awake()
        {
            // Skip warmup on emulators (causes native SIGSEGV on swiftshader)
            if (SystemInfo.deviceModel.Contains("sdk") ||
                SystemInfo.deviceModel.Contains("generic") ||
                SystemInfo.deviceModel.Contains("Emulator"))
            {
                Debug.Log("[ZD:ShaderWarmer] Skipped on emulator.");
                return;
            }
            Shader.WarmupAllShaders();
            Debug.Log("[ZD:ShaderWarmer] Shaders warmed up.");
        }
    }
}
