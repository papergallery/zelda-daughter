using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace ZeldaDaughter.Debugging
{
    /// <summary>
    /// Diagnostic logger to identify what causes SIGSEGV on Android emulator.
    /// Logs every step of scene initialization to narrow down the crash point.
    /// </summary>
    public class CrashDiagnostics : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeScene()
        {
            Debug.Log("[ZD:Diag] === BeforeSceneLoad ===");
            Debug.Log($"[ZD:Diag] GraphicsAPI={SystemInfo.graphicsDeviceType}");
            Debug.Log($"[ZD:Diag] GraphicsDevice={SystemInfo.graphicsDeviceName}");
            Debug.Log($"[ZD:Diag] GraphicsVersion={SystemInfo.graphicsDeviceVersion}");
            Debug.Log($"[ZD:Diag] MaxTexSize={SystemInfo.maxTextureSize}");
            Debug.Log($"[ZD:Diag] SupportsInstancing={SystemInfo.supportsInstancing}");
            Debug.Log($"[ZD:Diag] SupportsComputeShaders={SystemInfo.supportsComputeShaders}");
            Debug.Log($"[ZD:Diag] RenderPipeline={GraphicsSettings.currentRenderPipeline?.name ?? "null"}");
            Debug.Log($"[ZD:Diag] DeviceModel={SystemInfo.deviceModel}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterScene()
        {
            Debug.Log("[ZD:Diag] === AfterSceneLoad ===");

            // Log all root GameObjects
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Debug.Log($"[ZD:Diag] Scene={scene.name} rootCount={scene.rootCount}");

            var roots = scene.GetRootGameObjects();
            foreach (var go in roots)
            {
                var components = go.GetComponentsInChildren<Component>(true);
                int meshCount = 0, lightCount = 0, camCount = 0, otherCount = 0;
                string problematic = "";

                foreach (var c in components)
                {
                    if (c == null) continue;
                    var typeName = c.GetType().Name;

                    if (typeName.Contains("MeshRenderer") || typeName.Contains("MeshFilter"))
                        meshCount++;
                    else if (typeName.Contains("Light"))
                        lightCount++;
                    else if (typeName.Contains("Camera") || typeName.Contains("Cinemachine"))
                        camCount++;
                    else if (typeName == "Transform" || typeName == "RectTransform")
                        continue; // skip transforms
                    else
                        otherCount++;

                    // Flag potentially problematic components
                    if (typeName.Contains("Terrain") || typeName.Contains("NavMesh") ||
                        typeName.Contains("Particle") || typeName.Contains("Reflection") ||
                        typeName.Contains("Volume") || typeName.Contains("Cinemachine"))
                    {
                        problematic += typeName + ",";
                    }
                }

                Debug.Log($"[ZD:Diag] GO={go.name} children={go.transform.childCount} mesh={meshCount} light={lightCount} cam={camCount} other={otherCount}{(problematic.Length > 0 ? " FLAGGED=" + problematic : "")}");
            }

            // Create a delayed check to see if we survive the first frame
            var diagGO = new GameObject("[CrashDiagnostics]");
            DontDestroyOnLoad(diagGO);
            diagGO.AddComponent<CrashDiagnostics>();

            Debug.Log("[ZD:Diag] === Diagnostics attached, waiting for first frame ===");
        }

        private int _frameCount = 0;

        private void Update()
        {
            _frameCount++;
            if (_frameCount <= 5)
            {
                Debug.Log($"[ZD:Diag] Frame {_frameCount} rendered OK. FPS={1f / Time.unscaledDeltaTime:F0}");
            }
        }

        private void OnApplicationPause(bool pause)
        {
            Debug.Log($"[ZD:Diag] OnApplicationPause={pause}");
        }
    }
}
