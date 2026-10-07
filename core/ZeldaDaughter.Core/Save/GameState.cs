#nullable enable
using System;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Language;
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

        /// <summary>Hero position on the ground (x, z) and height, and the zone (scene) they are in.</summary>
        public Vec2 HeroPosition { get; set; }
        public float HeroHeight { get; set; }
        public float HeroFacingDegrees { get; set; }
        public string Zone { get; set; } = "";
    }
}
