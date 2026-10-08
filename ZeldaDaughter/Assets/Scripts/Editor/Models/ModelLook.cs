using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-10: the one place that decides what the imported CC0 models look like. D-08 swaps <see cref="ShaderName"/> (toon)
    /// and <see cref="Grade"/> here and reimports Assets/Art/Models — nothing else mentions a shader for models.
    /// </summary>
    public static class ModelLook
    {
        public const string ModelsRoot = "Assets/Art/Models/";
        public const string ShaderName = "Universal Render Pipeline/Lit";

        /// <summary>Muted warm palette (project-design.md): pull colours a little towards grey and warm them. Kenney's raw greens are teal.</summary>
        public static Color Grade(Color c)
        {
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            var muted = new Color(Mathf.Lerp(c.r, grey, 0.25f), Mathf.Lerp(c.g, grey, 0.25f), Mathf.Lerp(c.b, grey, 0.25f), c.a);
            return new Color(muted.r * 1.05f, muted.g * 1.0f, muted.b * 0.92f, c.a);
        }

        /// <summary>Material setup for one imported material; <paramref name="baseMap"/> may be null (flat colour).</summary>
        public static void Apply(Material material, Color color, Texture baseMap)
        {
            var shader = Shader.Find(ShaderName);
            if (shader != null) material.shader = shader;
            material.SetColor("_BaseColor", baseMap != null ? Color.white : Grade(color));
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }
    }
}
