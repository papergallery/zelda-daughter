using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Movement;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-17: project-design.md §6 — враги контратакуют с заметным замахом; можно отбежать и убежать.</summary>
    public class EnemyTests
    {
        static readonly WeaponSettings W = TestData.Load<WeaponSettings>("weapons.json");
        static readonly EnemySettings E = TestData.Load<EnemySettings>("enemies.json");
        static readonly SkillSettings SK = TestData.Load<SkillSettings>("skills.json");
        static readonly WoundSettings WS = TestData.Load<WoundSettings>("wounds.json");
        static readonly MovementSettings M = TestData.Load<MovementSettings>("movement.json");

        const float Dt = 0.05f;

        static HeroCombat Hero(float x = 0) => new HeroCombat(W, new Skills(SK), new HeroCondition(WS)) { Position = new Vec2(x, 0) };
        static Enemy Make(string id, float x) => new Enemy("e", E, id, new Vec2(x, 0));

        /// <summary>Runs the enemy; the hook may move the hero each step. Returns every event with its time.</summary>
        static List<(float t, EnemyEvent e)> Run(Enemy enemy, HeroCombat hero, float seconds, System.Action<float>? hook = null, float roll = 0.3f)
        {
            var log = new List<(float, EnemyEvent)>();
            for (float t = 0; t < seconds; t += Dt)
            {
                hook?.Invoke(t);
                foreach (var ev in enemy.Tick(Dt, hero, roll)) log.Add((t, ev));
            }
            return log;
        }

        static bool Has(List<(float t, EnemyEvent e)> log, EnemyEventKind k) => log.Any(x => x.e.Kind == k);

        [Fact]
        public void A_dodge_cancels_the_blow()
        {
            var hero = Hero();
            var boar = Make("boar", 1f);
            boar.Provoke();
            var log = new List<(float t, EnemyEvent e)>();
            bool moved = false;
            for (float t = 0; t < 6 && !Has(log, EnemyEventKind.Struck) && !Has(log, EnemyEventKind.Dodged); t += Dt)
            {
                foreach (var ev in boar.Tick(Dt, hero, 0.3f)) log.Add((t, ev));
                if (!moved && boar.State == EnemyState.Windup) { hero.Position = new Vec2(-2f - boar.Def.Range, 0); moved = true; } // finger off, step away
            }
            Assert.True(moved);
            Assert.True(Has(log, EnemyEventKind.Dodged));
            Assert.False(Has(log, EnemyEventKind.Struck));
            Assert.Equal(WS.MaxHp, hero.Condition.Hp);
            Assert.Equal(0f, hero.Condition.WoundLoad);
            Assert.True(hero.Skills.Get(Stat.Agility) > SK.Stats["agility"].Start, "уворот растит ловкость");
        }

        [Fact]
        public void Standing_still_takes_the_blow_boar_breaks_wolf_cuts()
        {
            var hero = Hero(); var boar = Make("boar", 1f); boar.Provoke();
            var log = Run(boar, hero, 3f);
            Assert.True(Has(log, EnemyEventKind.Struck));
            Assert.Equal(E.Enemies["boar"].Severity, hero.Condition.Severity(WoundType.Fracture), 3);
            Assert.True(hero.Condition.Hp < WS.MaxHp);
            Assert.True(hero.Skills.Get(Stat.Toughness) > SK.Stats["toughness"].Start, "урон растит стойкость");

            var hero2 = Hero(); var wolf = Make("wolf", 3f);
            Run(wolf, hero2, 4f);
            Assert.Equal(E.Enemies["wolf"].Severity, hero2.Condition.Severity(WoundType.Cut), 3);
        }

        [Fact]
        public void The_windup_is_never_shorter_than_the_minimum()
        {
            foreach (var id in E.Enemies.Keys)
            {
                Assert.True(E.Enemies[id].Windup >= E.MinWindup, id);
                var hero = Hero(); var en = Make(id, 1f); en.Provoke();
                var log = Run(en, hero, 4f);
                float start = log.First(x => x.e.Kind == EnemyEventKind.WindupStarted).t;
                float hit = log.First(x => x.e.Kind == EnemyEventKind.Struck || x.e.Kind == EnemyEventKind.Dodged).t;
                Assert.True(hit - start >= E.MinWindup - 1e-3f, $"{id}: замах {hit - start} с");
            }
        }

        [Fact]
        public void A_windup_shorter_in_data_is_stretched_to_the_minimum()
        {
            var bad = new EnemySettings { MinWindup = 0.6f, AlertSeconds = 0.5f, LoseInterestFactor = 2f };
            bad.Enemies["fox"] = new EnemyDef { Hp = 10, Damage = 1, Wound = "cut", Severity = 0.1f, Range = 1.5f, Cooldown = 1, Windup = 0.2f, ChaseSpeed = 3, AggroRange = 5, AggroOnSight = true };
            var en = new Enemy("f", bad, "fox", new Vec2(1, 0));
            en.Provoke();
            var hero = Hero();
            var log = Run(en, hero, 3f);
            float start = log.First(x => x.e.Kind == EnemyEventKind.WindupStarted).t;
            float hit = log.First(x => x.e.Kind == EnemyEventKind.Struck).t;
            Assert.True(hit - start >= 0.6f - 1e-3f);
        }

        [Fact]
        public void The_hero_can_outrun_every_chaser()
        {
            foreach (var id in E.Enemies.Keys)
            {
                Assert.True(E.Enemies[id].ChaseSpeed < M.RunSpeed, $"{id}: погоня должна быть медленнее бега {M.RunSpeed}");
                var hero = Hero(3f); var en = Make(id, 0f); en.Provoke();
                var log = new List<(float t, EnemyEvent e)>();
                float gap0 = 3f;
                for (float t = 0; t < 30; t += Dt)
                {
                    hero.Position = new Vec2(hero.Position.X + M.RunSpeed * Dt, 0);
                    foreach (var ev in en.Tick(Dt, hero, 0.3f)) log.Add((t, ev));
                    if (t < 5f) Assert.True(en.Position.X < hero.Position.X, "не догнал");
                }
                Assert.False(Has(log, EnemyEventKind.Struck), id);
                Assert.True(hero.Position.X - en.Position.X > gap0, id);
            }
        }

        [Fact]
        public void After_the_hero_is_knocked_out_the_enemy_walks_away()
        {
            var hero = Hero(); var wolf = Make("wolf", 1f); wolf.Provoke();
            for (float t = 0; t < 3 && wolf.State != EnemyState.Windup; t += Dt) wolf.Tick(Dt, hero, 0.3f);
            Assert.Equal(EnemyState.Windup, wolf.State);

            hero.Condition.Wound(WoundType.Cut, 1f); hero.Condition.Wound(WoundType.Fracture, 1f);
            Assert.True(hero.Condition.IsKnockedOut);
            float load = hero.Condition.WoundLoad;

            var log = Run(wolf, hero, 1f);
            Assert.Equal(EnemyState.Leaving, wolf.State);
            Assert.True(Has(log, EnemyEventKind.LostInterest));
            Assert.False(Has(log, EnemyEventKind.Struck));

            // the hero wakes up where he fell: the wolf must not come back (April: knockout loop)
            var more = Run(wolf, hero, 30f);
            hero.Condition.Tick(WS.KnockoutSeconds + 1, RestKind.None);
            more.AddRange(Run(wolf, hero, 30f));
            Assert.DoesNotContain(more, x => x.e.Kind == EnemyEventKind.Struck || x.e.Kind == EnemyEventKind.Alerted);
            Assert.Equal(load, hero.Condition.WoundLoad, 2);
            Assert.True(wolf.State == EnemyState.Idle || wolf.State == EnemyState.Wander);
        }

        [Fact]
        public void The_boar_wakes_only_on_damage_the_wolf_on_sight()
        {
            var hero = Hero();
            var boar = Make("boar", 3f);
            var log = Run(boar, hero, 10f);
            Assert.False(Has(log, EnemyEventKind.Alerted), "кабан не агрится на вид");

            var wolfNear = Make("wolf", E.Enemies["wolf"].AggroRange - 1f);
            Assert.True(Has(Run(wolfNear, Hero(), 1f), EnemyEventKind.Alerted));
            var wolfFar = Make("wolf", E.Enemies["wolf"].AggroRange + 2f);
            Assert.False(Has(Run(wolfFar, Hero(), 1f), EnemyEventKind.Alerted));

            // damage wakes the boar: through the hero's own blow
            var h = Hero(); var b = Make("boar", 1f);
            h.Strike("stick", b, 0f);
            Assert.Equal(EnemyState.Alert, b.State);
        }

        [Fact]
        public void Alert_lasts_before_the_chase_begins()
        {
            var hero = Hero(); var wolf = Make("wolf", 8f);
            wolf.Tick(Dt, hero, 0.3f);
            Assert.Equal(EnemyState.Alert, wolf.State);
            float x0 = wolf.Position.X;
            for (float t = 0; t < E.AlertSeconds - 0.15f; t += Dt) wolf.Tick(Dt, hero, 0.3f);
            Assert.Equal(EnemyState.Alert, wolf.State);
            Assert.Equal(x0, wolf.Position.X);
            Run(wolf, hero, 0.3f);
            Assert.NotEqual(EnemyState.Alert, wolf.State);
        }

        [Fact]
        public void Aggro_drops_beyond_the_factor_times_the_radius()
        {
            var def = E.Enemies["boar"];
            float limit = E.LoseInterestFactor * def.AggroRange;

            var hero = Hero(); var boar = Make("boar", 1f); boar.Provoke();
            hero.Position = new Vec2(1f - (limit - 0.5f), 0);
            boar.Tick(Dt, hero, 0.3f);
            Assert.NotEqual(EnemyState.Idle, boar.State);

            var hero2 = Hero(); var boar2 = Make("boar", 1f); boar2.Provoke();
            hero2.Position = new Vec2(1f - (limit + 0.5f), 0);
            var ev = boar2.Tick(Dt, hero2, 0.3f);
            Assert.Equal(EnemyState.Idle, boar2.State);
            Assert.Contains(ev, x => x.Kind == EnemyEventKind.LostInterest);
        }

        [Fact]
        public void A_big_blow_staggers_and_breaks_the_windup()
        {
            var def = E.Enemies["boar"];
            var hero = Hero(); var boar = Make("boar", 1f); boar.Provoke();
            for (float t = 0; t < 3 && boar.State != EnemyState.Windup; t += Dt) boar.Tick(Dt, hero, 0.3f);
            var ev = boar.Receive(def.StaggerShare * def.Hp, null, 0, 0);
            Assert.Equal(EnemyState.Staggered, boar.State);
            Assert.Contains(ev, x => x.Kind == EnemyEventKind.Staggered);

            var log = Run(boar, hero, def.StaggerSeconds - 0.2f);
            Assert.False(Has(log, EnemyEventKind.Struck), "замах сорван");
            Assert.Equal(EnemyState.Staggered, boar.State);
            Run(boar, hero, 0.4f);
            Assert.NotEqual(EnemyState.Staggered, boar.State);

            // a small blow does not stagger
            var calm = Make("boar", 1f);
            calm.Receive(def.StaggerShare * def.Hp - 1f, null, 0, 0);
            Assert.NotEqual(EnemyState.Staggered, calm.State);
        }

        [Fact]
        public void A_hammer_stuns_for_its_stun_time()
        {
            var hero = Hero(); var boar = Make("boar", 1f);
            var r = hero.Strike("hammer", boar, 0f);
            Assert.Equal(EnemyState.Staggered, boar.State);
            Assert.True(r.StunSeconds >= W.Weapons["hammer"].Stun);
            var log = Run(boar, hero, W.Weapons["hammer"].Stun - 0.2f);
            Assert.Equal(EnemyState.Staggered, boar.State);
        }

        [Fact]
        public void Death_leaves_a_carcass()
        {
            var hero = Hero(); var wolf = Make("wolf", 1f);
            var ev = wolf.Receive(wolf.Hp + 5, null, 0, 0);
            Assert.Contains(ev, x => x.Kind == EnemyEventKind.Died);
            Assert.True(wolf.IsCarcass);
            Assert.Equal(EnemyState.Dead, wolf.State);
            Assert.Empty(wolf.Tick(Dt, hero, 0.3f));
            Assert.Equal(StrikeOutcome.Unavailable, hero.Strike("stick", wolf, 0f).Outcome);
            Assert.Empty(wolf.Receive(10, null, 0, 0));
            Assert.Equal(0f, wolf.Hp);
        }

        [Fact]
        public void Idle_enemies_wander_by_outside_rolls_and_replay_identically()
        {
            List<string> Sim()
            {
                var hero = Hero(50f); var wolf = Make("wolf", 0f); var rolls = new System.Random(7);
                var trace = new List<string>();
                for (float t = 0; t < 30; t += Dt)
                {
                    foreach (var ev in wolf.Tick(Dt, hero, (float)rolls.NextDouble())) trace.Add($"{t:0.00} {ev.Kind}");
                    trace.Add(wolf.Position.ToString());
                }
                return trace;
            }
            var a = Sim(); var b = Sim();
            Assert.Equal(a, b);
            Assert.NotEqual("(0, 0)", a.Last());
        }
    }
}
