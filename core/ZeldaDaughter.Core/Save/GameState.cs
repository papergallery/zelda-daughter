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
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Save
{
    public enum HeroUseOutcome { Ate, Treated, NotUsable }

    /// <summary>What <see cref="GameState.UseOnHero"/> did.</summary>
    public readonly struct HeroUseResult
    {
        public readonly HeroUseOutcome Outcome;
        /// <summary>The hero's remark topic (remarks.json) for <see cref="HeroUseOutcome.NotUsable"/>, else null; the view calls <c>Say(Topic)</c>.</summary>
        public readonly string? Topic;
        /// <summary>Health the food gave (Ate), else 0.</summary>
        public readonly float Heal;
        /// <summary>The meal took the hunger away (she was peckish or worse and now is not): the view calls <c>Say(Topics.Sated)</c> (D-23).</summary>
        public readonly bool Sated;

        public HeroUseResult(HeroUseOutcome outcome, string? topic = null, float heal = 0f, bool sated = false) { Outcome = outcome; Topic = topic; Heal = heal; Sated = sated; }
        public override string ToString() => $"{Outcome} {Topic}".TrimEnd();
    }

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
            Enemies = new EnemyRoster(data.Enemies, Combat, () => WeaponInHand, id => Killed.Contains(id), OnEnemyDied);
            Nature.Predators.Locate = id => Enemies.Get(id)?.Position;
            Nature.Predators.Forbidden = p => Enemies.Blocked != null && Enemies.Blocked(p);
            Torch = new Torch(data.Camp, Bag);
            Enemies.Fire = FireAt;
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

        /// <summary>The hero's torch (D-23): burns down in real time, weakens, goes out and leaves a burnt stick.</summary>
        public Torch Torch { get; }

        /// <summary>
        /// Is there a fire whose fear reaches <paramref name="at"/> (D-23)? A lit campfire (enemies.json fire.campfireRadius) and the hero's
        /// burning torch (fire.torchRadius) scare; both reach less as they die. No allocation. The enemy roster asks it for every animal that fears fire.
        /// </summary>
        public bool FireAt(Vec2 at, out Vec2 source, out float radius)
        {
            var fear = Data.Enemies.Fire;
            var fires = Camp.Campfires;
            for (int i = 0; i < fires.Count; i++)
            {
                var f = fires[i];
                if (!f.IsLit) continue;
                float r = fear.CampfireRadius * f.Light;
                if ((f.Position - at).Length <= r) { source = f.Position; radius = r; return true; }
            }
            if (Torch.IsLit)
            {
                float r = fear.TorchRadius * Torch.Light;
                if ((HeroPosition - at).Length <= r) { source = HeroPosition; radius = r; return true; }
            }
            source = default; radius = 0f;
            return false;
        }

        /// <summary>A living predator (an enemy that attacks on sight — a wolf) within <paramref name="radius"/> metres of the hero: she may say she is afraid (remarks.json wolf_close). No allocation.</summary>
        public bool PredatorNear(float radius)
        {
            var list = Enemies.Active;
            for (int i = 0; i < list.Count; i++)
                if (!list[i].IsCarcass && list[i].Def.AggroOnSight && (list[i].Position - HeroPosition).Length <= radius) return true;
            return false;
        }

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
            if (!NightSeen && Data.Session.IsNight(Clock.Daylight)) NightSeen = true;
            Combat.Position = HeroPosition;
            Combat.Tick(dt);
            if (walkedMeters > 0f && !Condition.IsKnockedOut)
                Skills.Apply(SkillEvent.Walked(walkedMeters, Bag.IsOverloaded(Skills.CapacityMultiplier())), into.SkillChanges);
        }

        /// <summary>
        /// The first real night has come while she was awake (D-23): until then the bed does not let her sleep through it (remarks.json not_sleepy) —
        /// the demo's first night with its wolves is not to be skipped. Saved.
        /// </summary>
        public bool NightSeen { get; set; }

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
            if (Torch.Tick(dt, Nature.Weather.IsRaining) == TorchEvent.BurntOut) events.Add(new WorldEvent(WorldEventKind.TorchBurntOut, "torch", HeroPosition));
            _scorchCooldown = Math.Max(0f, _scorchCooldown - dt);
            if (_scorchCooldown <= 0f && Nature.Grass.BurningNear(HeroPosition, Data.Elements.Grass.BurnRadius))
            {
                Condition.Wound(WoundType.Burn, Data.Elements.Grass.ScorchSeverity);
                _scorchCooldown = Data.Elements.Grass.ScorchCooldownSeconds;
                events.Add(new WorldEvent(WorldEventKind.HeroScorched, "", HeroPosition));
            }
            // the dark calls a wolf / the morning sends it away: the roster follows
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind == WorldEventKind.PredatorSpawned)
                {
                    var wolf = Enemies.Spawn(e.Id, e.Detail, e.Position);
                    if (wolf == null) Nature.Predators.Released(e.Id); else wolf.Hunt();   // the dark wolf comes for her (D-23)
                }
                else if (e.Kind == WorldEventKind.PredatorDespawned) Enemies.Remove(e.Id);
                else if (e.Kind == WorldEventKind.PredatorDismissed) Enemies.Get(e.Id)?.Dismiss();
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

        /// <summary>The living enemies: spawn, tick, strike (C2). Night wolves called by <see cref="TickWorld"/> appear here by themselves.</summary>
        public EnemyRoster Enemies { get; }

        void OnEnemyDied(Enemy enemy)
        {
            EnemyKilled(enemy);
            Nature.Predators.Released(enemy.Id);
        }

        /// <summary>Marks learned by talking; shown only while the hero holds a map (D-04).</summary>
        public MapKnowledge Map { get; }
        /// <summary>The hero's notes (§5) — no statuses.</summary>
        public Notebook Notebook { get; }
        /// <summary>Requests of residents; <c>Give(npc, item)</c> on a drag of an item onto an NPC.</summary>
        public Quests Quests { get; }

        /// <summary>Starts a talk with an NPC; what its nodes do (marks, notes, requests, coin lessons) is applied to this state as they are reached.</summary>
        /// <param name="startNode">«start», or a node of the NPC's own (the thanks after a request: <c>QuestResult.Thanks</c>).</param>
        public Conversation Talk(string npcId, string startNode = "start") => new Conversation(Data.Dialogues, Language, npcId, ApplyEffect, startNode);

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
        /// <summary>
        /// The weapon the hero strikes with: the strongest one in the bag (by <c>damage</c>, a tie goes to the smaller id), else <c>fists</c>.
        /// Anything in <c>weapons.json</c> that is in the bag counts. No allocation.
        /// </summary>
        public string WeaponInHand
        {
            get
            {
                string best = WeaponSettings.Fists;
                float bestDamage = float.NegativeInfinity;
                var stacks = Bag.Stacks;
                for (int i = 0; i < stacks.Count; i++)
                {
                    string id = stacks[i].ItemId;
                    if (id == WeaponSettings.Fists || !Data.Weapons.Weapons.TryGetValue(id, out var w)) continue;
                    if (w.Damage > bestDamage || (w.Damage == bestDamage && string.CompareOrdinal(id, best) < 0)) { best = id; bestDamage = w.Damage; }
                }
                return best;
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

        /// <summary>
        /// Drag an item onto the hero (§6, §7, C4): food is eaten (<see cref="Eat"/>), medicine treats the wound it is for and is spent;
        /// anything else (or medicine with nothing to treat, or a knocked-out hero) changes nothing and gets the remark <c>use_nothing</c>.
        /// </summary>
        public HeroUseResult UseOnHero(string itemId)
        {
            if (Condition.IsKnockedOut || Bag.Count(itemId) < 1) return new HeroUseResult(HeroUseOutcome.NotUsable);
            if (Data.Hunger.Food.ContainsKey(itemId))
            {
                var before = Hunger.Level;
                var e = Eat(itemId);
                return new HeroUseResult(HeroUseOutcome.Ate, null, e.Heal, before != HungerLevel.Fed && Hunger.Level == HungerLevel.Fed);
            }
            if (Condition.Treat(itemId))
            {
                Bag.Remove(itemId);
                return new HeroUseResult(HeroUseOutcome.Treated);
            }
            return new HeroUseResult(HeroUseOutcome.NotUsable, Topics.UseNothing);
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
