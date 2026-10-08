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

        /// <summary>W0: layers the game's rules talk about — the ground the ray and drops land on, what blocks (CheckSphere for placing items), who acts.</summary>
        public static readonly string[] Layers = { "Ground", "Blocking", "Actors" };

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
            EnsureLayers();
            // Domain reload on entering Play Mode stays on: statics (GameData) start clean. The bridge's run_tests
            // switched it on by itself on 2026-10-07 — Apply() puts it back.
            EditorSettings.enterPlayModeOptionsEnabled = false;

            var asset = EnsurePipeline();
            GraphicsSettings.defaultRenderPipeline = asset;
            // What URP sets by itself on the first interactive open — explicit, so Apply() is the only source.
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
                QualitySettings.antiAliasing = 0; // MSAA lives in the URP asset
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            LookSetup.Apply(); // D-08: the watercolour Renderer Feature on this renderer
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
            Check("enterPlayModeOptions", EditorSettings.enterPlayModeOptionsEnabled, false);

            foreach (var layer in Layers) Check("layer." + layer, LayerMask.NameToLayer(layer) >= 0, true);
            Check("lights.linearIntensity", GraphicsSettings.lightsUseLinearIntensity, true);
            Check("lights.colorTemperature", GraphicsSettings.lightsUseColorTemperature, true);
            Check("quality.antiAliasing", QualitySettings.antiAliasing, 0);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            Check("look.feature", LookSetup.IsWired(), true);
            Check("pipeline.asset", asset != null, true);
            Check("pipeline.default", GraphicsSettings.defaultRenderPipeline == asset && asset != null, true);
            if (asset != null)
            {
                var so = new SerializedObject(asset);
                foreach (var (field, value) in Pipeline) Check("urp." + field, Read(so.FindProperty(field)), value);
            }
            Debug.Log(bad.Count == 0 ? "[ZD:Setup] OK" : "[ZD:Setup] MISMATCH " + string.Join(",", bad));
        }

        /// <summary>Puts <see cref="Layers"/> into the first free user layers (8…31) of ProjectSettings/TagManager.asset; those already there stay where they are.</summary>
        public static void EnsureLayers()
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            bool changed = false;
            foreach (var name in Layers)
            {
                bool present = false;
                for (int i = 0; i < layers.arraySize && !present; i++) present = layers.GetArrayElementAtIndex(i).stringValue == name;
                if (present) continue;
                int free = -1;
                for (int i = 8; i < layers.arraySize && free < 0; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) free = i;
                if (free < 0) { Debug.LogError($"[ZD:Setup] no free layer for {name}"); continue; }
                layers.GetArrayElementAtIndex(free).stringValue = name;
                changed = true;
                Debug.Log($"[ZD:Setup] layer {free} = {name}");
            }
            if (changed)
            {
                manager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
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
