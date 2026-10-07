using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Creates a minimal URP asset for emulator compatibility (no shadows, no SSAO, no HDR).
    /// </summary>
    public static class URPMinimalBuilder
    {
        [MenuItem("ZeldaDaughter/Setup/Create Minimal URP (Emulator)")]
        public static void CreateMinimalURP()
        {
            string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            // Create renderer
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, $"{folder}/URP_Renderer_Minimal.asset");

            // Create pipeline asset with minimal features
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = "URP_PipelineAsset_Minimal";

            // Disable everything that might crash on swiftshader
            pipelineAsset.renderScale = 0.5f;
            pipelineAsset.supportsCameraOpaqueTexture = false;
            pipelineAsset.supportsCameraDepthTexture = false;
            pipelineAsset.msaaSampleCount = 1;
            pipelineAsset.supportsHDR = false;
            pipelineAsset.shadowDistance = 0;

            // Disable shadows entirely
            var mainLightShadows = pipelineAsset.GetType().GetProperty("mainLightRenderingMode");
            // Use reflection to set shadow settings since direct API varies by version
            try
            {
                // Disable main light shadows
                var so = new SerializedObject(pipelineAsset);
                var shadowProp = so.FindProperty("m_MainLightShadowsSupported");
                if (shadowProp != null) shadowProp.boolValue = false;
                var addShadowProp = so.FindProperty("m_AdditionalLightShadowsSupported");
                if (addShadowProp != null) addShadowProp.boolValue = false;
                var softShadowProp = so.FindProperty("m_SoftShadowsSupported");
                if (softShadowProp != null) softShadowProp.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[URPMinimal] Could not disable shadows via SerializedObject: {e.Message}");
            }

            AssetDatabase.CreateAsset(pipelineAsset, $"{folder}/URP_PipelineAsset_Minimal.asset");
            AssetDatabase.SaveAssets();

            Debug.Log($"[URPMinimal] Created minimal pipeline at {folder}/URP_PipelineAsset_Minimal.asset");
        }

        /// <summary>
        /// Switches all quality levels to use the minimal URP pipeline.
        /// Call before Android build for emulator testing.
        /// </summary>
        public static void ApplyMinimalURP()
        {
            var minimalPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/URP_PipelineAsset_Minimal.asset");
            if (minimalPipeline == null)
            {
                Debug.LogWarning("[URPMinimal] Minimal pipeline not found, creating...");
                CreateMinimalURP();
                minimalPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/URP_PipelineAsset_Minimal.asset");
            }

            if (minimalPipeline == null)
            {
                Debug.LogError("[URPMinimal] Failed to create minimal pipeline!");
                return;
            }

            // Apply to graphics settings
            GraphicsSettings.defaultRenderPipeline = minimalPipeline;

            // Apply to all quality levels
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = minimalPipeline;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[URPMinimal] Applied minimal URP to all quality levels");
        }
    }
}
