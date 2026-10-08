using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// Stand-ins drawn in code until the real pictures exist (D-09): a coloured silhouette with a «nose» that shows which way it faces
    /// (front: a dot low on the head, back: none, side: a wedge to the right), four walking frames (the feet swap), a soft shadow blob,
    /// a paper plate. Nothing here is saved in assets except the plate the UI look bakes.
    /// </summary>
    public static class PlaceholderSprites
    {
        public const int Width = 64, Height = 128, FrameCount = 4;

        private static readonly Dictionary<string, CharacterSpriteSet> Sets = new Dictionary<string, CharacterSpriteSet>();
        private static Texture2D _shadow;
        private static Sprite _plate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Sets.Clear(); _shadow = null; _plate = null; }

        /// <summary>A silhouette set for a character id and colour (cached; ppm = 75, so the figure is 1.7 m).</summary>
        public static CharacterSpriteSet SetFor(string id, Color color, float pixelsPerMeter = 75f, float strideMeters = 0.8f)
        {
            string key = id + "|" + ColorUtility.ToHtmlStringRGB(color) + "|" + pixelsPerMeter;
            if (Sets.TryGetValue(key, out var cached) && cached != null) return cached;
            var set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            set.name = "placeholder_" + id;
            set.hideFlags = HideFlags.HideAndDontSave;
            set.Configure(Frames(color, Facing.Front), Frames(color, Facing.Back), Frames(color, Facing.Side), null, pixelsPerMeter, strideMeters, true, color);
            Sets[key] = set;
            return set;
        }

        private static Sprite[] Frames(Color body, Facing facing)
        {
            var frames = new Sprite[FrameCount];
            for (int f = 0; f < FrameCount; f++) frames[f] = Draw(body, facing, f);
            return frames;
        }

        private static Sprite Draw(Color body, Facing facing, int frame)
        {
            var px = new Color32[Width * Height];
            var fill = (Color32)body;
            var ink = (Color32)Color.Lerp(body, new Color(0.12f, 0.08f, 0.05f, 1f), 0.65f);
            var nose = (Color32)Color.Lerp(body, Color.white, 0.55f);
            var clear = new Color32(0, 0, 0, 0);

            const float bodyCx = 32f, bodyCy = 46f, bodyA = 20f, bodyB = 34f;   // torso ellipse
            const float headCx = 32f, headCy = 104f, headR = 17f;               // head circle
            int swing = frame == 1 ? 5 : frame == 3 ? -5 : 0;                   // feet swap on frames 1 and 3
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float bx = (x - bodyCx) / bodyA, by = (y - bodyCy) / bodyB;
                    float bd = bx * bx + by * by;
                    float hd = Mathf.Sqrt((x - headCx) * (x - headCx) + (y - headCy) * (y - headCy));
                    bool inBody = bd <= 1f, inHead = hd <= headR;
                    bool inFeet = y < 9 && (Mathf.Abs(x - (22 + swing)) < 6 || Mathf.Abs(x - (42 - swing)) < 6);
                    if (!(inBody || inHead || inFeet)) { px[y * Width + x] = clear; continue; }
                    bool edge = (inBody && bd > 0.80f && !inHead) || (inHead && hd > headR - 2.2f) || (inFeet && !inBody && y < 2);
                    px[y * Width + x] = edge ? ink : fill;
                }
            }
            if (facing == Facing.Front) Block(px, 30, 98, 5, 4, nose);                       // nose dot, low on the face
            if (facing == Facing.Side) for (int i = 0; i < 9; i++) Block(px, 47 + i, 104 - i / 2, 1, 5 - i / 2, nose); // wedge to the right
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0f), 100f);
        }

        private static void Block(Color32[] px, int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y0 + h); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x0 + w); x++)
                    px[y * Width + x] = c;
        }

        /// <summary>A soft round blob (white, alpha falling to the rim) for the shadow under a figure; tinted and sized by the material and the quad.</summary>
        public static Texture2D ShadowTexture
        {
            get
            {
                if (_shadow != null) return _shadow;
                const int n = 64;
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - n / 2f + 0.5f) * (x - n / 2f + 0.5f) + (y - n / 2f + 0.5f) * (y - n / 2f + 0.5f)) / (n / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        px[y * n + x] = new Color32(0, 0, 0, (byte)(255f * a * a));
                    }
                _shadow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                _shadow.SetPixels32(px);
                _shadow.Apply(false, true);
                return _shadow;
            }
        }

        /// <summary>A rounded paper plate with an ink border, 9-slice (border 20 px): bubbles, buttons, windows. Runtime copy; the UI look bakes a PNG of the same drawing.</summary>
        public static Sprite PaperPlate
        {
            get
            {
                if (_plate != null) return _plate;
                _plate = Sprite.Create(PlateTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
                _plate.hideFlags = HideFlags.HideAndDontSave;
                return _plate;
            }
        }

        /// <summary>The drawing of the plate (white, so the Image colour tints it): a 64×64 rounded square, ink-dark rim 3 px, paper inside.</summary>
        public static Texture2D PlateTexture()
        {
            const int n = 64;
            const float r = 16f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d > r) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    bool rim = d > r - 3f;
                    px[y * n + x] = rim ? new Color32(70, 55, 40, 255) : new Color32(255, 255, 255, 255);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
