using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// D-08: every number of the "pen + watercolour" look in one asset (Assets/Settings/WatercolorLook.asset, made by
    /// LookSetup). This is look, not balance — it does not live in data/*.json. Defaults are the values the reference frames
    /// F1 / F1n were judged with (docs/demo/frames/D-08-*.png); change them there, in the Inspector, and record why.
    /// </summary>
    [CreateAssetMenu(menuName = "Zelda/Watercolor look", fileName = "WatercolorLook")]
    public sealed class WatercolorSettings : ScriptableObject
    {
        [Tooltip("Off = the frame without the effect (comparison). The toon material of the models stays.")]
        public bool enabled = true;

        [Header("Pen line")]
        [Tooltip("Line width in pixels of a 2340-px-high screen; scaled with the screen height so a phone and a frame look alike.")]
        [Range(0.5f, 6f)] public float lineWidthPx = 1.4f;
        public float lineReferenceHeight = 2340f;
        public Color lineColor = new Color(0.110f, 0.145f, 0.082f, 0.95f); // #1c2515, the darkest pixels of the concept F1
        [Tooltip("Depth jump (second difference, metres) that makes a line.")]
        [Range(0.02f, 2f)] public float depthThreshold = 0.28f;
        [Tooltip("1 - dot(normals) that makes a line (creases).")]
        [Range(0.05f, 1.5f)] public float normalThreshold = 0.2f;
        [Range(0f, 1f)] public float lineWobble = 0.25f;

        [Header("Wash")]
        [Range(2f, 12f)] public float toneLevels = 6f;
        [Range(0f, 1f)] public float posterize = 0.6f;
        [Tooltip("How far (px) the colour of a spot wanders over its edge.")]
        [Range(0f, 8f)] public float bleedPx = 2.5f;
        [Range(0f, 1f)] public float edgeDarken = 0.22f;

        [Header("Paper")]
        public Color paperColor = new Color(0.96f, 0.91f, 0.80f, 1f);
        [Tooltip("Size of the paper texture tile in screen pixels.")]
        [Range(128f, 1024f)] public float paperTilePx = 420f;
        [Range(0f, 0.5f)] public float grain = 0.12f;
        [Range(0f, 0.5f)] public float blotch = 0.05f;
        [Tooltip("1 = the picture's own colours, less = more of the paper shows through (lifted shadows).")]
        [Range(0.5f, 1f)] public float pigment = 0.96f;

        [Header("Grade")]
        [Range(0.3f, 1.2f)] public float saturation = 0.92f;
        [Range(0.7f, 1.4f)] public float contrast = 1.02f;
        public Color warmTint = new Color(1.04f, 1.0f, 0.94f, 1f);
        [Range(0f, 1f)] public float vignette = 0.5f;
        [Range(0f, 1f)] public float vignetteStart = 0.45f;
        public Color vignetteColor = new Color(0.55f, 0.40f, 0.28f, 1f);

        [Header("Tone and night (D-21)")]
        [Tooltip("S-curve of the tone: lights lighter (the middle of the road), shadows deeper. 0 = none.")]
        [Range(0f, 1f)] public float toneCurve = 0.5f;
        [Tooltip("At night (the global _ZD_Night 0..1) the grade, the paper and the vignette turn deep blue instead of warm.")]
        public Color nightTint = new Color(0.78f, 0.92f, 1.30f, 1f);
        public Color nightPaper = new Color(0.40f, 0.50f, 0.78f, 1f);
        public Color nightVignette = new Color(0.30f, 0.38f, 0.62f, 1f);

        [Header("Morning mist (D-22b)")]
        [Tooltip("Colour of the low mist patches; alpha = opacity in the thick of a patch (the strength by time of day is MorningMist's).")]
        public Color mistColor = new Color(0.95f, 0.92f, 0.84f, 0.32f);
    }
}
