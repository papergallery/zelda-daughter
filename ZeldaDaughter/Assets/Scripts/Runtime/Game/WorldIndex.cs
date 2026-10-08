using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Input;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Game
{
    /// <summary>A named point of the scene (anchor, bed, station, enemy spawn…); <c>Detail</c> — what kind (station id, enemy id).</summary>
    public readonly struct WorldPoint
    {
        public readonly string Id;
        public readonly Vector3 Position;
        public readonly string Detail;

        public WorldPoint(string id, Vector3 position, string detail = "") { Id = id; Position = position; Detail = detail; }
    }

    /// <summary>
    /// The scene's register (docs/demo/unity-architecture.md §1): objects by id (<see cref="SceneTags"/>), anchors, zones, grass cells,
    /// stations, beds, enemy spawn points, and everything tappable — including things created in play (<see cref="RegisterDynamic"/>).
    /// Built from what SceneBuilder serialised on the objects, lazily on first use, so the order of Awake between components never matters.
    /// <see cref="RegisterInto"/> gives the core what it must know about the scene before a save is loaded.
    /// </summary>
    public sealed class WorldIndex : MonoBehaviour
    {
        [SerializeField] private SceneTags[] _objects = new SceneTags[0];
        [SerializeField] private ZoneArea[] _zones = new ZoneArea[0];

        private bool _built;
        private readonly Dictionary<string, SceneTags> _byId = new Dictionary<string, SceneTags>(StringComparer.Ordinal);
        private readonly Dictionary<string, Tappable> _tappableById = new Dictionary<string, Tappable>(StringComparer.Ordinal);
        private readonly List<Tappable> _tappables = new List<Tappable>();
        private readonly Dictionary<string, Vector3> _anchors = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly List<WorldPoint> _grassCells = new List<WorldPoint>();
        private readonly List<WorldPoint> _stations = new List<WorldPoint>();
        private readonly List<WorldPoint> _beds = new List<WorldPoint>();
        private readonly List<WorldPoint> _enemySpawns = new List<WorldPoint>();
        private readonly List<WorldPoint> _predatorZones = new List<WorldPoint>();

        public void Configure(SceneTags[] objects, ZoneArea[] zones)
        {
            _objects = objects ?? new SceneTags[0];
            _zones = zones ?? new ZoneArea[0];
            _built = false;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            _byId.Clear(); _tappableById.Clear(); _tappables.Clear(); _anchors.Clear();
            _grassCells.Clear(); _stations.Clear(); _beds.Clear(); _enemySpawns.Clear(); _predatorZones.Clear();
            foreach (var o in _objects)
            {
                if (o == null) continue;
                _byId[o.Id] = o;
                var pos = o.transform.position;
                if (o.Has("anchor")) _anchors[o.Id] = pos;
                if (o.Has("grass_cell")) _grassCells.Add(new WorldPoint(o.Id, pos));
                if (o.Has("predator_zone")) _predatorZones.Add(new WorldPoint(o.Id, pos));
                if (o.Has("bed")) _beds.Add(new WorldPoint(o.Id, pos));
                if (o.Station != null) _stations.Add(new WorldPoint(o.Id, pos, o.Station));
                else if (o.Has("station")) _stations.Add(new WorldPoint(o.Id, pos, o.FirstTagExcept("station", "poi", "poi_side")));
                if (o.Enemy != null) _enemySpawns.Add(new WorldPoint(o.Id, pos, o.Enemy));
                else if (o.Has("enemy_spawn")) _enemySpawns.Add(new WorldPoint(o.Id, pos, o.TagWithPrefix("enemy_", "enemy_spawn")));
                var t = o.GetComponent<Tappable>();
                if (t != null) RegisterDynamic(t);
            }
        }

        /// <summary>Every object of the scene by its id (null when the id is unknown).</summary>
        public SceneTags Find(string id) { Build(); return id != null && _byId.TryGetValue(id, out var o) ? o : null; }
        public IReadOnlyCollection<SceneTags> Objects { get { Build(); return _byId.Values; } }

        public Tappable FindTappable(string id) { Build(); return id != null && _tappableById.TryGetValue(id, out var t) ? t : null; }
        public IReadOnlyList<Tappable> Tappables { get { Build(); return _tappables; } }

        public IReadOnlyDictionary<string, Vector3> Anchors { get { Build(); return _anchors; } }
        public bool TryGetAnchor(string id, out Vector3 position) { Build(); return _anchors.TryGetValue(id, out position); }
        public IReadOnlyList<ZoneArea> Zones => _zones;
        public IReadOnlyList<WorldPoint> GrassCells { get { Build(); return _grassCells; } }
        public IReadOnlyList<WorldPoint> Stations { get { Build(); return _stations; } }
        public IReadOnlyList<WorldPoint> Beds { get { Build(); return _beds; } }
        /// <summary>Enemy spawn points: <c>Detail</c> is the enemy id from data/enemies.json (config field <c>enemy</c>, or the tag <c>enemy_&lt;id&gt;</c> of older scenes).</summary>
        public IReadOnlyList<WorldPoint> EnemySpawns { get { Build(); return _enemySpawns; } }
        public IReadOnlyList<WorldPoint> PredatorZones { get { Build(); return _predatorZones; } }

        /// <summary>A tappable created in play (an enemy, a carcass, a placed item) joins the picker's targets. Same id replaces.</summary>
        public void RegisterDynamic(Tappable t)
        {
            if (t == null) return;
            Build();
            if (_tappableById.TryGetValue(t.Id, out var old)) _tappables.Remove(old);
            _tappableById[t.Id] = t;
            _tappables.Add(t);
        }

        public void UnregisterDynamic(Tappable t)
        {
            if (t == null) return;
            if (_tappableById.TryGetValue(t.Id, out var cur) && cur == t) _tappableById.Remove(t.Id);
            _tappables.Remove(t);
        }

        /// <summary>
        /// What the core must know about this scene, before a save is loaded: grass cells, mud ground (zones tagged <c>mud</c>, as circles),
        /// the centres of the night predators' zones (ids from data/night.json).
        /// </summary>
        public void RegisterInto(GameState g)
        {
            Build();
            foreach (var c in _grassCells) g.Nature.Grass.AddCell(c.Id, new Vec2(c.Position.x, c.Position.z));
            foreach (var z in _zones)
            {
                if (z == null || !z.Has("mud")) continue;
                var a = z.Area;
                float r = a.Radius > 0f ? a.Radius : Mathf.Max(a.Size.X, a.Size.Z) * 0.5f;
                g.Nature.Mud.AddZone(z.Id, new Vec2(a.Center.X, a.Center.Z), r);
            }
            foreach (var p in _predatorZones) g.Nature.Predators.AddZone(p.Id, new Vec2(p.Position.x, p.Position.z));
            ZdLog.Info("Index", $"registered grass={_grassCells.Count} predatorZones={_predatorZones.Count} zones={_zones.Length}");
        }
    }
}
