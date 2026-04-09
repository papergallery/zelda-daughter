using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZeldaDaughter.Editor
{
    public static class URPSetupBuilder
    {
        [MenuItem("ZeldaDaughter/Setup/Create URP Pipeline Asset")]
        public static void CreateURPAssets()
        {
            string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            // Create Universal Renderer Data
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, $"{folder}/URP_Renderer.asset");

            // Create URP Pipeline Asset
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = "URP_PipelineAsset";

            // Mobile-friendly settings
            pipelineAsset.renderScale = 0.85f;
            pipelineAsset.supportsCameraOpaqueTexture = false;
            pipelineAsset.supportsCameraDepthTexture = false;
            pipelineAsset.msaaSampleCount = 1; // No MSAA for mobile
            pipelineAsset.supportsHDR = false;

            AssetDatabase.CreateAsset(pipelineAsset, $"{folder}/URP_PipelineAsset.asset");

            // Assign to Graphics Settings
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            // Assign to all Quality Levels
            var qualityNames = QualitySettings.names;
            for (int i = 0; i < qualityNames.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[URPSetup] Pipeline created and assigned: {folder}/URP_PipelineAsset.asset");
        }
    }
}
