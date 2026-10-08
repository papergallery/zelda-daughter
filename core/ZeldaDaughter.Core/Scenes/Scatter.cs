#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZeldaDaughter.Core.Common;

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
                var rng = new SplitMix64(sc.Seed);
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
                var f = Footprint.Of(o, catalog);
                if (f.HasValue) list.Add(f.Value);
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
