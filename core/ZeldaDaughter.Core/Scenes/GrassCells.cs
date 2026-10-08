#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>One cell of dry grass for the fire (<c>GrassField.AddCell(Id, Position)</c>).</summary>
    public readonly struct GrassCell
    {
        public readonly string Id;
        /// <summary>Ground position (x, z).</summary>
        public readonly Vec2 Position;

        public GrassCell(string id, Vec2 position) { Id = id; Position = position; }
        public override string ToString() => $"{Id} {Position}";
    }

    /// <summary>A zone of dry grass in a scene becomes a regular grid of cells (C7, §8): the fire needs cells within <c>neighborDistance</c> of each other.</summary>
    public static class GrassCells
    {
        /// <summary>
        /// Cells every <paramref name="spacing"/> metres inside <paramref name="area"/>, row by row (z, then x) from the area's bounds corner,
        /// the first row and column half a step in. Ids: <c>prefix_000</c>, <c>prefix_001</c>… Same area, same list. A zone smaller than one step
        /// still gets one cell, in its middle. Use <c>data/elements.json grass.cellSpacing</c> for the spacing.
        /// </summary>
        public static IReadOnlyList<GrassCell> Grid(Area area, float spacing, string prefix)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (spacing <= 0f || float.IsNaN(spacing)) throw new ArgumentOutOfRangeException(nameof(spacing), "spacing must be positive");
            var cells = new List<GrassCell>();
            var (minX, minZ, maxX, maxZ) = area.Bounds();
            for (int j = 0; minZ + spacing / 2f + j * spacing <= maxZ + 1e-4f; j++)
                for (int i = 0; minX + spacing / 2f + i * spacing <= maxX + 1e-4f; i++)
                {
                    float x = minX + spacing / 2f + i * spacing, z = minZ + spacing / 2f + j * spacing;
                    if (area.Contains(x, z)) cells.Add(new GrassCell(Name(prefix, cells.Count), new Vec2(x, z)));
                }
            if (cells.Count == 0)
            {
                float cx = (minX + maxX) / 2f, cz = (minZ + maxZ) / 2f;
                if (area.Contains(cx, cz)) cells.Add(new GrassCell(Name(prefix, 0), new Vec2(cx, cz)));
            }
            return cells;
        }

        static string Name(string prefix, int i) => prefix + "_" + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
