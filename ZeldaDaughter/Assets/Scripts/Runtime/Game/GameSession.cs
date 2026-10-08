using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// One playthrough in a scene (docs/demo/unity-architecture.md §2): the only owner of the core's GameState. Loads and saves it, steps it
    /// every frame in one fixed order (§2.3), forwards what the core reports to <see cref="SessionEvents"/>, and routes taps to the handler
    /// of the target's kind. No game logic lives here: the rules are in the core, the looks are in the presenters.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        /// <summary>The world moves in fixed steps, whatever the frame rate (§2.3 step 4).</summary>
        public const float WorldStepSeconds = 0.25f;
        /// <summary>NPC slots are checked twice a second (§2.3 step 6).</summary>
        public const float NpcSyncSeconds = 0.5f;
        /// <summary>Enemies think in fixed steps (§2.3 step 5); the view smooths what it draws.</summary>
        public const float EnemyStepSeconds = 0.05f;
        /// <summary>A long frame (a hitch, the editor paused) does not become a long step of the world.</summary>
        public const float MaxFrameSeconds = 0.1f;

        [SerializeField] private HeroController _hero;
        [SerializeField] private SunController _sun;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WorldIndex _index;
        [SerializeField] private string _zone;
        [SerializeField] private int _seed = 20261008;
        [SerializeField] private string _slot; // file name of the save slot; empty — this scene does not save (test scenes)

        private GameState _state;
        private Remarks _remarks;
        private readonly Action<Tappable>[] _handlers = new Action<Tappable>[Enum.GetValues(typeof(TapKind)).Length];
        private Action<string> _replyHandler;
        private float _autosaveLeft;
        private float _remarkCheckLeft;
        private float _worldAcc;
        private float _enemyAcc;
        private readonly StepReport _report = new StepReport();
        private readonly List<EnemyNotice> _notices = new List<EnemyNotice>(8);
        private float _npcSyncLeft;
        private bool _saveLocked; // the slot holds a save we refused to load: never overwrite it this session

        public GameState State => _state;
        public SessionEvents Events { get; } = new SessionEvents();
        public SessionUI UI => _ui;
        public WorldIndex Index => _index;
        public RollStreams Rolls { get; private set; }
        public HeroController Hero => _hero;

        /// <summary>The place of rest that overrides the campfire one: <see cref="RestKind.Tavern"/> while the hero sleeps or sits in the tavern; None — by the fire if any.</summary>
        public RestKind RestOverride { get; set; }

        /// <summary>Tests only: a folder for save slots instead of persistentDataPath, so tests never touch the real save.
        /// Reset on every Play Mode start.</summary>
        public static string SaveRootOverride { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SaveRootOverride = null;

        public static string SaveRoot => SaveRootOverride ?? Application.persistentDataPath;
        public static string SlotPathFor(string slot) => Path.Combine(SaveRoot, slot + ".json");

        /// <summary>This session's slot file, or null when the scene does not save.</summary>
        public string SlotPath => string.IsNullOrEmpty(_slot) ? null : SlotPathFor(_slot);

        public void Configure(HeroController hero, SunController sun, SessionUI ui, WorldIndex index, string zone, string slot)
        {
            _hero = hero;
            _sun = sun;
            _ui = ui;
            _index = index;
            _zone = zone;
            _slot = slot;
        }

        // ------------------------------------------------------------------ life

        private void Awake()
        {
            Rolls = new RollStreams(_seed);
            _state = NewState();
            _autosaveLeft = _state.Data.Session.AutosaveSeconds;
            Events.Skill += OnSkill;
            Events.BagChanged += OnBagChanged;
            Events.Enemy += OnEnemyNotice;
            _hero.SetSpeedSource(() => _state.SpeedMultiplier);
            _ui.Bind(Events);
            Load();
        }

        private void Start()
        {
            ZdLog.Info("Session", $"ready zone={_zone} day={_state.Clock.Day}");
            Events.RaiseStateReady(_state);
            OnBagChanged("start");
            SyncNpcs(true);
        }

        private void OnDestroy()
        {
            Events.Skill -= OnSkill;
            Events.BagChanged -= OnBagChanged;
            Events.Enemy -= OnEnemyNotice;
        }

        /// <summary>A fresh state registered with this scene and wired to the bus. Registration goes before a save is loaded (grass cells take their saved state as they are added).</summary>
        private GameState NewState()
        {
            var data = GameData.Current;
            var g = new GameState(data) { Zone = _zone };
            _remarks = new Remarks(data.Remarks);
            _index.RegisterInto(g);
            g.Map.MarkOpened += Events.RaiseMarkOpened;
            g.Notebook.EntryAdded += Events.RaiseNoteAdded;
            g.Quests.Done += Events.RaiseQuestDone;
            g.Carcasses.Disappeared += Events.RaiseCarcassGone;
            return g;
        }

        private void OnApplicationPause(bool paused) { if (paused) Save("pause"); }
        private void OnApplicationQuit() => Save("quit");

        // ------------------------------------------------------------------ the tick (§2.3)

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxFrameSeconds);
            var g = _state;
            var data = g.Data;

            var p = _hero.transform.position;
            g.HeroPosition = new Vec2(p.x, p.z);
            g.HeroHeight = p.y;
            g.HeroFacingDegrees = _hero.transform.eulerAngles.y;

            StepCore(dt, _hero.TakeWalked());
            StepWorld(dt);
            StepEnemies(dt);
            if (_sun != null) _sun.Apply(g.Clock.Daylight, g.Clock.TimeOfDay >= 0.5);

            _npcSyncLeft -= dt;
            if (_npcSyncLeft <= 0f)
            {
                _npcSyncLeft = NpcSyncSeconds;
                SyncNpcs(false);
            }

            _remarkCheckLeft -= dt;
            if (_remarkCheckLeft <= 0f)
            {
                _remarkCheckLeft = data.Session.RemarkCheckSeconds;
                g.Hints.Set("tappable_nearby", TappableNearby(p));
                g.Hints.Set("has_item", g.Bag.UsedSlots > 0);
                bool overloaded = g.Bag.IsOverloaded(g.Skills.CapacityMultiplier());
                var topics = _remarks.ConditionTopics(g.Condition, g.Hunger, overloaded, g.NightWithoutFire,
                    wolfClose: g.PredatorNear(data.Session.PredatorFearMeters), raining: g.Nature.Weather.IsRaining, torchDying: g.Torch.IsLit && g.Torch.Light < 1f);
                if (topics.Count > 0) SayFirst(topics);
            }
            var hint = g.Hints.Visible;
            _ui.ShowHint(hint != null ? g.Hints.TextOf(hint) : null);

            _autosaveLeft -= dt;
            if (_autosaveLeft <= 0f)
            {
                _autosaveLeft = data.Session.AutosaveSeconds;
                Save("timer");
            }
        }

        /// <summary>§2.3 step 3: the clock, the hero's condition (rest by a fire, or in the tavern by <see cref="RestOverride"/>), hunger, the fight's position and cooldown, the skills that grow by walking — one core call, <c>g.Step</c>.</summary>
        private void StepCore(float dt, float walkedMeters)
        {
            _state.Step(dt, walkedMeters, RestOverride, _report);
            var clock = _report.ClockEvents;
            for (int i = 0; i < clock.Count; i++) Events.RaiseClock(clock[i]);
            var cond = _report.ConditionEvents;
            for (int i = 0; i < cond.Count; i++) Events.RaiseCondition(cond[i]);
            var skills = _report.SkillChanges;
            for (int i = 0; i < skills.Count; i++) Events.RaiseSkill(skills[i]);
        }

        /// <summary>§2.3 step 4: weather, wind, grass, fires, night wolves — in steps of <see cref="WorldStepSeconds"/>, with a roll from the world's own stream.</summary>
        private void StepWorld(float dt)
        {
            _worldAcc += dt;
            while (_worldAcc >= WorldStepSeconds)
            {
                _worldAcc -= WorldStepSeconds;
                var events = _state.TickWorld(WorldStepSeconds, Rolls.World.Next());
                for (int i = 0; i < events.Count; i++)
                {
                    Events.RaiseWorld(events[i]);
                    if (events[i].Kind == WorldEventKind.TorchBurntOut)
                    {
                        ZdLog.Info("Nature", "torch_burnt_out");
                        BagChanged("torch burnt out");   // the torch left the bag and a burnt stick took its place: the light, the bag window follow
                    }
                }
            }
        }

        /// <summary>§2.3 step 5: the enemies of the core (<c>g.Enemies</c>: spawns, night wolves, deaths) in steps of <see cref="EnemyStepSeconds"/>, with a roll from the enemies' stream.</summary>
        private void StepEnemies(float dt)
        {
            _enemyAcc += dt;
            while (_enemyAcc >= EnemyStepSeconds)
            {
                _enemyAcc -= EnemyStepSeconds;
                _state.Enemies.Tick(EnemyStepSeconds, Rolls.Enemies.Next(), _notices);
                for (int i = 0; i < _notices.Count; i++) Events.RaiseEnemy(_notices[i]);
            }
        }

        private void SyncNpcs(bool snap)
        {
            var changes = _state.Npcs.Sync();
            for (int i = 0; i < changes.Count; i++) Events.RaiseNpcMoved(changes[i], snap);
        }

        private bool TappableNearby(Vector3 heroPos)
        {
            var session = _state.Data.Session;
            var list = _index.Tappables;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.Enabled || (t.Kind != TapKind.Npc && t.Kind != TapKind.Pickup) || !t.gameObject.activeInHierarchy) continue;
                if (session.IsNear(Vector3.Distance(t.transform.position, heroPos))) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ what the presenters call

        /// <summary>The player tapped a scene object (from a touch, or a test): the handler of its kind acts.</summary>
        public void Tap(string objectId)
        {
            _state.Hints.Did("tap");
            var t = _index.FindTappable(objectId);
            if (t == null || !t.Enabled || !t.gameObject.activeInHierarchy) return;
            _handlers[(int)t.Kind]?.Invoke(t);
        }

        /// <summary>A presenter takes the taps of one kind (in Start); several handlers on one kind all run.</summary>
        public void OnTap(TapKind kind, Action<Tappable> handler) => _handlers[(int)kind] += handler;

        /// <summary>The talk presenter takes the hero's answers.</summary>
        public void SetReplyHandler(Action<string> handler) => _replyHandler = handler;

        /// <summary>The hero answered with an icon (reply button, or a test) → the talk presenter.</summary>
        public void Reply(string icon) => _replyHandler?.Invoke(icon);

        /// <summary>The hero says a line of the topic, if its pauses allow. False if it stayed silent.</summary>
        public bool Say(string topic)
        {
            var line = _remarks.Say(topic, Time.time, Rolls.Remarks.Next);
            if (line == null) return false;
            Spoke(topic, line);
            return true;
        }

        private void SayFirst(IReadOnlyList<string> topics)
        {
            var line = _remarks.SayFirst(topics, Time.time, Rolls.Remarks.Next);
            if (line == null) return;
            string said = topics[0];
            var lines = _state.Data.Remarks.Topics;
            for (int i = 0; i < topics.Count; i++)
                if (lines.TryGetValue(topics[i], out var t) && t.Lines.Contains(line)) { said = topics[i]; break; }
            Spoke(said, line);
        }

        private void Spoke(string topic, string line)
        {
            Events.RaiseHeroSaid(topic, line);
            ZdLog.Info("Remark", $"{topic}: {line}");
        }

        /// <summary>The bag changed (picked, crafted, eaten, traded): everyone who shows or reads it is told.</summary>
        public void BagChanged(string why) => Events.RaiseBagChanged(why);

        /// <summary>A wolf ran from the fire while she is near: she remarks on it (D-23).</summary>
        private void OnEnemyNotice(EnemyNotice n)
        {
            if (n.Event.Kind != EnemyEventKind.Frightened) return;
            var e = _state.Enemies.Get(n.EnemyId);
            if (e != null && (e.Position - _state.HeroPosition).Length <= _state.Data.Session.PredatorFearMeters) Say(Topics.WolfFlees);
        }

        private void OnBagChanged(string why) => _state.Hints.Set("has_item", _state.Bag.UsedSlots > 0);

        private void OnSkill(SkillChange c)
        {
            if (c.Weapon == null && c.NewTier > c.OldTier) Say(Topics.Skill(c.Stat, c.NewTier));
        }

        /// <summary>
        /// Time passes at once (sleep, a long knockout): <paramref name="jump"/> moves the core's clock (<c>g.Sleep</c>, <c>g.SkipHours</c>) and returns its
        /// events; the bus gets them, then <c>TimeJumped</c>, and the NPCs are put at their new places without walking.
        /// </summary>
        public void JumpTime(Func<IReadOnlyList<ClockEvent>> jump, double hours)
        {
            var events = jump();
            for (int i = 0; i < events.Count; i++) Events.RaiseClock(events[i]);
            Events.RaiseTimeJumped(hours);
            SyncNpcs(true);
        }

        // ------------------------------------------------------------------ saving

        /// <summary>One save slot, written atomically (core SaveGame). Never throws: a failed write is logged.</summary>
        public void Save(string reason)
        {
            string path = SlotPath;
            if (path == null) return;
            if (_saveLocked) { ZdLog.Warn("Save", $"not saved ({reason}): the slot holds a save this game could not load"); return; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                SaveGame.WriteAtomic(path, SaveGame.Capture(_state));
                ZdLog.Info("Save", $"saved {reason}");
            }
            catch (Exception e)
            {
                ZdLog.Error("Save", $"write failed ({reason}): {e.GetType().Name}: {e.Message}");
            }
        }

        private void Load()
        {
            string path = SlotPath;
            if (path == null) return;
            try
            {
                var outcome = SaveGame.Load(_state, path, out var problem);
                if (outcome == LoadOutcome.NoSave) return;
                if (outcome == LoadOutcome.Rejected) { Refuse(problem); return; }
            }
            catch (Exception e)
            {
                Refuse($"{e.GetType().Name}: {e.Message}");
                return;
            }
            if (_state.Zone == _zone)
                _hero.Teleport(new Vector3(_state.HeroPosition.X, _state.HeroHeight, _state.HeroPosition.Y), _state.HeroFacingDegrees);
            _state.Zone = _zone;
            ZdLog.Info("Save", $"loaded day={_state.Clock.Day} t={_state.Clock.TimeOfDay:0.000}");
        }

        /// <summary>A save exists but cannot be used: play from a fresh state and leave the slot alone.</summary>
        private void Refuse(string problem)
        {
            _saveLocked = true;
            _state = NewState();
            ZdLog.Error("Save", $"slot refused, playing without saving: {problem}");
        }
    }
}
