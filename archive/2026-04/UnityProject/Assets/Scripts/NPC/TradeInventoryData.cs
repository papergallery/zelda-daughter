using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.NPC
{
    [Serializable]
    public struct TradeItem
    {
        public ItemData item;
        public int quantity;
        public int basePrice;
    }

    [Serializable]
    public struct PriceModifier
    {
        public ItemType itemType;
        public float multiplier;
    }

    [CreateAssetMenu(menuName = "ZeldaDaughter/NPC/Trade Inventory", fileName = "NewTradeInventory")]
    public class TradeInventoryData : ScriptableObject
    {
        [SerializeField] private TradeItem[] _stock;
        [SerializeField] private PriceModifier[] _priceModifiers;

        public IReadOnlyList<TradeItem> Stock => _stock;

        /// <summary>Цена покупки у торговца: baseValue предмета * модификатор типа.</summary>
        public int GetBuyPrice(ItemData item)
        {
            if (item == null) return 0;
            float modifier = GetModifier(item.ItemType);
            return Mathf.Max(1, Mathf.RoundToInt(item.BaseValue * modifier));
        }

        /// <summary>Цена продажи торговцу: цена покупки * 1.5.</summary>
        public int GetSellPrice(ItemData item)
        {
            return Mathf.Max(1, Mathf.RoundToInt(GetBuyPrice(item) * 1.5f));
        }

        public float GetModifier(ItemType type)
        {
            if (_priceModifiers != null)
            {
                foreach (var mod in _priceModifiers)
                {
                    if (mod.itemType == type)
                        return mod.multiplier;
                }
            }
            return 1f;
        }
    }
}
