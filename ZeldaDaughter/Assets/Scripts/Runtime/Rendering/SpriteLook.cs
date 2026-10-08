using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// How the billboard sprites are drawn: one asset (Assets/Art/Registries/SpriteLook.asset, made by RegistryBuilder). The sprite material is
    /// URP Unlit with alpha clip, two-sided — it writes depth, so the pen outline of D-08 (depth + normals) sees the figure; the shadow is a
    /// transparent soft blob on the ground. Look numbers, not balance.
    /// </summary>
    public sealed class SpriteLook : ScriptableObject
    {
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private Material _shadowMaterial;
        [SerializeField, Range(0.05f, 0.95f)] private float _alphaCutoff = 0.5f;
        [SerializeField] private float _shadowWidthMeters = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _shadowAlpha = 0.35f;
        [SerializeField] private float _shadowLift = 0.04f;
        [SerializeField, Range(1f, 2f)] private float _facingHysteresis = 1.25f;
        [SerializeField] private float _bobMeters = 0.035f;
        [SerializeField] private float _lyingLift = 0.12f;
        [SerializeField] private float _turnSeconds = 0.12f;
        [SerializeField] private float _startSeconds = 0.12f;
        [SerializeField] private float _stopSeconds = 0.15f;
        [SerializeField] private float _idleFrameSeconds = 0.3f;

        public Material SpriteMaterial => _spriteMaterial != null ? _spriteMaterial : Fallback(ref _runtimeSprite, false);
        public Material ShadowMaterial => _shadowMaterial != null ? _shadowMaterial : Fallback(ref _runtimeShadow, true);
        public float AlphaCutoff => _alphaCutoff;
        public float ShadowWidthMeters => _shadowWidthMeters;
        public float ShadowAlpha => _shadowAlpha;
        public float ShadowLift => _shadowLift;
        /// <summary>How clearly one axis must win before the figure turns to another view (1 = no hysteresis).</summary>
        public float FacingHysteresis => _facingHysteresis;
        /// <summary>Height of the bounce of a step, metres.</summary>
        public float BobMeters => _bobMeters;
        /// <summary>A figure lying on the ground is raised this much so it does not sink into it.</summary>
        public float LyingLift => _lyingLift;
        /// <summary>D-25: how long the drawn frames between the walking ones last (all of them together): a turn, the first steps, a stop.</summary>
        public float TurnSeconds => _turnSeconds;
        public float StartSeconds => _startSeconds;
        public float StopSeconds => _stopSeconds;
        /// <summary>One frame of the drawn breath of standing («idle»).</summary>
        public float IdleFrameSeconds => _idleFrameSeconds;

        private static Material _runtimeSprite, _runtimeShadow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _runtimeSprite = null; _runtimeShadow = null; }

        private Material Fallback(ref Material cache, bool shadow)
        {
            if (cache == null) cache = shadow ? NewShadowMaterial() : NewSpriteMaterial(_alphaCutoff);
            return cache;
        }

        public void Configure(Material sprite, Material shadow, float alphaCutoff, float shadowWidth, float shadowAlpha, float shadowLift)
        {
            _spriteMaterial = sprite;
            _shadowMaterial = shadow;
            _alphaCutoff = alphaCutoff;
            _shadowWidthMeters = shadowWidth;
            _shadowAlpha = shadowAlpha;
            _shadowLift = shadowLift;
        }

        /// <summary>
        /// Alpha clip, two-sided, depth write — the figure is a cut-out card in the 3D world. D-21: the toon shader in sprite-lit mode
        /// (ground-like light + additional lights, so the campfire and the torch light the figure); URP Unlit if the shader is not found.
        /// </summary>
        public static Material NewSpriteMaterial(float cutoff)
        {
            var toon = Shader.Find("Zelda/Toon");
            var m = new Material(toon != null ? toon : Shader.Find("Universal Render Pipeline/Unlit")) { name = "ZdSprite" };
            if (toon != null) ToSpriteLit(m, toon);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_ZWrite", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = 2450;
            return m;
        }

        /// <summary>Switches a sprite material to the toon shader in sprite-lit mode, keeping its texture, tint and cutoff. Idempotent.</summary>
        public static void ToSpriteLit(Material m, Shader toon)
        {
            Texture map = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            m.shader = toon;
            if (map != null) m.SetTexture("_BaseMap", map);
            m.SetFloat("_SpriteLit", 1f);
            m.EnableKeyword("_SPRITELIT");
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_Steps", 3f);
            m.SetFloat("_Softness", 0.12f);
            m.SetColor("_ShadowTint", new Color(0.62f, 0.62f, 0.72f, 1f));
            m.SetColor("_BaseColor", Color.white);
            m.renderQueue = 2450;
        }

        /// <summary>URP Unlit, transparent, no depth write — the soft blob under the feet.</summary>
        public static Material NewShadowMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ZdShadow" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            return m;
        }
    }
}
