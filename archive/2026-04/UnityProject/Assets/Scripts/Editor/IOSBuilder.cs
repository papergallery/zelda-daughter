using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZeldaDaughter.Editor
{
    public static class IOSBuilder
    {
        [MenuItem("ZeldaDaughter/Build iOS Xcode Project")]
        public static void BuildXcodeProject()
        {
            // Restore URP pipeline (AndroidBuilder disables it for emulator)
            var urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                "Assets/Settings/URP_PipelineAsset.asset");
            if (urpAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = urpAsset;
                }
                Debug.Log($"[IOSBuilder] URP restored: {urpAsset.name}");
            }
            else
            {
                Debug.LogWarning("[IOSBuilder] URP_PipelineAsset.asset not found! Shaders may be pink.");
            }
            // Приоритет: DemoScene → TestScene
            string scenePath = "Assets/Scenes/DemoScene.unity";
            if (!System.IO.File.Exists(scenePath))
                scenePath = "Assets/Scenes/TestScene.unity";

            string[] scenes = { scenePath };
            if (!System.IO.File.Exists(scenes[0]))
            {
                Debug.LogError("[IOSBuilder] Scene not found! Run 'ZeldaDaughter/Scenes/Build Demo Scene' first.");
                EditorApplication.Exit(1);
                return;
            }

            string outputPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../ios-build"));

            System.IO.Directory.CreateDirectory(outputPath);

            PlayerSettings.companyName = "PaperGallery";
            PlayerSettings.productName = "Zelda's Daughter";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.papergallery.zeldasdaughter");
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[IOSBuilder] Xcode project created: {outputPath}");
            }
            else
            {
                Debug.LogError($"[IOSBuilder] Build failed: {report.summary.result}");
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
