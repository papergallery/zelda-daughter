using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Condition;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-07: project-design.md §6 «Раны и состояние», «Лечение», «Нокаут».</summary>
    public class ConditionTests
    {
        static readonly WoundSettings S = TestData.Load<WoundSettings>("wounds.json");

        static HeroCondition New() => new HeroCondition(S);

        static List<ConditionEvent> Run(HeroCondition c, double seconds, RestKind rest = RestKind.None, double step = 0.1)
        {
            var all = new List<ConditionEvent>();
            for (double t = 0; t < seconds; t += step) all.AddRange(c.Tick((float)step, rest));
            return all;
        }

        [Fact]
        public void Each_wound_type_has_its_effect()
        {
            var c = New();
            c.Wound(WoundType.Fracture, 1f);
            Assert.Equal(S.Types["fracture"].SpeedAtFull, c.SpeedMultiplier, 3);
            c = New(); c.Wound(WoundType.Burn, 1f);
            Assert.Equal(S.Types["burn"].AccuracyAtFull, c.AccuracyMultiplier, 3);
            c = New(); c.Wound(WoundType.Poison, 1f);
            Assert.Equal(S.Types["poison"].AttackAtFull, c.AttackMultiplier, 3);
            Assert.True(c.Flags.HasFlag(VisibleState.Nauseous));
            c = New(); c.Wound(WoundType.Cut, 0.5f);
            Assert.True(c.Flags.HasFlag(VisibleState.Bleeding));
        }

        [Fact]
        public void Bleeding_drains_health_over_time()
        {
            var c = New();
            c.Wound(WoundType.Cut, 1f);
            Run(c, 10);
            Assert.InRange(c.Hp, S.MaxHp - 10 * S.Types["cut"].HpDrainPerSecond - 0.5f, S.MaxHp - 10 * S.Types["cut"].HpDrainPerSecond + 1.5f);
        }

        [Fact]
        public void Bleeding_to_zero_knocks_out()
        {
            // April: HP reaching 0 through bleeding never triggered the knockout and the game hung (PlayerHealthState.cs:80).
            var c = New();
            c.Wound(WoundType.Cut, 1f);
            var ev = Run(c, 400);
            Assert.Contains(ev, e => e.Kind == ConditionEventKind.KnockedOut);
        }

        [Fact]
        public void Same_type_is_replaced_only_by_a_heavier_wound()
        {
            var c = New();
            c.Wound(WoundType.Fracture, 0.6f);
            c.Wound(WoundType.Fracture, 0.3f);
            Assert.Equal(0.6f, c.Severity(WoundType.Fracture), 3);
            c.Wound(WoundType.Fracture, 0.8f);
            Assert.Equal(0.8f, c.Severity(WoundType.Fracture), 3);
        }

        [Fact]
        public void Accumulated_wounds_knock_out_even_with_health_left()
        {
            var c = New();
            c.Wound(WoundType.Fracture, 0.8f);
            c.Wound(WoundType.Burn, 0.7f);
            Assert.False(c.IsKnockedOut);
            var ev = c.Wound(WoundType.Cut, 0.6f);
            Assert.True(c.IsKnockedOut);
            Assert.True(c.Hp > 0);
            Assert.Contains(ev, e => e.Kind == ConditionEventKind.KnockedOut);
        }

        [Fact]
        public void Knockout_lasts_then_hero_gets_up_with_wounds_kept()
        {
            var c = New();
            c.Damage(1000);
            Assert.True(c.IsKnockedOut);
            c.Wound(WoundType.Fracture, 0.5f); // ignored while knocked out
            var ev = Run(c, S.KnockoutSeconds + 0.5);
            Assert.Contains(ev, e => e.Kind == ConditionEventKind.Revived);
            Assert.False(c.IsKnockedOut);
            Assert.Equal(S.MaxHp * S.ReviveHpFraction, c.Hp, 0);
            Assert.Equal(0f, c.Severity(WoundType.Fracture));
        }

        [Fact]
        public void Revive_does_not_happen_instantly()
        {
            // April: FadeOverlay was null and the hero got up after ~30 ms.
            var c = New();
            c.Damage(1000);
            Run(c, S.KnockoutSeconds * 0.5);
            Assert.True(c.IsKnockedOut);
        }

        [Fact]
        public void Wounds_after_knockout_remain()
        {
            var c = New();
            c.Wound(WoundType.Fracture, 0.9f);
            c.Damage(1000);
            Run(c, S.KnockoutSeconds + 0.5);
            Assert.True(c.Severity(WoundType.Fracture) > 0.85f);
        }

        [Fact]
        public void Three_healing_speeds_wait_rest_medicine()
        {
            var wait = New(); wait.Wound(WoundType.Fracture, 1f); Run(wait, 300, RestKind.None, 1);
            var rest = New(); rest.Wound(WoundType.Fracture, 1f); Run(rest, 300, RestKind.Campfire, 1);
            var med = New(); med.Wound(WoundType.Fracture, 1f);
            Assert.True(med.Treat("splint"));
            Assert.True(wait.Severity(WoundType.Fracture) > rest.Severity(WoundType.Fracture));
            Assert.True(rest.Severity(WoundType.Fracture) > med.Severity(WoundType.Fracture));
            Assert.Equal(0f, med.Severity(WoundType.Fracture));
        }

        [Fact]
        public void Waiting_heals_very_slowly()
        {
            // §6 «ждать — раны заживают очень медленно сами»: five real minutes leave most of a fracture.
            var c = New(); c.Wound(WoundType.Fracture, 1f);
            Run(c, 300, RestKind.None, 1);
            Assert.True(c.Severity(WoundType.Fracture) > 0.9f);
        }

        [Fact]
        public void Wrong_medicine_does_nothing()
        {
            var c = New(); c.Wound(WoundType.Burn, 1f);
            Assert.False(c.Treat("splint"));
            Assert.Equal(1f, c.Severity(WoundType.Burn));
        }

        [Fact]
        public void Sleep_heals_wounds_down_to_threshold_and_health_up()
        {
            var c = New();
            c.Wound(WoundType.Fracture, 0.9f);
            c.Wound(WoundType.Burn, 0.2f);
            c.Damage(80);
            c.Sleep();
            Assert.Equal(S.SleepWoundSeverity, c.Severity(WoundType.Fracture), 3);
            Assert.Equal(0.2f, c.Severity(WoundType.Burn), 3);
            Assert.Equal(S.MaxHp * S.SleepHpFraction, c.Hp, 0);
        }

        [Fact]
        public void Visible_state_replaces_health_bar()
        {
            var c = New();
            c.Wound(WoundType.Fracture, 0.5f);
            Assert.True(c.Flags.HasFlag(VisibleState.Limping));
            c.Damage(60);
            Assert.True(c.Flags.HasFlag(VisibleState.HoldingSide));
        }
    }
}
