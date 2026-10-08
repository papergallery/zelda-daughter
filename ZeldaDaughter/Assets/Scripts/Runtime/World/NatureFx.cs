using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// D-16: the elements and the dark as the scene shows them (docs/demo/unity-architecture.md §1, §8). The rules are the core's
    /// (<c>g.Nature</c>: weather, wind, grass, mud, night predators; <c>g.Camp</c>: campfires); this only draws what the core says, from its
    /// state (so a load, a time jump, a torch lit by the game itself all look right) and from <c>SessionEvents.World</c>:
    /// <list type="bullet">
    /// <item>rain — streaks around the hero, the ground darker (<c>_ZD_Wetness</c>, see below), mud puddles in the mud zones;</item>
    /// <item>grass — wet is darker, burning has a fire (at most <see cref="MaxFires"/> at once, the nearest to the hero), burnt is stubble and a dark patch;
    /// the patches of all burnt cells are one instanced draw (<c>Graphics.RenderMeshInstanced</c>);</item>
    /// <item>the campfire itself — model, flame and its light (<c>Campfire.Light</c>) — is D-14's <c>CampPresenter</c>, not drawn here;</item>
    /// <item>a tap on a grass cell with a torch in the bag, the hero close by — the cell is set alight (<c>g.IgniteGrass</c>);</item>
    /// <item>the hero's burn — her remark; a night wolf called / sent away — a log line (the wolf itself is D-13's).</item>
    /// </list>
    /// Wetness: <c>Shader.SetGlobalFloat("_ZD_Wetness")</c> 0…1 is set for any shader that wants it; until the toon shader reads it the ground
    /// renderer is darkened through a property block (<see cref="_tintGround"/>) — turn that off the day the shader does it, or the ground darkens twice.
    /// </summary>
    public sealed class NatureFx : MonoBehaviour
    {
        public const int MaxFires = 12;           // criteria D-16 p. 4: the fire pool
        public const float IgniteReach = 3.2f;    // a torch reaches a grass cell this far from the hero, m
        private const int FireLights = 4;         // only the first fires of the pool light the surroundings
        private const int MaxInstances = 1023;    // one RenderMeshInstanced call

        private static readonly int WetnessId = Shader.PropertyToID("_ZD_Wetness");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private GameSession _session;
        [SerializeField] private Renderer _ground;
        [SerializeField] private Transform _hero;
        [SerializeField] private FxRegistry _fx;
        [SerializeField] private Material _rainMat;   // Zelda/NatureFx, plain streaks
        [SerializeField] private Material _puffMat;   // alpha-blended soft discs: smoke, mud
        [SerializeField] private Material _flameMat;  // additive soft discs
        [SerializeField] private Material _burntMat;  // instanced dark patch
        [SerializeField] private bool _tintGround = false; // D-21: Zelda/Toon reads _ZD_Wetness itself now

        private GameState _g;
        private Mesh _quad;
        private MaterialPropertyBlock _block;
        private Color _groundBase = Color.white;

        // weather
        private ParticleSystem _rain;
        private float _rainLevel, _wet;
        private bool _raining, _rainSent;
        private float _lastWetApplied = -1f;

        // mud
        private sealed class MudView { public ZoneArea Zone; public Renderer Renderer; public MaterialPropertyBlock Block; public float Level = -1f; }
        private readonly List<MudView> _mud = new List<MudView>();

        // grass
        private struct Cell
        {
            public string Id;
            public Vector3 Position;
            public Renderer[] Renderers;
            public Color[] BaseColors;
            public Transform Root;
            public Vector3 BaseScale;
            public GrassState State;
            public int Slot;
        }
        private Cell[] _cells = new Cell[0];
        private readonly Dictionary<string, int> _cellOf = new Dictionary<string, int>(StringComparer.Ordinal);
        private MaterialPropertyBlock _cellBlock;
        private Matrix4x4[] _burnt = new Matrix4x4[0];
        private int _burntCount, _burningCount;
        private float _syncLeft;

        // fires
        private sealed class Fire
        {
            public GameObject Root;
            public ParticleSystem[] Systems;
            public ParticleSystem Smoke;
            public Light Light;
            public int Cell = -1;
            public float Phase;
        }
        private readonly Fire[] _fires = new Fire[MaxFires];
        private int _activeFires;
        private float _windLeft;

        public bool IsRaining => _raining;
        /// <summary>0 dry … 1 soaked: the same number as the global <c>_ZD_Wetness</c>.</summary>
        public float Wetness => _wet;
        /// <summary>0…1, how heavy the streaks fall now.</summary>
        public float RainLevel => _rainLevel;
        public int RainParticleCount => _rain != null ? _rain.particleCount : 0;
        public int BurningCells => _burningCount;
        public int BurntPatches => _burntCount;
        /// <summary>Fire effects in use now (never above <see cref="MaxFires"/>).</summary>
        public int ActiveFires => _activeFires;
        /// <summary>Fire effects built — the pool size.</summary>
        public int FirePoolSize { get { int n = 0; for (int i = 0; i < _fires.Length; i++) if (_fires[i] != null) n++; return n; } }
        public int MudZones => _mud.Count;
        public int WolvesCalled { get; private set; }
        public int WolvesSent { get; private set; }
        public int Scorched { get; private set; }

        public void Configure(GameSession session, Renderer ground, Transform hero, FxRegistry fx,
            Material rainMat, Material puffMat, Material flameMat, Material burntMat)
        {
            _session = session;
            _ground = ground;
            _hero = hero;
            _fx = fx;
            _rainMat = rainMat;
            _puffMat = puffMat;
            _flameMat = flameMat;
            _burntMat = burntMat;
        }

        // ------------------------------------------------------------------ life

        private void Start()
        {
            _quad = FireParticles.FlatQuad();
            _block = new MaterialPropertyBlock();
            _cellBlock = new MaterialPropertyBlock();
            if (_ground != null && _ground.sharedMaterial != null && _ground.sharedMaterial.HasProperty(BaseColorId)) _groundBase = _ground.sharedMaterial.GetColor(BaseColorId);
            BuildRain();
            BuildFirePool();
            Shader.SetGlobalFloat(WetnessId, 0f);
            _session.Events.StateReady += OnStateReady;
            _session.Events.World += OnWorld;
            _session.Events.TimeJumped += OnTimeJumped;
            _session.OnTap(TapKind.Scenery, OnScenery);
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnStateReady;
            _session.Events.World -= OnWorld;
            _session.Events.TimeJumped -= OnTimeJumped;
            Shader.SetGlobalFloat(WetnessId, 0f);
        }

        private void OnStateReady(GameState g)
        {
            _g = g;
            BuildCells();
            BuildMud();
            UpdateMud();
            _raining = g.Nature.Weather.IsRaining;
            _wet = _raining ? 1f : _wet;
            _rainLevel = _raining ? 1f : 0f;
            SyncCells();
            RaiseRain();
            ZdLog.Info("Nature", $"ready cells={_cells.Length} mud={_mud.Count} raining={_raining}");
        }

        private void OnTimeJumped(double hours)
        {
            if (_g == null) return;
            _raining = _g.Nature.Weather.IsRaining;
            _wet = _raining ? 1f : 0f; // hours passed: it has dried, or it is raining
            _rainLevel = _raining ? 1f : 0f;
            UpdateMud();
            SyncCells();
            RaiseRain();
        }

        // ------------------------------------------------------------------ the core's events

        private void OnWorld(WorldEvent e)
        {
            switch (e.Kind)
            {
                case WorldEventKind.RainStarted:
                case WorldEventKind.RainStopped:
                    RaiseRain();
                    break;
                case WorldEventKind.GrassIgnited:
                case WorldEventKind.GrassBurnedOut:
                case WorldEventKind.GrassExtinguished:
                case WorldEventKind.GrassWetted:
                case WorldEventKind.GrassDried:
                    SyncCells();
                    break;
                case WorldEventKind.HeroScorched:
                    Scorched++;
                    _session.Say(Topics.WoundBurn);
                    ZdLog.Info("Nature", "scorched");
                    break;
                case WorldEventKind.PredatorSpawned:
                    WolvesCalled++;
                    ZdLog.Info("Nature", $"wolf_called {e.Id} {e.Detail} at {e.Position.X:0.0},{e.Position.Y:0.0}");
                    break;
                case WorldEventKind.PredatorDespawned:
                    WolvesSent++;
                    ZdLog.Info("Nature", $"wolf_gone {e.Id}");
                    break;
            }
        }

        private void RaiseRain()
        {
            bool now = _g != null && _g.Nature.Weather.IsRaining;
            if (_rainSent == now && _rainSentOnce) return;
            _rainSent = now;
            _rainSentOnce = true;
            ZdLog.Info("Nature", now ? "rain_start" : "rain_stop");
            _session.Events.RaiseRainChanged(now);
        }
        private bool _rainSentOnce;

        // ------------------------------------------------------------------ the torch lights the grass

        private void OnScenery(Tappable t)
        {
            if (_g == null || !_cellOf.TryGetValue(t.Id, out int i)) return;
            TryIgnite(i);
        }

        /// <summary>The hero puts her torch to the grass cell (a tap on it): lit only if she carries a burning torch, stands close, and the cell is dry.</summary>
        public bool TryIgnite(string cellId) => _cellOf.TryGetValue(cellId, out int i) && TryIgnite(i);

        private bool TryIgnite(int i)
        {
            if (_g.Bag.Count("torch") <= 0) return false;
            var d = _cells[i].Position - _hero.position;
            d.y = 0f;
            if (d.magnitude > IgniteReach) return false;
            bool lit = _g.IgniteGrass(_cells[i].Id);
            ZdLog.Info("Nature", $"ignite {_cells[i].Id} {(lit ? "lit" : "no")}");
            if (lit)
            {
                _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Place, _cells[i].Position, "torch"));
                SyncCells();
            }
            return lit;
        }

        // ------------------------------------------------------------------ building

        private void BuildRain()
        {
            // D-21: the rain is drawn by the watercolour feature after the wash (pass ZdAfterWash), not before it.
            if (_rainMat != null) _rainMat.SetShaderPassEnabled("UniversalForward", false);
            var root = new GameObject("Rain");
            root.transform.SetParent(transform, false);
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.55f;
            main.startSpeed = 0f;
            main.startSize = 0.11f; // thick: the watercolour pass (after transparents) eats thin lines
            main.startColor = new Color(0.16f, 0.22f, 0.32f, 0.8f); // ink, not water
            main.maxParticles = 2500;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-7f);
            vel.y = new ParticleSystem.MinMaxCurve(-22f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 0.1f, 26f);
            var em = ps.emission;
            em.rateOverTime = 0f;
            var r = root.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 13f;
            r.velocityScale = 0f;
            r.sharedMaterial = _rainMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _rain = ps;
        }

        private void BuildFirePool()
        {
            var prefab = _fx != null ? _fx.Get("grass_fire") : null;
            var parent = new GameObject("Fires").transform;
            parent.SetParent(transform, false);
            for (int i = 0; i < MaxFires; i++)
            {
                var f = new Fire { Phase = i * 1.37f };
                if (prefab != null)
                {
                    f.Root = Instantiate(prefab, parent);
                    f.Systems = f.Root.GetComponentsInChildren<ParticleSystem>(true);
                }
                else
                {
                    f.Root = new GameObject("Fire_" + i);
                    f.Root.transform.SetParent(parent, false);
                    var flame = FireParticles.Flame(f.Root.transform, _flameMat, 1f);
                    f.Smoke = FireParticles.Smoke(f.Root.transform, _puffMat, 1f);
                    f.Systems = new[] { flame, f.Smoke };
                }
                if (i < FireLights)
                {
                    var lightGo = new GameObject("Light");
                    lightGo.transform.SetParent(f.Root.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                    f.Light = lightGo.AddComponent<Light>();
                    f.Light.type = LightType.Point;
                    f.Light.color = new Color(1f, 0.55f, 0.22f);
                    f.Light.range = 6f;
                    f.Light.intensity = 0f;
                    f.Light.shadows = LightShadows.None;
                    f.Light.enabled = false;
                }
                f.Root.SetActive(false);
                _fires[i] = f;
            }
        }

        private void BuildCells()
        {
            var points = _session.Index.GrassCells;
            _cells = new Cell[points.Count];
            _cellOf.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                var tags = _session.Index.Find(points[i].Id);
                var c = new Cell { Id = points[i].Id, Position = points[i].Position, Slot = -1, State = GrassState.Dry };
                if (tags != null)
                {
                    c.Root = tags.transform;
                    c.BaseScale = c.Root.localScale;
                    c.Renderers = tags.GetComponentsInChildren<Renderer>(true);
                    c.BaseColors = new Color[c.Renderers.Length];
                    for (int k = 0; k < c.Renderers.Length; k++)
                    {
                        var m = c.Renderers[k].sharedMaterial;
                        c.BaseColors[k] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                    }
                }
                else { c.Renderers = new Renderer[0]; c.BaseColors = new Color[0]; }
                _cells[i] = c;
                _cellOf[c.Id] = i;
            }
            _burnt = new Matrix4x4[Mathf.Min(MaxInstances, Mathf.Max(1, _cells.Length))];
        }

        private void BuildMud()
        {
            foreach (var m in _mud) if (m.Renderer != null) Destroy(m.Renderer.gameObject);
            _mud.Clear();
            foreach (var z in _session.Index.Zones)
            {
                if (z == null || !z.Has("mud")) continue;
                var a = z.Area;
                var go = new GameObject("Mud_" + z.Id);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(a.Center.X, 0.03f, a.Center.Z);
                float w = a.Radius > 0f ? a.Radius * 2f : a.Size.X, d = a.Radius > 0f ? a.Radius * 2f : a.Size.Z;
                go.transform.localScale = new Vector3(w * 1.15f, 1f, d * 1.15f);
                go.AddComponent<MeshFilter>().sharedMesh = _quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _puffMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                _mud.Add(new MudView { Zone = z, Renderer = r, Block = new MaterialPropertyBlock() });
            }
        }

        // ------------------------------------------------------------------ the frame

        private void Update()
        {
            if (_g == null) return;
            float dt = Mathf.Min(Time.deltaTime, GameSession.MaxFrameSeconds);

            _raining = _g.Nature.Weather.IsRaining;
            if (_raining != _rainSent) RaiseRain();
            UpdateWeather(dt);
            _mudLeft -= dt;
            if (_mudLeft <= 0f && (_raining || _mudShown))
            {
                _mudLeft = 0.25f;
                UpdateMud();
            }

            _syncLeft -= dt;
            if (_syncLeft <= 0f)
            {
                _syncLeft = 0.1f;
                SyncCells();
            }
            UpdateFires(dt);
            DrawBurnt();
        }

        private void LateUpdate()
        {
            if (_rain != null && _hero != null) _rain.transform.position = _hero.position + new Vector3(0f, 12f, 0f);
        }

        private void UpdateWeather(float dt)
        {
            float rainTarget = _raining ? 1f : 0f;
            _rainLevel = Mathf.MoveTowards(_rainLevel, rainTarget, dt / 3f);
            var em = _rain.emission;
            if (_rainLevel > 0.001f)
            {
                em.rateOverTime = 600f * _rainLevel;
                if (!_rain.isPlaying) _rain.Play();
            }
            else if (_rain.isPlaying) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            float dry = Mathf.Max(1f, _g.Data.Elements.Rain.DryAfterSeconds);
            _wet = Mathf.MoveTowards(_wet, rainTarget, dt / (_raining ? 12f : dry));
            if (Mathf.Abs(_wet - _lastWetApplied) > 0.002f)
            {
                _lastWetApplied = _wet;
                Shader.SetGlobalFloat(WetnessId, _wet);
                if (_tintGround && _ground != null)
                {
                    // wet earth is darker and a little cooler
                    var c = Color.Lerp(_groundBase, new Color(_groundBase.r * 0.48f, _groundBase.g * 0.56f, _groundBase.b * 0.66f, _groundBase.a), _wet);
                    _ground.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, c);
                    _ground.SetPropertyBlock(_block);
                }
            }
        }

        // Mud.Level looks the zone up with a lambda (it allocates): ask only while it rains or some mud is still shown.
        private float _mudLeft;
        private bool _mudShown;

        private void UpdateMud()
        {
            bool shown = false;
            for (int i = 0; i < _mud.Count; i++)
            {
                var m = _mud[i];
                float level = _g.Nature.Mud.Level(m.Zone.Id);
                if (level > 0.02f) shown = true;
                if (Mathf.Abs(level - m.Level) < 0.005f) continue;
                m.Level = level;
                m.Renderer.enabled = level > 0.02f;
                m.Renderer.GetPropertyBlock(m.Block);
                m.Block.SetColor(ColorId, new Color(0.30f, 0.21f, 0.13f, 0.82f * Mathf.SmoothStep(0f, 1f, level)));
                m.Renderer.SetPropertyBlock(m.Block);
            }
            _mudShown = shown;
        }

        // ------------------------------------------------------------------ grass

        /// <summary>Reads every cell from the core and restyles those that changed; then assigns the fires.</summary>
        private void SyncCells()
        {
            if (_g == null) return;
            var grass = _g.Nature.Grass;
            bool burntChanged = false, fireChanged = false;
            for (int i = 0; i < _cells.Length; i++)
            {
                var st = grass.StateOf(_cells[i].Id);
                if (st == _cells[i].State) continue;
                var old = _cells[i].State;
                _cells[i].State = st;
                Restyle(i);
                if (st == GrassState.Burnt || old == GrassState.Burnt) burntChanged = true;
                if (st == GrassState.Burning || old == GrassState.Burning) fireChanged = true;
            }
            if (burntChanged) RebuildBurnt();
            if (fireChanged || (_burningCount > _activeFires && _activeFires < MaxFires)) AssignFires();
        }

        private void Restyle(int i)
        {
            ref var c = ref _cells[i];
            Color tint;
            float height = 1f;
            switch (c.State)
            {
                case GrassState.Wet: tint = new Color(0.78f, 0.84f, 0.86f, 1f); break;
                case GrassState.Burning: tint = new Color(0.50f, 0.32f, 0.18f, 1f); break;
                case GrassState.Burnt: tint = new Color(0.13f, 0.115f, 0.10f, 1f); height = 0.3f; break;
                default: tint = Color.white; break;
            }
            for (int k = 0; k < c.Renderers.Length; k++)
            {
                var r = c.Renderers[k];
                if (r == null) continue;
                if (c.State == GrassState.Dry) { r.SetPropertyBlock(null); continue; }
                var b = c.BaseColors[k];
                _cellBlock.Clear();
                _cellBlock.SetColor(BaseColorId, new Color(b.r * tint.r, b.g * tint.g, b.b * tint.b, b.a));
                r.SetPropertyBlock(_cellBlock);
            }
            if (c.Root != null) c.Root.localScale = new Vector3(c.BaseScale.x, c.BaseScale.y * height, c.BaseScale.z);
        }

        private void RebuildBurnt()
        {
            int n = 0;
            for (int i = 0; i < _cells.Length && n < _burnt.Length; i++)
            {
                if (_cells[i].State != GrassState.Burnt) continue;
                // a stable "random" turn and size per cell: no state to store
                uint h = Hash(_cells[i].Id);
                float angle = (h & 1023u) / 1023f * 360f;
                float size = 1.7f + ((h >> 10) & 255u) / 255f * 0.6f;
                _burnt[n++] = Matrix4x4.TRS(_cells[i].Position + new Vector3(0f, 0.04f, 0f), Quaternion.Euler(0f, angle, 0f), new Vector3(size, 1f, size));
            }
            _burntCount = n;
        }

        /// <summary>FNV-1a: the same on every platform (string.GetHashCode is not).</summary>
        private static uint Hash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619u;
            return h * 2654435761u;
        }

        private void DrawBurnt()
        {
            if (_burntCount == 0 || _burntMat == null) return;
            var rp = new RenderParams(_burntMat)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(2000f, 50f, 2000f)),
            };
            Graphics.RenderMeshInstanced(rp, _quad, 0, _burnt, _burntCount);
        }

        // ------------------------------------------------------------------ fires

        /// <summary>
        /// A fire effect per burning cell, at most <see cref="MaxFires"/>: a cell that went out frees its fire; a new one takes a free fire,
        /// or, if none is free, the place of the farthest fire when it is clearly nearer to the hero.
        /// </summary>
        private void AssignFires()
        {
            for (int s = 0; s < MaxFires; s++)
            {
                var f = _fires[s];
                if (f.Cell >= 0 && _cells[f.Cell].State != GrassState.Burning) Release(s);
            }
            int burning = 0;
            var hero = _hero != null ? _hero.position : Vector3.zero;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].State != GrassState.Burning) { _cells[i].Slot = -1; continue; }
                burning++;
                if (_cells[i].Slot >= 0) continue;
                int free = -1;
                for (int s = 0; s < MaxFires; s++) if (_fires[s].Cell < 0) { free = s; break; }
                if (free < 0)
                {
                    int far = -1; float farD = -1f;
                    for (int s = 0; s < MaxFires; s++)
                    {
                        float d = (_cells[_fires[s].Cell].Position - hero).sqrMagnitude;
                        if (d > farD) { farD = d; far = s; }
                    }
                    if (far < 0 || (_cells[i].Position - hero).sqrMagnitude + 4f >= farD) continue;
                    Release(far);
                    free = far;
                }
                Occupy(free, i);
            }
            _burningCount = burning;
            int active = 0;
            for (int s = 0; s < MaxFires; s++) if (_fires[s].Cell >= 0) active++;
            _activeFires = active;
        }

        private void Occupy(int slot, int cell)
        {
            var f = _fires[slot];
            f.Cell = cell;
            _cells[cell].Slot = slot;
            f.Root.transform.position = _cells[cell].Position;
            f.Root.SetActive(true);
            for (int k = 0; k < f.Systems.Length; k++) if (f.Systems[k] != null) { f.Systems[k].Clear(); f.Systems[k].Play(); }
            if (f.Light != null) f.Light.enabled = true;
        }

        private void Release(int slot)
        {
            var f = _fires[slot];
            if (f.Cell >= 0) _cells[f.Cell].Slot = -1;
            f.Cell = -1;
            if (f.Light != null) f.Light.enabled = false;
            f.Root.SetActive(false);
        }

        private void UpdateFires(float dt)
        {
            if (_activeFires == 0) return;
            float t = Time.time;
            _windLeft -= dt;
            bool wind = _windLeft <= 0f;
            if (wind) _windLeft = 0.5f;
            var w = _g.Nature.Wind;
            for (int s = 0; s < MaxFires; s++)
            {
                var f = _fires[s];
                if (f.Cell < 0) continue;
                if (f.Light != null)
                {
                    float flick = 0.8f + 0.2f * Mathf.Sin(t * 13f + f.Phase) + 0.12f * Mathf.Sin(t * 29f + f.Phase * 2f);
                    f.Light.intensity = 2.2f * flick;
                }
                if (wind && f.Smoke != null)
                {
                    var force = f.Smoke.forceOverLifetime;
                    force.enabled = true;
                    force.space = ParticleSystemSimulationSpace.World;
                    force.x = new ParticleSystem.MinMaxCurve(w.Direction.X * w.Strength * 2.5f);
                    force.y = new ParticleSystem.MinMaxCurve(0f);
                    force.z = new ParticleSystem.MinMaxCurve(w.Direction.Y * w.Strength * 2.5f);
                }
            }
        }
    }

    /// <summary>The effects of fire made by code when the registry has no prefab for them (Ian's Fire Pack comes through <see cref="FxRegistry"/>).</summary>
    public static class FireParticles
    {
        private static ParticleSystem New(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            return ps;
        }

        /// <summary>Tongues of flame rising from a point: orange-yellow, additive, shrinking as they go. <paramref name="size"/> 1 — a grass fire.</summary>
        public static ParticleSystem Flame(Transform parent, Material additive, float size)
        {
            var ps = New("Flame", parent, additive);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * size, 1.1f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * size, 0.95f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.35f, 0.9f), new Color(1f, 0.45f, 0.15f, 0.9f));
            main.maxParticles = 48;
            var em = ps.emission;
            em.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.22f * size;
            shape.rotation = new Vector3(-90f, 0f, 0f); // the cone's +Z becomes up
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.38f, 0.1f), 0.6f), new GradientColorKey(new Color(0.35f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
            return ps;
        }

        /// <summary>Grey smoke that rises, widens and fades; in world space so the wind can carry it.</summary>
        public static ParticleSystem Smoke(Transform parent, Material blended, float size)
        {
            var ps = New("Smoke", parent, blended);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * size, 0.8f * size);
            main.startColor = new Color(0.28f, 0.27f, 0.26f, 0.40f);
            main.maxParticles = 24;
            var em = ps.emission;
            em.rateOverTime = 5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.2f * size;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.position = new Vector3(0f, 0.6f * size, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            return ps;
        }

        /// <summary>A 1 × 1 m quad lying flat (normal up) with UVs 0…1 — decals, patches.</summary>
        public static Mesh FlatQuad()
        {
            var m = new Mesh { name = "ZD_FlatQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }
    }
}
