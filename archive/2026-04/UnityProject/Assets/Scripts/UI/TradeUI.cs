using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.UI
{
    public class TradeUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _merchantPanel;
        [SerializeField] private RectTransform _playerPanel;
        [SerializeField] private RectTransform _tradeZone;
        [SerializeField] private Image _balanceIndicator;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _itemSlotPrefab;

        public event Action<List<TradeSlot>, List<TradeSlot>> OnConfirmTrade;
        public event Action OnCancelTrade;

        private List<TradeSlot> _offeredByMerchant = new();
        private List<TradeSlot> _offeredByPlayer = new();

        private MerchantInventory _currentMerchant;
        private PlayerInventory _currentPlayer;
        private bool _showPrices;

        private void Awake()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        public void Show(MerchantInventory merchant, PlayerInventory player, bool showPrices)
        {
            _currentMerchant = merchant;
            _currentPlayer = player;
            _showPrices = showPrices;

            _offeredByMerchant.Clear();
            _offeredByPlayer.Clear();

            PopulateMerchantPanel();
            PopulatePlayerPanel();
            UpdateBalanceIndicator();

            if (_canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeTo(1f, 0.2f));
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOutAndDeactivate(0.2f));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void UpdateBalanceIndicator()
        {
            if (_balanceIndicator == null) return;

            int merchantValue = CalcValue(_offeredByMerchant, true);
            int playerValue = CalcValue(_offeredByPlayer, false);

            bool balanced = playerValue >= merchantValue;
            float t = merchantValue > 0 ? Mathf.Clamp01((float)playerValue / merchantValue) : 1f;
            _balanceIndicator.color = Color.Lerp(Color.red, Color.green, t);
        }

        private void OnConfirmClicked()
        {
            OnConfirmTrade?.Invoke(_offeredByMerchant, _offeredByPlayer);
        }

        private void OnCancelClicked()
        {
            OnCancelTrade?.Invoke();
        }

        private void PopulateMerchantPanel()
        {
            if (_merchantPanel == null || _currentMerchant == null) return;
            ClearPanel(_merchantPanel);

            foreach (var slot in _currentMerchant.CurrentStock)
            {
                SpawnSlot(_merchantPanel, slot.Item, slot.Quantity, _showPrices
                    ? _currentMerchant.GetBuyPrice(slot.Item)
                    : 0);
            }
        }

        private void PopulatePlayerPanel()
        {
            if (_playerPanel == null || _currentPlayer == null) return;
            ClearPanel(_playerPanel);

            foreach (var stack in _currentPlayer.Items)
            {
                SpawnSlot(_playerPanel, stack.Item, stack.Amount, _showPrices
                    ? (_currentMerchant != null ? _currentMerchant.GetSellPrice(stack.Item) : 0)
                    : 0);
            }
        }

        private void SpawnSlot(RectTransform parent, ItemData item, int qty, int price)
        {
            if (_itemSlotPrefab == null || item == null) return;

            var slotGo = Instantiate(_itemSlotPrefab, parent);
            if (slotGo.TryGetComponent<InventorySlotUI>(out var slotUI))
                slotUI.Setup(0, new ItemStack(item, qty));
        }

        private void ClearPanel(RectTransform panel)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
                Destroy(panel.GetChild(i).gameObject);
        }

        private int CalcValue(List<TradeSlot> slots, bool isMerchantSide)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (slot.Item == null) continue;
                int unitPrice = _currentMerchant != null
                    ? (isMerchantSide
                        ? _currentMerchant.GetBuyPrice(slot.Item)
                        : _currentMerchant.GetSellPrice(slot.Item))
                    : slot.Item.BaseValue;
                total += unitPrice * slot.Quantity;
            }
            return total;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = _canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = target;
        }

        private IEnumerator FadeOutAndDeactivate(float duration)
        {
            yield return FadeTo(0f, duration);
            gameObject.SetActive(false);
        }
    }
}
