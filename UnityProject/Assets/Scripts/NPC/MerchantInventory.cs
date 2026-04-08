using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.NPC
{
    [Serializable]
    public struct TradeSlot
    {
        public ItemData Item;
        public int Quantity;
    }

    public class MerchantInventory : MonoBehaviour, ISaveable
    {
        [SerializeField] private TradeInventoryData _baseData;
        [SerializeField] private string _merchantId;

        private List<TradeSlot> _currentStock = new();

        public string MerchantId => _merchantId;
        public IReadOnlyList<TradeSlot> CurrentStock => _currentStock;

        public string SaveId => "merchant_" + _merchantId;

        private void Awake()
        {
            InitializeStock();
        }

        private void OnEnable()
        {
            SaveManager.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Unregister(this);
        }

        private void InitializeStock()
        {
            _currentStock.Clear();
            if (_baseData == null) return;

            foreach (var tradeItem in _baseData.Stock)
            {
                if (tradeItem.item == null || tradeItem.quantity <= 0) continue;
                _currentStock.Add(new TradeSlot
                {
                    Item = tradeItem.item,
                    Quantity = tradeItem.quantity
                });
            }
        }

        public bool CanSell(int index, int amount)
        {
            if (index < 0 || index >= _currentStock.Count) return false;
            return _currentStock[index].Quantity >= amount;
        }

        /// <summary>Торговец продаёт игроку — уменьшает количество в слоте.</summary>
        public void SellToPlayer(int index, int amount)
        {
            if (!CanSell(index, amount)) return;

            var slot = _currentStock[index];
            int newQty = slot.Quantity - amount;

            if (newQty <= 0)
                _currentStock.RemoveAt(index);
            else
                _currentStock[index] = new TradeSlot { Item = slot.Item, Quantity = newQty };
        }

        /// <summary>Игрок продаёт торговцу — добавляет предмет в stock.</summary>
        public void BuyFromPlayer(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return;

            for (int i = 0; i < _currentStock.Count; i++)
            {
                if (_currentStock[i].Item == item)
                {
                    _currentStock[i] = new TradeSlot
                    {
                        Item = item,
                        Quantity = _currentStock[i].Quantity + amount
                    };
                    return;
                }
            }

            _currentStock.Add(new TradeSlot { Item = item, Quantity = amount });
        }

        public int GetBuyPrice(ItemData item)
        {
            if (_baseData == null) return item != null ? item.BaseValue : 0;
            return _baseData.GetBuyPrice(item);
        }

        public int GetSellPrice(ItemData item)
        {
            if (_baseData == null) return item != null ? item.BaseValue : 0;
            return _baseData.GetSellPrice(item);
        }

        // --- ISaveable ---

        [Serializable]
        private struct StockEntry
        {
            public string ItemId;
            public int Quantity;
        }

        [Serializable]
        private struct SaveData
        {
            public List<StockEntry> Entries;
        }

        public object CaptureState()
        {
            var entries = new List<StockEntry>(_currentStock.Count);
            foreach (var slot in _currentStock)
            {
                if (slot.Item == null) continue;
                entries.Add(new StockEntry { ItemId = slot.Item.Id, Quantity = slot.Quantity });
            }
            return new SaveData { Entries = entries };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;
            if (data.Entries == null) return;

            _currentStock.Clear();
            foreach (var entry in data.Entries)
            {
                var item = Resources.Load<ItemData>(entry.ItemId);
                if (item == null)
                {
                    Debug.LogWarning($"[MerchantInventory] RestoreState: ItemData '{entry.ItemId}' не найден в Resources.");
                    continue;
                }
                _currentStock.Add(new TradeSlot { Item = item, Quantity = entry.Quantity });
            }
        }
    }
}
