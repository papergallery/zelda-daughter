#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Dialogue;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Language;
using ZeldaDaughter.Core.Movement;
using ZeldaDaughter.Core.Loot;
using ZeldaDaughter.Core.Npcs;
using ZeldaDaughter.Core.Onboarding;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Save
{
    /// <summary>Everything the core knows about one playthrough — what one save slot holds (C-13).</summary>
    public sealed class GameState
    {
        public GameState(DataSet data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Clock = new WorldClock(data.World);
            Condition = new HeroCondition(data.Wounds);
            Hunger = new Hunger(data.Hunger);
            Skills = new Skills(data.Skills);
            Bag = new Bag(data.Inventory, data.Items);
            Crafting = new Crafting.Crafting(data);
            Language = new Comprehension(data.Language);
            Hints = new Hints(data.Onboarding);
            Npcs = new NpcRoster(data.Npcs, Clock);
            // endurance and hunger scale recovery wherever it happens (natural regeneration, food) — D-01
            Condition.HealScale = () => Skills.HealMultiplier() * Hunger.Multiplier;
            Trade = new Trade(data.Traders, data.Items, Bag, Npcs.IsShopOpen, Language);
            Map = new MapKnowledge(data.Map, () => Bag.Count(MapKnowledge.MapItem) > 0);
            Notebook = new Notebook(data.Notebook);
            Quests = new Quests(data.Quests, Bag, Notebook, Map);
            Carcasses = new Carcasses(data.Enemies, Bag);
            Camp = new Camp(data, Bag, Crafting);
            Nature = new Nature(data.Elements, data.Night, data.Movement.Terrain.TryGetValue("mud", out var mud) ? mud : 1f);
            Combat = new HeroCombat(data.Weapons, Skills, Condition, Hunger, Bag);
        }

        public DataSet Data { get; }
        public WorldClock Clock { get; }
        public HeroCondition Condition { get; }
        public Hunger Hunger { get; }
        public Skills Skills { get; }
        public Bag Bag { get; }
        public Crafting.Crafting Crafting { get; }
        public Comprehension Language { get; }
        public Hints Hints { get; }
        /// <summary>Where the NPCs are by the clock (D-02). Nothing to save: the slot is a function of the time.</summary>
        public NpcRoster Npcs { get; }
        /// <summary>Barter and coins with the shop NPCs (D-03); a shop deals only while its NPC is at the counter.</summary>
        public Trade Trade { get; }
        /// <summary>Things placed on the ground and campfires (D-06): place, use an item on them, rest and light.</summary>
        public Camp Camp { get; }
        /// <summary>Weather, wind, grass fire, mud and night predators (D-06); the scene registers its cells, zones and mud ground at start.</summary>
        public Nature Nature { get; }

        /// <summary>Resting by a lit campfire (the zone around it): pass to <c>Condition.Tick(dt, g.CurrentRest())</c>; in the tavern the view passes <c>RestKind.Tavern</c>. Needs <c>HeroPosition</c>.</summary>
        public RestKind CurrentRest() => Camp.IsLitNear(HeroPosition, Data.Camp.RestRadius) ? RestKind.Campfire : RestKind.None;

        /// <summary>Night, no fire in reach and no torch in the bag — the hero may remark on the dark (remarks.json night_no_fire).</summary>
        public bool NightWithoutFire => Data.Session.IsNight(Clock.Daylight) && Bag.Count("torch") == 0 && !Camp.IsLitNear(HeroPosition, Data.Camp.LightRadius);

        /// <summary>
        /// One frame of the hero's own state (the view's tick, docs/demo/unity-architecture.md §2.3): the clock moves, the wounds tick
        /// (resting at a lit campfire, or <paramref name="restOverride"/> when it is not <c>None</c> — the tavern), hunger grows, her blows
        /// follow her position and cool down, and the metres she <paramref name="walkedMeters"/> train endurance (and carrying, when overloaded).
        /// Needs <c>HeroPosition</c> set for this frame. <paramref name="into"/> is cleared and filled. The world, enemies and NPCs are ticked separately.
        /// </summary>
        public void Step(float dt, float walkedMeters, RestKind restOverride, StepReport into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (dt <= 0f) return;
            Clock.Advance(dt, into.ClockEvents);
            Condition.Tick(dt, restOverride != RestKind.None ? restOverride : CurrentRest(), into.ConditionEvents);
            Hunger.Advance(dt);
            Combat.Position = HeroPosition;
            Combat.Tick(dt);
            if (walkedMeters > 0f && !Condition.IsKnockedOut)
                Skills.Apply(SkillEvent.Walked(walkedMeters, Bag.IsOverloaded(Skills.CapacityMultiplier())), into.SkillChanges);
        }

        float _scorchCooldown;

        /// <summary>
        /// The world moves on by <paramref name="dt"/> real seconds: campfires burn, rain comes and goes, wind turns, grass catches and burns out,
        /// mud forms and dries, night wolves are called. <paramref name="roll"/> — one random number 0..1 from the view per step. Returns what happened.
        /// </summary>
        public IReadOnlyList<WorldEvent> TickWorld(float dt, double roll)
        {
            var events = _worldEvents;
            events.Clear();
            if (dt <= 0f) return NoWorldEvents;
            _inLight ??= p => Camp.IsLitNear(p, Data.Camp.LightRadius);   // one delegate for good: a frame allocates nothing (C8)
            Nature.Tick(dt, roll, Camp.Campfires, HeroPosition, Clock.Daylight, _inLight, events);
            var burnt = Camp.Tick(dt, Nature.Weather.IsRaining);
            for (int i = 0; i < burnt.Count; i++) events.Add(burnt[i]);
            _scorchCooldown = Math.Max(0f, _scorchCooldown - dt);
            if (_scorchCooldown <= 0f && Nature.Grass.BurningNear(HeroPosition, Data.Elements.Grass.BurnRadius))
            {
                Condition.Wound(WoundType.Burn, Data.Elements.Grass.ScorchSeverity);
                _scorchCooldown = Data.Elements.Grass.ScorchCooldownSeconds;
                events.Add(new WorldEvent(WorldEventKind.HeroScorched, "", HeroPosition));
            }
            return events.Count == 0 ? NoWorldEvents : events.ToArray();   // a quiet frame: the shared empty list
        }

        static readonly WorldEvent[] NoWorldEvents = new WorldEvent[0];
        readonly List<WorldEvent> _worldEvents = new List<WorldEvent>(8);
        Func<Vec2, bool>? _inLight;

        /// <summary>A burning torch in the bag sets a dry grass cell alight. False without a torch or on ground that cannot burn.</summary>
        public bool IgniteGrass(string cellId) => Bag.Count("torch") > 0 && Nature.Grass.Has(cellId) && Nature.Grass.Ignite(cellId);

        /// <summary>Drag an item onto a world object: a placed thing, a campfire or a burning grass cell (a torch is lit from any fire).</summary>
        public UseResult UseOnWorld(string objectId, string itemId)
        {
            if (Nature.Grass.Has(objectId))
                return Nature.Grass.StateOf(objectId) == GrassState.Burning ? Camp.UseOnKinds(new[] { "fire" }, itemId) : new UseResult(UseOutcome.NoTarget);
            return Camp.Use(objectId, itemId);
        }

        /// <summary>Dead enemies lying in the world; a tap calls <c>Carcasses.Tap(id)</c> (D-05).</summary>
        public Carcasses Carcasses { get; }

        /// <summary>An enemy died: remember the kill (it never comes back) and leave its carcass where it fell. Call once, on <c>EnemyEventKind.Died</c>.</summary>
        public Carcass? EnemyKilled(Enemy enemy)
        {
            Killed.Add(enemy.Id);
            return Carcasses.Spawn(enemy.Id, enemy.DefId, enemy.Position);
        }

        /// <summary>Marks learned by talking; shown only while the hero holds a map (D-04).</summary>
        public MapKnowledge Map { get; }
        /// <summary>The hero's notes (§5) — no statuses.</summary>
        public Notebook Notebook { get; }
        /// <summary>Requests of residents; <c>Give(npc, item)</c> on a drag of an item onto an NPC.</summary>
        public Quests Quests { get; }

        /// <summary>Starts a talk with an NPC; what its nodes do (marks, notes, requests, coin lessons) is applied to this state as they are reached.</summary>
        public Conversation Talk(string npcId) => new Conversation(Data.Dialogues, Language, npcId, ApplyEffect);

        void ApplyEffect(DialogueEffect e)
        {
            switch (e.Type)
            {
                case "mark": Map.Open(e.Id); break;
                case "note": Notebook.Add(e.Id); break;
                case "offer": Quests.Offer(e.Id); break;
                case "teach_coins": Trade.TeachCoins(); break;
            }
        }
        /// <summary>The hero's side of a fight, wired to this state's skills, wounds, hunger and bag (view sets <c>Position</c>).</summary>
        public HeroCombat Combat { get; }

        /// <summary>Hero position on the ground (x, z) and height, and the zone (scene) they are in.</summary>
        public Vec2 HeroPosition { get; set; }
        public float HeroHeight { get; set; }
        public float HeroFacingDegrees { get; set; }
        public string Zone { get; set; } = "";

        /// <summary>World objects the hero took (picked-up items) — they do not come back after a load.</summary>
        public System.Collections.Generic.HashSet<string> Picked { get; } = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        /// <summary>Enemies the hero killed — they do not come back after a load. Living enemies are not saved: they return to their spawn (D-01 decision).</summary>
        public HashSet<string> Killed { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// The product of <see cref="SpeedModifiers"/>, every factor clamped as <c>SpeedModel</c> clamps it: wounds, bag load, hunger, mud.
        /// No enumerator, no allocation — the view reads it every frame (C8). Pass to <c>SpeedModel.Step(…, float modifier, …)</c>.
        /// </summary>
        public float SpeedMultiplier
        {
            get
            {
                var m = Data.Movement;
                return Clamp(Condition.SpeedMultiplier, m) * Clamp(Bag.SpeedMultiplier(Skills.CapacityMultiplier()), m)
                     * Clamp(Hunger.Multiplier, m) * Clamp(Nature.Mud.SpeedAt(HeroPosition), m);
            }
        }

        static float Clamp(float v, MovementSettings m) => Math.Max(m.MultiplierMin, Math.Min(m.MultiplierMax, v));

        readonly float[] _modifiers = new float[4];

        /// <summary>
        /// Walking-speed multipliers for <c>SpeedModel</c>: wounds (limp), bag load, hunger, mud. The same array every call, refreshed
        /// in place (no iterator, no allocation): use it at once, do not keep it.
        /// </summary>
        public IEnumerable<float> SpeedModifiers()
        {
            _modifiers[0] = Condition.SpeedMultiplier;
            _modifiers[1] = Bag.SpeedMultiplier(Skills.CapacityMultiplier());
            _modifiers[2] = Hunger.Multiplier;
            _modifiers[3] = Nature.Mud.SpeedAt(HeroPosition);
            return _modifiers;
        }

        /// <summary>Drag food onto the hero: hunger drops, health rises by the food's heal × recovery scale. Not food — nothing happens.</summary>
        public EatEffect Eat(string itemId)
        {
            if (!Data.Hunger.Food.ContainsKey(itemId) || !Bag.Remove(itemId)) return new EatEffect(false, 0);
            var e = Hunger.Eat(itemId);
            Condition.Heal(e.Heal);
            return e;
        }

        /// <summary>Game hours pass (sleep, a long knockout): the clock and hunger both move. Returns the clock events.</summary>
        public IReadOnlyList<ClockEvent> SkipHours(double hours)
        {
            var ev = Clock.SkipHours(hours);
            if (hours > 0) Hunger.Advance((float)(hours / 24.0 * Data.World.DayLengthSeconds));
            return ev;
        }

        /// <summary>Sleep in a bed (§6): <c>SleepHours</c> pass, wounds and health recover to the sleep thresholds, and the hero wakes hungrier.</summary>
        public IReadOnlyList<ClockEvent> Sleep()
        {
            var ev = SkipHours(Data.World.SleepHours);
            Condition.Sleep();
            return ev;
        }
    }
}
