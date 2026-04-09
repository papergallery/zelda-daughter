using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class MaterialSimplifier
    {
        /// <summary>
        /// Заменяет URP/Lit на URP/Simple Lit на всех материалах в Assets/.
        /// Simple Lit использует Blinn-Phong вместо PBR и гораздо легче для SwiftShader.
        /// </summary>
        [MenuItem("ZeldaDaughter/Fix Materials/Convert Lit to Simple Lit")]
        public static void ConvertLitToSimpleLit()
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            var simpleLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");

            if (simpleLitShader == null)
            {
                Debug.LogError("[MaterialSimplifier] Simple Lit shader not found!");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int count = 0;

            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.shader == litShader || mat.shader.name == "Universal Render Pipeline/Lit")
                {
                    // Сохраняем основные свойства перед сменой шейдера
                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                    Texture mainTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                    float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;

                    mat.shader = simpleLitShader;

                    // Восстанавливаем свойства (Simple Lit использует те же имена для базовых)
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                    if (mat.HasProperty("_BaseMap") && mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

                    EditorUtility.SetDirty(mat);
                    count++;
                    Debug.Log($"[MaterialSimplifier] Converted: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MaterialSimplifier] Done. Converted {count} materials from Lit to Simple Lit.");
        }

        /// <summary>
        /// Конвертирует все материалы обратно в URP/Lit (для реальных устройств).
        /// </summary>
        [MenuItem("ZeldaDaughter/Fix Materials/Revert Simple Lit to Lit")]
        public static void RevertSimpleLitToLit()
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            var simpleLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int count = 0;

            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.shader == simpleLitShader || mat.shader.name == "Universal Render Pipeline/Simple Lit")
                {
                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                    Texture mainTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;

                    mat.shader = litShader;

                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                    if (mat.HasProperty("_BaseMap") && mainTex != null) mat.SetTexture("_BaseMap", mainTex);

                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MaterialSimplifier] Reverted {count} materials from Simple Lit to Lit.");
        }
    }
}
