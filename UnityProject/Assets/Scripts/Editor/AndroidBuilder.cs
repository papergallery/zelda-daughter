using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZeldaDaughter.Editor
{
    public static class AndroidBuilder
    {
        [MenuItem("ZeldaDaughter/Build Android APK")]
        public static void BuildAPK()
        {
            // Set Android SDK/JDK paths
            EditorPrefs.SetString("AndroidSdkRoot", "/opt/android-sdk");
            EditorPrefs.SetString("JdkPath", "/usr/lib/jvm/java-17-openjdk-amd64");
            // Use internal JDK/SDK checkboxes off
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetString("AndroidNdkRootR23B", "/opt/android-sdk/ndk/23.1.7779620");
            EditorPrefs.SetBool("NdkUseEmbedded", false);

            // EmuMin2 — лёгкая сцена для эмулятора (Player + Input + деревья)
            // EmulatorScene крашит SwiftShader (слишком сложная)
            // DemoScene — для реального устройства
            string scenePath = "Assets/Scenes/EmuStage4.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                scenePath = "Assets/Scenes/DemoScene.unity";
                if (!System.IO.File.Exists(scenePath))
                    scenePath = "Assets/Scenes/TestScene.unity";
            }

            string[] scenes = { scenePath };
            if (!System.IO.File.Exists(scenes[0]))
            {
                Debug.LogError("[AndroidBuilder] Scene not found! Run 'ZeldaDaughter/Scenes/Build Demo Scene' first.");
                EditorApplication.Exit(1);
                return;
            }

            string outputPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../ZeldaDaughter.apk"));

            // Ensure output directory exists
            System.IO.Directory.CreateDirectory("Builds/Android");

            // Configure Android settings
            PlayerSettings.companyName = "PaperGallery";
            PlayerSettings.productName = "Zelda's Daughter";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.papergallery.zeldasdaughter");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Disable Burst AOT (bcl.exe not available on this server)
            EditorPrefs.SetBool("BurstCompilation", false);

            // Force GLES3 (Vulkan crashes on SwiftShader with complex scenes)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
                GraphicsDeviceType.OpenGLES3
            });

            // Disable URP entirely for emulator — URP crashes SwiftShader regardless of shaders
            // EmulatorScene uses Unlit/Color materials that work fine with Built-in RP
            GraphicsSettings.defaultRenderPipeline = null;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }
            Debug.Log("[AndroidBuilder] URP disabled — using Built-in RP for emulator");

            // Add ZD_DEBUG for debug logging
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            if (!defines.Contains("ZD_DEBUG"))
            {
                defines = string.IsNullOrEmpty(defines) ? "ZD_DEBUG" : defines + ";ZD_DEBUG";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, defines);
            }

            // Convert URP/Lit → URP/Simple Lit for SwiftShader emulator compatibility
            // Simple Lit uses Blinn-Phong instead of PBR — avoids SIGSEGV on swiftshader
            MaterialSimplifier.ConvertLitToSimpleLit();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuilder] APK built successfully: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            }
            else
            {
                Debug.LogError($"[AndroidBuilder] Build failed: {report.summary.result}");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error)
                            Debug.LogError($"  {msg.content}");
                    }
                }
                EditorApplication.Exit(1);
            }
        }
    }
}
