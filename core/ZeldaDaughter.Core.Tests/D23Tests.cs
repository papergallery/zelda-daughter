using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-23, docs/done-criteria/D-23.md: the loop of the demo — the map within reach, fire against wolves, a torch that burns down, item descriptions, thanks, new remarks.</summary>
    public class D23Tests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);
        const float Dt = 0.05f;

        static GameState Noon()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            return g;
        }

        // ------------------------------------------------------------------ 1. the map is within reach

        [Fact]
        public void A_typical_round_earns_more_than_the_map_costs()
        {
            var r = D.Traders.TypicalRound;
            float price = RoundIncome.GoalPrice(D, r);
            Assert.Equal(30f, price, 3);                                      // the map: value 24 × 1.25
            float hunt = RoundIncome.QuestCoins(D, r) + RoundIncome.LootValue(D, r);
            Assert.True(hunt >= price, $"requests + the hunt bring {hunt}, the map costs {price}");
            Assert.True(RoundIncome.Total(D, r, includeGathered: true) >= price * 1.5f, "with what is picked up on the way there is room to spare");
        }

        [Fact]
        public void The_same_round_pays_for_the_map_at_the_merchants_own_counter_without_a_tour_of_the_town()
        {
            var r = D.Traders.TypicalRound;
            Assert.True(RoundIncome.Total(D, r, includeGathered: false, onlyAt: r.Seller) >= RoundIncome.GoalPrice(D, r));
        }

        [Fact]
        public void Without_the_knife_and_the_requests_the_map_is_far_away_which_is_what_the_hunt_is_for()
        {
            // bare hands: one fang per carcass, no requests — nowhere near; the knife (the smith's, or ore on the anvil) is the way in
            var fang = D.Enemies.Enemies["boar"].Loot.Minimal.Sum(i => RoundIncome.BestPrice(D, i.Key) * i.Value);
            Assert.True(fang < RoundIncome.GoalPrice(D, D.Traders.TypicalRound) / 4);
        }

        [Fact]
        public void The_full_round_is_really_sellable_at_the_merchants_by_the_trade_rules()
        {
            var g = Noon();
            var r = D.Traders.TypicalRound;
            foreach (var kv in D.Enemies.Enemies["boar"].Loot.Full) g.Bag.Add(kv.Key, kv.Value);
            g.Bag.Add("coin", (int)RoundIncome.QuestCoins(D, r));
            g.Bag.Add("stone", 3);
            Assert.Equal(TradeOutcome.Done, g.Trade.Execute("merchant", new TradeOffer(new[] { new TradeLine("stone", 3) }, new[] { new TradeLine("stick", 1) })).Outcome);   // the first barter
            Assert.True(g.Trade.TeachCoins());
            var give = new List<TradeLine> { new TradeLine("coin", g.Bag.Count("coin")) };
            foreach (var kv in D.Enemies.Enemies["boar"].Loot.Full) give.Add(new TradeLine(kv.Key, g.Bag.Count(kv.Key)));
            var res = g.Trade.Execute("merchant", new TradeOffer(give, new[] { new TradeLine("map", 1) }));
            Assert.Equal(TradeOutcome.Done, res.Outcome);
            Assert.Equal(1, g.Bag.Count("map"));
        }

        // ------------------------------------------------------------------ 2. fire against wolves

        static GameState Arena(out Enemy wolf, Vec2 wolfAt)
        {
            var g = Noon();
            g.HeroPosition = new Vec2(0, 0);
            g.Combat.Position = new Vec2(0, 0);
            wolf = g.Enemies.Spawn("w", "wolf", wolfAt)!;
            return g;
        }

        static void Run(GameState g, float seconds, Action<float>? each = null)
        {
            var notices = new List<EnemyNotice>();
            for (float t = 0; t < seconds; t += Dt) { each?.Invoke(t); g.Enemies.Tick(Dt, 0.4, notices); }
        }

        [Fact]
        public void A_wolf_does_not_come_closer_to_a_campfire_than_its_reach_and_never_winds_up_at_the_one_who_sits_by_it()
        {
            var g = Arena(out var wolf, new Vec2(18, 0));
            g.Camp.Restore(1, null, new[] { new Campfire("fire", new Vec2(0, 0), 5000f, 10f) });
            wolf.Provoke();
            var notices = new List<EnemyNotice>();
            float nearest = float.MaxValue;
            for (float t = 0; t < 60; t += Dt)
            {
                g.Enemies.Tick(Dt, 0.4, notices);
                Assert.DoesNotContain(notices, n => n.Event.Kind == EnemyEventKind.WindupStarted || n.Event.Kind == EnemyEventKind.Struck);
                nearest = Math.Min(nearest, wolf.Position.Length);
            }
            Assert.True(nearest >= D.Enemies.Fire.CampfireRadius - 0.01f, $"the wolf got to {nearest} m");
            Assert.True(wolf.Position.Length <= D.Enemies.Fire.CampfireRadius + 1.5f, "…and stands at the edge of the light, waiting");
            Assert.Equal(0f, g.Condition.WoundLoad);
        }

        [Fact]
        public void A_wolf_inside_the_reach_runs_from_the_fire_and_says_so_once()
        {
            var g = Arena(out var wolf, new Vec2(3, 0));
            g.Camp.Restore(1, null, new[] { new Campfire("fire", new Vec2(0, 0), 5000f, 10f) });
            wolf.Provoke();
            var notices = new List<EnemyNotice>();
            int frightened = 0;
            float far = 0f;
            for (float t = 0; t < 4; t += Dt)
            {
                g.Enemies.Tick(Dt, 0.4, notices);
                frightened += notices.Count(n => n.Event.Kind == EnemyEventKind.Frightened);
                far = Math.Max(far, wolf.Position.Length);
            }
            Assert.Equal(1, frightened);
            Assert.True(far >= D.Enemies.Fire.CampfireRadius, $"it ran {far} m from the fire");
        }

        [Fact]
        public void The_boar_does_not_care_about_fire()
        {
            var g = Noon();
            g.HeroPosition = new Vec2(0, 0);
            g.Combat.Position = new Vec2(0, 0);
            var boar = g.Enemies.Spawn("b", "boar", new Vec2(4, 0))!;
            g.Camp.Restore(1, null, new[] { new Campfire("fire", new Vec2(0, 0), 5000f, 10f) });
            boar.Provoke();
            bool wound = false;
            var notices = new List<EnemyNotice>();
            for (float t = 0; t < 6 && !wound; t += Dt) { g.Enemies.Tick(Dt, 0.4, notices); wound |= notices.Any(n => n.Event.Kind == EnemyEventKind.WindupStarted); }
            Assert.True(wound);
        }

        [Fact]
        public void A_torch_keeps_a_wolf_off_the_one_who_carries_it_even_when_she_walks_into_it()
        {
            var g = Arena(out var wolf, new Vec2(8, 0));
            g.Bag.Add("torch");
            wolf.Provoke();
            var notices = new List<EnemyNotice>();
            var struck = false;
            for (float t = 0; t < 40; t += Dt)
            {
                // she walks toward the wolf and back, the torch with her
                float x = 6f * (float)Math.Sin(t * 0.5);
                g.HeroPosition = new Vec2(x, 0);
                g.Combat.Position = g.HeroPosition;
                g.Enemies.Tick(Dt, 0.4, notices);
                struck |= notices.Any(n => n.Event.Kind == EnemyEventKind.Struck);
                Assert.True((wolf.Position - g.HeroPosition).Length >= D.Enemies.Fire.TorchRadius * g.Torch.Light - 0.3f, $"t={t}: {(wolf.Position - g.HeroPosition).Length}");
            }
            Assert.False(struck);
            Assert.True(D.Enemies.Fire.TorchRadius < D.Enemies.Fire.CampfireRadius, "a torch scares less than a campfire");
        }

        [Fact]
        public void A_wolf_attacks_one_who_has_no_fire()
        {
            var g = Arena(out var wolf, new Vec2(9, 0));
            wolf.Provoke();
            bool windup = false, struck = false;
            var notices = new List<EnemyNotice>();
            for (float t = 0; t < 8 && !struck; t += Dt)
            {
                g.Enemies.Tick(Dt, 0.4, notices);
                windup |= notices.Any(n => n.Event.Kind == EnemyEventKind.WindupStarted);
                struck |= notices.Any(n => n.Event.Kind == EnemyEventKind.Struck);
            }
            Assert.True(windup && struck, "no fire — the wolf comes, winds up and bites");
        }

        [Fact]
        public void A_dying_fire_scares_less_and_a_dead_one_not_at_all()
        {
            var g = Noon();
            g.Camp.Restore(1, null, new[] { new Campfire("fire", new Vec2(0, 0), D.Camp.FadeSeconds / 2, D.Camp.FadeSeconds) });
            Assert.True(g.FireAt(new Vec2(D.Enemies.Fire.CampfireRadius * 0.4f, 0), out _, out float r));
            Assert.Equal(D.Enemies.Fire.CampfireRadius / 2, r, 3);
            Assert.False(g.FireAt(new Vec2(D.Enemies.Fire.CampfireRadius * 0.6f, 0), out _, out _));
            var none = Noon();
            Assert.False(none.FireAt(new Vec2(0.1f, 0), out _, out _));
        }

        [Fact]
        public void Fire_data_is_checked()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["enemies.json"] = files["enemies.json"].Replace("\"campfireRadius\": 9.0", "\"campfireRadius\": 5.0");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("campfireRadius"));
        }

        // ------------------------------------------------------------------ 3. the torch burns down

        [Fact]
        public void A_lit_torch_burns_for_the_time_in_data_then_becomes_a_burnt_stick()
        {
            var g = Noon();
            g.Bag.Add("torch");
            var events = new List<WorldEventKind>();
            float t = 0;
            for (; t < D.Camp.TorchBurnSeconds * 2 && g.Bag.Count("torch") > 0; t += 0.25f)
                foreach (var e in g.TickWorld(0.25f, 0.9)) events.Add(e.Kind);
            Assert.Equal(D.Camp.TorchBurnSeconds, t, 0);
            Assert.Equal(0, g.Bag.Count("torch"));
            Assert.Equal(1, g.Bag.Count("burnt_stick"));
            Assert.Equal(1, events.Count(k => k == WorldEventKind.TorchBurntOut));
            Assert.False(g.Torch.IsLit);
            Assert.Equal(0f, g.Torch.Light);
        }

        [Fact]
        public void The_light_of_the_torch_is_full_then_weakens_over_the_last_seconds()
        {
            var g = Noon();
            g.Bag.Add("torch");
            Assert.Equal(1f, g.Torch.Light);
            g.TickWorld(0.25f, 0.9);
            Assert.Equal(1f, g.Torch.Light);
            float prev = 1f;
            bool dimmed = false;
            for (float t = 0; g.Bag.Count("torch") > 0; t += 0.25f)
            {
                g.TickWorld(0.25f, 0.9);
                if (g.Bag.Count("torch") == 0) break;
                Assert.True(g.Torch.Light <= prev + 1e-6f);
                prev = g.Torch.Light;
                if (g.Torch.Left < D.Camp.TorchFadeSeconds / 2) { Assert.True(g.Torch.Light < 0.6f); dimmed = true; }
            }
            Assert.True(dimmed);
        }

        [Fact]
        public void A_torch_burns_down_faster_in_the_rain()
        {
            var dry = Noon(); dry.Bag.Add("torch");
            var wet = Noon(); wet.Bag.Add("torch");
            dry.Nature.Weather.Restore(false, 0, 0);
            wet.Nature.Weather.Restore(true, 100000f, 0);
            for (int i = 0; i < 100; i++) { dry.TickWorld(0.25f, 0.9); wet.TickWorld(0.25f, 0.9); }
            Assert.Equal(D.Camp.TorchBurnSeconds - 25f, dry.Torch.Left, 1);
            Assert.Equal(D.Camp.TorchBurnSeconds - 25f * D.Camp.TorchRainBurnFactor, wet.Torch.Left, 1);
        }

        [Fact]
        public void No_torch_in_the_bag_nothing_burns_and_a_new_one_starts_full_the_burnt_stick_feeds_a_fire()
        {
            var g = Noon();
            for (int i = 0; i < 40; i++) g.TickWorld(0.25f, 0.9);
            Assert.Equal(0f, g.Torch.Saved);
            g.Bag.Add("torch");
            Assert.Equal(D.Camp.TorchBurnSeconds, g.Torch.Left);
            Assert.Equal(1f, g.Torch.Light);
            Assert.True(D.Camp.Fuel.ContainsKey(D.Camp.BurntItem));
            Assert.Equal(0, D.Items[D.Camp.BurntItem].Value);
        }

        [Fact]
        public void The_state_of_the_torch_is_saved()
        {
            var g = Noon();
            g.Bag.Add("torch");
            for (int i = 0; i < 200; i++) g.TickWorld(0.25f, 0.9);
            float left = g.Torch.Left;
            Assert.True(left < D.Camp.TorchBurnSeconds - 49);
            string a = SaveGame.Capture(g);
            var fresh = new GameState(D);
            SaveGame.Restore(fresh, a);
            Assert.Equal(left, fresh.Torch.Left, 2);
            Assert.Equal(a, SaveGame.Capture(fresh));
        }

        [Fact]
        public void A_torch_that_burns_is_fine_with_no_allocations_in_a_quiet_frame()
        {
            var g = Noon();
            g.Bag.Add("torch");
            g.TickWorld(0.25f, 0.5);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) g.TickWorld(0.25f, 0.5);
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 2048);
        }

        // ------------------------------------------------------------------ 5. descriptions

        [Fact]
        public void Every_item_has_a_short_description_without_numbers_in_living_russian()
        {
            Assert.All(D.Items.Values, it =>
            {
                Assert.False(string.IsNullOrWhiteSpace(it.Description), it.Id);
                Assert.InRange(it.Description.Length, 20, 160);
                Assert.DoesNotContain(it.Description, c => char.IsDigit(c));
                Assert.Contains(it.Description, c => c >= 'А' && c <= 'я');
                Assert.EndsWith(".", it.Description.TrimEnd());
            });
            Assert.Equal(D.Items.Count, D.Items.Values.Select(i => i.Description).Distinct().Count());
        }

        [Fact]
        public void A_missing_description_is_a_data_problem()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["items.json"] = files["items.json"].Replace("\"description\": \"Звериный клык.", "\"description\": \"\", \"x\": \"Звериный клык.");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("fang") && p.Contains("description"));
        }

        // ------------------------------------------------------------------ 6. thanks

        [Fact]
        public void Every_request_ends_with_the_receivers_thanks_and_the_reward_lands_in_the_bag()
        {
            foreach (var kv in D.Quests.Quests)
            {
                var g = Noon();
                g.Language.Restore(1f);   // she understands the words: the node reads as text, not as runes
                var q = kv.Value;
                foreach (var h in q.HandOut) g.Bag.Add(h.Key, h.Value);
                if (q.RequiresOffer) Assert.Equal(OfferOutcome.Accepted, g.Quests.Offer(kv.Key));
                foreach (var n in q.Need) g.Bag.Add(n.Key, n.Value - g.Bag.Count(n.Key));
                var r = g.Quests.Give(q.Receiver, q.Need.Keys.First());
                Assert.Equal(QuestOutcome.Done, r.Outcome);
                Assert.Equal(q.Thanks, r.Thanks);
                var node = D.Dialogues.Npcs[q.Receiver].Nodes[q.Thanks];
                Assert.Contains("спасибо", node.Line.ToLowerInvariant());
                Assert.True(node.Icons.Count > 0, "runes or icons by the stage of the language");
                foreach (var it in q.Reward.Items) Assert.Equal(it.Value, g.Bag.Count(it.Key));
                var talk = g.Talk(q.Receiver, r.Thanks!);
                Assert.Equal(node.Line, talk.Current.Text);
                Assert.True(talk.Ended);
            }
        }

        // ------------------------------------------------------------------ 7. the heroine speaks

        static readonly string[] NewTopics = { Topics.WolfClose, Topics.WolfFlees, Topics.TorchDying, Topics.Rain, Topics.FirstPurchase, Topics.Sated };

        [Fact]
        public void At_least_three_new_situations_have_three_or_more_lines_each()
        {
            Assert.True(NewTopics.Length >= 3);
            foreach (var t in NewTopics)
            {
                Assert.True(D.Remarks.Topics.ContainsKey(t), t);
                Assert.True(D.Remarks.Topics[t].Lines.Count >= 3, t);
                Assert.Equal(D.Remarks.Topics[t].Lines.Count, D.Remarks.Topics[t].Lines.Distinct().Count());
                Assert.Contains(t, Topics.All);
            }
        }

        [Fact]
        public void A_new_topic_never_repeats_its_line_twice_in_a_row()
        {
            foreach (var t in NewTopics)
            {
                var rem = new Remarks.Remarks(D.Remarks);
                string? prev = null;
                double now = 0;
                var rng = new Random(7);
                for (int i = 0; i < 60; i++)
                {
                    now += D.Remarks.Topics[t].Cooldown + D.Remarks.GlobalGapSeconds + 1;
                    var line = rem.Say(t, now, n => rng.Next(n));
                    Assert.NotNull(line);
                    Assert.NotEqual(prev, line);
                    prev = line;
                }
            }
        }

        [Fact]
        public void The_state_calls_for_the_new_remarks()
        {
            var rem = new Remarks.Remarks(D.Remarks);
            var c = new HeroCondition(D.Wounds);
            var h = new Hunger(D.Hunger);
            Assert.Contains(Topics.WolfClose, rem.ConditionTopics(c, h, false, false, wolfClose: true));
            Assert.Contains(Topics.Rain, rem.ConditionTopics(c, h, false, false, raining: true));
            Assert.Contains(Topics.TorchDying, rem.ConditionTopics(c, h, false, false, torchDying: true));
            Assert.Empty(rem.ConditionTopics(c, h, false, false));
        }

        [Fact]
        public void A_wolf_close_by_is_known_to_the_core()
        {
            var g = Noon();
            g.HeroPosition = new Vec2(0, 0);
            Assert.False(g.PredatorNear(25f));
            g.Enemies.Spawn("w", "wolf", new Vec2(20, 0));
            g.Enemies.Spawn("b", "boar", new Vec2(2, 0));
            Assert.True(g.PredatorNear(25f));
            Assert.False(g.PredatorNear(15f), "the boar is not a hunter on sight");
        }

        [Fact]
        public void The_first_purchase_is_marked_once_and_a_meal_that_fills_her_is_marked()
        {
            var g = Noon();
            g.Bag.Add("stone", 6);
            var r1 = g.Trade.Execute("merchant", new TradeOffer(new[] { new TradeLine("stone", 3) }, new[] { new TradeLine("stick", 1) }));
            Assert.Equal(TradeOutcome.Done, r1.Outcome);
            Assert.True(r1.FirstPurchase);
            var r2 = g.Trade.Execute("merchant", new TradeOffer(new[] { new TradeLine("stone", 3) }, new[] { new TradeLine("stick", 1) }));
            Assert.False(r2.FirstPurchase);
            var sell = Noon();
            sell.Bag.Add("fang");
            Assert.True(sell.Trade.Execute("merchant", new TradeOffer(new[] { new TradeLine("fang", 1) }, new[] { new TradeLine("stick", 1) })).FirstPurchase, "taking a thing off the shelf is a purchase, barter or coins");

            var hungry = Noon();
            hungry.Hunger.Restore(D.Hunger.HungryAt + 0.01f);
            hungry.Bag.Add("cooked_meat", 5);
            bool sated = false;
            for (int i = 0; i < 5 && !sated; i++) sated = hungry.UseOnHero("cooked_meat").Sated;
            Assert.True(sated);
            Assert.Equal(HungerLevel.Fed, hungry.Hunger.Level);
            var fed = Noon();
            fed.Bag.Add("berries");
            Assert.False(fed.UseOnHero("berries").Sated, "not hungry before — no «наелась»");
        }

        [Fact]
        public void Typical_round_data_is_checked()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["traders.json"] = files["traders.json"].Replace("\"quests\": [ \"letter\"", "\"quests\": [ \"letterr\"");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("typicalRound") && p.Contains("letterr"));
        }
    }
}
