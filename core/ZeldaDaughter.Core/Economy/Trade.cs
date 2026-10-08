#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Language;

namespace ZeldaDaughter.Core.Economy
{
    public enum TradeOutcome { Done, Unknown, Closed, Empty, CoinsUnknown, MissingItems, NotWanted, NotInStock, NotEnough, NoRoom }

    /// <summary>One line of an offer: an item, how many; on the take side it may come from the trader's buyback shelf instead of her stock.</summary>
    public readonly struct TradeLine
    {
        public readonly string Item;
        public readonly int Count;
        public readonly bool FromBuyback;

        public TradeLine(string item, int count, bool fromBuyback = false) { Item = item; Count = count; FromBuyback = fromBuyback; }
        public override string ToString() => $"{Item}×{Count}{(FromBuyback ? " (buyback)" : "")}";
    }

    /// <summary>What the hero hands over and what she takes; coins are the item «coin» on either side (change).</summary>
    public readonly struct TradeOffer
    {
        public readonly IReadOnlyList<TradeLine> Give;
        public readonly IReadOnlyList<TradeLine> Take;
        public TradeOffer(IReadOnlyList<TradeLine> give, IReadOnlyList<TradeLine> take) { Give = give; Take = take; }
    }

    public readonly struct TradeResult
    {
        public readonly TradeOutcome Outcome;
        /// <summary>The trader's valuation of what the hero gave / of what she took (coins).</summary>
        public readonly float GiveValue;
        public readonly float TakeValue;
        /// <summary>This was the very first barter — the moment someone may start teaching coins.</summary>
        public readonly bool FirstBarter;

        public TradeResult(TradeOutcome outcome, float give = 0, float take = 0, bool firstBarter = false)
        {
            Outcome = outcome; GiveValue = give; TakeValue = take; FirstBarter = firstBarter;
        }
        public override string ToString() => $"{Outcome} give={GiveValue:0.##} take={TakeValue:0.##}";
    }

    /// <summary>A stock shelf entry: <c>Count</c> -1 — never runs out.</summary>
    public readonly struct StockEntry
    {
        public readonly string Item;
        public readonly int Count;
        public StockEntry(string item, int count) { Item = item; Count = count; }
    }

    /// <summary>
    /// Barter + coins (project-design.md §2). A deal swaps sets of items (coins are an item), is accepted when the trader values what the hero
    /// gives at least as high as what she takes, and is all-or-nothing on the bag. Items the hero sells go to that trader's buyback shelf.
    /// A shop trades only while its NPC stands at the counter (<c>isOpen</c>, from NpcRoster). Coins are unknown at first: the first deal is
    /// a barter, then someone «teaches» (<see cref="TeachCoins"/>) — or the hero simply learns the language (language.json moneyAt).
    /// </summary>
    public sealed class Trade
    {
        const float Eps = 1e-4f;

        readonly TraderSettings _s;
        readonly IReadOnlyDictionary<string, ItemDef> _items;
        readonly Bag _bag;
        readonly Func<string, bool> _isOpen;
        readonly Comprehension _lang;
        readonly Dictionary<string, Dictionary<string, int>> _sold = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string, int>> _buyback = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        bool _taught;

        public Trade(TraderSettings settings, IReadOnlyDictionary<string, ItemDef> items, Bag bag, Func<string, bool> isOpen, Comprehension language)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
            _isOpen = isOpen ?? throw new ArgumentNullException(nameof(isOpen));
            _lang = language ?? throw new ArgumentNullException(nameof(language));
        }

        public int BarterDeals { get; private set; }
        public bool KnowsCoins => _taught || _lang.UnderstandsMoney;
        public bool IsTrader(string traderId) => _s.Traders.ContainsKey(traderId);

        /// <summary>An NPC taught the hero what coins are. Only after a first barter; true when she knows them now.</summary>
        public bool TeachCoins()
        {
            if (KnowsCoins) return true;
            if (BarterDeals == 0) return false;
            _taught = true;
            return true;
        }

        /// <summary>What this trader pays for one piece. Coins are worth their face value; 0 — not wanted.</summary>
        public float BuyValue(string traderId, string itemId)
        {
            var it = _items[itemId];
            if (it.Kind == "currency") return it.Value;
            return it.Value * _s.BuyShare * Def(traderId).Multiplier(it);
        }

