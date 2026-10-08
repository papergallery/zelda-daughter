#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Combat;

namespace ZeldaDaughter.Core.Npcs
{
    /// <summary>What an NPC is doing in a schedule slot (project-design.md §2: work and trade by day, tavern in the evening, sleep at night).</summary>
    public enum NpcActivity { Work, Trade, Tavern, Sleep, Stroll }

    /// <summary>data/npcs.json (D-02). Key = NPC id = the key in dialogues.json (and traders.json for shops).</summary>
    public sealed class NpcSettings
    {
        public Dictionary<string, NpcDef> Npcs { get; set; } = new Dictionary<string, NpcDef>();
    }

    public sealed class NpcDef
    {
        public string Name { get; set; } = "";
        /// <summary>The NPC has a shop (a trader in traders.json): open only while the activity is <see cref="NpcActivity.Trade"/>.</summary>
        public bool Shop { get; set; }
        /// <summary>Slots by game hour (0..24), ascending. The last slot runs on past midnight until the first one of the next day.</summary>
        public List<ScheduleEntry> Schedule { get; set; } = new List<ScheduleEntry>();
    }

    public sealed class ScheduleEntry
    {
        /// <summary>Game hour the slot starts (0..24, e.g. 17.5).</summary>
        public double Hour { get; set; }
        /// <summary>Id of the scene object the NPC stands at (tag «anchor» in scenes/region.json); the path is the view's business.</summary>
        public string Anchor { get; set; } = "";
        public string Activity { get; set; } = "";

        public NpcActivity? ParsedActivity => EnumNames.Parse<NpcActivity>(Activity);
    }
}
