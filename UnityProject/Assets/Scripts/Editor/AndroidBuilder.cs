using UnityEditor;
using UnityEngine;

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

            // Приоритет: DemoScene → TestScene
            string scenePath = "Assets/Scenes/DemoScene.unity";
            if (!System.IO.File.Exists(scenePath))
                scenePath = "Assets/Scenes/TestScene.unity";

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
