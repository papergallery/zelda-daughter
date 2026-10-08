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

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore };
        static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        static readonly Regex ColorPattern = new Regex("^#[0-9a-fA-F]{6}$");
        static readonly HashSet<string> Shapes = new HashSet<string> { "cube", "sphere", "capsule", "cylinder", "plane", "quad" };

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
        public IReadOnlyList<string> Validate(Func<string, bool>? prefabExists = null)
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
                Thing(p, o.Id, o.Shape, o.Prefab, prefabExists);
                Color(p, o.Id, o.Color);
                if (o.Scale.X <= 0 || o.Scale.Y <= 0 || o.Scale.Z <= 0) p.Add($"{Name}: '{o.Id}' — масштаб > 0");
            }
            return p;
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
    }
}
