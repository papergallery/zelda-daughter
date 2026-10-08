using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-08: wires the look into the project by code, idempotent — the settings asset (made once, never overwritten, so tuning in
    /// the Inspector survives), the WatercolorFeature on the mobile URP renderer, and the toon shader on the generated materials.
    /// Called from ProjectSetup.Apply; also Zelda → Look → Apply. Log: "[ZD:Look] …".
    /// </summary>
    public static class LookSetup
    {
        public const string SettingsPath = "Assets/Settings/WatercolorLook.asset";
        const string RendererPath = "Assets/Settings/URP_Mobile_Renderer.asset";
        const string PostShaderPath = "Assets/Shaders/ZeldaWatercolor.shader";
        const string GeneratedMaterials = "Assets/Generated/Materials";

        [MenuItem("Zelda/Look/Apply")]
        public static void Apply()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WatercolorSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WatercolorSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PostShaderPath);
            if (shader == null) { Debug.LogError("[ZD:Look] no shader at " + PostShaderPath); return; }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null) { Debug.LogError("[ZD:Look] no renderer at " + RendererPath + " (run Zelda → Project → Apply settings first)"); return; }

            var feature = renderer.rendererFeatures.OfType<WatercolorFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<WatercolorFeature>();
                feature.name = "Watercolor";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                var so = new SerializedObject(renderer);
                var list = so.FindProperty("m_RendererFeatures");
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                var map = so.FindProperty("m_RendererFeatureMap");
                map.InsertArrayElementAtIndex(map.arraySize);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            feature.Configure(settings, shader);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(settings);

            int converted = ConvertGeneratedMaterials();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZD:Look] applied feature={feature.name} settings={SettingsPath} toonMaterials={converted}");
        }

        /// <summary>Materials made by SceneBuilder before D-08 are URP/Lit; "created once" means they would stay so.</summary>
        static int ConvertGeneratedMaterials()
        {
            var toon = ModelLook.LoadShader();
            if (toon == null || !AssetDatabase.IsValidFolder(GeneratedMaterials)) return 0;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { GeneratedMaterials }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == toon) continue;
                var color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                mat.shader = toon;
                mat.SetColor("_BaseColor", color);
                ModelLook.Style(mat);
                EditorUtility.SetDirty(mat);
                n++;
            }
            return n;
        }

        public static bool IsWired()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            return renderer != null && renderer.rendererFeatures.OfType<WatercolorFeature>().Any(f => f != null && f.Settings != null);
        }
    }
}
