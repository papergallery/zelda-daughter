using System.Linq;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-06: project-design.md §6 «Прогрессия в стиле Kenshi» — practice, not experience points.</summary>
    public class SkillsTests
    {
        static readonly SkillSettings S = TestData.Load<SkillSettings>("skills.json");

        static Skills New() => new Skills(S);

        [Fact]
        public void Starts_from_data()
        {
            var k = New();
            Assert.Equal(5f, k.Get(Stat.Strength));
            Assert.Equal(0f, k.Weapon(WeaponClass.Bow));
        }

        [Fact]
        public void A_miss_still_trains_accuracy_but_less_than_a_hit()
        {
            var hit = New(); hit.Apply(SkillEvent.Attack(WeaponClass.Blunt, hit: true));
            var miss = New(); miss.Apply(SkillEvent.Attack(WeaponClass.Blunt, hit: false));
            float start = S.Stats["accuracy"].Start;
            Assert.True(miss.Get(Stat.Accuracy) > start, "failure trains too (§6)");
            Assert.True(hit.Get(Stat.Accuracy) > miss.Get(Stat.Accuracy));
        }

        [Fact]
        public void Taking_damage_trains_toughness()
        {
            var k = New();
            k.Apply(SkillEvent.Damaged(30));
            Assert.True(k.Get(Stat.Toughness) > S.Stats["toughness"].Start);
        }

        [Fact]
        public void Walking_overloaded_trains_carry_capacity_walking_light_does_not()
        {
            var light = New(); light.Apply(SkillEvent.Walked(50, overloaded: false));
            var heavy = New(); heavy.Apply(SkillEvent.Walked(50, overloaded: true));
            Assert.Equal(S.Stats["carry_capacity"].Start, light.Get(Stat.CarryCapacity));
            Assert.True(heavy.Get(Stat.CarryCapacity) > S.Stats["carry_capacity"].Start);
            Assert.True(light.Get(Stat.Endurance) > S.Stats["endurance"].Start);
        }

        [Fact]
        public void Victory_trains_with_each_stats_own_curve()
        {
            // April took the accuracy victory bonus from the strength curve (ActionTracker.cs:90).
            var k = New();
            k.Apply(SkillEvent.Victory(WeaponClass.Blade));
            float accGain = k.Get(Stat.Accuracy) - S.Stats["accuracy"].Start;
            float expected = Skills.Gain(S.Stats["accuracy"].Victory, S.Stats["accuracy"].Rate, S.Stats["accuracy"].Decay, S.Stats["accuracy"].Start, S.Max);
            Assert.Equal(expected, accGain, 4);
        }

        [Fact]
        public void Gains_shrink_as_skill_grows_and_stop_at_max()
        {
            float low = Skills.Gain(1, 0.8f, 0.5f, 10, 100);
            float high = Skills.Gain(1, 0.8f, 0.5f, 90, 100);
            Assert.True(low > high);
            Assert.Equal(0f, Skills.Gain(1, 0.8f, 0.5f, 100, 100));
            var k = New();
            for (int i = 0; i < 100000; i++) k.Apply(SkillEvent.Attack(WeaponClass.Fists, true));
            Assert.True(k.Get(Stat.Strength) <= S.Max);
        }

        [Fact]
        public void New_weapon_is_slow_and_inaccurate_until_practised()
        {
            var k = New();
            var fresh = k.WeaponHandling(WeaponClass.Bow);
            Assert.Equal(S.NewWeapon.DamageFrom, fresh.DamageMultiplier, 3);
            Assert.Equal(S.NewWeapon.SpeedFrom, fresh.SpeedMultiplier, 3);
            Assert.Equal(S.NewWeapon.HitFrom, fresh.HitBonus, 3);
            for (int i = 0; i < 3000; i++) k.Apply(SkillEvent.Attack(WeaponClass.Bow, hit: i % 2 == 0));
            var practised = k.WeaponHandling(WeaponClass.Bow);
            Assert.True(practised.DamageMultiplier > fresh.DamageMultiplier);
            Assert.True(practised.SpeedMultiplier > fresh.SpeedMultiplier);
            Assert.True(practised.HitBonus > fresh.HitBonus);
        }

        [Fact]
        public void Effects_follow_the_stat()
        {
            var k = New();
            float n = 5f / S.Max;
            Assert.Equal(S.Effects.BaseHitChance + n * (1 - S.Effects.BaseHitChance), k.HitChance(), 3);
            Assert.Equal(1 + n * S.Effects.MaxCapacityBonus, k.CapacityMultiplier(), 3);
            Assert.Equal(1 + n * S.Effects.MaxDamageBonus, k.DamageMultiplier(), 3);
        }

        [Fact]
        public void Crossing_a_tier_is_reported_once()
        {
            var k = New();
            var changes = Enumerable.Range(0, 400).SelectMany(_ => k.Apply(SkillEvent.Damaged(100))).Where(c => c.Stat == Stat.Toughness && c.NewTier != c.OldTier).ToList();
            Assert.Equal(changes.Select(c => c.NewTier).Distinct().Count(), changes.Count);
            Assert.Contains(changes, c => c.NewTier == 1);
        }
    }
}
