using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-01: стабилизация ядра перед демо — находки ревью (голод/перегруз, сохранение, враги, данные).</summary>
    public class StabilityTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);
        const float Dt = 0.05f;

        static Enemy Make(GameState g, string id, float x) => new Enemy("e", g.Data.Enemies, id, new Vec2(x, 0));

        static GameState HeroState(float hunger = 0f, int ore = 0, float endurance = 0f)
        {
            var g = new GameState(D);
            g.Hunger.Restore(hunger);
            if (ore > 0) g.Bag.Add("ore", ore);
            if (endurance > 0) g.Skills.Restore(new Dictionary<string, float> { ["endurance"] = endurance }, new Dictionary<string, float>());
            g.Combat.Position = new Vec2(0, 0);
            return g;
        }

        // ---- 1. голод, перегруз, выносливость ----

        [Fact]
        public void A_hungry_hero_hits_weaker_and_slower()
        {
            var fed = HeroState(0f);
            var starving = HeroState(1f);
            Assert.True(starving.Hunger.Multiplier < 1f);
            var a = fed.Combat.Strike("stick", Make(fed, "boar", 1f), 0f);
            var b = starving.Combat.Strike("stick", Make(starving, "boar", 1f), 0f);
            Assert.Equal(StrikeOutcome.Hit, a.Outcome);
            Assert.Equal(StrikeOutcome.Hit, b.Outcome);
            Assert.Equal(a.Damage * starving.Hunger.Multiplier, b.Damage, 3);
            Assert.True(b.Damage < a.Damage);
            Assert.Equal(fed.Combat.CooldownLeft / starving.Hunger.Multiplier, starving.Combat.CooldownLeft, 3);
            Assert.True(starving.Combat.CooldownLeft > fed.Combat.CooldownLeft);
        }

        [Fact]
        public void A_hungry_but_below_the_threshold_hero_is_unchanged()
        {
            var fed = HeroState(0f);
            var peckish = HeroState(D.Hunger.HungryAt);
            var a = fed.Combat.Strike("stick", Make(fed, "boar", 1f), 0f);
            var b = peckish.Combat.Strike("stick", Make(peckish, "boar", 1f), 0f);
            Assert.Equal(a.Damage, b.Damage, 4);
            Assert.Equal(fed.Combat.CooldownLeft, peckish.Combat.CooldownLeft, 4);
        }

        [Fact]
        public void An_overloaded_hero_attacks_slower_but_not_weaker()
        {
            var light = HeroState();
            var heavy = HeroState(ore: 25);
            Assert.True(heavy.Bag.IsOverloaded(heavy.Skills.CapacityMultiplier()));
            var a = light.Combat.Strike("stick", Make(light, "boar", 1f), 0f);
            var b = heavy.Combat.Strike("stick", Make(heavy, "boar", 1f), 0f);
            Assert.Equal(a.Damage, b.Damage, 4);
            float speed = heavy.Bag.SpeedMultiplier(heavy.Skills.CapacityMultiplier());
            Assert.True(speed < 1f);
            Assert.Equal(light.Combat.CooldownLeft / speed, heavy.Combat.CooldownLeft, 3);
        }

        [Fact]
        public void An_enduring_hero_heals_faster()
        {
            var weak = HeroState(); var strong = HeroState(endurance: 100f);
            weak.Condition.Damage(50); strong.Condition.Damage(50);
            weak.Condition.Tick(10, RestKind.None); strong.Condition.Tick(10, RestKind.None);
            float gainWeak = weak.Condition.Hp - 50f, gainStrong = strong.Condition.Hp - 50f;
            Assert.Equal(D.Wounds.NaturalHpRegenPerSecond * 10 * weak.Skills.HealMultiplier(), gainWeak, 3);
            Assert.True(strong.Skills.HealMultiplier() > weak.Skills.HealMultiplier());
            Assert.Equal(D.Wounds.NaturalHpRegenPerSecond * 10 * 2f, gainStrong, 3); // endurance 100 → ×(1 + maxHealBonus)
            Assert.True(gainStrong > gainWeak);

            var a = HeroState(); var b = HeroState(endurance: 100f);
            a.Condition.Damage(50); b.Condition.Damage(50);
            a.Condition.Heal(5); b.Condition.Heal(5);
            Assert.Equal(5f * a.Skills.HealMultiplier(), a.Condition.Hp - 50f, 3);
            Assert.Equal(10f, b.Condition.Hp - 50f, 3);
        }

        [Fact]
        public void A_starving_hero_heals_slower_and_food_heals_through_the_same_scale()
        {
            var fed = HeroState(0.3f); var starving = HeroState(1f);
            fed.Condition.Damage(50); starving.Condition.Damage(50);
            fed.Condition.Tick(10, RestKind.None); starving.Condition.Tick(10, RestKind.None);
            Assert.Equal((fed.Condition.Hp - 50f) * starving.Hunger.Multiplier, starving.Condition.Hp - 50f, 3);
            Assert.True(starving.Condition.Hp < fed.Condition.Hp);

            var g = HeroState(0.5f);
            g.Condition.Damage(50);
            g.Bag.Add("meat");
            var e = g.Eat("meat");
            Assert.True(e.Eaten);
            Assert.Equal(0, g.Bag.Count("meat"));
            Assert.Equal(50f + D.Hunger.Food["meat"].Heal * g.Skills.HealMultiplier(), g.Condition.Hp, 3);
            Assert.Equal(0.5f - D.Hunger.Food["meat"].Satiety, g.Hunger.Value, 3);
            Assert.False(g.Eat("stone").Eaten);
        }

        [Fact]
        public void Movement_modifiers_carry_wounds_load_and_hunger()
        {
            var g = HeroState(hunger: 1f, ore: 25);
            g.Condition.Wound(WoundType.Fracture, 1f);
            var m = g.SpeedModifiers().ToList();
            Assert.Contains(g.Condition.SpeedMultiplier, m);
            Assert.Contains(g.Hunger.Multiplier, m);
            Assert.Contains(g.Bag.SpeedMultiplier(g.Skills.CapacityMultiplier()), m);
        }

        // ---- 2. убитые враги и сохранение ----

        [Fact]
        public void Killed_enemies_stay_dead_after_a_load()
        {
            var g = new GameState(D);
            g.Killed.Add("wolf_1");
            g.Picked.Add("pickup_stick");
            string json = SaveGame.Capture(g);
            var fresh = new GameState(D);
            SaveGame.Restore(fresh, json);
            Assert.Contains("wolf_1", fresh.Killed);
            Assert.Equal(json, SaveGame.Capture(fresh));
            // loading into a used state replaces, not adds
            fresh.Killed.Add("boar_9");
            SaveGame.Restore(fresh, json);
            Assert.DoesNotContain("boar_9", fresh.Killed);
        }

        [Fact]
        public void An_old_version_one_save_still_loads_with_nothing_killed()
        {
            var g = new GameState(D);
            g.Picked.Add("x");
            var obj = Newtonsoft.Json.Linq.JObject.Parse(SaveGame.Capture(g));
            obj["Version"] = 1;
            obj.Remove("Killed");
            obj.Remove("KnockoutLeft");
            var fresh = new GameState(D);
            fresh.Killed.Add("stale");
            SaveGame.Restore(fresh, obj.ToString());
            Assert.Empty(fresh.Killed);
            Assert.Contains("x", fresh.Picked);
            Assert.True(SaveGame.Version >= 2);
        }

        [Fact]
        public void A_save_during_a_knockout_keeps_the_knockout_and_does_not_double_it()
        {
            var g = new GameState(D);
            g.Condition.Damage(500);
            Assert.True(g.Condition.IsKnockedOut);
            g.Condition.Tick(2f, RestKind.None);
            float left = g.Condition.KnockoutLeft;
            Assert.Equal(D.Wounds.KnockoutSeconds - 2f, left, 3);

            var fresh = new GameState(D);
            SaveGame.Restore(fresh, SaveGame.Capture(g));
            Assert.True(fresh.Condition.IsKnockedOut);
            Assert.Equal(left, fresh.Condition.KnockoutLeft, 3);
            Assert.Equal(0f, fresh.Condition.Hp, 3);

            var ev = fresh.Condition.Tick(left + 0.1f, RestKind.None);
            Assert.Single(ev, e => e.Kind == ConditionEventKind.Revived);
            Assert.False(fresh.Condition.IsKnockedOut);
            Assert.Equal(D.Wounds.MaxHp * D.Wounds.ReviveHpFraction, fresh.Condition.Hp, 3);
            Assert.DoesNotContain(fresh.Condition.Tick(1f, RestKind.None), e => e.Kind == ConditionEventKind.KnockedOut);
        }

        [Fact]
        public void A_healthy_save_does_not_knock_out_on_load()
        {
            var g = new GameState(D);
            g.Condition.Damage(99.5f);
            var fresh = new GameState(D);
            SaveGame.Restore(fresh, SaveGame.Capture(g));
            Assert.False(fresh.Condition.IsKnockedOut);
            Assert.Equal(0f, fresh.Condition.KnockoutLeft);
        }

        [Fact]
        public void A_slot_holding_null_falls_back_to_the_backup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "zd-d01-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string slot = Path.Combine(dir, "slot.json");
                string a = SaveGame.Capture(new GameState(D));
                SaveGame.WriteAtomic(slot, a);
                SaveGame.WriteAtomic(slot, a + " ");
                File.WriteAllText(slot, "null");
                Assert.Equal(a, SaveGame.ReadSlot(slot));
                File.WriteAllText(slot, "");
                Assert.Equal(a, SaveGame.ReadSlot(slot));
            }
            finally { Directory.Delete(dir, true); }
        }

        // ---- 4. срочные реплики ----

        [Fact]
        public void SayFirst_skips_a_topic_on_pause_and_speaks_the_next()
        {
            var r = new Remarks.Remarks(D.Remarks);
            var topics = new[] { Topics.HealthCritical, Topics.HungerStarving };
            string? first = r.SayFirst(topics, 0, n => 0);
            Assert.Contains(first, D.Remarks.Topics[Topics.HealthCritical].Lines);
            double later = D.Remarks.GlobalGapSeconds + 1;
            Assert.True(later < D.Remarks.Topics[Topics.HealthCritical].Cooldown);
            string? second = r.SayFirst(topics, later, n => 0);
            Assert.Contains(second, D.Remarks.Topics[Topics.HungerStarving].Lines);
            Assert.Null(r.SayFirst(topics, later + 1, n => 0)); // global gap
            Assert.Null(r.SayFirst(new string[0], 1000, n => 0));
        }

        // ---- 5. враг не возвращается после нокаута героя ----

        [Theory]
        [InlineData(0.1f)]
        [InlineData(0.5f)]
        [InlineData(0.9f)]
        public void A_wolf_that_walked_away_does_not_come_back_until_the_hero_does(float roll)
        {
            var g = new GameState(D);
            var hero = g.Combat;
            var wolf = Make(g, "wolf", 1f);
            wolf.Provoke();
            for (float t = 0; t < 3 && wolf.State != EnemyState.Windup; t += Dt) wolf.Tick(Dt, hero, roll);
            Assert.Equal(EnemyState.Windup, wolf.State);
            hero.Condition.Wound(WoundType.Cut, 1f); hero.Condition.Wound(WoundType.Fracture, 1f);
            Assert.True(hero.Condition.IsKnockedOut);

            var log = new List<EnemyEvent>();
            for (float t = 0; t < 300f; t += Dt)
            {
                hero.Condition.Tick(Dt, RestKind.None); // the hero wakes in the middle of the walk-away
                log.AddRange(wolf.Tick(Dt, hero, roll));
            }
            Assert.False(hero.Condition.IsKnockedOut);
            Assert.DoesNotContain(log, e => e.Kind == EnemyEventKind.Alerted || e.Kind == EnemyEventKind.Struck);

            // the hero walks up by himself: the wolf notices
            hero.Position = wolf.Position + new Vec2(-5f, 0f);
            var near = wolf.Tick(Dt, hero, roll);
            Assert.Contains(near, e => e.Kind == EnemyEventKind.Alerted);
        }

        [Fact]
        public void A_hit_wakes_the_wolf_that_was_ignoring_the_hero()
        {
            var g = new GameState(D);
            var hero = g.Combat;
            var wolf = Make(g, "wolf", 1f);
            wolf.Provoke();
            hero.Condition.Damage(500);
            for (float t = 0; t < 60f && wolf.State != EnemyState.Idle; t += Dt) { hero.Condition.Tick(Dt, RestKind.None); wolf.Tick(Dt, hero, 0.5f); }
            hero.Position = wolf.Position + new Vec2(-1f, 0f);
            var r = hero.Strike("stick", wolf, 0f);
            Assert.Equal(StrikeOutcome.Hit, r.Outcome);
            Assert.Equal(EnemyState.Alert, wolf.State);
        }

        // ---- 7. события врага доходят наружу ----

        [Fact]
        public void A_blow_reports_the_skill_changes_and_the_knockout()
        {
            var g = new GameState(D);
            var hero = g.Combat;
            hero.Condition.Damage(g.Data.Wounds.MaxHp - 1f);
            var boar = Make(g, "boar", 1f);
            boar.Provoke();
            var log = new List<EnemyEvent>();
            for (float t = 0; t < 6 && !log.Any(e => e.Kind == EnemyEventKind.Struck); t += Dt) log.AddRange(boar.Tick(Dt, hero, 0.3f));
            var struck = log.Single(e => e.Kind == EnemyEventKind.Struck);
            Assert.Contains(struck.SkillChanges, c => c.Stat == Stat.Toughness && c.Weapon == null);
            Assert.Contains(log, e => e.Kind == EnemyEventKind.HeroKnockedOut);
        }

        [Fact]
        public void A_dodge_reports_the_agility_change()
        {
            var g = new GameState(D);
            var hero = g.Combat;
            var boar = Make(g, "boar", 1f);
            boar.Provoke();
            var log = new List<EnemyEvent>();
            bool moved = false;
            for (float t = 0; t < 6 && !log.Any(e => e.Kind == EnemyEventKind.Dodged); t += Dt)
            {
                log.AddRange(boar.Tick(Dt, hero, 0.3f));
                if (!moved && boar.State == EnemyState.Windup) { hero.Position = new Vec2(-5f, 0); moved = true; }
            }
            var dodged = log.Single(e => e.Kind == EnemyEventKind.Dodged);
            Assert.Contains(dodged.SkillChanges, c => c.Stat == Stat.Agility);
        }

        [Fact]
        public void A_strike_hands_back_what_it_did_to_the_enemy()
        {
            var g = new GameState(D);
            var hammered = Make(g, "boar", 1f);
            var r = g.Combat.Strike("hammer", hammered, 0f);
            Assert.Contains(r.EnemyEvents, e => e.Kind == EnemyEventKind.Staggered);
            var g2 = new GameState(D);
            var dying = Make(g2, "wolf", 1f);
            dying.Receive(dying.Hp - 0.5f, null, 0, 0);
            Assert.Contains(g2.Combat.Strike("sword", dying, 0f).EnemyEvents, e => e.Kind == EnemyEventKind.Died);
        }

        // ---- 8. раны врага ----

        [Fact]
        public void A_broken_enemy_chases_slower()
        {
            var g = new GameState(D);
            g.Combat.Position = new Vec2(-9f, 0);
            float Run(bool broken)
            {
                var wolf = Make(g, "wolf", 0f);
                wolf.Provoke();
                if (broken) wolf.Receive(0f, WoundType.Fracture, 1f, 0f);
                for (float t = 0; t < 3f; t += Dt) wolf.Tick(Dt, g.Combat, 0.3f);
                return -wolf.Position.X;
            }
            float healthy = Run(false), hurt = Run(true);
            Assert.True(healthy > 2f);
            Assert.True(hurt < healthy * 0.7f, $"{hurt} vs {healthy}");
            Assert.True(hurt > 0f);
        }

        [Fact]
        public void A_cut_enemy_bleeds_slowly_and_can_bleed_out()
        {
            var g = new GameState(D);
            g.Combat.Position = new Vec2(-500f, 0); // far away: no fight
            var boar = Make(g, "boar", 0f);
            boar.Receive(0f, WoundType.Cut, 0.5f, 0f);
            for (float t = 0; t < 10f; t += Dt) boar.Tick(Dt, g.Combat, 0.3f);
            Assert.True(boar.Hp < boar.MaxHp);
            Assert.True(boar.Hp > boar.MaxHp - 20f, "slow");
            var log = new List<EnemyEvent>();
            for (float t = 0; t < 600f && !boar.IsCarcass; t += Dt) log.AddRange(boar.Tick(Dt, g.Combat, 0.3f));
            Assert.True(boar.IsCarcass);
            Assert.Contains(log, e => e.Kind == EnemyEventKind.Died);

            var brokenOnly = Make(g, "boar", 0f);
            brokenOnly.Receive(0f, WoundType.Fracture, 1f, 0f);
            for (float t = 0; t < 60f; t += Dt) brokenOnly.Tick(Dt, g.Combat, 0.3f);
            Assert.Equal(brokenOnly.MaxHp, brokenOnly.Hp);
        }

        // ---- 9. проверки данных ----

        static Dictionary<string, string> RealFiles()
        {
            var files = new Dictionary<string, string>();
            foreach (var f in Directory.GetFiles(TestPaths.DataRoot, "*.json")) files[Path.GetFileName(f)] = File.ReadAllText(f);
            return files;
        }

        static DataException Broken(string file, string from, string to)
        {
            var files = RealFiles();
            Assert.Contains(from, files[file]);
            files[file] = files[file].Replace(from, to);
            return Assert.Throws<DataException>(() => DataSet.Load(name => files[name]));
        }

        [Fact]
        public void A_reply_icon_must_be_in_the_icon_list()
        {
            var e = Broken("dialogues.json", "{ \"icon\": \"town\", \"to\": \"town\" }", "{ \"icon\": \"bogus\", \"to\": \"town\" }");
            Assert.Contains(e.Problems, p => p.Contains("dialogues.json") && p.Contains("'bogus'"));
        }

        [Fact]
        public void Onboarding_conditions_and_actions_must_be_known()
        {
            var e = Broken("onboarding.json", "\"showWhen\": \"tappable_nearby\"", "\"showWhen\": \"nearby_tapable\"");
            Assert.Contains(e.Problems, p => p.Contains("onboarding.json") && p.Contains("nearby_tapable"));
            var e2 = Broken("onboarding.json", "\"doneBy\": \"swipe\"", "\"doneBy\": \"swipe_left\"");
            Assert.Contains(e2.Problems, p => p.Contains("onboarding.json") && p.Contains("swipe_left"));
        }

        [Fact]
        public void A_world_recipe_result_must_be_an_item_or_a_declared_world_object()
        {
            var e = Broken("recipes.json", "\"result\": \"campfire\", \"keep\"", "\"result\": \"camp_fire\", \"keep\"");
            Assert.Contains(e.Problems, p => p.Contains("recipes.json") && p.Contains("camp_fire"));
            Assert.Contains("campfire", D.WorldObjects);
        }

        [Fact]
        public void Weapons_json_must_have_fists()
        {
            var e = Broken("weapons.json", "\"fists\":", "\"hands\":");
            Assert.Contains(e.Problems, p => p.Contains("weapons.json") && p.Contains("fists"));
        }

        [Fact]
        public void A_weapon_item_without_a_weapons_entry_is_named()
        {
            var e = Broken("weapons.json", "\"sword\":", "\"sword_\":");
            Assert.Contains(e.Problems, p => p.Contains("items.json") && p.Contains("'sword'") && p.Contains("weapons.json"));
        }

        [Fact]
        public void Enum_fields_accept_names_only_not_numbers()
        {
            var e = Broken("weapons.json", "\"class\": \"fists\"", "\"class\": \"3\"");
            Assert.Contains(e.Problems, p => p.Contains("weapons.json") && p.Contains("'3'"));
            var e2 = Broken("weapons.json", "\"wound\": \"cut\",      \"severity\": 0.3, \"stun\": 0 },\n    \"knife\"", "\"wound\": \"1\",      \"severity\": 0.3, \"stun\": 0 },\n    \"knife\"");
            Assert.Contains(e2.Problems, p => p.Contains("weapons.json") && p.Contains("'1'"));
            var e3 = Broken("enemies.json", "\"wound\": \"fracture\"", "\"wound\": \"1\"");
            Assert.Contains(e3.Problems, p => p.Contains("enemies.json") && p.Contains("'1'"));
            Assert.Null(new WeaponDef { Class = "2" }.ParsedClass);
            Assert.Null(new WeaponDef { Wound = "Cut,Burn" }.ParsedWound);
            Assert.Equal(WeaponClass.Blunt, new WeaponDef { Class = "Blunt" }.ParsedClass);
        }

        [Fact]
        public void An_unknown_item_kind_is_named()
        {
            var e = Broken("items.json", "\"kind\": \"medicine\"", "\"kind\": \"potion\"");
            Assert.Contains(e.Problems, p => p.Contains("items.json") && p.Contains("potion"));
        }

        [Fact]
        public void Enemy_wound_effects_name_real_wounds()
        {
            var e = Broken("enemies.json", "\"cut\":      {", "\"cutt\":     {");
            Assert.Contains(e.Problems, p => p.Contains("enemies.json") && p.Contains("cutt"));
        }

        // ---- 11. сон двигает голод ----

        [Fact]
        public void Sleeping_makes_the_hero_hungry()
        {
            var g = new GameState(D);
            g.Hunger.Restore(0.3f);
            double hours0 = g.Clock.TotalHours;
            float h0 = g.Hunger.Value;
            g.Condition.Damage(60);
            g.Sleep();
            Assert.Equal(D.World.SleepHours, g.Clock.TotalHours - hours0, 3);
            float expected = (float)(D.World.SleepHours / 24.0 * D.World.DayLengthSeconds / D.Hunger.SecondsToFull);
            Assert.Equal(h0 + expected, g.Hunger.Value, 3);
            Assert.True(g.Hunger.Level >= HungerLevel.Peckish, "проспала 8 часов — проголодалась");
            Assert.True(g.Condition.Hp >= D.Wounds.MaxHp * D.Wounds.SleepHpFraction);
        }

        [Fact]
        public void Skipping_hours_moves_hunger_too_and_never_past_starving()
        {
            var g = new GameState(D);
            g.SkipHours(24 * 30);
            Assert.Equal(1f, g.Hunger.Value);
        }
    }
}
