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

        /// <summary>Walking-speed multipliers for <c>SpeedModel</c>: wounds (limp), bag load, hunger.</summary>
        public IEnumerable<float> SpeedModifiers()
        {
            yield return Condition.SpeedMultiplier;
            yield return Bag.SpeedMultiplier(Skills.CapacityMultiplier());
            yield return Hunger.Multiplier;
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
