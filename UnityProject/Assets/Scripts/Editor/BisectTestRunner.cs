using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Builds APK with a specific bisect scene for crash testing.
    /// Usage: -executeMethod ZeldaDaughter.Editor.BisectTestRunner.BuildScene -scene BisectMeshScene
    /// </summary>
    public static class BisectTestRunner
    {
        public static void BuildScene()
        {
            // Get scene name from command line
            string sceneName = "BisectMeshScene";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-scene" && i + 1 < args.Length)
                    sceneName = args[i + 1];
            }

            string scenePath = $"Assets/Scenes/{sceneName}.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogError($"[BisectTest] Scene not found: {scenePath}");
                EditorApplication.Exit(1);
                return;
            }

            // Same settings as AndroidBuilder
            EditorPrefs.SetString("AndroidSdkRoot", "/opt/android-sdk");
            EditorPrefs.SetString("JdkPath", "/usr/lib/jvm/java-17-openjdk-amd64");
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetString("AndroidNdkRootR23B", "/opt/android-sdk/ndk/23.1.7779620");
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            EditorPrefs.SetBool("BurstCompilation", false);

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // Disable URP for testing
            GraphicsSettings.defaultRenderPipeline = null;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }

            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            if (!defines.Contains("ZD_DEBUG"))
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android,
                    string.IsNullOrEmpty(defines) ? "ZD_DEBUG" : defines + ";ZD_DEBUG");

            string outputPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../ZeldaDaughter.apk"));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"[BisectTest] Built {sceneName}: {outputPath}");
            else
            {
                Debug.LogError($"[BisectTest] Build failed for {sceneName}");
                EditorApplication.Exit(1);
            }
        }
    }
}
