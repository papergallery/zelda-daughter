#nullable enable
using System.Collections.Generic;
using ZeldaDaughter.Core.Data;

namespace ZeldaDaughter.Core.Economy
{
    /// <summary>data/traders.json (D-03). Trader id = NPC id with <c>shop: true</c> in npcs.json.</summary>
    public sealed class TraderSettings
    {
        /// <summary>A trader pays this share of an item's value (× her own multiplier)…</summary>
        public float BuyShare { get; set; }
        /// <summary>…and sells at value × this. Share × any multiplier must stay below it (no profit in going round the traders).</summary>
        public float SellMarkup { get; set; }
        public Dictionary<string, TraderDef> Traders { get; set; } = new Dictionary<string, TraderDef>();
    }

    public sealed class TraderDef
    {
        /// <summary>Multiplier for things she has no opinion about.</summary>
        public float DefaultBuy { get; set; } = 1f;
        /// <summary>Item id → multiplier (smith: ore 1.8). 0 — she does not buy it.</summary>
        public Dictionary<string, float> Buys { get; set; } = new Dictionary<string, float>();
        /// <summary>Item kind → multiplier; an item entry wins over its kind.</summary>
        public Dictionary<string, float> BuysKinds { get; set; } = new Dictionary<string, float>();
        public List<StockLine> Stock { get; set; } = new List<StockLine>();

        public float Multiplier(ItemDef item)
        {
            if (Buys.TryGetValue(item.Id, out var m)) return m;
            if (BuysKinds.TryGetValue(item.Kind, out m)) return m;
            return DefaultBuy;
        }
    }

    public sealed class StockLine
    {
        public string Item { get; set; } = "";
        /// <summary>Pieces for the whole game; absent — never runs out.</summary>
        public int? Count { get; set; }
    }
}
