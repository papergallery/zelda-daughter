#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>One terrain override: where <see cref="Area"/> covers a point, the ground there is <see cref="Terrain"/>.</summary>
    public sealed class TerrainLayer
    {
        public string Terrain { get; set; } = "";
        public Area Area { get; set; } = new Area();
    }

    /// <summary>
    /// Which terrain id (data/movement.json: ground, road, water, mud…) the hero stands on at (x, z) — D-10. Layers are checked
    /// from the last to the first, so a bridge zone listed after the river wins over the water under it. The speed multiplier
    /// itself stays in <c>SpeedModel</c> (terrain "water" = ×0.4).
    /// </summary>
    public sealed class TerrainMap
    {
        public string Default { get; set; } = "ground";
        public List<TerrainLayer> Layers { get; set; } = new List<TerrainLayer>();

        public string At(float x, float z)
        {
            for (int i = Layers.Count - 1; i >= 0; i--)
                if (Layers[i].Area.Contains(x, z)) return Layers[i].Terrain;
            return Default;
        }

        /// <summary>Ground terrain, then paths (default "road"), water (default "water"), then zones that name a terrain — in file order.</summary>
        public static TerrainMap From(SceneConfig config)
        {
            var map = new TerrainMap { Default = config.Ground.Terrain };
            foreach (var p in config.Paths) map.Layers.Add(new TerrainLayer { Terrain = string.IsNullOrEmpty(p.Terrain) ? "road" : p.Terrain!, Area = p });
            foreach (var w in config.Water) map.Layers.Add(new TerrainLayer { Terrain = string.IsNullOrEmpty(w.Terrain) ? "water" : w.Terrain!, Area = w });
            foreach (var z in config.Zones)
                if (!string.IsNullOrEmpty(z.Terrain)) map.Layers.Add(new TerrainLayer { Terrain = z.Terrain!, Area = z });
            return map;
        }

        public string ToJson() => JsonConvert.SerializeObject(this);

        public static TerrainMap FromJson(string json) => JsonConvert.DeserializeObject<TerrainMap>(json) ?? new TerrainMap();
    }
}
