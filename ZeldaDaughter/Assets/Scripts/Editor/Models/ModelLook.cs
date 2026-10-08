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
        /// <summary>D-21: cool umber-blue shadow (concept: "сине-серо-умбровые"), not the warm brown of D-08.</summary>
        public static readonly Color ShadowTint = new Color(0.56f, 0.55f, 0.66f, 1f);
        public const string ShaderPath = "Assets/Shaders/ZeldaToon.shader";

        /// <summary>
        /// Muted warm palette (project-design.md). Kenney's raw greens are teal and its wood is orange. D-21: 60 % of the chroma, greens
        /// pulled to olive (hue ≈ 75°), orange wood (hue 15–35°) to brown (less chroma, darker), then a light warm cast. The same rules as
        /// GradePalette in ZeldaToon.shader (for palette textures) — change both together.
        /// </summary>
        public static Color Grade(Color c)
        {
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            const float k = 0.6f;
            var muted = new Color(Mathf.Lerp(grey, c.r, k), Mathf.Lerp(grey, c.g, k), Mathf.Lerp(grey, c.b, k), c.a);
            Color.RGBToHSV(muted, out float h, out float s, out float v);
            float green = Smooth(0.16f, 0.22f, h) * (1f - Smooth(0.45f, 0.55f, h));
            h = Mathf.Lerp(h, 0.21f, green * 0.7f);
            s *= Mathf.Lerp(1f, 0.5f, green);
            v *= Mathf.Lerp(1f, 0.72f, green);
            float wood = Smooth(0.04f, 0.06f, h) * (1f - Smooth(0.10f, 0.13f, h)) * Smooth(0.30f, 0.50f, s);
            h = Mathf.Lerp(h, 0.075f, wood * 0.5f);
            s *= Mathf.Lerp(1f, 0.42f, wood);
            v *= Mathf.Lerp(1f, 0.5f, wood);
            var o = Color.HSVToRGB(h, s, v);
            return new Color(o.r * 1.05f, o.g, o.b * 0.86f, c.a);
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
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
            // A palette / trim texture is regraded in the shader by the same rules (the flat colour above is graded here).
            material.SetFloat("_Grade", baseMap != null ? 1f : 0f);
            if (baseMap != null) material.EnableKeyword("_ZD_GRADE"); else material.DisableKeyword("_ZD_GRADE");
            Style(material);
        }

        /// <summary>The look numbers shared by every toon material: 3 light steps, soft border, warm shadow.</summary>
        public static void Style(Material material)
        {
            material.SetFloat("_Steps", 3f);
            material.SetFloat("_Softness", 0.07f);
            material.SetColor("_ShadowTint", ShadowTint);
        }
    }
}
