#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>A point on the ground plane (x, z metres).</summary>
    public struct Pt
    {
        public float X { get; set; }
        public float Z { get; set; }

        public Pt(float x, float z) { X = x; Z = z; }
        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}, {1}", X, Z);
    }

    /// <summary>
    /// A region on the ground (D-10): rect (center + size, optional rotation about the vertical), circle (center + radius) or
    /// strip (polyline + width — roads, rivers). Paths, water, zones and scatter areas all use it. Pure geometry, no Unity.
    /// </summary>
    public class Area
    {
        /// <summary>"rect" | "circle" | "strip"; empty = inferred from the fields given.</summary>
        public string Shape { get; set; } = "";
        /// <summary>Id of a path / water / zone whose geometry this area reuses (scatter areas).</summary>
        public string? Ref { get; set; }
        public Pt Center { get; set; }
        public Pt Size { get; set; }
        /// <summary>Degrees about the vertical (rect only).</summary>
        public float Rotation { get; set; }
        public float Radius { get; set; }
        public List<Pt> Points { get; set; } = new List<Pt>();
        public float Width { get; set; }

        public string Kind
        {
            get
            {
                if (!string.IsNullOrEmpty(Shape)) return Shape;
                if (Points.Count >= 1) return "strip";
                if (Radius > 0) return "circle";
                if (Size.X > 0 || Size.Z > 0) return "rect";
                return "";
            }
        }

        /// <summary>Distance to the region's edge: negative inside, 0 on the edge, positive outside.</summary>
        public float SignedDistance(float x, float z)
        {
            switch (Kind)
            {
                case "circle":
                    return (float)Math.Sqrt((x - Center.X) * (x - Center.X) + (z - Center.Z) * (z - Center.Z)) - Radius;
                case "strip":
                    return DistanceToPolyline(x, z) - Width / 2f;
                case "rect":
                    {
                        double a = Rotation * Math.PI / 180.0;
                        // Unity yaw: +angle turns clockwise seen from above; world → local is the inverse turn.
                        float dx = x - Center.X, dz = z - Center.Z;
                        float lx = (float)(dx * Math.Cos(a) - dz * Math.Sin(a));
                        float lz = (float)(dx * Math.Sin(a) + dz * Math.Cos(a));
                        float ex = Math.Abs(lx) - Size.X / 2f, ez = Math.Abs(lz) - Size.Z / 2f;
                        if (ex > 0 || ez > 0) return (float)Math.Sqrt(Math.Max(ex, 0f) * Math.Max(ex, 0f) + Math.Max(ez, 0f) * Math.Max(ez, 0f));
                        return Math.Max(ex, ez);
                    }
                default:
                    return float.PositiveInfinity;
            }
        }

        /// <summary>The region alone (no id, tags or terrain), for a scene object to carry.</summary>
        public string ToJson() => Newtonsoft.Json.JsonConvert.SerializeObject(this, typeof(Area), new Newtonsoft.Json.JsonSerializerSettings());

        public static Area FromJson(string json) => Newtonsoft.Json.JsonConvert.DeserializeObject<Area>(json) ?? new Area();

        public bool Contains(float x, float z) => SignedDistance(x, z) <= 0f;

        float DistanceToPolyline(float x, float z)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i + 1 < Points.Count; i++)
            {
                var a = Points[i];
                var b = Points[i + 1];
                float vx = b.X - a.X, vz = b.Z - a.Z;
                float len2 = vx * vx + vz * vz;
                float t = len2 <= 1e-9f ? 0f : Math.Max(0f, Math.Min(1f, ((x - a.X) * vx + (z - a.Z) * vz) / len2));
                float px = a.X + vx * t - x, pz = a.Z + vz * t - z;
                best = Math.Min(best, (float)Math.Sqrt(px * px + pz * pz));
            }
            return best;
        }

        /// <summary>Axis-aligned box around the region: (minX, minZ, maxX, maxZ).</summary>
        public (float MinX, float MinZ, float MaxX, float MaxZ) Bounds()
        {
            switch (Kind)
            {
                case "circle":
                    return (Center.X - Radius, Center.Z - Radius, Center.X + Radius, Center.Z + Radius);
                case "strip":
                    {
                        float h = Width / 2f, minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                        foreach (var p in Points)
                        {
                            minX = Math.Min(minX, p.X - h); maxX = Math.Max(maxX, p.X + h);
                            minZ = Math.Min(minZ, p.Z - h); maxZ = Math.Max(maxZ, p.Z + h);
                        }
                        return (minX, minZ, maxX, maxZ);
                    }
                case "rect":
                    {
                        double a = Rotation * Math.PI / 180.0;
                        float hx = Size.X / 2f, hz = Size.Z / 2f;
                        float ex = (float)(Math.Abs(Math.Cos(a)) * hx + Math.Abs(Math.Sin(a)) * hz);
                        float ez = (float)(Math.Abs(Math.Sin(a)) * hx + Math.Abs(Math.Cos(a)) * hz);
                        return (Center.X - ex, Center.Z - ez, Center.X + ex, Center.Z + ez);
                    }
                default:
                    return (0, 0, 0, 0);
            }
        }

        /// <summary>Area in m² (strip: length × width, ignoring overlaps at bends).</summary>
        public float SquareMetres()
        {
            switch (Kind)
            {
                case "circle": return (float)(Math.PI * Radius * Radius);
                case "rect": return Size.X * Size.Z;
                case "strip":
                    {
                        float len = 0;
                        for (int i = 0; i + 1 < Points.Count; i++)
                            len += (float)Math.Sqrt((Points[i + 1].X - Points[i].X) * (Points[i + 1].X - Points[i].X) + (Points[i + 1].Z - Points[i].Z) * (Points[i + 1].Z - Points[i].Z));
                        return len * Width;
                    }
                default: return 0f;
            }
        }

        /// <summary>Problems with this region's own fields ("" prefix is the caller's); empty when fine.</summary>
        public IEnumerable<string> Check()
        {
            switch (Kind)
            {
                case "circle": if (Radius <= 0) yield return "радиус > 0"; break;
                case "rect": if (Size.X <= 0 || Size.Z <= 0) yield return "size > 0 по обеим осям"; break;
                case "strip":
                    if (Points.Count < 2) yield return "полоса: минимум 2 точки";
                    if (Width <= 0) yield return "полоса: width > 0";
                    break;
                default: yield return string.IsNullOrEmpty(Shape) ? "не задана форма (rect / circle / strip)" : $"неизвестная форма '{Shape}'"; break;
            }
        }
    }
}
