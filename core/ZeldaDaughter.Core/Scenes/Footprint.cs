#nullable enable
using System;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>Oriented rectangle on the ground that a built thing covers (its model's measured bounds, or a primitive's scale).</summary>
    internal readonly struct Footprint
    {
        public readonly float Cx, Cz, Hx, Hz, Cos, Sin;

        public Footprint(float cx, float cz, float hx, float hz, float yawDegrees)
        {
            Cx = cx; Cz = cz; Hx = hx; Hz = hz;
            double a = yawDegrees * Math.PI / 180.0;
            Cos = (float)Math.Cos(a); Sin = (float)Math.Sin(a);
        }

        /// <summary>Distance to the edge: negative inside, 0 on the edge, positive outside.</summary>
        public float SignedDistance(float x, float z)
        {
            float dx = x - Cx, dz = z - Cz;
            float lx = dx * Cos - dz * Sin, lz = dx * Sin + dz * Cos;
            float ex = Math.Abs(lx) - Hx, ez = Math.Abs(lz) - Hz;
            if (ex > 0 || ez > 0) return (float)Math.Sqrt(Math.Max(ex, 0f) * Math.Max(ex, 0f) + Math.Max(ez, 0f) * Math.Max(ez, 0f));
            return Math.Max(ex, ez);
        }

        /// <summary>The footprint of a catalog model (by its measured bounds) or of a primitive (by its scale); null for markers, prefabs and the rest.</summary>
        public static Footprint? Of(ObjectConfig o, ModelCatalog? catalog)
        {
            float yaw = o.Rotation.Y;
            if (!string.IsNullOrEmpty(o.Model) && catalog != null && catalog.Has(o.Model!))
            {
                var b = catalog.Bounds(o.Model!);
                double a = yaw * Math.PI / 180.0;
                float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                float lx = b.CenterX * o.Scale.X, lz = b.CenterZ * o.Scale.Z;
                // Unity yaw turns the local centre offset: (x, z) → (x cos + z sin, −x sin + z cos).
                return new Footprint(o.Position.X + lx * cos + lz * sin, o.Position.Z - lx * sin + lz * cos, b.SizeX * o.Scale.X / 2f, b.SizeZ * o.Scale.Z / 2f, yaw);
            }
            if (!string.IsNullOrEmpty(o.Shape) && o.Shape != "empty")
                return new Footprint(o.Position.X, o.Position.Z, o.Scale.X / 2f, o.Scale.Z / 2f, yaw);
            return null;
        }
    }
}
