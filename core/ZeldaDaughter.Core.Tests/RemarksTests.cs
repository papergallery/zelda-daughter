using System.Linq;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Remarks;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-12: the hero's lines instead of a HUD (project-design.md §1, §6, §7).</summary>
    public class RemarksTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static Remarks.Remarks New() => new Remarks.Remarks(D.Remarks);

        [Fact]
        public void Every_topic_the_code_asks_for_has_lines()
        {
            foreach (var t in Topics.All) Assert.True(D.Remarks.Topics.ContainsKey(t), t);
            foreach (Stat s in System.Enum.GetValues(typeof(Stat)))
                for (int tier = 1; tier <= 3; tier++) Assert.True(D.Remarks.Topics.ContainsKey(Topics.Skill(s, tier)), Topics.Skill(s, tier));
        }

        [Fact]
        public void Topic_cooldown_and_global_gap_hold_back_lines()
        {
            var r = New();
            Assert.NotNull(r.Say(Topics.Overload, 0, n => 0));
            Assert.Null(r.Say(Topics.HungerHungry, 1, n => 0));                       // global gap
            Assert.NotNull(r.Say(Topics.HungerHungry, D.Remarks.GlobalGapSeconds + 1, n => 0));
            Assert.Null(r.Say(Topics.Overload, D.Remarks.GlobalGapSeconds * 3, n => 0)); // overload cooldown 60
            Assert.NotNull(r.Say(Topics.Overload, 61 + D.Remarks.GlobalGapSeconds * 3, n => 0));
        }

        [Fact]
        public void Same_line_is_not_said_twice_in_a_row()
        {
            var r = New();
            var a = r.Say(Topics.CraftFail, 0, n => 0);
            var b = r.Say(Topics.CraftFail, 100, n => 0);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Most_urgent_condition_comes_first()
        {
            var c = new HeroCondition(D.Wounds);
            var h = new Hunger(D.Hunger);
            h.Restore(0.8f);
            Assert.Equal(Topics.HungerHungry, New().ConditionTopics(c, h, overloaded: true, nightWithoutFire: false).First());
            c.Wound(WoundType.Fracture, 0.6f);
            Assert.Equal(Topics.WoundFracture, New().ConditionTopics(c, h, true, false).First());
            c.Damage(90);
            Assert.Equal(Topics.HealthCritical, New().ConditionTopics(c, h, true, false).First());
        }

        [Fact]
        public void Healthy_fed_light_hero_in_daylight_says_nothing()
        {
            Assert.Empty(New().ConditionTopics(new HeroCondition(D.Wounds), new Hunger(D.Hunger), false, false));
        }

        [Fact]
        public void The_heroine_speaks_of_herself_in_the_feminine()
        {
            // docs/demo/decisions.md №1: the hero is a young woman. A first-person past tense must be feminine:
            // "устала", "обожглась" — never "устал", "обжёгся". Masculine endings -л / -лся are the tell.
            var masculine = new System.Text.RegularExpressions.Regex(@"\b\w{2,}(лся|л)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var notVerbs = new[] { "вол", "стол", "пол", "угол", "ствол", "котёл", "орёл", "осёл", "пепел", "уголь" }; // nouns that end like a past tense
            var bad = D.Remarks.Topics.SelectMany(t => t.Value.Lines)
                .Where(l => masculine.Matches(l).Cast<System.Text.RegularExpressions.Match>().Any(m => !notVerbs.Contains(m.Value.ToLowerInvariant())))
                .ToList();
            Assert.Empty(bad);
        }
    }
}
