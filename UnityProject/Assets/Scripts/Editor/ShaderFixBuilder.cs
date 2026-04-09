using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class ShaderFixBuilder
    {
        private static readonly (string from, string to)[] ShaderMap =
        {
            ("Standard",                    "Universal Render Pipeline/Lit"),
            ("Standard (Specular setup)",   "Universal Render Pipeline/Lit"),
            ("Mobile/Diffuse",              "Universal Render Pipeline/Simple Lit"),
            ("Mobile/VertexLit",            "Universal Render Pipeline/Simple Lit"),
            ("Unlit/Color",                 "Universal Render Pipeline/Unlit"),
            ("Unlit/Texture",               "Universal Render Pipeline/Unlit"),
        };

        [MenuItem("ZeldaDaughter/Fix All Material Shaders (URP)")]
        public static void FixAllMaterials()
        {
            int fixed_count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string newShaderName = GetReplacementShaderName(mat);
                if (newShaderName == null) continue;

                var replacement = Shader.Find(newShaderName);
                if (replacement == null)
                {
                    Debug.LogWarning($"[ShaderFix] Replacement shader not found: {newShaderName}");
                    continue;
                }

                Debug.Log($"[ShaderFix] {path}: {mat.shader.name} → {newShaderName}");
                mat.shader = replacement;
                EditorUtility.SetDirty(mat);
                fixed_count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShaderFix] Done. Fixed {fixed_count} materials out of {guids.Length} total.");
        }

        private static string GetReplacementShaderName(Material mat)
        {
            if (mat.shader == null)
                return "Universal Render Pipeline/Lit";

            string shaderName = mat.shader.name;

            if (shaderName.Contains("Hidden/InternalErrorShader"))
                return "Universal Render Pipeline/Lit";

            foreach (var (from, to) in ShaderMap)
            {
                if (shaderName == from)
                    return to;
            }

            return null;
        }
    }
}
