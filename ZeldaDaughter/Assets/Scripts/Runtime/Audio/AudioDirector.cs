using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// A place of the scene that sounds (D-17): <c>town</c>, <c>field</c>, <c>river</c> fade the ambience in over <see cref="Fade"/> metres outside the
    /// edge; <c>stone</c> and <c>wood</c> change the footsteps inside. The geometry is a core <see cref="Area"/> (circle, rect or strip), serialised by
    /// SceneBuilder from the config.
    /// </summary>
    [Serializable]
    public sealed class SoundZone
    {
        public string Kind;
        public string AreaJson;
        public float Fade = 20f;
        [NonSerialized] private Area _area;

        public Area Area => _area ?? (_area = Area.FromJson(AreaJson));

        /// <summary>1 inside, falling linearly to 0 at <see cref="Fade"/> metres outside the edge.</summary>
        public float Weight(Vector3 p)
        {
            float d = Area.SignedDistance(p.x, p.z);
            if (d <= 0f) return 1f;
            return Fade <= 0f ? 0f : Mathf.Clamp01(1f - d / Fade);
        }

        public bool Contains(Vector3 p) => Area.SignedDistance(p.x, p.z) <= 0f;
    }

    /// <summary>The ambience layers and how loud each should be for the place, the light and the weather — a pure function, so a test can read it.</summary>
    public static class AmbientMix
    {
        public const int Forest = 0, Wind = 1, Field = 2, River = 3, Town = 4, Night = 5, Rain = 6, Count = 7;
        public static readonly string[] Ids = { "amb_forest", "amb_wind", "amb_field", "amb_river", "amb_town", "amb_night", "amb_rain" };

        /// <summary>Daylight share → how much of the day sounds (birds, crowd) are heard: night is crickets and wind.</summary>
        public static float Day(float daylight) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((daylight - 0.15f) / 0.45f));

        public static void Targets(float town, float field, float river, float daylight, bool raining, float[] into)
        {
            float day = Day(daylight);
            float wet = raining ? 1f : 0f;
            into[Forest] = (1f - Mathf.Max(town, field)) * day * (1f - 0.5f * wet);
            into[Wind] = 0.6f * (1f - 0.7f * town);
            into[Field] = field * (0.5f + 0.5f * day);
            into[River] = river;
            into[Town] = town * (0.35f + 0.65f * day) * (1f - 0.4f * wet);
            into[Night] = (1f - day) * (1f - 0.6f * town);
            into[Rain] = wet;
        }
    }

    /// <summary>
    /// D-17: every sound of the game (project-design.md §9: no soundtrack — ambience, steps, actions, fire; the only music is the bard in the tavern,
    /// <see cref="BardSource"/>). Listens to the session's events and plays from a pool of AudioSources made once in Awake — the number never changes.
    /// <list type="bullet">
    /// <item>Ambience: seven looped layers (forest, wind, field, river, town, night, rain) cross-faded by the hero's place (<see cref="SoundZone"/>), the daylight and the rain.</item>
    /// <item>Steps: <c>HeroStep</c> from HeroView, the surface under the foot — a <c>stone</c>/<c>wood</c> zone, else the terrain of the hero (<c>grass</c>, <c>road</c>, <c>mud</c>, <c>water</c>…).</item>
    /// <item>Actions: hero's hands, blows, the beast's windup, looting, trading, windows (paper), sleep.</item>
    /// <item>Fire: the nearest lit campfires (3 voices) and the hero's torch.</item>
    /// <item>Night calls: in the wild at night, now a wolf howling, now an owl, from somewhere far around, every 25…60 seconds.</item>
    /// </list>
    /// A sound with no clip (the purchased files are not in the project) is silence; one warning for a sound that is not in the registry at all,
    /// one line when the whole registry is empty. The wolf, the bard, the wooden steps and the night are generated clips of our own in
    /// Assets/Art/Audio/Generated (D-17b, in git).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const int OneShotVoices = 8, FireVoices = 3;
        private const float StrideDedupSeconds = 0.15f, AmbientFadeSeconds = 3f, FireRefreshSeconds = 0.2f;

        [SerializeField] private GameSession _session;
        [SerializeField] private HeroController _hero;
        [SerializeField] private SoundRegistry _sounds;
        [SerializeField] private SoundZone[] _zones = new SoundZone[0];
        [SerializeField] private float _fireMaxDistance = 40f;

        private readonly Dictionary<string, SoundDef> _defs = new Dictionary<string, SoundDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _lastClip = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _lastAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _played = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _warned = new HashSet<string>();

        private AudioSource[] _layers, _voices, _fires;
        private AudioSource _torch;
        private int _nextVoice;
        private readonly float[] _target = new float[AmbientMix.Count];
        private readonly float[] _level = new float[AmbientMix.Count];
        private readonly int[] _fireOrder = new int[FireVoices];

        private GameState _g;
        private float _fireTimer, _townTimer, _nightTimer = 20f, _lastGrassFire = -10f;
        private bool _built, _anyClip;

        // what the last things were — read by the tests and the log
        public string LastStepSurface { get; private set; } = "";
        public AudioClip LastStepClip { get; private set; }
        public string LastPlayed { get; private set; } = "";
        public int SourceCount => (_layers?.Length ?? 0) + (_voices?.Length ?? 0) + (_fires?.Length ?? 0) + (_torch != null ? 1 : 0);
        public IReadOnlyList<SoundZone> Zones => _zones;
        /// <summary>The loudness the layer is heading to / is at now (0…1, before the sound's own volume).</summary>
        public float LayerTarget(int layer) => _target[layer];
        public float LayerLevel(int layer) => _level[layer];
        public int PlayedCount(string id) => _played.TryGetValue(id, out var n) ? n : 0;

        public void Configure(GameSession session, HeroController hero, SoundRegistry sounds, SoundZone[] zones)
        {
            bool live = Application.isPlaying && isActiveAndEnabled; // the scene builder (edit mode) only stores the references
            if (live) OnDisable();
            _session = session;
            _hero = hero;
            _sounds = sounds;
            _zones = zones ?? new SoundZone[0];
            _built = false;
            if (live) OnEnable();
        }

        /// <summary>For tests and tools: the places can be replaced after the scene is built.</summary>
        public void SetZones(SoundZone[] zones) => _zones = zones ?? new SoundZone[0];

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;
            _defs.Clear();
            _anyClip = false;
            if (_sounds != null)
                foreach (var d in _sounds.Sounds)
                {
                    if (d == null || string.IsNullOrEmpty(d.Id)) continue;
                    _defs[d.Id] = d;
                    if (d.Clips != null && d.Clips.Length > 0) _anyClip = true;
                }

            if (_layers == null) // the pool is made once: the number of sources never changes
            {
                _layers = new AudioSource[AmbientMix.Count];
                for (int i = 0; i < _layers.Length; i++) _layers[i] = MakeSource("Layer_" + AmbientMix.Ids[i], false, true);
                _voices = new AudioSource[OneShotVoices];
                for (int i = 0; i < _voices.Length; i++) _voices[i] = MakeSource("Voice_" + i, false, false);
                _fires = new AudioSource[FireVoices];
                for (int i = 0; i < _fires.Length; i++) _fires[i] = MakeSource("Fire_" + i, true, true);
                _torch = MakeSource("Torch", true, true);
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                var d = Def(AmbientMix.Ids[i]);
                if (d != null && d.Clips.Length > 0) _layers[i].clip = d.Clips[UnityEngine.Random.Range(0, d.Clips.Length)];
            }
            if (!_anyClip) ZdLog.Warn("Audio", "no clips in the sound registry (Assets/ThirdParty is not here): the game is silent");
        }

        private AudioSource MakeSource(string name, bool spatial, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.volume = 0f;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 3f;
            s.maxDistance = 35f;
            return s;
        }

        private void OnEnable()
        {
            if (_session == null) return;
            Build();
            OnDisable(); // never twice
            var e = _session.Events;
            e.StateReady += OnStateReady;
            e.HeroStep += OnHeroStep;
            e.HeroActed += OnHeroActed;
            e.PickedUp += OnPickedUp;
            e.HeroStruck += OnHeroStruck;
            e.Enemy += OnEnemy;
            e.Looted += OnLooted;
            e.Placed += OnPlaced;
            e.UsedOnWorld += OnUsed;
            e.Traded += OnTraded;
            e.WindowOpened += OnWindow;
            e.WindowClosed += OnWindow;
            e.TimeJumped += OnTimeJumped;
            e.World += OnWorld;
            e.HeroDown += OnHeroDown;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            var e = _session.Events;
            e.StateReady -= OnStateReady;
            e.HeroStep -= OnHeroStep;
            e.HeroActed -= OnHeroActed;
            e.PickedUp -= OnPickedUp;
            e.HeroStruck -= OnHeroStruck;
            e.Enemy -= OnEnemy;
            e.Looted -= OnLooted;
            e.Placed -= OnPlaced;
            e.UsedOnWorld -= OnUsed;
            e.Traded -= OnTraded;
            e.WindowOpened -= OnWindow;
            e.WindowClosed -= OnWindow;
            e.TimeJumped -= OnTimeJumped;
            e.World -= OnWorld;
            e.HeroDown -= OnHeroDown;
        }

        private void OnStateReady(GameState g) => _g = g;

        // ------------------------------------------------------------------ the pool

        /// <summary>The sound with that id (empty or not); null and one warning when the registry has no such id.</summary>
        private SoundDef Def(string id)
        {
            if (_defs.TryGetValue(id, out var d)) return d;
            if (_sounds != null && _warned.Add(id)) ZdLog.Warn("Art", $"missing sound {id}");
            return null;
        }

        /// <summary>
        /// Plays one shot of the sound at a world point (or flat when it is not spatial / <paramref name="at"/> is null). Returns the voice that
        /// plays it, or null when there is nothing to play (no clips, or the same sound a moment ago).
        /// </summary>
        public AudioSource Play(string id, Vector3? at = null, float scale = 1f, float maxSeconds = 0f, float dedupSeconds = 0f)
        {
            Build();
            var def = Def(id);
            if (def == null || def.Clips == null || def.Clips.Length == 0) return null;
            float now = Time.unscaledTime;
            if (dedupSeconds > 0f && _lastAt.TryGetValue(id, out var last) && now - last < dedupSeconds) return null;
            _lastAt[id] = now;

            var voice = FreeVoice();
            int n = def.Clips.Length, pick = 0;
            if (n > 1)
            {
                _lastClip.TryGetValue(id, out var prev);
                pick = UnityEngine.Random.Range(0, n - 1);
                if (pick >= prev) pick++; // never the same clip twice in a row
            }
            _lastClip[id] = pick;
            var clip = def.Clips[pick];

            voice.Stop();
            voice.clip = clip;
            bool spatial = def.Spatial && at.HasValue;
            voice.spatialBlend = spatial ? 1f : 0f;
            if (spatial) voice.transform.position = at.Value;
            voice.volume = Mathf.Clamp01(def.Volume * scale);
            voice.pitch = 1f + (def.PitchJitter > 0f ? UnityEngine.Random.Range(-def.PitchJitter, def.PitchJitter) : 0f);
            voice.Play();
            if (maxSeconds > 0f) voice.SetScheduledEndTime(AudioSettings.dspTime + maxSeconds);

            _played[id] = PlayedCount(id) + 1;
            LastPlayed = id;
            LastStepClip = id.StartsWith("step_", StringComparison.Ordinal) ? clip : LastStepClip;
            return voice;
        }

        private AudioSource FreeVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                int k = (_nextVoice + i) % _voices.Length;
                if (!_voices[k].isPlaying) { _nextVoice = (k + 1) % _voices.Length; return _voices[k]; }
            }
            var steal = _voices[_nextVoice]; // all busy: the oldest goes
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return steal;
        }

        private Vector3 HeroPoint => _hero != null ? _hero.transform.position : Vector3.zero;
        private static Vector3 Ground(ZeldaDaughter.Core.Common.Vec2 v) => new Vector3(v.X, 0f, v.Y);

        // ------------------------------------------------------------------ steps

        /// <summary>The sound id of the footstep on that spot: a stone or wood zone, else the terrain (grass, road, mud, water; anything else is bare ground).</summary>
        public string SurfaceAt(Vector3 p, string terrain)
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                var z = _zones[i];
                if (z.Kind == "wood" && z.Contains(p)) return "wood";
                if (z.Kind == "stone" && z.Contains(p)) return "stone";
            }
            switch (terrain)
            {
                case "grass": case "road": case "mud": case "water": return terrain;
                default: return "ground";
            }
        }

        /// <summary>Where the terrain under a foot comes from; null — the hero's own (<c>HeroController.CurrentTerrain</c>). A seam for tests.</summary>
        public Func<Vector3, string> TerrainSource { get; set; }

        private void OnHeroStep(Vector3 position, bool limping)
        {
            string terrain = TerrainSource != null ? TerrainSource(position) : _hero != null ? _hero.CurrentTerrain : "grass";
            string surface = SurfaceAt(position, terrain);
            Play("step_" + surface, position, limping ? 0.75f : 1f);
            if (surface != LastStepSurface)
                ZdLog.Info("Audio", $"step {surface}{(LastStepClip != null ? " " + LastStepClip.name : " (silent)")}");
            LastStepSurface = surface;
        }

        /// <summary>Steps taken from outside HeroView (tests, other views): the same path as the event.</summary>
        public void Step(Vector3 position, bool limping = false) => OnHeroStep(position, limping);

        // ------------------------------------------------------------------ actions

        private void OnHeroActed(HeroAct act)
        {
            var at = HeroPoint;
            switch (act.Kind)
            {
                case HeroActKind.Strike: Play("swing", at, 1f, 0f, StrideDedupSeconds); break; // the swing (D-26: quiet, its own sound; the hit is louder, the miss is another one)
                case HeroActKind.Pickup: Play("act_pickup", at, 1f, 0f, StrideDedupSeconds); break;
                case HeroActKind.Eat: Play("act_eat", at, 1f, 0f, StrideDedupSeconds); break;
                case HeroActKind.Treat: Play("act_treat", at, 1f, 0f, StrideDedupSeconds); break;
                case HeroActKind.Butcher: Play("act_butcher", at, 1f, 0f, StrideDedupSeconds); break;
                case HeroActKind.Place: Play("act_place", at, 1f, 0f, StrideDedupSeconds); break;
                case HeroActKind.Craft: Play("act_craft", at, 1f, 0f, StrideDedupSeconds); break;
            }
        }

        private void OnPickedUp(string objectId, string itemId) => Play("act_pickup", HeroPoint, 1f, 0f, StrideDedupSeconds);
        private void OnLooted(string carcassId, ZeldaDaughter.Core.Loot.LootResult r) => Play("act_butcher", HeroPoint, 1f, 0f, StrideDedupSeconds);
        private void OnPlaced(ZeldaDaughter.Core.World.PlacedObject o) => Play("act_place", Ground(o.Position), 1f, 0f, StrideDedupSeconds);
        private void OnUsed(string objectId, UseResult r) { if (r.Outcome == UseOutcome.Done || r.Outcome == UseOutcome.Refueled) Play("act_place", HeroPoint, 1f, 0f, StrideDedupSeconds); }
        private void OnTraded(ZeldaDaughter.Core.Economy.TradeResult r) { if (r.Outcome == ZeldaDaughter.Core.Economy.TradeOutcome.Done) Play("act_trade"); }
        private void OnWindow(string id) => Play("ui_paper", null, 1f, 0f, StrideDedupSeconds);
        private void OnTimeJumped(double hours) { if (hours >= 1.0) Play("act_sleep"); }
        private void OnHeroDown(bool down) { if (down) Play("hit_hero", HeroPoint); }

        private string DefOf(string enemyId)
        {
            var e = _g?.Enemies.Get(enemyId);
            if (e != null) return e.DefId;
            return enemyId.Contains("wolf") ? "wolf" : enemyId.Contains("boar") ? "boar" : enemyId;
        }

        private Vector3 EnemyPoint(string enemyId)
        {
            var e = _g?.Enemies.Get(enemyId);
            return e != null ? Ground(e.Position) : HeroPoint;
        }

        private void OnHeroStruck(string enemyId, StrikeResult r)
        {
            var at = EnemyPoint(enemyId);
            if (r.Outcome == StrikeOutcome.Miss) { Play("hit_miss", at); return; }   // D-26: the miss has its own sound (after the swing)
            if (r.Outcome != StrikeOutcome.Hit) return;
            bool fists = _g == null || _g.WeaponInHand == WeaponSettings.Fists;
            Play(fists ? "hit_fists" : "hit_blade", at);
            Play("hit_" + DefOf(enemyId), at);
            if (r.Killed) Play("kill", at);   // D-26: the killing blow is heard apart from an ordinary hit
        }

        private void OnEnemy(EnemyNotice n)
        {
            switch (n.Event.Kind)
            {
                case EnemyEventKind.WindupStarted: Play("windup_" + DefOf(n.EnemyId), EnemyPoint(n.EnemyId)); break;
                case EnemyEventKind.Struck: Play("strike_" + DefOf(n.EnemyId), EnemyPoint(n.EnemyId)); Play("hit_hero", HeroPoint); break;
                case EnemyEventKind.Dodged: Play("hit_miss", EnemyPoint(n.EnemyId)); break;
                case EnemyEventKind.Died: Play("hit_" + DefOf(n.EnemyId), EnemyPoint(n.EnemyId), 1f, 0f, StrideDedupSeconds); break;
            }
        }

        private void OnWorld(WorldEvent e)
        {
            if (e.Kind != WorldEventKind.GrassIgnited) return;
            if (Time.unscaledTime - _lastGrassFire < 0.5f) return;
            _lastGrassFire = Time.unscaledTime;
            Play("fire_grass", Ground(e.Position), 1f, 3f);
        }

        // ------------------------------------------------------------------ ambience, fire, town

        private void Update()
        {
            if (!_built) return;
            float dt = Time.unscaledDeltaTime;
            var p = HeroPoint;
            float town = 0f, field = 0f, river = 0f;
            for (int i = 0; i < _zones.Length; i++)
            {
                var z = _zones[i];
                switch (z.Kind)
                {
                    case "town": town = Mathf.Max(town, z.Weight(p)); break;
                    case "field": field = Mathf.Max(field, z.Weight(p)); break;
                    case "river": river = Mathf.Max(river, z.Weight(p)); break;
                }
            }
            float daylight = _g != null ? _g.Clock.Daylight : 1f;
            bool raining = _g != null && _g.Nature.Weather.IsRaining;
            AmbientMix.Targets(town, field, river, daylight, raining, _target);

            float step = dt / AmbientFadeSeconds;
            for (int i = 0; i < AmbientMix.Count; i++)
            {
                _level[i] = Mathf.MoveTowards(_level[i], _target[i], step);
                var s = _layers[i];
                var def = Def(AmbientMix.Ids[i]);
                if (def == null || s.clip == null) continue;
                s.volume = _level[i] * def.Volume;
                if (_level[i] > 0.002f) { if (!s.isPlaying) s.Play(); }
                else if (s.isPlaying) s.Stop();
            }

            _fireTimer -= dt;
            if (_fireTimer <= 0f) { _fireTimer = FireRefreshSeconds; RefreshFires(p); }
            TickTown(dt, town, daylight);
            TickNight(dt, town, daylight);
        }

        private void RefreshFires(Vector3 hero)
        {
            var def = Def("fire_campfire");
            int found = 0;
            if (_g != null && def != null && def.Clips.Length > 0)
            {
                // the FireVoices nearest lit fires within reach: pick the nearest not yet chosen, FireVoices times
                var list = _g.Camp.Campfires;
                for (int k = 0; k < FireVoices; k++)
                {
                    int best = -1;
                    float bestD = _fireMaxDistance;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!list[i].IsLit || Chosen(i, found)) continue;
                        float d = (Ground(list[i].Position) - hero).magnitude;
                        if (d <= bestD) { bestD = d; best = i; }
                    }
                    if (best < 0) break;
                    _fireOrder[found++] = best;
                }
            }
            for (int i = 0; i < _fires.Length; i++)
            {
                var s = _fires[i];
                if (i < found)
                {
                    var f = _g.Camp.Campfires[_fireOrder[i]];
                    Loop(s, def, Ground(f.Position), f.Light);
                }
                else Loop(s, null, Vector3.zero, 0f);
            }
            bool torch = _g != null && _g.Bag.Count("torch") > 0;
            Loop(_torch, torch ? Def("fire_torch") : null, hero, 1f);
        }

        private bool Chosen(int index, int count)
        {
            for (int i = 0; i < count; i++) if (_fireOrder[i] == index) return true;
            return false;
        }

        private static void Loop(AudioSource s, SoundDef def, Vector3 at, float scale)
        {
            if (def == null || def.Clips.Length == 0 || scale <= 0.001f) { if (s.isPlaying) s.Stop(); s.volume = 0f; return; }
            if (s.clip != def.Clips[0]) { s.Stop(); s.clip = def.Clips[0]; }
            s.transform.position = at;
            s.volume = def.Volume * scale;
            if (!s.isPlaying) s.Play();
        }

        private static readonly string[] NightShots = { "amb_night_wolf", "amb_night_owl" };

        /// <summary>Whether the night calls (wolf, owl) are heard: dark enough and not in the town.</summary>
        public static bool NightCalls(float town, float daylight) => town < 0.5f && AmbientMix.Day(daylight) < 0.2f;

        /// <summary>In the wild at night: a wolf howling or an owl, far around (18…30 m), every 25…60 seconds.</summary>
        private void TickNight(float dt, float town, float daylight)
        {
            if (!NightCalls(town, daylight)) return;
            _nightTimer -= dt;
            if (_nightTimer > 0f) return;
            _nightTimer = UnityEngine.Random.Range(25f, 60f);
            var dir = UnityEngine.Random.insideUnitCircle.normalized;
            var at = HeroPoint + new Vector3(dir.x, 0f, dir.y) * UnityEngine.Random.Range(18f, 30f);
            Play(NightShots[UnityEngine.Random.Range(0, NightShots.Length)], at);
        }

        private static readonly string[] TownShots = { "amb_town_smith", "amb_town_cart", "amb_town_life" };

        /// <summary>In the town by day: now a hammer on the anvil, now a cart, now a hen — from somewhere around, every 6…14 seconds.</summary>
        private void TickTown(float dt, float town, float daylight)
        {
            if (town < 0.5f || daylight < 0.3f) return;
            _townTimer -= dt;
            if (_townTimer > 0f) return;
            _townTimer = UnityEngine.Random.Range(6f, 14f);
            var dir = UnityEngine.Random.insideUnitCircle.normalized;
            var at = HeroPoint + new Vector3(dir.x, 0f, dir.y) * UnityEngine.Random.Range(10f, 24f);
            Play(TownShots[UnityEngine.Random.Range(0, TownShots.Length)], at);
        }
    }
}