        /// <summary>What she asks for one piece from her stock.</summary>
        public float SellPrice(string traderId, string itemId)
        {
            var it = _items[itemId];
            return it.Kind == "currency" ? it.Value : it.Value * _s.SellMarkup;
        }

        /// <summary>What she asks to give back something the hero sold: exactly what she paid.</summary>
        public float BuybackPrice(string traderId, string itemId) => BuyValue(traderId, itemId);

        /// <summary>Whole coins that cover <paramref name="count"/> pieces from stock (rounded up).</summary>
        public int CoinPrice(string traderId, string itemId, int count) => (int)Math.Ceiling(SellPrice(traderId, itemId) * count - Eps);

        /// <summary>The shelf now: stock left (static list from data, minus what the hero bought).</summary>
        public IReadOnlyList<StockEntry> Stock(string traderId)
        {
            var list = new List<StockEntry>();
            foreach (var line in Def(traderId).Stock) list.Add(new StockEntry(line.Item, Left(traderId, line)));
            return list;
        }

        /// <summary>What the hero sold to this trader and can buy back, ordered by item id.</summary>
        public IReadOnlyList<TradeLine> Buyback(string traderId)
        {
            var list = new List<TradeLine>();
            if (_buyback.TryGetValue(traderId, out var d))
                foreach (var kv in d.OrderBy(x => x.Key, StringComparer.Ordinal)) if (kv.Value > 0) list.Add(new TradeLine(kv.Key, kv.Value, true));
            return list;
        }

        /// <summary>
        /// What <see cref="Execute"/> would answer for this offer — the same checks in the same order (unknown, closed, empty, coins unknown,
        /// missing items, not wanted, not in stock, not enough, no room), with the values — and nothing changes: not the bag, not the stock,
        /// not the buyback shelf. The trade window asks it on every change of the offer (C5). <see cref="TradeResult.FirstBarter"/> says
        /// whether this deal would be the very first barter.
        /// </summary>
        public TradeResult Evaluate(string traderId, TradeOffer offer) => Check(traderId, offer, out _, out _);

        public TradeResult Execute(string traderId, TradeOffer offer)
        {
            var r = Check(traderId, offer, out var give, out var take);
            if (r.Outcome != TradeOutcome.Done || give == null || take == null) return r;

            foreach (var kv in give) _bag.Remove(kv.Key.item, kv.Value);
            foreach (var kv in take) _bag.Add(kv.Key.item, kv.Value);   // room was checked

            foreach (var kv in give)
                if (!IsCoin(kv.Key.item)) Bump(_buyback, traderId, kv.Key.item, kv.Value);
            foreach (var kv in take)
            {
                if (IsCoin(kv.Key.item)) continue;
                if (kv.Key.fromBuyback) Bump(_buyback, traderId, kv.Key.item, -kv.Value);
                else Bump(_sold, traderId, kv.Key.item, kv.Value);
            }
            bool coins = give.Keys.Any(k => IsCoin(k.item)) || take.Keys.Any(k => IsCoin(k.item));
            if (!coins) BarterDeals++;
            return r;
        }

