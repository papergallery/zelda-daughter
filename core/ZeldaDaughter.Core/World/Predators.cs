#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.World
{
    /// <summary>data/night.json (D-06).</summary>
    public sealed class NightSettings
    {
        /// <summary>Enemy id from enemies.json that comes out at night.</summary>
        public string Enemy { get; set; } = "";
        public int MaxAtNight { get; set; }
        /// <summary>Daylight (0..1) below which predators appear; the count grows to <see cref="MaxAtNight"/> as it gets darker.</summary>
        public float DaylightBelow { get; set; }
        public float SpawnIntervalSeconds { get; set; }
        public float MinHeroDistance { get; set; }
        public float DespawnDistance { get; set; }
        public float ZoneRadius { get; set; }
        public int PerZoneMax { get; set; }
        /// <summary>Ids of scene objects that mark the zones (scenes/region.json); a zone the scene does not register is ignored.</summary>
        public List<string> Zones { get; set; } = new List<string>();
    }

    /// <summary>
    /// Night predators (§2 «ночью опаснее»): how many wolves the dark calls for, where they may appear (zones from the scene), not near the hero
    /// and not in the light of a fire. The core says «spawn wolf night_wolf_3 here» — the view creates the <c>Enemy</c> — and «send it away» in the morning.
    /// </summary>
    public sealed class Predators
    {
        readonly NightSettings _s;
        readonly Dictionary<string, Vec2> _zones = new Dictionary<string, Vec2>(StringComparer.Ordinal);
        readonly List<string> _zoneOrder = new List<string>();
        readonly Dictionary<string, (string zone, Vec2 position)> _alive = new Dictionary<string, (string, Vec2)>(StringComparer.Ordinal);
        readonly List<string> _aliveOrder = new List<string>();
        float _timer;
        int _counter;

        public Predators(NightSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        /// <summary>The view tells where a living predator is now (for the morning despawn). Default: where it spawned.</summary>
        public Func<string, Vec2?>? Locate { get; set; }
        public int AliveCount => _alive.Count;
        public IReadOnlyList<string> Alive => _aliveOrder;
        internal int Counter => _counter;
        internal float Timer => _timer;

        /// <summary>Registers a zone from the scene (the position of the object called <paramref name="zoneId"/>).</summary>
        public void AddZone(string zoneId, Vec2 center)
        {
            if (!_s.Zones.Contains(zoneId) || _zones.ContainsKey(zoneId)) return;
            _zones[zoneId] = center;
            _zoneOrder.Add(zoneId);
        }

        public int TargetCount(float daylight)
        {
            if (daylight >= _s.DaylightBelow) return 0;
            return (int)Math.Min(_s.MaxAtNight, Math.Ceiling(_s.MaxAtNight * (1.0 - Math.Max(0f, daylight) / _s.DaylightBelow) - 1e-9));
        }

        /// <summary>A predator is gone for good (killed, despawned by the view): its place can be taken by a new one.</summary>
        public void Released(string enemyId)
        {
            if (_alive.Remove(enemyId)) _aliveOrder.Remove(enemyId);
        }

        internal void Tick(float dt, Vec2 hero, float daylight, double roll, Func<Vec2, bool> inLight, List<WorldEvent> events)
        {
            _timer += dt;
            int target = TargetCount(daylight);
            if (_alive.Count > target)
            {
                foreach (var id in _aliveOrder.ToList())
                {
                    if (_alive.Count <= target) break;
                    var pos = Locate?.Invoke(id) ?? _alive[id].position;
                    if ((pos - hero).Length <= _s.DespawnDistance) continue;
                    Released(id);
                    events.Add(new WorldEvent(WorldEventKind.PredatorDespawned, id, pos, _s.Enemy));
                }
                return;
            }
            if (_alive.Count >= target || _timer < _s.SpawnIntervalSeconds) return;
            var open = _zoneOrder.Where(z => _alive.Values.Count(a => a.zone == z) < _s.PerZoneMax).ToList();
            if (open.Count == 0) return;
            string zone = open[Math.Min(open.Count - 1, (int)(Rolls.At(roll, 11) * open.Count))];
            double angle = Rolls.At(roll, 12) * 2 * Math.PI;
            float r = (float)(Math.Sqrt(Rolls.At(roll, 13)) * _s.ZoneRadius);
            var at = _zones[zone] + new Vec2((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r);
            if ((at - hero).Length < _s.MinHeroDistance || inLight(at)) return;   // try again next step with the next roll
            string newId = $"night_{_s.Enemy}_{++_counter}";
            _alive[newId] = (zone, at);
            _aliveOrder.Add(newId);
            _timer = 0f;
            events.Add(new WorldEvent(WorldEventKind.PredatorSpawned, newId, at, _s.Enemy));
        }

        internal void Restore(int counter, float timer)
        {
            _counter = Math.Max(0, counter);
            _timer = Math.Max(0f, timer);
            _alive.Clear(); _aliveOrder.Clear();   // living enemies are not saved (D-01): the night calls new ones
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
