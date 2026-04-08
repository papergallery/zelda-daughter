using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.NPC
{
    public class TradeManager : MonoBehaviour
    {
        [SerializeField] private TradeUI _tradeUI;
        [SerializeField] private LanguageSystem _languageSystem;
        [SerializeField] private PlayerInventory _playerInventory;

        private MerchantInventory _currentMerchant;
        private bool _isTrading;

        public static event Action OnTradeCompleted;

        public bool IsTrading => _isTrading;

        private void Awake()
        {
            if (_playerInventory == null)
                _playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (_tradeUI != null)
            {
                _tradeUI.OnConfirmTrade += ExecuteTrade;
                _tradeUI.OnCancelTrade += CloseTrade;
            }
        }

        private void OnDisable()
        {
            if (_tradeUI != null)
            {
                _tradeUI.OnConfirmTrade -= ExecuteTrade;
                _tradeUI.OnCancelTrade -= CloseTrade;
            }
        }

        public void OpenTrade(MerchantInventory merchant, PlayerInventory player)
        {
            if (merchant == null || player == null) return;

            _currentMerchant = merchant;
            _playerInventory = player;
            _isTrading = true;

            bool showPrices = _languageSystem != null && _languageSystem.KnowsCurrency;
            _tradeUI.Show(merchant, player, showPrices);
        }

        public void ExecuteTrade(List<TradeSlot> fromMerchant, List<TradeSlot> fromPlayer)
        {
            if (!_isTrading || _currentMerchant == null || _playerInventory == null) return;

            bool knowsCurrency = _languageSystem != null && _languageSystem.KnowsCurrency;

            int merchantValue = CalcMerchantValue(fromMerchant, knowsCurrency);
            int playerValue = CalcPlayerValue(fromPlayer, knowsCurrency);

            if (!IsTradeBalanced(merchantValue, playerValue)) return;

            // Торговец отдаёт предметы игроку
            foreach (var slot in fromMerchant)
            {
                int index = FindMerchantSlotIndex(slot.Item);
                if (index >= 0)
                    _currentMerchant.SellToPlayer(index, slot.Quantity);

                _playerInventory.AddItem(slot.Item, slot.Quantity);
            }

            // Игрок отдаёт предметы торговцу
            foreach (var slot in fromPlayer)
            {
                _playerInventory.RemoveItem(slot.Item, slot.Quantity);
                _currentMerchant.BuyFromPlayer(slot.Item, slot.Quantity);
            }

            OnTradeCompleted?.Invoke();
        }

        public void CloseTrade()
        {
            _tradeUI.Hide();
            _isTrading = false;
            _currentMerchant = null;
        }

        /// <summary>Торговец не продешевит: сделка разрешена если игрок предлагает >= ценности товара.</summary>
        public static bool IsTradeBalanced(int merchantValue, int playerValue)
        {
            return playerValue >= merchantValue;
        }

        public static void ClearEvents()
        {
            OnTradeCompleted = null;
        }

        private int CalcMerchantValue(List<TradeSlot> slots, bool knowsCurrency)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (slot.Item == null) continue;
                int unitPrice = knowsCurrency
                    ? _currentMerchant.GetBuyPrice(slot.Item)
                    : slot.Item.BaseValue;
                total += unitPrice * slot.Quantity;
            }
            return total;
        }

        private int CalcPlayerValue(List<TradeSlot> slots, bool knowsCurrency)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (slot.Item == null) continue;
                int unitPrice = knowsCurrency
                    ? _currentMerchant.GetSellPrice(slot.Item)
                    : slot.Item.BaseValue;
                total += unitPrice * slot.Quantity;
            }
            return total;
        }

        private int FindMerchantSlotIndex(ItemData item)
        {
            var stock = _currentMerchant.CurrentStock;
            for (int i = 0; i < stock.Count; i++)
            {
                if (stock[i].Item == item) return i;
            }
            return -1;
        }
    }
}
