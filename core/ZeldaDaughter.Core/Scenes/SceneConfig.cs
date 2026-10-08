#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>
    /// scenes/*.json — what one editor builder turns into a Unity scene (T-04, docs/scene-config.md).
    /// Object format follows the April region config (id, prefab, position, rotation, scale, tags) plus primitives.
    /// </summary>
    public sealed class SceneConfig
    {
        public string Name { get; set; } = "";
        public GroundConfig Ground { get; set; } = new GroundConfig();
        public LightConfig Light { get; set; } = new LightConfig();
        public AmbientConfig Ambient { get; set; } = new AmbientConfig();
        public CameraConfig Camera { get; set; } = new CameraConfig();
        public HeroConfig Hero { get; set; } = new HeroConfig();
        public BuildConfig Build { get; set; } = new BuildConfig();
        public SaveConfig Save { get; set; } = new SaveConfig();
        public List<ObjectConfig> Objects { get; set; } = new List<ObjectConfig>();
        /// <summary>Roads and tracks: ribbons on the ground, terrain "road" by default (D-10).</summary>
        public List<StripConfig> Paths { get; set; } = new List<StripConfig>();
        /// <summary>
        /// Invisible footpaths for the residents (C7): strips like <see cref="Paths"/> but nothing is drawn and the terrain underfoot is not changed;
        /// <see cref="RouteGraph"/> walks along them and the paths together (a shortcut between a door and a road, a lane across a field).
        /// </summary>
        public List<StripConfig> Walkways { get; set; } = new List<StripConfig>();
        /// <summary>Rivers: ribbons of water, terrain "water" (the hero is slowed) unless a zone with another terrain (a bridge) covers the spot.</summary>
        public List<StripConfig> Water { get; set; } = new List<StripConfig>();
        /// <summary>Named regions with tags (predator spawn, wet grass…) and an optional terrain override (mud, bridge deck = road).</summary>
        public List<ZoneConfig> Zones { get; set; } = new List<ZoneConfig>();
        /// <summary>Decor scattered over regions, deterministic from the seed.</summary>
        public List<ScatterConfig> Scatter { get; set; } = new List<ScatterConfig>();

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore };
        static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        static readonly Regex ColorPattern = new Regex("^#[0-9a-fA-F]{6}$");
        static readonly HashSet<string> Shapes = new HashSet<string> { "cube", "sphere", "capsule", "cylinder", "plane", "quad", "empty" };

        public static SceneConfig Parse(string json) => JsonConvert.DeserializeObject<SceneConfig>(json, Json) ?? new SceneConfig();

        /// <summary>
        /// Scene names for the player build, in build order (first = the one the game starts in): included scenes sorted by
        /// <c>build.order</c>. Throws when two included scenes share an order — the order must be explicit.
        /// </summary>
        public static IReadOnlyList<string> BuildList(IEnumerable<SceneConfig> configs)
        {
            var included = new List<SceneConfig>();
            foreach (var c in configs) if (c.Build.Include) included.Add(c);
            included.Sort((a, b) => a.Build.Order != b.Build.Order ? a.Build.Order.CompareTo(b.Build.Order) : string.CompareOrdinal(a.Name, b.Name));
            for (int i = 1; i < included.Count; i++)
                if (included[i].Build.Order == included[i - 1].Build.Order)
                    throw new InvalidOperationException($"scenes {included[i - 1].Name} and {included[i].Name} have the same build.order {included[i].Build.Order}");
            var names = new List<string>();
            foreach (var c in included) names.Add(c.Name);
            return names;
        }

        /// <summary>Problems as "scene: id: what"; empty when the scene can be built.</summary>
        /// <param name="enemyDefs">Enemy ids from enemies.json: when given, <c>objects[].enemy</c> must be one of them.</param>
        /// <param name="stations">Station ids from recipes.json: when given, <c>objects[].station</c> must be one of them.</param>
        public IReadOnlyList<string> Validate(Func<string, bool>? prefabExists = null, ModelCatalog? catalog = null, ICollection<string>? terrains = null,
            ICollection<string>? enemyDefs = null, ICollection<string>? stations = null)
        {
            var p = new List<string>();
            if (!IdPattern.IsMatch(Name.Replace('-', '_'))) p.Add($"{Name}: name — [a-z0-9_-] с буквы");
            if (Ground.SizeX <= 0 || Ground.SizeZ <= 0) p.Add($"{Name}: ground — размер должен быть положительным");
            Color(p, "ground", Ground.Color);
            Color(p, "light", Light.Color);
            Color(p, "ambient", Ambient.Color);
            if (Camera.Size <= 0 && Camera.Orthographic) p.Add($"{Name}: camera — size > 0");
            if (Camera.Distance <= 0) p.Add($"{Name}: camera — distance > 0");
            if (Camera.Pitch <= 0 || Camera.Pitch >= 90) p.Add($"{Name}: camera — pitch в (0; 90)");
            Inside(p, "hero", Hero.Spawn);
            Thing(p, "hero", Hero.Shape, Hero.Prefab, prefabExists);
            var ids = new HashSet<string>();
            foreach (var o in Objects)
            {
                if (!IdPattern.IsMatch(o.Id)) p.Add($"{Name}: '{o.Id}' — id только [a-z0-9_] с буквы");
                if (!ids.Add(o.Id)) p.Add($"{Name}: '{o.Id}' — повтор id");
                Inside(p, o.Id, o.Position);
                if (!string.IsNullOrEmpty(o.Model))
                {
                    if (!string.IsNullOrEmpty(o.Shape) || !string.IsNullOrEmpty(o.Prefab)) p.Add($"{Name}: '{o.Id}' — model нельзя вместе с shape / prefab");
                    if (catalog != null && !catalog.Has(o.Model!)) p.Add($"{Name}: '{o.Id}' — модели '{o.Model}' нет в data/models.json");
                }
                else if (o.Marker)
                {
                    if (!string.IsNullOrEmpty(o.Shape) || !string.IsNullOrEmpty(o.Prefab)) p.Add($"{Name}: '{o.Id}' — marker нельзя вместе с shape / prefab");
                }
                else Thing(p, o.Id, o.Shape, o.Prefab, prefabExists);
                Color(p, o.Id, o.Color);
                if (o.Enemy != null && enemyDefs != null && !enemyDefs.Contains(o.Enemy)) p.Add($"{Name}: '{o.Id}' — враг '{o.Enemy}' нет в data/enemies.json");
                if (o.Station != null && stations != null && !stations.Contains(o.Station)) p.Add($"{Name}: '{o.Id}' — станка '{o.Station}' нет в data/recipes.json");
                if (o.Scale.X <= 0 || o.Scale.Y <= 0 || o.Scale.Z <= 0) p.Add($"{Name}: '{o.Id}' — масштаб > 0");
            }
            ValidateLayout(p, catalog, terrains);
            return p;
        }

        void ValidateLayout(List<string> p, ModelCatalog? catalog, ICollection<string>? terrains)
        {
            var areaIds = new HashSet<string>();
            void Id(string id)
            {
                if (!IdPattern.IsMatch(id)) p.Add($"{Name}: '{id}' — id только [a-z0-9_] с буквы");
                else if (!areaIds.Add(id)) p.Add($"{Name}: '{id}' — повтор id среди дорог, воды и зон");
            }
            void Terrain(string id, string? t)
            {
                if (!string.IsNullOrEmpty(t) && terrains != null && !terrains.Contains(t!)) p.Add($"{Name}: '{id}' — террейна '{t}' нет в data/movement.json");
            }
            foreach (var s in Paths)
            {
                Id(s.Id);
                if (s.Kind != "strip") p.Add($"{Name}: '{s.Id}' — путь задаётся points + width");
                foreach (var e in s.Check()) p.Add($"{Name}: '{s.Id}' — {e}");
                Color(p, s.Id, s.Color);
                Terrain(s.Id, s.Terrain);
            }
            foreach (var s in Walkways)
            {
                Id(s.Id);
                if (s.Kind != "strip") p.Add($"{Name}: '{s.Id}' — тропа для жителей задаётся points + width");
                foreach (var e in s.Check()) p.Add($"{Name}: '{s.Id}' — {e}");
            }
            foreach (var s in Water)
            {
                Id(s.Id);
                if (s.Kind != "strip") p.Add($"{Name}: '{s.Id}' — вода задаётся points + width");
                foreach (var e in s.Check()) p.Add($"{Name}: '{s.Id}' — {e}");
                Color(p, s.Id, s.Color);
                Terrain(s.Id, s.Terrain);
            }
            foreach (var z in Zones)
            {
                Id(z.Id);
                foreach (var e in z.Check()) p.Add($"{Name}: '{z.Id}' — {e}");
                Terrain(z.Id, z.Terrain);
            }
            var scatterIds = new HashSet<string>();
            foreach (var sc in Scatter)
            {
                if (!IdPattern.IsMatch(sc.Id)) p.Add($"{Name}: '{sc.Id}' — id только [a-z0-9_] с буквы");
                else if (!scatterIds.Add(sc.Id)) p.Add($"{Name}: '{sc.Id}' — повтор id россыпи");
                if (!string.IsNullOrEmpty(sc.Area.Ref))
                {
                    if (ResolveArea(sc.Area) == null) p.Add($"{Name}: '{sc.Id}' — area.ref '{sc.Area.Ref}' не найден");
                }
                else foreach (var e in sc.Area.Check()) p.Add($"{Name}: '{sc.Id}' — {e}");
                if (sc.Models.Count == 0) p.Add($"{Name}: '{sc.Id}' — пустой список моделей");
                foreach (var m in sc.Models)
                {
                    if (m.Weight <= 0) p.Add($"{Name}: '{sc.Id}' — вес модели '{m.Id}' > 0");
                    if (catalog != null && !catalog.Has(m.Id)) p.Add($"{Name}: '{sc.Id}' — модели '{m.Id}' нет в data/models.json");
                }
                if (sc.Density <= 0) p.Add($"{Name}: '{sc.Id}' — density > 0 (штук на 100 м²)");
                if (sc.Scale.Min <= 0 || sc.Scale.Max < sc.Scale.Min) p.Add($"{Name}: '{sc.Id}' — scale: 0 < min ≤ max");
                if (sc.MaxCount <= 0) p.Add($"{Name}: '{sc.Id}' — maxCount > 0");
                foreach (var zid in sc.Avoid.Zones)
                    if (!areaIds.Contains(zid)) p.Add($"{Name}: '{sc.Id}' — avoid.zones: '{zid}' не найден");
            }
        }

        /// <summary>The geometry a scatter area stands for: itself, or the path / water / zone it references (null when the reference is dangling).</summary>
        public Area? ResolveArea(Area a)
        {
            if (string.IsNullOrEmpty(a.Ref)) return a;
            foreach (var s in Paths) if (s.Id == a.Ref) return s;
            foreach (var s in Water) if (s.Id == a.Ref) return s;
            foreach (var z in Zones) if (z.Id == a.Ref) return z;
            return null;
        }

        void Inside(List<string> p, string who, V3 pos)
        {
            if (Math.Abs(pos.X) > Ground.SizeX / 2 || Math.Abs(pos.Z) > Ground.SizeZ / 2)
                p.Add($"{Name}: '{who}' — за краем земли ({pos})");
        }

        void Thing(List<string> p, string who, string? shape, string? prefab, Func<string, bool>? prefabExists)
        {
            bool hasShape = !string.IsNullOrEmpty(shape), hasPrefab = !string.IsNullOrEmpty(prefab);
            if (hasShape == hasPrefab) p.Add($"{Name}: '{who}' — нужен ровно один из shape / prefab");
            if (hasShape && !Shapes.Contains(shape!)) p.Add($"{Name}: '{who}' — неизвестная форма '{shape}'");
            if (hasPrefab && prefabExists != null && !prefabExists(prefab!)) p.Add($"{Name}: '{who}' — нет префаба {prefab}");
        }

        void Color(List<string> p, string who, string? c)
        {
            if (!string.IsNullOrEmpty(c) && !ColorPattern.IsMatch(c)) p.Add($"{Name}: '{who}' — цвет '{c}' не #rrggbb");
        }
    }

    /// <summary>Does the scene go into the player build, and where in the order (0 = the first scene the game opens).</summary>
    public sealed class BuildConfig
    {
        public bool Include { get; set; } = true;
        public int Order { get; set; } = 1000;
    }

    /// <summary>Where this scene saves. An empty slot means saving is off (test scenes) — they never touch the player's slot.</summary>
    public sealed class SaveConfig
    {
        public string? Slot { get; set; }
    }

    public struct V3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", X, Y, Z);
    }

    public sealed class GroundConfig
    {
        public float SizeX { get; set; }
        public float SizeZ { get; set; }
        public string Color { get; set; } = "#808080";
        /// <summary>Terrain id from data/movement.json (speed multiplier).</summary>
        public string Terrain { get; set; } = "ground";
    }

    public sealed class LightConfig
    {
        public V3 Rotation { get; set; } = new V3(50, -30, 0);
        public string Color { get; set; } = "#ffffff";
        public float Intensity { get; set; } = 1f;
        /// <summary>none | hard | soft.</summary>
        public string Shadows { get; set; } = "hard";
    }

    public sealed class AmbientConfig
    {
        public string Color { get; set; } = "#808080";
    }

    public sealed class CameraConfig
    {
        public bool Orthographic { get; set; } = true;
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Distance { get; set; }
        public float Size { get; set; }
        public float FieldOfView { get; set; } = 30f;
        public float FollowSmoothTime { get; set; }
    }

    public sealed class HeroConfig
    {
        public V3 Spawn { get; set; }
        public string? Shape { get; set; }
        public string? Prefab { get; set; }
        public string Color { get; set; } = "#c0a070";
    }

    public sealed class ObjectConfig
    {
        public string Id { get; set; } = "";
        public string? Shape { get; set; }
        public string? Prefab { get; set; }
        public V3 Position { get; set; }
        public V3 Rotation { get; set; }
        public V3 Scale { get; set; } = new V3(1, 1, 1);
        public string? Color { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        /// <summary>Item id from data/items.json the hero picks up by tapping this object.</summary>
        public string? Item { get; set; }
        /// <summary>Model id from data/models.json (instead of shape / prefab).</summary>
        public string? Model { get; set; }
        /// <summary>Force the model's collider on / off; null = whatever the catalog says.</summary>
        public bool? Collide { get; set; }
        /// <summary>Enemy id from data/enemies.json that lives here: the object is its spawn point (D-13); the game creates the enemy unless it was killed.</summary>
        public string? Enemy { get; set; }
        /// <summary>Station id from data/recipes.json (anvil, smelter…): a tap opens the crafting window for it (D-15).</summary>
        public string? Station { get; set; }
        /// <summary>An invisible point without a model: anchors of NPC schedules, spawn points, predator zones (D-10). Only an id, a place and tags.</summary>
        public bool Marker { get; set; }
    }

    /// <summary>A road or a river: a ribbon along <c>points</c>, <c>width</c> metres wide.</summary>
    public sealed class StripConfig : Area
    {
        public string Id { get; set; } = "";
        public string? Color { get; set; }
        /// <summary>Terrain id for the speed rule; empty = "road" for paths, "water" for water.</summary>
        public string? Terrain { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class ZoneConfig : Area
    {
        public string Id { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        /// <summary>Terrain override inside the zone (a bridge deck = "road", mud…); empty = geometry only.</summary>
        public string? Terrain { get; set; }
    }

    public sealed class ScatterModel
    {
        public string Id { get; set; } = "";
        public float Weight { get; set; } = 1f;
    }

    public sealed class ScaleRange
    {
        public float Min { get; set; } = 1f;
        public float Max { get; set; } = 1f;
    }

    /// <summary>How far scattered things keep from other things (metres beyond their edges); null = don't care.</summary>
    public sealed class AvoidConfig
    {
        public float? Paths { get; set; }
        public float? Water { get; set; }
        public float? Objects { get; set; }
        public List<string> Zones { get; set; } = new List<string>();
        public float ZoneMargin { get; set; }
    }

    /// <summary>Decor over a region: <c>density</c> items per 100 m², models by weight, positions from <c>seed</c> only.</summary>
    public sealed class ScatterConfig
    {
        public string Id { get; set; } = "";
        public Area Area { get; set; } = new Area();
        public List<ScatterModel> Models { get; set; } = new List<ScatterModel>();
        public float Density { get; set; }
        public int Seed { get; set; }
        public ScaleRange Scale { get; set; } = new ScaleRange();
        public AvoidConfig Avoid { get; set; } = new AvoidConfig();
        /// <summary>Minimum distance between two scattered items of this entry.</summary>
        public float MinSpacing { get; set; }
        /// <summary>Give the items their catalog colliders (trees, rocks); grass and flowers stay walk-through.</summary>
        public bool Collide { get; set; }
        public bool RandomYaw { get; set; } = true;
        public int MaxCount { get; set; } = 3000;
    }
}
