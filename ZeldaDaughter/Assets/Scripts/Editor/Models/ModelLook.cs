using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-10: the one place that decides what the imported CC0 models look like. D-08: the shader is the toon shader
    /// (Assets/Shaders/ZeldaToon.shader); change <see cref="Grade"/> or the step settings here and bump ModelImport.GetVersion
    /// to reimport Assets/Art/Models — nothing else mentions a shader for models.
    /// </summary>
    public static class ModelLook
    {
        public const string ModelsRoot = "Assets/Art/Models/";
        public const string ShaderName = "Zelda/Toon";
        public const string ShaderPath = "Assets/Shaders/ZeldaToon.shader";

        /// <summary>Muted warm palette (project-design.md): pull colours a little towards grey and warm them. Kenney's raw greens are teal.</summary>
        public static Color Grade(Color c)
        {
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            float k = 0.55f; // D-08: Kenney's greens are teal; keep 55 % of the chroma, then warm (less blue) — olive, not mint
            var muted = new Color(Mathf.Lerp(grey, c.r, k), Mathf.Lerp(grey, c.g, k), Mathf.Lerp(grey, c.b, k), c.a);
            return new Color(muted.r * 1.08f, muted.g * 1.0f, muted.b * 0.78f, c.a);
        }

        /// <summary>The toon shader; loaded by path because an importer can run before the shader is registered under its name.</summary>
        public static Shader LoadShader()
        {
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            return shader != null ? shader : Shader.Find(ShaderName);
        }

        /// <summary>Material setup for one imported material; <paramref name="baseMap"/> may be null (flat colour).</summary>
        public static void Apply(Material material, Color color, Texture baseMap)
        {
            var shader = LoadShader();
            if (shader != null) material.shader = shader;
            material.SetColor("_BaseColor", baseMap != null ? Color.white : Grade(color));
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            Style(material);
        }

        /// <summary>The look numbers shared by every toon material: 3 light steps, soft border, warm shadow.</summary>
        public static void Style(Material material)
        {
            material.SetFloat("_Steps", 3f);
            material.SetFloat("_Softness", 0.07f);
            material.SetColor("_ShadowTint", new Color(0.66f, 0.52f, 0.52f, 1f));
        }
    }
}
