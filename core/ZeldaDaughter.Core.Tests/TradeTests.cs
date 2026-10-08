using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-03: project-design.md §2 «Экономика — бартер + монеты».</summary>
    public class TradeTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState Noon()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            return g;
        }

        static TradeOffer Swap(string give, int gn, string take, int tn) =>
            new TradeOffer(new[] { new TradeLine(give, gn) }, new[] { new TradeLine(take, tn) });

        [Fact]
        public void Traders_value_the_same_thing_differently()
        {
            var g = Noon();
            Assert.True(g.Trade.BuyValue("smith", "ore") > g.Trade.BuyValue("herbalist", "ore"));
            Assert.True(g.Trade.BuyValue("herbalist", "berries") > g.Trade.BuyValue("smith", "berries"));
        }

        [Fact]
        public void Ore_buys_a_knife_from_the_smith_by_barter_but_the_herbalist_does_not_want_it()
        {
            var g = Noon();
            g.Bag.Add("ore", 3);
            var r = g.Trade.Execute("smith", Swap("ore", 3, "knife", 1));
            Assert.Equal(TradeOutcome.Done, r.Outcome);
            Assert.Equal(1, g.Bag.Count("knife"));
            Assert.Equal(0, g.Bag.Count("ore"));
            // the same ore at the herbalist is not enough for even a bandage
            var h = Noon();
            h.Bag.Add("ore", 1);
            Assert.Equal(TradeOutcome.NotEnough, h.Trade.Execute("herbalist", Swap("ore", 1, "burn_salve", 1)).Outcome);
        }

        [Fact]
        public void Berries_are_worth_more_to_the_herbalist_than_to_the_smith()
        {
            var g = Noon();
            g.Bag.Add("berries", 6);
            Assert.Equal(TradeOutcome.NotWanted, g.Trade.Execute("smith", Swap("berries", 6, "knife", 1)).Outcome); // the smith does not buy food at all
            Assert.Equal(6, g.Bag.Count("berries"));
            Assert.Equal(TradeOutcome.Done, g.Trade.Execute("herbalist", Swap("berries", 6, "bandage", 1)).Outcome);
        }

        [Fact]
        public void Without_coins_you_can_still_buy_by_barter_and_the_first_barter_comes_before_coin_lessons()
        {
            var g = Noon();
            g.Bag.Add("coin", 20);
            Assert.False(g.Trade.KnowsCoins);
            Assert.Equal(TradeOutcome.CoinsUnknown, g.Trade.Execute("merchant", Swap("coin", 20, "cloth", 1)).Outcome);
            Assert.False(g.Trade.TeachCoins());          // nobody teaches coins before a first barter
            Assert.False(g.Trade.KnowsCoins);

            g.Bag.Add("fang", 2);
            Assert.Equal(TradeOutcome.Done, g.Trade.Execute("merchant", Swap("fang", 2, "cloth", 1)).Outcome);
            Assert.Equal(1, g.Trade.BarterDeals);
            Assert.True(g.Trade.TeachCoins());
            Assert.True(g.Trade.KnowsCoins);
            var pay = g.Trade.Execute("merchant", Swap("coin", 20, "flint", 1));
            Assert.Equal(TradeOutcome.Done, pay.Outcome);
            Assert.Equal(1, g.Bag.Count("flint"));
        }

        [Fact]
        public void Understanding_the_language_also_teaches_money()
        {
            var g = Noon();
            g.Language.Restore(D.Language.MoneyAt + 0.01f);
            Assert.True(g.Trade.KnowsCoins);
        }

        [Fact]
        public void Coins_come_back_as_change_when_selling()
        {
            var g = Noon();
            g.Bag.Add("berries", 4);
            g.Bag.Add("fang"); g.Bag.Add("fang");
            g.Trade.Execute("merchant", Swap("fang", 2, "cloth", 1));
            g.Trade.TeachCoins();
            var r = g.Trade.Execute("herbalist", Swap("berries", 4, "coin", 3));
            Assert.Equal(TradeOutcome.Done, r.Outcome);
            Assert.Equal(3, g.Bag.Count("coin"));
        }

        [Fact]
        public void What_you_sold_can_be_bought_back_for_what_you_were_paid()
        {
            var g = Noon();
            g.Bag.Add("berries", 5);
            g.Bag.Add("fang", 2);
            g.Trade.Execute("merchant", Swap("fang", 2, "cloth", 1));
            g.Trade.TeachCoins();
            float paid = g.Trade.BuyValue("herbalist", "berries") * 5;
            var sold = g.Trade.Execute("herbalist", new TradeOffer(new[] { new TradeLine("berries", 5) }, new[] { new TradeLine("coin", (int)System.Math.Floor(paid)) }));
            Assert.Equal(TradeOutcome.Done, sold.Outcome);
            Assert.Equal(5, g.Trade.Buyback("herbalist").Single(x => x.Item == "berries").Count);
            Assert.Empty(g.Trade.Buyback("smith"));

            g.Bag.Add("coin", 20);
            var back = g.Trade.Execute("herbalist", new TradeOffer(new[] { new TradeLine("coin", 20) }, new[] { new TradeLine("berries", 5, fromBuyback: true) }));
            Assert.Equal(TradeOutcome.Done, back.Outcome);
            Assert.Equal(5, g.Bag.Count("berries"));
            Assert.Empty(g.Trade.Buyback("herbalist"));
            // the buyback price is what the trader paid
            Assert.Equal(g.Trade.BuyValue("herbalist", "berries"), g.Trade.BuybackPrice("herbalist", "berries"), 4);
        }

        [Fact]
        public void No_room_leaves_bag_stock_and_buyback_as_they_were()
        {
            var g = Noon();
            g.Bag.Add("berries", 1);
            g.Bag.Add("fang", 2);
            g.Trade.Execute("merchant", Swap("fang", 2, "cloth", 1));
            g.Trade.TeachCoins();
            g.Bag.Add("coin", 99);
            // fill every slot with different non-stackable-with-anything-else items
            foreach (var id in D.Items.Keys.Where(k => k != "coin" && k != "berries" && k != "cloth" && k != "arrowhead"))
            {
                if (g.Bag.UsedSlots >= D.Inventory.Slots) break;
                g.Bag.Add(id);
            }
            Assert.Equal(D.Inventory.Slots, g.Bag.UsedSlots);
            var before = SaveGame.Capture(g);
            var r = g.Trade.Execute("smith", new TradeOffer(new[] { new TradeLine("coin", 99) }, new[] { new TradeLine("arrowhead", 30) }));
            Assert.Equal(TradeOutcome.NoRoom, r.Outcome);
            Assert.Equal(before, SaveGame.Capture(g));
        }

        [Fact]
        public void Missing_items_and_empty_stock_and_unwanted_things_are_refused()
        {
            var g = Noon();
            Assert.Equal(TradeOutcome.MissingItems, g.Trade.Execute("smith", Swap("ore", 1, "knife", 1)).Outcome);
            g.Bag.Add("stick");
            Assert.Equal(TradeOutcome.NotInStock, g.Trade.Execute("smith", Swap("stick", 1, "berries", 1)).Outcome); // the smith sells no berries
            Assert.Equal(TradeOutcome.Empty, g.Trade.Execute("smith", new TradeOffer(new TradeLine[0], new TradeLine[0])).Outcome);
        }

        [Fact]
        public void A_limited_stock_runs_out_and_stays_out()
        {
            var g = Noon();
            g.Bag.Add("ore", 7);
            Assert.Equal(TradeOutcome.Done, g.Trade.Execute("smith", Swap("ore", 7, "sword", 1)).Outcome);
            g.Bag.Add("ore", 7);
            Assert.Equal(TradeOutcome.NotInStock, g.Trade.Execute("smith", Swap("ore", 7, "sword", 1)).Outcome);
        }

        [Fact]
        public void Shut_shops_do_not_trade()
        {
            var g = Noon();
            g.Clock.SetTime(1, 2.0 / 24.0);
            g.Bag.Add("ore", 3);
            Assert.Equal(TradeOutcome.Closed, g.Trade.Execute("smith", Swap("ore", 3, "knife", 1)).Outcome);
            Assert.Equal(3, g.Bag.Count("ore"));
        }

        [Fact]
        public void Unknown_trader_and_worthless_things_are_refused()
        {
            var g = Noon();
            g.Bag.Add("map");
            Assert.Equal(TradeOutcome.Unknown, g.Trade.Execute("nobody", Swap("map", 1, "cloth", 1)).Outcome);
            Assert.Equal(TradeOutcome.Unknown, g.Trade.Execute("guard", Swap("map", 1, "cloth", 1)).Outcome); // no shop
        }

        [Fact]
        public void No_trader_buys_dearer_than_any_sells_so_there_is_no_arbitrage()
        {
            var t = D.Traders;
            foreach (var item in D.Items.Values.Where(i => i.Value > 0 && i.Kind != "currency"))
            {
                float bestBuy = t.Traders.Values.Max(tr => t.BuyShare * tr.Multiplier(item));
                Assert.True(bestBuy < t.SellMarkup, $"{item.Id}: best buy {bestBuy} vs markup {t.SellMarkup}");
            }
        }

        [Fact]
        public void Data_is_consistent_shops_have_traders_stock_has_prices()
        {
            foreach (var kv in D.Npcs.Npcs) Assert.Equal(kv.Value.Shop, D.Traders.Traders.ContainsKey(kv.Key));
            foreach (var tr in D.Traders.Traders.Values)
                foreach (var s in tr.Stock) Assert.True(D.Items[s.Item].Value > 0, s.Item);
            Assert.Equal(1, D.Items["coin"].Value);
        }

        [Fact]
        public void Broken_trader_data_is_named()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["traders.json"] = files["traders.json"].Replace("\"knife\"", "\"nife\"");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("traders.json") && p.Contains("nife"));
        }

        [Fact]
        public void Same_inputs_same_deals_and_the_save_round_trips()
        {
            int g0Deals = 0;
            string Play()
            {
                var g = Noon();
                g.Bag.Add("berries", 6); g.Bag.Add("fang", 2); g.Bag.Add("ore", 7);
                g.Trade.Execute("merchant", Swap("fang", 2, "cloth", 1));
                g.Trade.TeachCoins();
                g.Trade.Execute("herbalist", Swap("berries", 6, "coin", 4));
                g.Trade.Execute("smith", Swap("ore", 7, "sword", 1));
                g0Deals = g.Trade.BarterDeals;
                return SaveGame.Capture(g);
            }
            string a = Play();
            Assert.Equal(a, Play());
            var fresh = Noon();
            SaveGame.Restore(fresh, a);
            Assert.Equal(a, SaveGame.Capture(fresh));
            Assert.True(fresh.Trade.KnowsCoins);
            Assert.Equal(g0Deals, fresh.Trade.BarterDeals);
            Assert.Equal(6, fresh.Trade.Buyback("herbalist").Single().Count);
            // sold sword stays gone after load
            fresh.Bag.Add("ore", 7);
            Assert.Equal(TradeOutcome.NotInStock, fresh.Trade.Execute("smith", Swap("ore", 7, "sword", 1)).Outcome);
        }

        [Fact]
        public void Version_one_and_two_saves_still_load()
        {
            var g = Noon();
            var json = SaveGame.Capture(g).Replace($"\"Version\": {SaveGame.Version}", "\"Version\": 2");
            var fresh = Noon();
            SaveGame.Restore(fresh, json);
            Assert.False(fresh.Trade.KnowsCoins);
        }
    }
}
