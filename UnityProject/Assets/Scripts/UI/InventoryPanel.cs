using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private Transform _slotsParent;
        [SerializeField] private CanvasGroup _canvasGroup;

        private InventorySlotUI[] _slots;
        private PlayerInventory _inventory;
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public InventorySlotUI[] Slots => _slots;

        public static event System.Action OnPanelOpened;
        public static event System.Action OnPanelClosed;

        private void Awake()
        {
            _inventory = FindObjectOfType<PlayerInventory>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            PlayerInventory.OnInventoryChanged += RefreshSlots;
            RadialMenuController.OnSectorSelected += HandleSectorSelected;
        }

        private void OnDisable()
        {
            PlayerInventory.OnInventoryChanged -= RefreshSlots;
            RadialMenuController.OnSectorSelected -= HandleSectorSelected;
        }

        private void HandleSectorSelected(int sectorIndex)
        {
            // Сектор 0 = Инвентарь
            if (sectorIndex == 0)
                Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            SetVisible(true);
            BuildSlots();
            OnPanelOpened?.Invoke();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            SetVisible(false);
            OnPanelClosed?.Invoke();

            if (Time.timeScale == 0f)
                Time.timeScale = 1f;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
            }
        }

        private void BuildSlots()
        {
            if (_inventory == null || _slotPrefab == null || _slotsParent == null)
                return;

            int maxSlots = _inventory.MaxSlots;

            // Создаём слоты один раз, потом обновляем
            if (_slots == null || _slots.Length != maxSlots)
            {
                // Очистить старые
                if (_slots != null)
                {
                    for (int i = 0; i < _slots.Length; i++)
                    {
                        if (_slots[i] != null)
                            Destroy(_slots[i].gameObject);
                    }
                }

                _slots = new InventorySlotUI[maxSlots];
                for (int i = 0; i < maxSlots; i++)
                {
                    var go = Instantiate(_slotPrefab, _slotsParent);
                    _slots[i] = go.GetComponent<InventorySlotUI>();
                }
            }

            RefreshSlots();
        }

        private void RefreshSlots()
        {
            if (_slots == null || _inventory == null || !_isOpen)
                return;

            for (int i = 0; i < _slots.Length; i++)
            {
                var stack = _inventory.GetSlot(i);
                _slots[i].Setup(i, stack);
            }
        }
    }
}
