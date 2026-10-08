#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.World
{
    /// <summary>data/elements.json (D-06): the minimum of §8 — fire on dry grass, wind, rain, mud.</summary>
    public sealed class ElementsSettings
    {
        public GrassSettings Grass { get; set; } = new GrassSettings();
        public RainSettings Rain { get; set; } = new RainSettings();
        public MudSettings Mud { get; set; } = new MudSettings();
        public WindSettings Wind { get; set; } = new WindSettings();
    }

    public sealed class GrassSettings
    {
        public float NeighborDistance { get; set; }
        /// <summary>Step of the grid of grass cells over a dry-grass zone, m (C9; <c>GrassCells.Grid</c>): not more than <see cref="NeighborDistance"/>, or the field is not connected.</summary>
        public float CellSpacing { get; set; }
        public float BurnSeconds { get; set; }
        public float SpreadPerSecond { get; set; }
        public float WindGain { get; set; }
        public float MinSpreadFactor { get; set; }
        public float CampfireSparkRadius { get; set; }
        public float CampfireSparkPerSecond { get; set; }
        public float BurnRadius { get; set; }
        public float ScorchSeverity { get; set; }
        public float ScorchCooldownSeconds { get; set; }
    }

    public sealed class RainSettings
    {
        public float ChancePerSecond { get; set; }
        public float MinSeconds { get; set; }
        public float MaxSeconds { get; set; }
        public float WetAfterSeconds { get; set; }
        public float DryAfterSeconds { get; set; }
    }

    public sealed class MudSettings
    {
        public float RiseSeconds { get; set; }
        public float DryingSeconds { get; set; }
    }

    public sealed class WindSettings
    {
        public float ChangeEverySeconds { get; set; }
        public float MinStrength { get; set; }
        public float MaxStrength { get; set; }
        public float StartDirectionDegrees { get; set; }
        public float StartStrength { get; set; }
    }

    /// <summary>The wind: a direction on the ground and a strength 0..1; it changes now and then by a roll.</summary>
    public sealed class Wind
    {
        readonly WindSettings _s;
        float _timer;

        public Wind(WindSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            double a = settings.StartDirectionDegrees * Math.PI / 180.0;
            Direction = new Vec2((float)Math.Cos(a), (float)Math.Sin(a));
            Strength = settings.StartStrength;
        }

        /// <summary>Unit vector the wind blows towards (x, z).</summary>
        public Vec2 Direction { get; private set; }
        public float Strength { get; private set; }
        internal float Timer => _timer;

        public void Set(Vec2 direction, float strength)
        {
            var d = direction.Normalized;
            if (d.Length > 0f) Direction = d;
            Strength = Math.Max(0f, Math.Min(1f, strength));
        }

        internal void Tick(float dt, double roll, List<WorldEvent> events)
        {
            _timer += dt;
            if (_timer < _s.ChangeEverySeconds) return;
            _timer %= _s.ChangeEverySeconds;
            double a = Rolls.At(roll, 1) * 2 * Math.PI;
            Direction = new Vec2((float)Math.Cos(a), (float)Math.Sin(a));
            Strength = (float)(_s.MinStrength + (_s.MaxStrength - _s.MinStrength) * Rolls.At(roll, 2));
            events.Add(new WorldEvent(WorldEventKind.WindChanged));
        }

        internal void Restore(Vec2 direction, float strength, float timer)
        {
            Set(direction, strength);
            _timer = Math.Max(0f, timer);
        }
    }

    /// <summary>Rain at random (a roll from outside) or by script. Grass and mud read how long it has been raining.</summary>
    public sealed class Weather
    {
        readonly RainSettings _s;

        public Weather(RainSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public bool IsRaining { get; private set; }
        public float RainLeft { get; private set; }
        /// <summary>Seconds this rain has lasted.</summary>
        public float RainElapsed { get; private set; }

        public void StartRain(float seconds)
        {
            if (seconds <= 0f) return;
            IsRaining = true;
            RainLeft = seconds;
            RainElapsed = 0f;
        }

        public void StopRain()
        {
            IsRaining = false;
            RainLeft = 0f;
            RainElapsed = 0f;
        }

        internal void Tick(float dt, double roll, List<WorldEvent> events)
        {
            if (IsRaining)
            {
                RainElapsed += dt;
                RainLeft -= dt;
                if (RainLeft <= 0f)
                {
                    StopRain();
                    events.Add(new WorldEvent(WorldEventKind.RainStopped));
                }
                return;
            }
            double p = Math.Min(1.0, _s.ChancePerSecond * dt);
            if (p > 0 && roll < p)
            {
                StartRain((float)(_s.MinSeconds + (_s.MaxSeconds - _s.MinSeconds) * (roll / p)));
                events.Add(new WorldEvent(WorldEventKind.RainStarted));
            }
        }

        internal void Restore(bool raining, float left, float elapsed)
        {
            IsRaining = raining && left > 0f;
            RainLeft = IsRaining ? left : 0f;
            RainElapsed = IsRaining ? Math.Max(0f, elapsed) : 0f;
        }
    }

    /// <summary>Ground that turns to mud in the rain: zones the scene registers (a path, a field); speed drops towards the «mud» terrain value.</summary>
    public sealed class MudZones
    {
        sealed class Zone
        {
            public string Id = "";
            public Vec2 Center;
            public float Radius;
            public float Level;
        }

        readonly MudSettings _s;
        readonly float _fullMudSpeed;
        readonly List<Zone> _zones = new List<Zone>();
        readonly Dictionary<string, float> _pending = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <param name="fullMudSpeed">Speed multiplier in full mud — movement.json terrain.mud.</param>
        public MudZones(MudSettings settings, float fullMudSpeed)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _fullMudSpeed = fullMudSpeed;
        }

        public void AddZone(string id, Vec2 center, float radius)
        {
            if (_zones.Exists(z => z.Id == id)) return;
            _zones.Add(new Zone { Id = id, Center = center, Radius = radius, Level = _pending.TryGetValue(id, out var l) ? l : 0f });
        }

        /// <summary>0 dry … 1 deep mud; 0 for an unknown zone.</summary>
        public float Level(string zoneId)
        {
            for (int i = 0; i < _zones.Count; i++) if (_zones[i].Id == zoneId) return _zones[i].Level; // a loop, not Find(lambda): the view asks every 0.25 s in rain and a closure allocates (D-19)
            return 0f;
        }

        /// <summary>Walking-speed multiplier at a point (1 outside every zone; the worst of the zones it lies in).</summary>
        public float SpeedAt(Vec2 position)
        {
            float m = 1f;
            foreach (var z in _zones)
                if ((position - z.Center).Length <= z.Radius) m = Math.Min(m, 1f + (_fullMudSpeed - 1f) * z.Level);
            return m;
        }

        internal void Tick(float dt, bool raining)
        {
            foreach (var z in _zones)
            {
                float d = raining ? dt / Math.Max(_s.RiseSeconds, 1e-3f) : -dt / Math.Max(_s.DryingSeconds, 1e-3f);
                z.Level = Math.Max(0f, Math.Min(1f, z.Level + d));
            }
        }

        internal IReadOnlyDictionary<string, float> State()
        {
            var d = new Dictionary<string, float>(_pending, StringComparer.Ordinal);
            foreach (var z in _zones) d[z.Id] = z.Level;
            return d;
        }

        internal void Restore(IReadOnlyDictionary<string, float>? levels)
        {
            _pending.Clear();
            if (levels != null) foreach (var kv in levels) _pending[kv.Key] = Math.Max(0f, Math.Min(1f, kv.Value));
            foreach (var z in _zones) z.Level = _pending.TryGetValue(z.Id, out var l) ? l : 0f;
        }
    }
}
