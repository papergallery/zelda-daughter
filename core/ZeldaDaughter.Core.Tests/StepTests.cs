using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C1 (docs/demo/unity-architecture.md §2.3, §4): one call per frame moves the clock, the hero's state, hunger, her blows and her stamina.</summary>
    public class StepTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState G()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            g.HeroPosition = new Vec2(0, 0);
            return g;
        }

        [Fact]
        public void Step_moves_the_clock_and_hunger_by_dt()
        {
            var g = G();
            var report = new StepReport();
            double t0 = g.Clock.TimeOfDay;
            float h0 = g.Hunger.Value;
            g.Step(2f, 0f, RestKind.None, report);
            Assert.Equal(t0 + 2.0 / D.World.DayLengthSeconds, g.Clock.TimeOfDay, 9);
            Assert.Equal(h0 + 2f / D.Hunger.SecondsToFull, g.Hunger.Value, 6);
        }

        [Fact]
        public void Step_reports_the_phase_change()
        {
            var g = G();
            var report = new StepReport();
            g.Clock.SetTime(1, D.World.DuskStart - 0.0001);
            g.Step((float)(0.001 * D.World.DayLengthSeconds), 0f, RestKind.None, report);
            Assert.Contains(report.ClockEvents, e => e.Kind == ClockEventKind.PhaseChanged && e.Phase == DayPhase.Dusk);
        }

        [Fact]
        public void Report_is_cleared_by_every_step()
        {
            var g = G();
            var report = new StepReport();
            g.Clock.SetTime(1, D.World.DuskStart - 0.0001);
            g.Step((float)(0.001 * D.World.DayLengthSeconds), 0f, RestKind.None, report);
            Assert.NotEmpty(report.ClockEvents);
            g.Step(0.016f, 0f, RestKind.None, report);
            Assert.Empty(report.ClockEvents);
            Assert.Empty(report.ConditionEvents);
            Assert.Empty(report.SkillChanges);
        }

        [Fact]
        public void The_wound_closes_faster_by_a_lit_campfire_than_in_the_open()
        {
            var open = G();
            var camp = G();
            camp.Camp.Restore(1, null, new[] { new Campfire("f", new Vec2(1, 0), 1000f, 10f) });
            Assert.Equal(RestKind.Campfire, camp.CurrentRest());
            foreach (var g in new[] { open, camp }) g.Condition.Wound(WoundType.Cut, 0.5f);
            var r = new StepReport();
            for (int i = 0; i < 100; i++) { open.Step(0.5f, 0f, RestKind.None, r); camp.Step(0.5f, 0f, RestKind.None, r); }
            Assert.True(camp.Condition.Severity(WoundType.Cut) < open.Condition.Severity(WoundType.Cut));
        }

        [Fact]
        public void An_explicit_rest_wins_over_the_fire_check()
        {
            var tavern = G();
            var open = G();
            foreach (var g in new[] { tavern, open }) g.Condition.Wound(WoundType.Cut, 0.5f);
            var r = new StepReport();
            for (int i = 0; i < 100; i++) { tavern.Step(0.5f, 0f, RestKind.Tavern, r); open.Step(0.5f, 0f, RestKind.None, r); }
            Assert.True(tavern.Condition.Severity(WoundType.Cut) < open.Condition.Severity(WoundType.Cut));
        }

        [Fact]
        public void Knockout_ends_and_the_report_says_so()
        {
            var g = G();
            var r = new StepReport();
            g.Condition.Damage(1000);
            g.Step(D.Wounds.KnockoutSeconds + 1f, 0f, RestKind.None, r);
            Assert.Contains(r.ConditionEvents, e => e.Kind == ConditionEventKind.Revived);
        }

        [Fact]
        public void Walking_trains_endurance_and_the_report_carries_the_change()
        {
            var g = G();
            var r = new StepReport();
            float e0 = g.Skills.Get(Stat.Endurance);
            bool seen = false;
            for (int i = 0; i < 400 && !seen; i++)
            {
                g.Step(0.1f, 5f, RestKind.None, r);
                seen = r.SkillChanges.Any(c => c.Stat == Stat.Endurance);
            }
            Assert.True(seen);
            Assert.True(g.Skills.Get(Stat.Endurance) > e0);
        }

        [Fact]
        public void Standing_still_trains_nothing()
        {
            var g = G();
            var r = new StepReport();
            float e0 = g.Skills.Get(Stat.Endurance);
            for (int i = 0; i < 200; i++) g.Step(0.1f, 0f, RestKind.None, r);
            Assert.Equal(e0, g.Skills.Get(Stat.Endurance));
        }

        [Fact]
        public void An_overloaded_hero_walking_trains_carrying()
        {
            var g = G();
            var r = new StepReport();
            Assert.True(g.Bag.Add("ore", 25));
            Assert.True(g.Bag.IsOverloaded(g.Skills.CapacityMultiplier()));
            float c0 = g.Skills.Get(Stat.CarryCapacity);
            for (int i = 0; i < 400; i++) g.Step(0.1f, 5f, RestKind.None, r);
            Assert.True(g.Skills.Get(Stat.CarryCapacity) > c0);
        }

        [Fact]
        public void The_hero_side_of_combat_follows_her_position_and_cooldown()
        {
            var g = G();
            var r = new StepReport();
            g.HeroPosition = new Vec2(3, 4);
            g.Step(0.016f, 0f, RestKind.None, r);
            Assert.Equal(new Vec2(3, 4), g.Combat.Position);
        }

        [Fact]
        public void Step_allocates_nothing_on_an_ordinary_frame()
        {
            var g = G();
            var r = new StepReport();
            for (int i = 0; i < 60; i++) g.Step(0.016f, 0.04f, RestKind.None, r);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            long used = 0;
            for (int i = 0; i < 300; i++)
            {
                long b = System.GC.GetAllocatedBytesForCurrentThread();
                g.Step(0.016f, 0.04f, RestKind.None, r);
                long a = System.GC.GetAllocatedBytesForCurrentThread();
                if (r.ClockEvents.Count == 0 && r.ConditionEvents.Count == 0 && r.SkillChanges.Count == 0) used += a - b;
            }
            Assert.Equal(0, used);
        }
    }
}
