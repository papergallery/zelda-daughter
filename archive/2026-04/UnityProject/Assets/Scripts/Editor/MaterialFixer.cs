using UnityEngine;
using UnityEditor;
using System.IO;

namespace ZeldaDaughter.Editor
{
    public static class MaterialFixer
    {
        [MenuItem("ZeldaDaughter/Fix/Replace Missing Shaders with URP Lit")]
        public static void FixAllMaterials()
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                litShader = Shader.Find("Standard");
                Debug.LogWarning("[MaterialFixer] URP/Lit не найден, используем Standard");
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" });
            int fixed_count = 0;

            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader"
                    || mat.shader.name.Contains("Error"))
                {
                    // Save color before changing shader
                    Color color = Color.white;
                    if (mat.HasProperty("_Color"))
                        color = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor"))
                        color = mat.GetColor("_BaseColor");

                    mat.shader = litShader;

                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", color);

                    EditorUtility.SetDirty(mat);
                    fixed_count++;
                    Debug.Log($"[MaterialFixer] Fixed: {path} → {litShader.name}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MaterialFixer] {fixed_count} материалов исправлено.");
        }
    }
}
