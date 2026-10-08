using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// Procedural paper for the watercolour pass: 256² tileable RGBA, no asset on disk.
    /// R fine grain (fibres), G blotches (uneven wash density, also breaks the posterisation borders), B/A two smooth warp fields.
    /// Deterministic (fixed seeds).
    /// </summary>
    public static class PaperTexture
    {
        public const int Size = 256;

        public static Texture2D Create()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "ZeldaPaper",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float grain = 0.5f * Fbm(u, v, 64, 3, 11) + 0.5f * Hash(x, y, 5);
                float fibre = Fbm(u * 1.0f, v * 0.25f + 0.1f, 96, 2, 17); // slightly stretched
                grain = Mathf.Clamp01(0.7f * grain + 0.3f * fibre);
                float blotch = Fbm(u, v, 4, 4, 23);
                float warpA = Fbm(u, v, 5, 3, 31);
                float warpB = Fbm(u, v, 6, 3, 41);
                px[y * Size + x] = new Color32(B(Contrast(grain)), B(Contrast(blotch)), B(warpA), B(warpB));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static byte B(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
        static float Contrast(float v) => Mathf.Clamp01((v - 0.5f) * 1.6f + 0.5f);

        // Periodic value noise: lattice of `period` cells over the unit square; octaves double the period.
        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(u, v, period << o, seed + o * 7);
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float Value(float u, float v, int period, int seed)
        {
            float fx = u * period, fy = v * period;
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = Smooth(fx - ix), ty = Smooth(fy - iy);
            int x0 = ix % period, y0 = iy % period, x1 = (ix + 1) % period, y1 = (iy + 1) % period;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
