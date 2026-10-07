using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// T-01: project settings by code, idempotent (project-design.md §11 «всё кодом»). Apply() sets, Report() checks.
    /// Batchmode: -executeMethod ZeldaDaughter.Editor.ProjectSetup.Apply / .Report — lines "[ZD:Setup] …" in the log.
    /// URP numbers: April URP asset (HDR/MSAA off, 1 cascade, no soft shadows, no depth/opaque textures) + SRP Batcher on,
    /// shadows 25 m / 1024 (docs/april-review.md §3). Changing a value here is the only way to change it.
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string RendererPath = SettingsDir + "/URP_Mobile_Renderer.asset";
        const string PipelinePath = SettingsDir + "/URP_Mobile.asset";
        const string AppId = "com.papergallery.zeldasdaughter"; // April id (never published)

        static readonly (string field, object value)[] Pipeline =
        {
            ("m_SupportsHDR", false),
            ("m_MSAA", 1),
            ("m_RenderScale", 1f),
            ("m_RequireDepthTexture", false),
            ("m_RequireOpaqueTexture", false),
            ("m_MainLightShadowsSupported", true),
            ("m_MainLightShadowmapResolution", 1024),
            ("m_ShadowDistance", 25f),
            ("m_ShadowCascadeCount", 1),
            ("m_SoftShadowsSupported", false),
            ("m_UseSRPBatcher", true),
            ("m_SupportsDynamicBatching", false),
            ("m_GPUResidentDrawerMode", 0),
        };

        [MenuItem("Zelda/Project/Apply settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = "papergallery";
            PlayerSettings.productName = "Zelda's Daughter";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, AppId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            SetProjectSetting("activeInputHandler", 1); // Input System only

            var asset = EnsurePipeline();
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[ZD:Setup] applied");
            Report();
        }

        [MenuItem("Zelda/Project/Report settings")]
        public static void Report()
        {
            var bad = new List<string>();
            void Check(string name, object actual, object expected)
            {
                bool ok = Equals(actual, expected) || actual?.ToString() == expected?.ToString();
                Debug.Log($"[ZD:Setup] {name}={actual}{(ok ? "" : $" (want {expected})")}");
                if (!ok) bad.Add(name);
            }

            Check("appId.android", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), AppId);
            Check("orientation", PlayerSettings.defaultInterfaceOrientation, UIOrientation.Portrait);
            Check("colorSpace", PlayerSettings.colorSpace, ColorSpace.Linear);
            Check("backend.android", PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), ScriptingImplementation.IL2CPP);
            Check("arch.android", PlayerSettings.Android.targetArchitectures, AndroidArchitecture.ARM64);
            Check("minSdk", PlayerSettings.Android.minSdkVersion, AndroidSdkVersions.AndroidApiLevel28);
            Check("textures.android", EditorUserBuildSettings.androidBuildSubtarget, MobileTextureSubtarget.ASTC);
            Check("activeInputHandler", GetProjectSetting("activeInputHandler"), 1);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            Check("pipeline.asset", asset != null, true);
            Check("pipeline.default", GraphicsSettings.defaultRenderPipeline == asset && asset != null, true);
            if (asset != null)
            {
                var so = new SerializedObject(asset);
                foreach (var (field, value) in Pipeline) Check("urp." + field, Read(so.FindProperty(field)), value);
            }
            Debug.Log(bad.Count == 0 ? "[ZD:Setup] OK" : "[ZD:Setup] MISMATCH " + string.Join(",", bad));
        }

        static UniversalRenderPipelineAsset EnsurePipeline()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir)) AssetDatabase.CreateFolder("Assets", "Settings");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }
            var so = new SerializedObject(asset);
            foreach (var (field, value) in Pipeline) Write(so.FindProperty(field), value, field);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void Write(SerializedProperty p, object value, string field)
        {
            if (p == null) { Debug.LogError($"[ZD:Setup] no field {field} in URP asset"); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        static object Read(SerializedProperty p)
        {
            if (p == null) return null;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.Float: return p.floatValue;
                default: return p.intValue;
            }
        }

        static SerializedObject ProjectSettingsObject() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);

        static void SetProjectSetting(string name, int value)
        {
            var so = ProjectSettingsObject();
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogError($"[ZD:Setup] no project setting {name}"); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static object GetProjectSetting(string name) => ProjectSettingsObject().FindProperty(name)?.intValue;
    }
}
