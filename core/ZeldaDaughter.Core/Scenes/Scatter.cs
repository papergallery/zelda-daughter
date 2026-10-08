#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>One placed piece of decor.</summary>
    public sealed class ScatterPlacement
    {
        public string Id { get; set; } = "";
        public string ScatterId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public float X { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
        public float Scale { get; set; } = 1f;
        public bool Collide { get; set; }
    }

    /// <summary>
    /// D-10: decor scattered over regions of a scene. The same config and catalog always give the same list (own SplitMix64 per entry,
    /// no System.Random), so a scene built twice has one hash. Items keep away from roads, water, buildings and named zones by the
    /// margins in <c>avoid</c> and from the edge of the ground.
    /// </summary>
    public static class Scatterer
    {
        const float EdgeMargin = 1f;
        const int TriesPerItem = 30;

        sealed class Rng
        {
            ulong _s;
            public Rng(int seed) { _s = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL); }
            public ulong Next()
            {
                unchecked
                {
                    _s += 0x9E3779B97F4A7C15UL;
                    ulong z = _s;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    return z ^ (z >> 31);
                }
            }
            public float Float() => (float)((Next() >> 40) / (double)(1UL << 24));
        }

        /// <summary>Oriented rectangle on the ground that a built thing covers.</summary>
        readonly struct Footprint
        {
            public readonly float Cx, Cz, Hx, Hz, Cos, Sin;
            public Footprint(float cx, float cz, float hx, float hz, float yawDegrees)
            {
                Cx = cx; Cz = cz; Hx = hx; Hz = hz;
                double a = yawDegrees * Math.PI / 180.0;
                Cos = (float)Math.Cos(a); Sin = (float)Math.Sin(a);
            }
            public float SignedDistance(float x, float z)
            {
                float dx = x - Cx, dz = z - Cz;
                float lx = dx * Cos - dz * Sin, lz = dx * Sin + dz * Cos;
                float ex = Math.Abs(lx) - Hx, ez = Math.Abs(lz) - Hz;
                if (ex > 0 || ez > 0) return (float)Math.Sqrt(Math.Max(ex, 0f) * Math.Max(ex, 0f) + Math.Max(ez, 0f) * Math.Max(ez, 0f));
                return Math.Max(ex, ez);
            }
        }

        public static IReadOnlyList<ScatterPlacement> Generate(SceneConfig config, ModelCatalog? catalog)
        {
            var result = new List<ScatterPlacement>();
            var footprints = Footprints(config, catalog);
            float halfX = config.Ground.SizeX / 2f - EdgeMargin, halfZ = config.Ground.SizeZ / 2f - EdgeMargin;
            foreach (var sc in config.Scatter)
            {
                var area = config.ResolveArea(sc.Area);
                if (area == null || sc.Models.Count == 0 || sc.Density <= 0) continue;
                var (minX, minZ, maxX, maxZ) = area.Bounds();
                minX = Math.Max(minX, -halfX); maxX = Math.Min(maxX, halfX);
                minZ = Math.Max(minZ, -halfZ); maxZ = Math.Min(maxZ, halfZ);
                if (maxX <= minX || maxZ <= minZ) continue;

                int want = Math.Min(sc.MaxCount, (int)Math.Round(area.SquareMetres() * sc.Density / 100f));
                float totalWeight = 0f;
                foreach (var m in sc.Models) totalWeight += m.Weight;
                var rng = new Rng(sc.Seed);
                var cells = new Dictionary<(int, int), List<(float X, float Z)>>();
                float cell = Math.Max(sc.MinSpacing, 0.5f);
                int placed = 0, tries = 0, maxTries = Math.Max(want * TriesPerItem, 1);
                while (placed < want && tries++ < maxTries)
                {
                    float x = minX + (maxX - minX) * rng.Float();
                    float z = minZ + (maxZ - minZ) * rng.Float();
                    // The random draws below are taken even for rejected candidates, keeping the stream independent of the rejection order.
                    float pick = rng.Float() * totalWeight;
                    float yaw = sc.RandomYaw ? rng.Float() * 360f : 0f;
                    float scale = sc.Scale.Min + (sc.Scale.Max - sc.Scale.Min) * rng.Float();
                    if (!area.Contains(x, z)) continue;
                    if (!Allowed(config, sc.Avoid, footprints, x, z)) continue;
                    if (sc.MinSpacing > 0f && TooClose(cells, cell, x, z, sc.MinSpacing)) continue;
                    string model = sc.Models[sc.Models.Count - 1].Id;
                    float acc = 0f;
                    foreach (var m in sc.Models) { acc += m.Weight; if (pick < acc) { model = m.Id; break; } }
                    var key = (Floor(x / cell), Floor(z / cell));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<(float, float)>();
                    list.Add((x, z));
                    result.Add(new ScatterPlacement
                    {
                        Id = $"{sc.Id}_{placed:000}", ScatterId = sc.Id, ModelId = model, X = x, Z = z, Yaw = yaw, Scale = scale, Collide = sc.Collide,
                    });
                    placed++;
                }
            }
            return result;
        }

        static int Floor(float v) => (int)Math.Floor(v);

        static bool TooClose(Dictionary<(int, int), List<(float X, float Z)>> cells, float cell, float x, float z, float spacing)
        {
            int cx = Floor(x / cell), cz = Floor(z / cell), r = (int)Math.Ceiling(spacing / cell);
            for (int i = cx - r; i <= cx + r; i++)
                for (int j = cz - r; j <= cz + r; j++)
                    if (cells.TryGetValue((i, j), out var list))
                        foreach (var q in list)
                            if ((q.X - x) * (q.X - x) + (q.Z - z) * (q.Z - z) < spacing * spacing) return true;
            return false;
        }

        static bool Allowed(SceneConfig config, AvoidConfig avoid, List<Footprint> footprints, float x, float z)
        {
            if (avoid.Paths.HasValue)
                foreach (var s in config.Paths) if (s.SignedDistance(x, z) < avoid.Paths.Value) return false;
            if (avoid.Water.HasValue)
                foreach (var s in config.Water) if (s.SignedDistance(x, z) < avoid.Water.Value) return false;
            foreach (var id in avoid.Zones)
                foreach (var zone in config.Zones) if (zone.Id == id && zone.SignedDistance(x, z) < avoid.ZoneMargin) return false;
            if (avoid.Objects.HasValue)
                foreach (var f in footprints) if (f.SignedDistance(x, z) < avoid.Objects.Value) return false;
            return true;
        }

        /// <summary>What every built object covers: a catalog model by its measured bounds, a primitive by its scale. Prefab paths are not known here.</summary>
        static List<Footprint> Footprints(SceneConfig config, ModelCatalog? catalog)
        {
            var list = new List<Footprint>();
            foreach (var o in config.Objects)
            {
                float yaw = o.Rotation.Y;
                if (!string.IsNullOrEmpty(o.Model) && catalog != null && catalog.Has(o.Model!))
                {
                    var b = catalog.Bounds(o.Model!);
                    double a = yaw * Math.PI / 180.0;
                    float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                    float lx = b.CenterX * o.Scale.X, lz = b.CenterZ * o.Scale.Z;
                    // Unity yaw turns the local centre offset: (x, z) → (x cos + z sin, −x sin + z cos).
                    list.Add(new Footprint(o.Position.X + lx * cos + lz * sin, o.Position.Z - lx * sin + lz * cos, b.SizeX * o.Scale.X / 2f, b.SizeZ * o.Scale.Z / 2f, yaw));
                }
                else if (!string.IsNullOrEmpty(o.Shape))
                    list.Add(new Footprint(o.Position.X, o.Position.Z, o.Scale.X / 2f, o.Scale.Z / 2f, yaw));
            }
            return list;
        }

        /// <summary>A short digest of a placement list — equal lists, equal text.</summary>
        public static string Digest(IEnumerable<ScatterPlacement> placements)
        {
            var sb = new StringBuilder();
            foreach (var p in placements)
                sb.Append(p.Id).Append('|').Append(p.ModelId).Append('|').Append(p.X.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
                  .Append(p.Z.ToString("0.###", CultureInfo.InvariantCulture)).Append('|').Append(p.Yaw.ToString("0.#", CultureInfo.InvariantCulture)).Append('|')
                  .Append(p.Scale.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            ulong h = 1469598103934665603UL; // FNV-1a
            foreach (char c in sb.ToString()) unchecked { h ^= c; h *= 1099511628211UL; }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