        TradeResult Check(string traderId, TradeOffer offer, out Dictionary<(string item, bool fromBuyback), int>? giveOut, out Dictionary<(string item, bool fromBuyback), int>? takeOut)
        {
            giveOut = takeOut = null;
            if (!_s.Traders.TryGetValue(traderId, out var def)) return new TradeResult(TradeOutcome.Unknown);
            if (!_isOpen(traderId)) return new TradeResult(TradeOutcome.Closed);
            var give = Merge(offer.Give, false);
            var take = Merge(offer.Take, true);
            if (give == null || take == null || give.Count == 0 || take.Count == 0) return new TradeResult(TradeOutcome.Empty);

            bool coins = give.Keys.Any(k => IsCoin(k.item)) || take.Keys.Any(k => IsCoin(k.item));
            if (coins && !KnowsCoins) return new TradeResult(TradeOutcome.CoinsUnknown);
            foreach (var kv in give)
                if (_bag.Count(kv.Key.item) < kv.Value) return new TradeResult(TradeOutcome.MissingItems);

            float giveValue = 0, takeValue = 0;
            foreach (var kv in give)
            {
                float v = BuyValue(traderId, kv.Key.item);
                if (v <= 0) return new TradeResult(TradeOutcome.NotWanted);
                giveValue += v * kv.Value;
            }
            foreach (var kv in take)
            {
                string item = kv.Key.item;
                if (IsCoin(item)) takeValue += kv.Value;
                else if (kv.Key.fromBuyback)
                {
                    if (!_buyback.TryGetValue(traderId, out var shelf) || !shelf.TryGetValue(item, out var have) || have < kv.Value)
                        return new TradeResult(TradeOutcome.NotInStock);
                    takeValue += BuybackPrice(traderId, item) * kv.Value;
                }
                else
                {
                    var line = def.Stock.FirstOrDefault(x => x.Item == item);
                    if (line == null) return new TradeResult(TradeOutcome.NotInStock);
                    int left = Left(traderId, line);
                    if (left >= 0 && left < kv.Value) return new TradeResult(TradeOutcome.NotInStock);
                    takeValue += SellPrice(traderId, item) * kv.Value;
                }
            }
            if (giveValue + Eps < takeValue) return new TradeResult(TradeOutcome.NotEnough, giveValue, takeValue);

            var giveCounts = new List<KeyValuePair<string, int>>();
            foreach (var kv in give) giveCounts.Add(new KeyValuePair<string, int>(kv.Key.item, kv.Value));
            var takeCounts = new List<KeyValuePair<string, int>>();
            foreach (var kv in take) takeCounts.Add(new KeyValuePair<string, int>(kv.Key.item, kv.Value));
            if (!_bag.CanExchange(giveCounts, takeCounts)) return new TradeResult(TradeOutcome.NoRoom, giveValue, takeValue);

            giveOut = give;
            takeOut = take;
            return new TradeResult(TradeOutcome.Done, giveValue, takeValue, !coins && BarterDeals == 0);
        }

        TraderDef Def(string traderId) => _s.Traders.TryGetValue(traderId, out var d) ? d : throw new ArgumentException($"traders.json: no trader '{traderId}'", nameof(traderId));
        bool IsCoin(string item) => _items.TryGetValue(item, out var d) && d.Kind == "currency";

        int Left(string traderId, StockLine line)
        {
            if (line.Count == null) return -1;
            int sold = _sold.TryGetValue(traderId, out var d) && d.TryGetValue(line.Item, out var n) ? n : 0;
            return Math.Max(0, line.Count.Value - sold);
        }

        /// <summary>Sums duplicate lines; null for an unknown item or a non-positive count.</summary>
        Dictionary<(string item, bool fromBuyback), int>? Merge(IReadOnlyList<TradeLine> lines, bool isTake)
        {
            var m = new Dictionary<(string item, bool fromBuyback), int>();
            foreach (var l in lines ?? Array.Empty<TradeLine>())
            {
                if (l.Count <= 0 || !_items.ContainsKey(l.Item)) return null;
                var key = (l.Item, isTake && l.FromBuyback);
                m[key] = (m.TryGetValue(key, out var n) ? n : 0) + l.Count;
            }
            return m;
        }

        static void Bump(Dictionary<string, Dictionary<string, int>> d, string trader, string item, int by)
        {
            if (!d.TryGetValue(trader, out var inner)) d[trader] = inner = new Dictionary<string, int>(StringComparer.Ordinal);
            int now = (inner.TryGetValue(item, out var n) ? n : 0) + by;
            if (now > 0) inner[item] = now; else inner.Remove(item);
        }

        // --- save / load (D-03) ---

        public bool Taught => _taught;
        public IReadOnlyDictionary<string, Dictionary<string, int>> SoldState => _sold;
        public IReadOnlyDictionary<string, Dictionary<string, int>> BuybackState => _buyback;

        public void Restore(bool taught, int barterDeals, IReadOnlyDictionary<string, Dictionary<string, int>>? sold, IReadOnlyDictionary<string, Dictionary<string, int>>? buyback)
        {
            _taught = taught;
            BarterDeals = Math.Max(0, barterDeals);
            Load(_sold, sold);
            Load(_buyback, buyback);
        }

        void Load(Dictionary<string, Dictionary<string, int>> into, IReadOnlyDictionary<string, Dictionary<string, int>>? from)
        {
            into.Clear();
            if (from == null) return;
            foreach (var t in from)
            {
                if (!_s.Traders.ContainsKey(t.Key)) continue;
                foreach (var it in t.Value) if (_items.ContainsKey(it.Key) && it.Value > 0) Bump(into, t.Key, it.Key, it.Value);
            }
        }
    }
}
