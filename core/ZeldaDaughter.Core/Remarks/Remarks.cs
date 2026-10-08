#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Remarks
{
    /// <summary>data/remarks.json (C-12).</summary>
    public sealed class RemarkSettings
    {
        public float GlobalGapSeconds { get; set; }
        /// <summary>Shares of max HP below which the hero mentions health; wound severity from which a wound is mentioned.</summary>
        public float HealthHurtBelow { get; set; }
        public float HealthBadBelow { get; set; }
        public float HealthCriticalBelow { get; set; }
        public float WoundSpeaksFrom { get; set; }
        public Dictionary<string, RemarkTopic> Topics { get; set; } = new Dictionary<string, RemarkTopic>();
    }

    public sealed class RemarkTopic
    {
        public float Cooldown { get; set; }
        public List<string> Lines { get; set; } = new List<string>();
    }

    /// <summary>Topic ids the code asks for; data must have lines for each (test).</summary>
    public static class Topics
    {
        public const string HealthHurt = "health_hurt", HealthBad = "health_bad", HealthCritical = "health_critical";
        public const string WoundCut = "wound_cut", WoundFracture = "wound_fracture", WoundBurn = "wound_burn", WoundPoison = "wound_poison";
        public const string HungerPeckish = "hunger_peckish", HungerHungry = "hunger_hungry", HungerStarving = "hunger_starving";
        public const string Overload = "overload", NightNoFire = "night_no_fire";
        public const string CraftOk = "craft_ok", CraftFail = "craft_fail", CraftStation = "craft_station", CraftNoRoom = "craft_no_room";
        public const string HintWorldUse = "hint_world_use", PlaceInvalid = "place_invalid", NeedFire = "need_fire";
        public const string ButcherNoKnife = "butcher_no_knife", LootEmpty = "loot_empty";

        public static readonly string[] All =
        {
            HealthHurt, HealthBad, HealthCritical, WoundCut, WoundFracture, WoundBurn, WoundPoison, HungerPeckish, HungerHungry,
            HungerStarving, Overload, NightNoFire, CraftOk, CraftFail, CraftStation, CraftNoRoom, HintWorldUse, PlaceInvalid,
            NeedFire, ButcherNoKnife, LootEmpty,
        };

        public static string Skill(Stat stat, int tier) => $"skill_{Skills.Key(stat)}_{tier}";

        public static string Wound(WoundType t) => "wound_" + HeroCondition.Key(t);
    }

    /// <summary>
    /// What the hero says and when (project-design.md §1: state is read from lines, not bars). Each topic has its own
    /// cooldown, there is a gap between any two lines, and a topic does not repeat its last line.
    /// </summary>
    public sealed class Remarks
    {
        readonly RemarkSettings _s;
        readonly Dictionary<string, double> _lastByTopic = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _lastLine = new Dictionary<string, string>(StringComparer.Ordinal);
        double _lastAny = double.NegativeInfinity;

        public Remarks(RemarkSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        /// <summary>A line for the topic, or null if it is too soon. <paramref name="pick"/> chooses an index 0..n-1 (seeded outside).</summary>
        public string? Say(string topic, double now, Func<int, int> pick)
        {
            if (!_s.Topics.TryGetValue(topic, out var t) || t.Lines.Count == 0) return null;
            if (now - _lastAny < _s.GlobalGapSeconds) return null;
            if (_lastByTopic.TryGetValue(topic, out double last) && now - last < t.Cooldown) return null;
            var options = new List<string>(t.Lines);
            if (options.Count > 1 && _lastLine.TryGetValue(topic, out var prev)) options.Remove(prev);
            string line = options[Math.Abs(pick(options.Count)) % options.Count];
            _lastByTopic[topic] = now;
            _lastAny = now;
            _lastLine[topic] = line;
            return line;
        }

        /// <summary>
        /// The first of <paramref name="topics"/> (most urgent first) that is not on pause, spoken; null if all are paused or the global
        /// gap is still on. An urgent topic on its cooldown must not silence the ones behind it (D-01).
        /// </summary>
        public string? SayFirst(IReadOnlyList<string> topics, double now, Func<int, int> pick)
        {
            foreach (var topic in topics)
            {
                var line = Say(topic, now, pick);
                if (line != null) return line;
                if (now - _lastAny < _s.GlobalGapSeconds) return null; // the gap is shared: asking further would not help
            }
            return null;
        }

        /// <summary>Topics the hero's state calls for, most urgent first.</summary>
        public IReadOnlyList<string> ConditionTopics(HeroCondition c, Hunger h, bool overloaded, bool nightWithoutFire)
        {
            var list = new List<string>();
            if (c.IsKnockedOut) return list;
            float hp = c.HpFraction;
            if (hp < _s.HealthCriticalBelow) list.Add(Topics.HealthCritical);
            var worst = WoundType.Cut;
            float worstSev = 0;
            foreach (WoundType t in Enum.GetValues(typeof(WoundType)))
                if (c.Severity(t) > worstSev) { worst = t; worstSev = c.Severity(t); }
            if (worstSev >= _s.WoundSpeaksFrom) list.Add(Topics.Wound(worst));
            if (h.Level == HungerLevel.Starving) list.Add(Topics.HungerStarving);
            if (hp >= _s.HealthCriticalBelow && hp < _s.HealthBadBelow) list.Add(Topics.HealthBad);
            if (h.Level == HungerLevel.Hungry) list.Add(Topics.HungerHungry);
            if (overloaded) list.Add(Topics.Overload);
            if (hp >= _s.HealthBadBelow && hp < _s.HealthHurtBelow) list.Add(Topics.HealthHurt);
            if (nightWithoutFire) list.Add(Topics.NightNoFire);
            if (h.Level == HungerLevel.Peckish) list.Add(Topics.HungerPeckish);
            return list;
        }
    }
}
