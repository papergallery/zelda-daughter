#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Language;
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
