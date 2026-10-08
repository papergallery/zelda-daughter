#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.World
{
    public enum GrassState { Dry, Wet, Burning, Burnt }

    /// <summary>
    /// Grass cells (project-design.md §8): the scene registers a cell per patch of grass; fire goes from a burning cell to dry neighbours
    /// (within <c>neighborDistance</c>) faster downwind and slower upwind, burns <c>burnSeconds</c> and leaves burnt ground that never burns again;
    /// wet grass does not catch; a lit campfire's sparks can start a fire in dry cells next to it. One roll per step; each (cell, neighbour) pair
    /// derives its own from it (<see cref="Rolls"/>).
    /// </summary>
    public sealed class GrassField
    {
        sealed class Cell
        {
            public string Id = "";
            public Vec2 Position;
            public GrassState State;
            /// <summary>Burning: seconds burnt. Wet: seconds since the rain stopped.</summary>
            public float Timer;
            public readonly List<int> Neighbors = new List<int>();
        }

        readonly GrassSettings _s;
        readonly RainSettings _rain;
        readonly Wind _wind;
        readonly List<Cell> _cells = new List<Cell>();
        // scratch lists of Tick, reused so that a frame allocates nothing (C8)
        readonly List<int> _burnOut = new List<int>();
        readonly List<int> _catches = new List<int>();
        readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, (GrassState state, float timer)> _pending = new Dictionary<string, (GrassState, float)>(StringComparer.Ordinal);

        public GrassField(GrassSettings settings, RainSettings rain, Wind wind)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _rain = rain ?? throw new ArgumentNullException(nameof(rain));
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
        }

        public int Count => _cells.Count;
        public bool Has(string cellId) => _index.ContainsKey(cellId);
        public GrassState StateOf(string cellId) => _cells[Index(cellId)].State;
        public Vec2 PositionOf(string cellId) => _cells[Index(cellId)].Position;

        /// <summary>Registers a patch of grass (from the scene's objects). Cells within neighborDistance become neighbours; a saved state is applied.</summary>
        public void AddCell(string id, Vec2 position)
        {
            if (_index.ContainsKey(id)) return;
            var c = new Cell { Id = id, Position = position };
            if (_pending.TryGetValue(id, out var p)) { c.State = p.state; c.Timer = p.timer; }
            int n = _cells.Count;
            for (int i = 0; i < n; i++)
            {
                if ((_cells[i].Position - position).Length > _s.NeighborDistance) continue;
                _cells[i].Neighbors.Add(n);
                c.Neighbors.Add(i);
            }
            _cells.Add(c);
            _index[id] = n;
        }

        /// <summary>Sets a dry cell alight (a torch, lightning). False for wet, burning, burnt or unknown ground.</summary>
        public bool Ignite(string cellId)
        {
            if (!_index.TryGetValue(cellId, out int i) || _cells[i].State != GrassState.Dry) return false;
            Light(_cells[i]);
            return true;
        }

        static void Light(Cell c) { c.State = GrassState.Burning; c.Timer = 0f; }

        /// <summary>Chance per second that fire in <paramref name="fromId"/> catches <paramref name="toId"/> (for tests and the view's sparks).</summary>
        public float SpreadChancePerSecond(string fromId, string toId)
        {
            var a = _cells[Index(fromId)].Position;
            var b = _cells[Index(toId)].Position;
            var dir = (b - a).Normalized;
            float cos = dir.X * _wind.Direction.X + dir.Y * _wind.Direction.Y;
            float factor = Math.Max(_s.MinSpreadFactor, 1f + _s.WindGain * _wind.Strength * cos);
            return _s.SpreadPerSecond * factor;
        }

        /// <summary>Is any cell burning within <paramref name="radius"/> of the point.</summary>
        public bool BurningNear(Vec2 position, float radius)
        {
            foreach (var c in _cells) if (c.State == GrassState.Burning && (c.Position - position).Length <= radius) return true;
            return false;
        }

        internal void Tick(float dt, double roll, bool raining, float rainElapsed, IReadOnlyList<Campfire> fires, List<WorldEvent> events)
        {
            if (dt <= 0f) return;
            var burnOut = _burnOut; burnOut.Clear();
            var catches = _catches; catches.Clear();
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                if (c.State == GrassState.Burning)
                {
                    c.Timer += dt;
                    if (c.Timer >= _s.BurnSeconds) burnOut.Add(i);
                    foreach (int j in c.Neighbors)
                    {
                        if (_cells[j].State != GrassState.Dry) continue;
                        double p = 1.0 - Math.Exp(-SpreadChancePerSecond(c.Id, _cells[j].Id) * dt);
                        if (Rolls.At(roll, i, j) < p) catches.Add(j);
                    }
                }
                else if (c.State == GrassState.Wet)
                {
                    if (raining) c.Timer = 0f;
                    else { c.Timer += dt; }
                }
            }
            // sparks from lit campfires
            double sparkP = 1.0 - Math.Exp(-_s.CampfireSparkPerSecond * dt);
            for (int f = 0; f < fires.Count; f++)
            {
                if (!fires[f].IsLit) continue;
                for (int i = 0; i < _cells.Count; i++)
                    if (_cells[i].State == GrassState.Dry && (_cells[i].Position - fires[f].Position).Length <= _s.CampfireSparkRadius && Rolls.At(roll, 1000003 + i, f) < sparkP)
                        catches.Add(i);
            }
            for (int b = 0; b < burnOut.Count; b++)
            {
                int i = burnOut[b];
                _cells[i].State = GrassState.Burnt;
                events.Add(new WorldEvent(WorldEventKind.GrassBurnedOut, _cells[i].Id, _cells[i].Position));
            }
            catches.Sort();   // in cell order (events must be deterministic); duplicates are skipped below
            for (int k = 0; k < catches.Count; k++)
            {
                int j = catches[k];
                if (k > 0 && catches[k - 1] == j) continue;
                if (_cells[j].State != GrassState.Dry) continue;
                Light(_cells[j]);
                events.Add(new WorldEvent(WorldEventKind.GrassIgnited, _cells[j].Id, _cells[j].Position));
            }
            // rain: long enough — grass is wet and fires go out; after the rain it dries cell by cell
            if (raining && rainElapsed >= _rain.WetAfterSeconds)
            {
                foreach (var c in _cells)
                {
                    if (c.State == GrassState.Dry) { c.State = GrassState.Wet; c.Timer = 0f; events.Add(new WorldEvent(WorldEventKind.GrassWetted, c.Id, c.Position)); }
                    else if (c.State == GrassState.Burning) { c.State = GrassState.Burnt; events.Add(new WorldEvent(WorldEventKind.GrassExtinguished, c.Id, c.Position)); }
                }
            }
            else if (!raining)
            {
                foreach (var c in _cells)
                    if (c.State == GrassState.Wet && c.Timer >= _rain.DryAfterSeconds)
                    {
                        c.State = GrassState.Dry; c.Timer = 0f;
                        events.Add(new WorldEvent(WorldEventKind.GrassDried, c.Id, c.Position));
                    }
            }
        }

        /// <summary>Wets every dry cell (a scripted downpour, tests).</summary>
        public void Wet()
        {
            foreach (var c in _cells) if (c.State == GrassState.Dry) { c.State = GrassState.Wet; c.Timer = 0f; }
        }

        int Index(string id) => _index.TryGetValue(id, out int i) ? i : throw new ArgumentException($"no grass cell '{id}' — the scene must register it (AddCell)", nameof(id));

        internal IReadOnlyDictionary<string, (GrassState state, float timer)> State()
        {
            var d = new Dictionary<string, (GrassState, float)>(_pending, StringComparer.Ordinal);
            foreach (var c in _cells)
            {
                if (c.State == GrassState.Dry) d.Remove(c.Id);
                else d[c.Id] = (c.State, c.Timer);
            }
            return d;
        }

        internal void Restore(IReadOnlyDictionary<string, (GrassState state, float timer)>? saved)
        {
            _pending.Clear();
            if (saved != null) foreach (var kv in saved) _pending[kv.Key] = kv.Value;
            foreach (var c in _cells)
            {
                if (_pending.TryGetValue(c.Id, out var p)) { c.State = p.state; c.Timer = p.timer; }
                else { c.State = GrassState.Dry; c.Timer = 0f; }
            }
        }
    }
}
