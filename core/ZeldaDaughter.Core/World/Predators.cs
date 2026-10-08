#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.World
{
    /// <summary>A circle of the scene where night predators never appear: the town (the object <see cref="Anchor"/> + radius).</summary>
    public sealed class SafeArea
    {
        /// <summary>Id of a scene object (scenes/region.json) that marks the centre.</summary>
        public string Anchor { get; set; } = "";
        public float Radius { get; set; }
    }

    /// <summary>The rectangle of the ground where a predator may be called (the scene's ground less a margin).</summary>
    public sealed class SpawnBounds
    {
        public float MinX { get; set; }
        public float MaxX { get; set; }
        public float MinZ { get; set; }
        public float MaxZ { get; set; }
        public bool Contains(Vec2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinZ && p.Y <= MaxZ;
    }

    /// <summary>data/night.json (D-06, D-23).</summary>
    public sealed class NightSettings
    {
        /// <summary>Enemy id from enemies.json that comes out at night.</summary>
        public string Enemy { get; set; } = "";
        public int MaxAtNight { get; set; }
        /// <summary>Daylight (0..1) below which predators appear; the count grows to <see cref="MaxAtNight"/> as it gets darker.</summary>
        public float DaylightBelow { get; set; }
        public float SpawnIntervalSeconds { get; set; }
        /// <summary>A new predator appears on a ring around the hero: not nearer than this …</summary>
        public float MinHeroDistance { get; set; }
        /// <summary>… and not farther than this (D-23: «20–35 м», not 130).</summary>
        public float MaxHeroDistance { get; set; }
        /// <summary>By day a predator farther from the hero than this simply goes; a nearer one walks away first.</summary>
        public float DespawnDistance { get; set; }
        /// <summary>No predator is called inside these circles (the town).</summary>
        public List<SafeArea> SafeAreas { get; set; } = new List<SafeArea>();
        public SpawnBounds Bounds { get; set; } = new SpawnBounds { MinX = -1e6f, MaxX = 1e6f, MinZ = -1e6f, MaxZ = 1e6f };
    }

    /// <summary>
    /// Night predators (§2 «ночью опаснее»): how many wolves the dark calls for, where they may appear (a ring of 20–35 m around the hero, not
    /// in the town, not in the light of a fire, not where the view forbids) and when they go. The core says «spawn wolf night_wolf_3 here» —
    /// the view creates the <c>Enemy</c> — and in the morning «send it away» (it walks off; once far it is removed).
    /// </summary>
    public sealed class Predators
    {
        readonly NightSettings _s;
        readonly List<(Vec2 center, float radius)> _safe = new List<(Vec2, float)>();
        readonly Dictionary<string, Vec2> _alive = new Dictionary<string, Vec2>(StringComparer.Ordinal);
        readonly List<string> _aliveOrder = new List<string>();
        readonly List<string> _dismissed = new List<string>();
        float _timer;
        int _counter;

        public Predators(NightSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        /// <summary>The view tells where a living predator is now (for the morning despawn). Default: where it spawned.</summary>
        public Func<string, Vec2?>? Locate { get; set; }
        /// <summary>The view's verdict on a spot (a wall, deep water): true — nobody may be called there.</summary>
        public Func<Vec2, bool>? Forbidden { get; set; }
        public int AliveCount => _alive.Count;
        public IReadOnlyList<string> Alive => _aliveOrder;
        internal int Counter => _counter;
        internal float Timer => _timer;

        /// <summary>Registers a safe circle from the scene (the position of the object named in <see cref="SafeArea.Anchor"/>).</summary>
        public void AddSafe(string anchor, Vec2 center)
        {
            foreach (var a in _s.SafeAreas)
                if (a.Anchor == anchor) _safe.Add((center, a.Radius));
        }

        public bool IsSafe(Vec2 p)
        {
            for (int i = 0; i < _safe.Count; i++)
                if ((p - _safe[i].center).Length <= _safe[i].radius) return true;
            return false;
        }

        public int TargetCount(float daylight)
        {
            if (daylight >= _s.DaylightBelow) return 0;
            return (int)Math.Min(_s.MaxAtNight, Math.Ceiling(_s.MaxAtNight * (1.0 - Math.Max(0f, daylight) / _s.DaylightBelow) - 1e-9));
        }

        /// <summary>
        /// Where the predator would be called for these three numbers (0..1): a point on the ring between the minimum and maximum
        /// distance from the hero (uniform by area). The caller still checks the place (<see cref="Allowed"/>).
        /// </summary>
        public Vec2 RingPoint(Vec2 hero, double angleRoll, double distanceRoll)
        {
            double angle = angleRoll * 2 * Math.PI;
            double lo = _s.MinHeroDistance * _s.MinHeroDistance, hi = _s.MaxHeroDistance * _s.MaxHeroDistance;
            float r = (float)Math.Sqrt(lo + distanceRoll * (hi - lo));
            return hero + new Vec2((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r);
        }

        /// <summary>A place a predator may be called to: inside the ground, outside the town, outside the light, not forbidden by the view.</summary>
        public bool Allowed(Vec2 at, Func<Vec2, bool> inLight) =>
            _s.Bounds.Contains(at) && !IsSafe(at) && !inLight(at) && !(Forbidden?.Invoke(at) ?? false);

        /// <summary>A predator is gone for good (killed, despawned by the view): its place can be taken by a new one.</summary>
        public void Released(string enemyId)
        {
            if (_alive.Remove(enemyId)) _aliveOrder.Remove(enemyId);
            _dismissed.Remove(enemyId);
        }

        internal void Tick(float dt, Vec2 hero, float daylight, double roll, Func<Vec2, bool> inLight, List<WorldEvent> events)
        {
            _timer += dt;
            int target = TargetCount(daylight);
            if (_alive.Count > target)
            {
                int excess = _alive.Count - target;
                for (int i = _aliveOrder.Count - 1; i >= 0 && excess > 0; i--)
                {
                    string id = _aliveOrder[i];
                    var pos = Locate?.Invoke(id) ?? _alive[id];
                    if ((pos - hero).Length > _s.DespawnDistance)
                    {
                        Released(id);
                        events.Add(new WorldEvent(WorldEventKind.PredatorDespawned, id, pos, _s.Enemy));
                    }
                    else if (!_dismissed.Contains(id))
                    {
                        _dismissed.Add(id);   // the morning: it walks away, and goes when it is far
                        events.Add(new WorldEvent(WorldEventKind.PredatorDismissed, id, pos, _s.Enemy));
                    }
                    excess--;
                }
                return;
            }
            if (_alive.Count >= target || _timer < _s.SpawnIntervalSeconds) return;
            var at = RingPoint(hero, Rolls.At(roll, 11), Rolls.At(roll, 12));
            if (!Allowed(at, inLight)) return;   // try again next step with the next roll
            string newId = $"night_{_s.Enemy}_{++_counter}";
            _alive[newId] = at;
            _aliveOrder.Add(newId);
            _timer = 0f;
            events.Add(new WorldEvent(WorldEventKind.PredatorSpawned, newId, at, _s.Enemy));
        }

        internal void Restore(int counter, float timer)
        {
            _counter = Math.Max(0, counter);
            _timer = Math.Max(0f, timer);
            _alive.Clear(); _aliveOrder.Clear(); _dismissed.Clear();   // living enemies are not saved (D-01): the night calls new ones
        }
    }

    /// <summary>Weather, wind, grass, mud and night predators of one world (D-06) — ticked together by <c>GameState.TickWorld</c>.</summary>
    public sealed class Nature
    {
        public Nature(ElementsSettings elements, NightSettings night, float fullMudSpeed)
        {
            Wind = new Wind(elements.Wind);
            Weather = new Weather(elements.Rain);
            Grass = new GrassField(elements.Grass, elements.Rain, Wind);
            Mud = new MudZones(elements.Mud, fullMudSpeed);
            Predators = new Predators(night);
        }

        public Wind Wind { get; }
        public Weather Weather { get; }
        public GrassField Grass { get; }
        public MudZones Mud { get; }
        public Predators Predators { get; }

        internal void Tick(float dt, double roll, IReadOnlyList<Campfire> fires, Vec2 hero, float daylight, Func<Vec2, bool> inLight, List<WorldEvent> events)
        {
            Wind.Tick(dt, roll, events);
            Weather.Tick(dt, roll, events);
            Grass.Tick(dt, roll, Weather.IsRaining, Weather.RainElapsed, fires, events);
            Mud.Tick(dt, Weather.IsRaining);
            Predators.Tick(dt, hero, daylight, roll, inLight, events);
        }
    }
}
