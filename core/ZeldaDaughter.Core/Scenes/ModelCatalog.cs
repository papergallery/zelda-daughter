#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>Collider of a model in its own (metre, Y-up) frame. "box" and "capsule" without numbers fit the model's bounds.</summary>
    public sealed class ColliderDef
    {
        /// <summary>none | box | capsule | parts (composite: every part keeps its own collider).</summary>
        public string Kind { get; set; } = "box";
        public float Radius { get; set; }
        public float Height { get; set; }
        /// <summary>Box / capsule horizontal size factor against the bounds (rocks: a bit tighter than the mesh). 1 = the bounds.</summary>
        public float Shrink { get; set; } = 1f;
    }

    /// <summary>One piece of a composite model (a house = walls + roof).</summary>
    public sealed class ModelPart
    {
        public string Model { get; set; } = "";
        public Pt Offset { get; set; }
        public float Y { get; set; }
        /// <summary>Degrees about the vertical.</summary>
        public float Yaw { get; set; }
        /// <summary>"none" drops this part's collider (an open doorway).</summary>
        public string? Collider { get; set; }
    }

    public sealed class ModelDef
    {
        /// <summary>FBX under Assets/Art/Models; empty for a composite.</summary>
        public string Path { get; set; } = "";
        public List<ModelPart> Parts { get; set; } = new List<ModelPart>();
        public ColliderDef Collider { get; set; } = new ColliderDef();
        public List<string> Tags { get; set; } = new List<string>();
        public bool IsComposite => Parts.Count > 0;
    }

    /// <summary>Measured box of an imported model: centre and size in metres.</summary>
    public readonly struct Box3
    {
        public readonly float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public Box3(float minX, float minY, float minZ, float maxX, float maxY, float maxZ) { MinX = minX; MinY = minY; MinZ = minZ; MaxX = maxX; MaxY = maxY; MaxZ = maxZ; }
        public float SizeX => MaxX - MinX;
        public float SizeY => MaxY - MinY;
        public float SizeZ => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) / 2f;
        public float CenterY => (MinY + MaxY) / 2f;
        public float CenterZ => (MinZ + MaxZ) / 2f;
    }

    /// <summary>
    /// data/models.json (id → FBX or composite, collider, tags) + data/model-bounds.json (measured in Unity, D-10). Scenes name models
    /// by id; the core answers footprints for scatter avoidance and validation without Unity.
    /// </summary>
    public sealed class ModelCatalog
    {
        public Dictionary<string, ModelDef> Models { get; set; } = new Dictionary<string, ModelDef>();
        readonly Dictionary<string, Box3> _bounds = new Dictionary<string, Box3>();
        readonly Dictionary<string, Box3> _cache = new Dictionary<string, Box3>();

        sealed class BoundsFile { public Dictionary<string, BoundsEntry> Bounds { get; set; } = new Dictionary<string, BoundsEntry>(); }
        sealed class BoundsEntry { public float[] Center { get; set; } = new float[3]; public float[] Size { get; set; } = new float[3]; }

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore };

        public static ModelCatalog Parse(string modelsJson, string boundsJson)
        {
            var c = JsonConvert.DeserializeObject<ModelCatalog>(modelsJson, Json) ?? new ModelCatalog();
            var b = JsonConvert.DeserializeObject<BoundsFile>(boundsJson, Json) ?? new BoundsFile();
            foreach (var kv in b.Bounds)
            {
                var e = kv.Value;
                c._bounds[kv.Key] = new Box3(e.Center[0] - e.Size[0] / 2, e.Center[1] - e.Size[1] / 2, e.Center[2] - e.Size[2] / 2,
                    e.Center[0] + e.Size[0] / 2, e.Center[1] + e.Size[1] / 2, e.Center[2] + e.Size[2] / 2);
            }
            return c;
        }

        public static ModelCatalog Load(string dataRoot) =>
            Parse(File.ReadAllText(Path.Combine(dataRoot, "models.json")), File.ReadAllText(Path.Combine(dataRoot, "model-bounds.json")));

        public bool Has(string id) => Models.ContainsKey(id);

        public ModelDef Get(string id) => Models.TryGetValue(id, out var m) ? m : throw new ArgumentException($"Unknown model '{id}' — add it to data/models.json", nameof(id));

        /// <summary>Box around the model at scale 1 in its own frame (composite: the union of its parts).</summary>
        public Box3 Bounds(string id) => BoundsOf(id, 0);

        Box3 BoundsOf(string id, int depth)
        {
            if (_cache.TryGetValue(id, out var cached)) return cached;
            if (depth > 4) throw new InvalidOperationException($"Model '{id}': composite nesting too deep (cycle?)");
            var m = Get(id);
            Box3 box;
            if (!m.IsComposite)
            {
                if (!_bounds.TryGetValue(m.Path, out box)) throw new InvalidOperationException($"Model '{id}': no measured bounds for {m.Path} — run Zelda → Models → Measure bounds");
            }
            else
            {
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                foreach (var p in m.Parts)
                {
                    var b = BoundsOf(p.Model, depth + 1);
                    double a = p.Yaw * Math.PI / 180.0;
                    float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                    foreach (float x in new[] { b.MinX, b.MaxX })
                        foreach (float z in new[] { b.MinZ, b.MaxZ })
                        {
                            // Unity yaw: (x, z) → (x cos + z sin, −x sin + z cos).
                            float wx = x * cos + z * sin + p.Offset.X, wz = -x * sin + z * cos + p.Offset.Z;
                            minX = Math.Min(minX, wx); maxX = Math.Max(maxX, wx);
                            minZ = Math.Min(minZ, wz); maxZ = Math.Max(maxZ, wz);
                        }
                    minY = Math.Min(minY, b.MinY + p.Y); maxY = Math.Max(maxY, b.MaxY + p.Y);
                }
                box = new Box3(minX, minY, minZ, maxX, maxY, maxZ);
            }
            _cache[id] = box;
            return box;
        }

        /// <summary>Problems as "models: id: what"; empty when every model resolves and has bounds.</summary>
        public IReadOnlyList<string> Validate(Func<string, bool>? fileExists = null)
        {
            var p = new List<string>();
            var kinds = new HashSet<string> { "none", "box", "capsule", "parts" };
            foreach (var kv in Models)
            {
                var id = kv.Key;
                var m = kv.Value;
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z][a-z0-9_]*$")) p.Add($"models: '{id}' — id только [a-z0-9_] с буквы");
                if (string.IsNullOrEmpty(m.Path) == !m.IsComposite) p.Add($"models: '{id}' — нужен ровно один из path / parts");
                if (!kinds.Contains(m.Collider.Kind)) p.Add($"models: '{id}' — неизвестный collider '{m.Collider.Kind}'");
                if (m.Collider.Kind == "parts" && !m.IsComposite) p.Add($"models: '{id}' — collider parts только у составной модели");
                if (m.Collider.Shrink <= 0 || m.Collider.Shrink > 1) p.Add($"models: '{id}' — shrink в (0; 1]");
                if (!m.IsComposite && !string.IsNullOrEmpty(m.Path))
                {
                    if (!_bounds.ContainsKey(m.Path)) p.Add($"models: '{id}' — нет замеренных границ для {m.Path}");
                    if (fileExists != null && !fileExists(m.Path)) p.Add($"models: '{id}' — нет файла {m.Path}");
                }
                foreach (var part in m.Parts)
                    if (!Models.ContainsKey(part.Model)) p.Add($"models: '{id}' — часть '{part.Model}' не из каталога");
            }
            foreach (var id in Models.Keys)
            {
                try { if (p.Count == 0) Bounds(id); }
                catch (Exception e) { p.Add($"models: '{id}' — {e.Message}"); }
            }
            return p;
        }
    }
}
