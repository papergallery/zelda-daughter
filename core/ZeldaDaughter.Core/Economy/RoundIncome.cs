#nullable enable
using System;
using System.Linq;
using ZeldaDaughter.Core.Data;

namespace ZeldaDaughter.Core.Economy
{
    /// <summary>The income of a typical round (D-23): is the map within a player's reach. Pure arithmetic over data — no state.</summary>
    public static class RoundIncome
    {
        /// <summary>The best price any trader pays for one piece of the item (0 — nobody buys it). Coins count at face value.</summary>
        public static float BestPrice(DataSet d, string itemId)
        {
            var it = d.Items[itemId];
            if (it.Kind == "currency") return it.Value;
            float best = 0f;
            foreach (var t in d.Traders.Traders.Values) best = Math.Max(best, it.Value * d.Traders.BuyShare * t.Multiplier(it));
            return best;
        }

        /// <summary>What <paramref name="seller"/> alone pays for one piece (the player who sells only where she buys).</summary>
        public static float PriceAt(DataSet d, string traderId, string itemId)
        {
            var it = d.Items[itemId];
            return it.Kind == "currency" ? it.Value : it.Value * d.Traders.BuyShare * d.Traders.Traders[traderId].Multiplier(it);
        }

        /// <summary>Coins from the requests of the round.</summary>
        public static float QuestCoins(DataSet d, TypicalRound r) =>
            r.Quests.Sum(q => d.Quests.Quests[q].Reward.Items.Where(i => d.Items[i.Key].Kind == "currency").Sum(i => i.Value * d.Items[i.Key].Value));

        /// <summary>The full loot (knife) of the carcasses of the round, sold at the best price per item (<paramref name="onlyAt"/> — to one trader).</summary>
        public static float LootValue(DataSet d, TypicalRound r, string? onlyAt = null) =>
            r.Carcasses.Sum(c => d.Enemies.Enemies[c.Key].Loot.Full.Sum(i => c.Value * i.Value * Price(d, i.Key, onlyAt)));

        /// <summary>What was picked up on the way, sold the same way.</summary>
        public static float GatheredValue(DataSet d, TypicalRound r, string? onlyAt = null) => r.Gathered.Sum(i => i.Value * Price(d, i.Key, onlyAt));

        static float Price(DataSet d, string item, string? onlyAt) => onlyAt == null ? BestPrice(d, item) : PriceAt(d, onlyAt, item);

        /// <summary>Coins' worth of everything the round brings in. With <paramref name="includeGathered"/> false — only requests and the hunt.</summary>
        public static float Total(DataSet d, TypicalRound r, bool includeGathered, string? onlyAt = null) =>
            QuestCoins(d, r) + LootValue(d, r, onlyAt) + (includeGathered ? GatheredValue(d, r, onlyAt) : 0f);

        /// <summary>Coins the seller asks for the goal.</summary>
        public static float GoalPrice(DataSet d, TypicalRound r) => d.Items[r.Goal].Value * d.Traders.SellMarkup;
    }
}
