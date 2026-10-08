using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C5: <c>Trade.Evaluate</c> answers what a deal would give — the trade window's «will she agree» — and changes nothing.</summary>
    public class TradeEvaluateTests
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

        static string Snapshot(GameState g) =>
            string.Join(",", g.Bag.Stacks.Select(s => $"{s.ItemId}:{s.Count}")) + "|" + g.Trade.BarterDeals
            + "|" + string.Join(",", g.Trade.Stock("smith").Select(s => $"{s.Item}:{s.Count}"))
            + "|" + string.Join(",", g.Trade.Buyback("smith").Select(s => s.ToString()));

        [Fact]
        public void Evaluate_gives_the_same_answer_as_Execute_but_changes_nothing()
        {
            var g = Noon();
            g.Bag.Add("ore", 3);
            var offer = Swap("ore", 3, "knife", 1);
            string before = Snapshot(g);
            var e = g.Trade.Evaluate("smith", offer);
            Assert.Equal(before, Snapshot(g));
            Assert.Equal(TradeOutcome.Done, e.Outcome);
            Assert.True(e.FirstBarter);

            var x = g.Trade.Execute("smith", offer);
            Assert.Equal(e.Outcome, x.Outcome);
            Assert.Equal(e.GiveValue, x.GiveValue, 5);
            Assert.Equal(e.TakeValue, x.TakeValue, 5);
            Assert.Equal(e.FirstBarter, x.FirstBarter);
            Assert.Equal(1, g.Bag.Count("knife"));
        }

        [Fact]
        public void Evaluate_can_be_asked_again_and_again()
        {
            var g = Noon();
            g.Bag.Add("ore", 3);
            var offer = Swap("ore", 3, "knife", 1);
            for (int i = 0; i < 3; i++) Assert.Equal(TradeOutcome.Done, g.Trade.Evaluate("smith", offer).Outcome);
            Assert.Equal(3, g.Bag.Count("ore"));
            Assert.Equal(0, g.Trade.BarterDeals);
        }

        [Fact]
        public void Evaluate_says_not_enough_with_both_values()
        {
            var g = Noon();
            g.Bag.Add("ore", 1);
            var e = g.Trade.Evaluate("smith", Swap("ore", 1, "knife", 1));
            Assert.Equal(TradeOutcome.NotEnough, e.Outcome);
            Assert.True(e.GiveValue < e.TakeValue);
            Assert.Equal(1, g.Bag.Count("ore"));
        }

        [Fact]
        public void Evaluate_reports_a_closed_shop_missing_items_and_unknown_traders()
        {
            var g = Noon();
            Assert.Equal(TradeOutcome.Unknown, g.Trade.Evaluate("peasant", Swap("ore", 1, "knife", 1)).Outcome);
            Assert.Equal(TradeOutcome.MissingItems, g.Trade.Evaluate("smith", Swap("ore", 1, "knife", 1)).Outcome);
            g.Clock.SetTime(1, 0.95);
            g.Bag.Add("ore", 3);
            Assert.Equal(TradeOutcome.Closed, g.Trade.Evaluate("smith", Swap("ore", 3, "knife", 1)).Outcome);
        }

        [Fact]
        public void Evaluate_sees_that_there_is_no_room_without_touching_the_bag()
        {
            var g = Noon();
            g.Bag.Add("ore", 5);
            g.Bag.Add("metal", 5);
            // every other slot is full of single knives; the deal frees two slots (ore, metal) but asks for three (a knife each)
            while (g.Bag.UsedSlots < D.Inventory.Slots) Assert.True(g.Bag.Add("knife"));
            string before = Snapshot(g);
            var give = new[] { new TradeLine("ore", 5), new TradeLine("metal", 5) };
            var take = new[] { new TradeLine("knife", 3) };
            var e = g.Trade.Evaluate("smith", new TradeOffer(give, take));
            Assert.Equal(before, Snapshot(g));
            Assert.Equal(TradeOutcome.NoRoom, e.Outcome);
            Assert.Equal(TradeOutcome.NoRoom, g.Trade.Execute("smith", new TradeOffer(give, take)).Outcome);
            Assert.Equal(before, Snapshot(g));
            // two knives fit into the two freed slots
            Assert.Equal(TradeOutcome.Done, g.Trade.Evaluate("smith", new TradeOffer(give, new[] { new TradeLine("knife", 2) })).Outcome);
        }
    }
}
