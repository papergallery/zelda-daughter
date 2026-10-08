using System;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-16: project-design.md §6 «Управление боем», «Оружие», «Прогрессия» — один тап, один удар, один расчёт.</summary>
    public class CombatTests
    {
        static readonly WeaponSettings W = TestData.Load<WeaponSettings>("weapons.json");
        static readonly EnemySettings E = TestData.Load<EnemySettings>("enemies.json");
        static readonly SkillSettings SK = TestData.Load<SkillSettings>("skills.json");
        static readonly WoundSettings WS = TestData.Load<WoundSettings>("wounds.json");

        const float Hit = 0f;     // roll below any chance
        const float Miss = 0.999f;

        static (HeroCombat hero, Enemy enemy) Arena(string enemy = "boar", float distance = 1f)
        {
            var hero = new HeroCombat(W, new Skills(SK), new HeroCondition(WS));
            return (hero, new Enemy("t", E, enemy, new Vec2(distance, 0)));
        }

        [Fact]
        public void A_miss_trains_accuracy_a_hit_trains_strength()
        {
            var (h, e) = Arena();
            var r = h.Strike("stick", e, Miss);
            Assert.Equal(StrikeOutcome.Miss, r.Outcome);
            Assert.True(h.Skills.Get(Stat.Accuracy) > SK.Stats["accuracy"].Start, "§6: промах растит точность");

            var (h2, e2) = Arena();
            var r2 = h2.Strike("stick", e2, Hit);
            Assert.Equal(StrikeOutcome.Hit, r2.Outcome);
            Assert.True(h2.Skills.Get(Stat.Strength) > SK.Stats["strength"].Start, "попадание растит силу");
            Assert.True(h2.Skills.Get(Stat.Accuracy) > h.Skills.Get(Stat.Accuracy), "попадание точность растит сильнее промаха");
        }

        [Fact]
        public void A_hammer_breaks_a_bone_a_blade_cuts_fists_leave_no_wound()
        {
            var (h, e) = Arena();
            var hammer = h.Strike("hammer", e, Hit);
            Assert.Equal(WoundType.Fracture, hammer.Wound);
            Assert.Equal(W.Weapons["hammer"].Severity, hammer.Severity, 3);
            Assert.Equal(W.Weapons["hammer"].Stun, hammer.StunSeconds, 3);

            foreach (var id in new[] { "sword", "knife", "sharpened_stick" })
            {
                var (h2, e2) = Arena();
                var r = h2.Strike(id, e2, Hit);
                Assert.Equal(WoundType.Cut, r.Wound);
                Assert.Equal(W.Weapons[id].Severity, r.Severity, 3);
            }

            var (h3, e3) = Arena();
            var fists = h3.Strike("fists", e3, Hit);
            Assert.Null(fists.Wound);
            Assert.Equal(0f, fists.Severity);
            Assert.True(fists.Damage > 0);
        }

        [Fact]
        public void The_wound_lands_on_the_enemy_not_on_the_hero()
        {
            var (h, e) = Arena();
            h.Strike("hammer", e, Hit);
            Assert.True(e.WoundSeverity(WoundType.Fracture) > 0);
            Assert.Equal(0f, h.Condition.WoundLoad);
            Assert.Equal(WS.MaxHp, h.Condition.Hp);
        }

        [Fact]
        public void A_new_weapon_is_weaker_and_less_accurate_until_practised()
        {
            var (fresh, _) = Arena();
            var skilled = new HeroCombat(W, new Skills(SK), new HeroCondition(WS));
            skilled.Skills.Restore(new System.Collections.Generic.Dictionary<string, float>(), new System.Collections.Generic.Dictionary<string, float> { ["blade"] = SK.Max });

            Assert.True(fresh.HitChance("sword") < skilled.HitChance("sword"));
            var a = Arena().Item2; var b = Arena().Item2;
            float weak = fresh.Strike("sword", a, Hit).Damage;
            float strong = skilled.Strike("sword", b, Hit).Damage;
            Assert.True(weak < strong, "§6: первые попытки с новым оружием слабее");
            Assert.True(weak < W.Weapons["sword"].Damage);
        }

        [Fact]
        public void One_strike_is_one_calculation()
        {
            // April: damage was applied twice per tap (CombatController).
            var (h, e) = Arena();
            float before = e.Hp;
            var r = h.Strike("stick", e, Hit);
            Assert.Equal(before - r.Damage, e.Hp, 3);

            var reference = new Skills(SK);
            reference.Apply(SkillEvent.Attack(WeaponClass.Blunt, hit: true));
            Assert.Equal(reference.Get(Stat.Strength), h.Skills.Get(Stat.Strength), 4);
            Assert.Equal(reference.Weapon(WeaponClass.Blunt), h.Skills.Weapon(WeaponClass.Blunt), 4);

            // the second tap inside the cooldown is not a strike at all
            float hp = e.Hp, str = h.Skills.Get(Stat.Strength);
            var again = h.Strike("stick", e, Hit);
            Assert.Equal(StrikeOutcome.Cooldown, again.Outcome);
            Assert.Equal(hp, e.Hp);
            Assert.Equal(str, h.Skills.Get(Stat.Strength));
        }

        [Fact]
        public void The_roll_comes_from_outside_and_decides_by_the_chance()
        {
            var (h, _) = Arena();
            float chance = h.HitChance("stick");
            Assert.InRange(chance, 0.01f, 0.99f);
            Assert.Equal(StrikeOutcome.Hit, Arena().hero.Strike("stick", Arena().enemy, chance - 0.001f).Outcome);
            Assert.Equal(StrikeOutcome.Miss, Arena().hero.Strike("stick", Arena().enemy, chance).Outcome);

            // same roll, same state — same result
            var r1 = Arena().hero.Strike("stick", Arena().enemy, 0.2f);
            var r2 = Arena().hero.Strike("stick", Arena().enemy, 0.2f);
            Assert.Equal(r1.Outcome, r2.Outcome);
            Assert.Equal(r1.Damage, r2.Damage);
        }

        [Fact]
        public void A_miss_is_a_glancing_blow_without_a_wound()
        {
            var (h, e) = Arena();
            var r = h.Strike("hammer", e, Miss);
            Assert.Equal(StrikeOutcome.Miss, r.Outcome);
            Assert.Null(r.Wound);
            Assert.Equal(0f, r.StunSeconds);
            var (h2, e2) = Arena();
            float full = h2.Strike("hammer", e2, Hit).Damage;
            Assert.Equal(full * W.MissDamageShare, r.Damage, 3);
        }

        [Fact]
        public void Strength_and_wounds_change_the_blow()
        {
            float Damage(HeroCombat h) => h.Strike("stick", Arena().enemy, Hit).Damage;
            var strong = Arena().hero;
            strong.Skills.Restore(new System.Collections.Generic.Dictionary<string, float> { ["strength"] = SK.Max }, new System.Collections.Generic.Dictionary<string, float>());
            Assert.True(Damage(strong) > Damage(Arena().hero));

            var burnt = Arena().hero;
            burnt.Condition.Wound(WoundType.Burn, 1f);
            Assert.True(burnt.HitChance("stick") < Arena().hero.HitChance("stick"), "ожог снижает точность");
            var sick = Arena().hero;
            sick.Condition.Wound(WoundType.Poison, 1f);
            Assert.True(Damage(sick) < Damage(Arena().hero), "яд ослабляет удар");
        }

        [Fact]
        public void The_cooldown_ticks_and_the_blow_needs_reach()
        {
            var (h, e) = Arena(distance: 1f);
            h.Strike("stick", e, Miss);
            Assert.True(h.CooldownLeft > 0);
            h.Tick(h.CooldownLeft + 0.01f);
            Assert.Equal(0f, h.CooldownLeft);
            Assert.NotEqual(StrikeOutcome.Cooldown, h.Strike("stick", e, Miss).Outcome);

            var (h2, far) = Arena(distance: W.Weapons["stick"].Range + 0.5f);
            var r = h2.Strike("stick", far, Hit);
            Assert.Equal(StrikeOutcome.OutOfRange, r.Outcome);
            Assert.Equal(0f, h2.CooldownLeft);
            Assert.Equal(far.MaxHp, far.Hp);
            Assert.Equal(SK.Stats["accuracy"].Start, h2.Skills.Get(Stat.Accuracy));
        }

        [Fact]
        public void A_knocked_out_hero_does_not_strike_and_unknown_weapons_are_loud()
        {
            var (h, e) = Arena();
            h.Condition.Wound(WoundType.Cut, 1f); h.Condition.Wound(WoundType.Fracture, 1f);
            Assert.True(h.Condition.IsKnockedOut);
            Assert.Equal(StrikeOutcome.Unavailable, h.Strike("stick", e, Hit).Outcome);
            Assert.Throws<ArgumentException>(() => Arena().hero.Strike("banana", Arena().enemy, Hit));
        }

        [Fact]
        public void Killing_an_enemy_trains_everything_once_more()
        {
            var (h, e) = Arena();
            e.Receive(e.Hp - 1f, null, 0, 0);
            var r = h.Strike("sword", e, Hit);
            Assert.True(r.Killed);
            var reference = new Skills(SK);
            reference.Apply(SkillEvent.Attack(WeaponClass.Blade, true));
            reference.Apply(SkillEvent.Victory(WeaponClass.Blade));
            Assert.Equal(reference.Get(Stat.Toughness), h.Skills.Get(Stat.Toughness), 4);
            Assert.Equal(reference.Get(Stat.Strength), h.Skills.Get(Stat.Strength), 4);
        }
    }
}
