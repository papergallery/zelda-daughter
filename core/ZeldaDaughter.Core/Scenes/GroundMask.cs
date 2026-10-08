#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>
    /// D-22b: the ground of a scene drawn by one material instead of ribbons and discs on top of it. A texture over the whole ground
    /// holds, per texel, the signed distance (metres, negative inside) to four kinds of surface; the ground shader (Zelda/Toon, _ZD_GROUND)
    /// breaks the edge with noise (a ragged road edge), tones the sand and the grass by world noise and paves the square.
    /// <list type="bullet">
    /// <item>R — roads and tracks (<see cref="SceneConfig.Paths"/>, all of them);</item>
    /// <item>G — paved zones (zones tagged <c>paved</c>: the cobbled square);</item>
    /// <item>B — water (<see cref="SceneConfig.Water"/>: the damp bank along a river);</item>
    /// <item>A — tilled or trodden zones (zones tagged <c>field</c> or <c>bare</c>: the field, the earth around a campfire).</item>
    /// </list>
    /// Distances are clamped to ±<see cref="Range"/> and stored as bytes (0.5 = the edge), so bilinear filtering of the texture gives a smooth
    /// edge at any texel size. Rows go from the south edge (−z) up, like Texture2D.SetPixels32. Pure C#: the same config, the same bytes.
    /// </summary>
    public sealed class GroundMask
    {
        /// <summary>Distances beyond ±Range metres are stored as Range.</summary>
        public const float Range = 4f;
        public const string PavedTag = "paved";
        public const string FieldTag = "field";
        /// <summary>D-22b: trodden bare earth (around a campfire) — drawn like the field channel.</summary>
        public const string BareTag = "bare";

        public int Width { get; private set; }
        public int Height { get; private set; }
        public float MetresPerTexel { get; private set; }
        /// <summary>World x, z of the south-west corner of the texture (the corner of the ground).</summary>
        public float MinX { get; private set; }
        public float MinZ { get; private set; }
        /// <summary>RGBA bytes, row-major from the south edge.</summary>
        public byte[] Rgba { get; private set; } = Array.Empty<byte>();

        /// <summary>
        /// Second layer (D-22b, stylised ambient occlusion of the concept f1): R — signed distance to the cool soft shadow spot under a tree, rock,
        /// bush, stump or log; G, B, A — free (255). Same size and order as <see cref="Rgba"/>; empty until <see cref="BakeSpots"/>.
        /// </summary>
        public byte[] Spots { get; private set; } = Array.Empty<byte>();

        /// <summary>A shadow spot on the ground: centre and radius, metres.</summary>
        public readonly struct Spot
        {
            public readonly float X, Z, R;
            public Spot(float x, float z, float r) { X = x; Z = z; R = r; }
        }

        /// <summary>
        /// Spots under the things that stand on the ground: objects of the config and scattered models whose catalog tags are tree, rock (not
        /// small), bush or wood. Radius from the measured bounds × scale: a crown shades ≈ a third of its width around the trunk, a rock or a bush
        /// a little more than itself.
        /// </summary>
        public static List<Spot> SpotsOf(SceneConfig c, ModelCatalog catalog, IEnumerable<ScatterPlacement> placements)
        {
            var list = new List<Spot>();
            void Add(string model, float x, float z, float scale)
            {
                if (!catalog.Has(model)) return;
                var def = catalog.Get(model);
                if (def.IsSprite) return;
                var t = def.Tags;
                float k = t.Contains("tree") ? 0.34f : (t.Contains("rock") && !t.Contains("small")) || t.Contains("bush") || t.Contains("wood") ? 0.6f : 0f;
                if (k <= 0f) return;
                var b = catalog.Bounds(model);
                list.Add(new Spot(x, z, Math.Max(b.SizeX, b.SizeZ) * scale * k));
            }
            foreach (var o in c.Objects)
                if (!string.IsNullOrEmpty(o.Model)) Add(o.Model!, o.Position.X, o.Position.Z, o.Scale.X);
            foreach (var p in placements) Add(p.ModelId, p.X, p.Z, p.Scale);
            return list;
        }

        /// <summary>Rasterises the spots into <see cref="Spots"/> (each spot touches only the texels within its radius + Range).</summary>
        public void BakeSpots(IEnumerable<Spot> spots)
        {
            var d = new float[Width * Height];
            for (int k = 0; k < d.Length; k++) d[k] = float.PositiveInfinity;
            foreach (var s in spots)
            {
                float reach = s.R + Range;
                int i0 = Math.Max(0, (int)Math.Floor((s.X - reach - MinX) / MetresPerTexel)), i1 = Math.Min(Width - 1, (int)Math.Ceiling((s.X + reach - MinX) / MetresPerTexel));
                int j0 = Math.Max(0, (int)Math.Floor((s.Z - reach - MinZ) / MetresPerTexel)), j1 = Math.Min(Height - 1, (int)Math.Ceiling((s.Z + reach - MinZ) / MetresPerTexel));
                for (int j = j0; j <= j1; j++)
                {
                    float z = MinZ + (j + 0.5f) * MetresPerTexel - s.Z;
                    for (int i = i0; i <= i1; i++)
                    {
                        float x = MinX + (i + 0.5f) * MetresPerTexel - s.X;
                        float v = (float)Math.Sqrt(x * x + z * z) - s.R;
                        int k = j * Width + i;
                        if (v < d[k]) d[k] = v;
                    }
                }
            }
            var bytes = new byte[Width * Height * 4];
            for (int k = 0; k < d.Length; k++)
            {
                bytes[k * 4] = Encode(d[k]);
                bytes[k * 4 + 1] = bytes[k * 4 + 2] = bytes[k * 4 + 3] = 255;
            }
            Spots = bytes;
        }

        /// <summary>Stored distance (metres) to the nearest shadow spot at the texel under (x, z).</summary>
        public float SpotAt(float x, float z)
        {
            int i = (int)Math.Floor((x - MinX) / MetresPerTexel), j = (int)Math.Floor((z - MinZ) / MetresPerTexel);
            if (Spots.Length == 0 || i < 0 || j < 0 || i >= Width || j >= Height) return Range;
            return Decode(Spots[(j * Width + i) * 4]);
        }

        /// <summary>Does the scene need a ground mask at all (anything drawn into it)?</summary>
        public static bool Wanted(SceneConfig c)
        {
            if (c.Paths.Count > 0 || c.Water.Count > 0) return true;
            foreach (var z in c.Zones) if (z.Tags.Contains(PavedTag) || z.Tags.Contains(FieldTag) || z.Tags.Contains(BareTag)) return true;
            return false;
        }

        public static GroundMask Bake(SceneConfig c, float metresPerTexel = 0.4f)
        {
            if (metresPerTexel <= 0f) throw new ArgumentOutOfRangeException(nameof(metresPerTexel));
            var m = new GroundMask
            {
                MetresPerTexel = metresPerTexel,
                Width = Math.Max(1, (int)Math.Ceiling(c.Ground.SizeX / metresPerTexel)),
                Height = Math.Max(1, (int)Math.Ceiling(c.Ground.SizeZ / metresPerTexel)),
                MinX = -c.Ground.SizeX / 2f,
                MinZ = -c.Ground.SizeZ / 2f,
            };
            var paved = c.Zones.FindAll(z => z.Tags.Contains(PavedTag));
            var field = c.Zones.FindAll(z => z.Tags.Contains(FieldTag) || z.Tags.Contains(BareTag));
            var bytes = new byte[m.Width * m.Height * 4];
            for (int j = 0; j < m.Height; j++)
            {
                float z = m.MinZ + (j + 0.5f) * metresPerTexel;
                for (int i = 0; i < m.Width; i++)
                {
                    float x = m.MinX + (i + 0.5f) * metresPerTexel;
                    int k = (j * m.Width + i) * 4;
                    bytes[k] = Encode(Nearest(c.Paths, x, z));
                    bytes[k + 1] = Encode(Nearest(paved, x, z));
                    bytes[k + 2] = Encode(Nearest(c.Water, x, z));
                    bytes[k + 3] = Encode(Nearest(field, x, z));
                }
            }
            m.Rgba = bytes;
            return m;
        }

        /// <summary>Smallest signed distance to any of the areas; +∞ when there are none. Areas whose box is farther than Range are skipped.</summary>
        static float Nearest<T>(System.Collections.Generic.List<T> areas, float x, float z) where T : Area
        {
            float best = float.PositiveInfinity;
            foreach (var a in areas)
            {
                var (minX, minZ, maxX, maxZ) = a.Bounds();
                if (x < minX - Range || x > maxX + Range || z < minZ - Range || z > maxZ + Range) continue;
                float d = a.SignedDistance(x, z);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Metres → byte: −Range → 0, the edge → 128, +Range (or farther) → 255.</summary>
        public static byte Encode(float metres)
        {
            if (float.IsPositiveInfinity(metres) || metres >= Range) return 255;
            if (metres <= -Range) return 0;
            return (byte)Math.Round((metres / Range * 0.5f + 0.5f) * 255f);
        }

        /// <summary>Byte → metres (what the shader does with the filtered value).</summary>
        public static float Decode(byte b) => (b / 255f - 0.5f) * 2f * Range;

        /// <summary>Stored distance (metres) of channel 0..3 at the texel under (x, z); +Range outside the texture.</summary>
        public float At(int channel, float x, float z)
        {
            int i = (int)Math.Floor((x - MinX) / MetresPerTexel), j = (int)Math.Floor((z - MinZ) / MetresPerTexel);
            if (i < 0 || j < 0 || i >= Width || j >= Height) return Range;
            return Decode(Rgba[(j * Width + i) * 4 + channel]);
        }
    }
}
