using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _amountText;
        [SerializeField] private Image _background;
        [SerializeField] private Color _emptyColor = new(0.15f, 0.15f, 0.15f, 0.5f);
        [SerializeField] private Color _filledColor = new(0.25f, 0.25f, 0.25f, 0.7f);
        [SerializeField] private Color _highlightColor = new(0.8f, 0.6f, 0.2f, 0.8f);
        [SerializeField] private float _dragStartTime = 0.3f;

        public static event System.Action<int> OnSlotTapped;
        public static event System.Action<int> OnSlotDragStart;

        private int _slotIndex;
        private bool _hasItem;
        private float _pointerDownTime;
        private bool _pointerDown;

        public int SlotIndex => _slotIndex;
        public bool HasItem => _hasItem;

        public void Setup(int index, ItemStack stack)
        {
            _slotIndex = index;

            if (stack.Item != null && stack.Amount > 0)
            {
                _hasItem = true;
                if (_icon != null)
                {
                    _icon.sprite = stack.Item.Icon;
                    _icon.enabled = true;
                    _icon.color = Color.white;
                }
                if (_amountText != null)
                {
                    _amountText.text = stack.Amount > 1 ? stack.Amount.ToString() : "";
                    _amountText.enabled = stack.Amount > 1;
                }
                if (_background != null)
                    _background.color = _filledColor;
            }
            else
            {
                _hasItem = false;
                if (_icon != null)
                    _icon.enabled = false;
                if (_amountText != null)
                    _amountText.enabled = false;
                if (_background != null)
                    _background.color = _emptyColor;
            }
        }

        public void SetHighlight(bool on)
        {
            if (_background != null)
                _background.color = on ? _highlightColor : (_hasItem ? _filledColor : _emptyColor);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _pointerDownTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pointerDown) return;
            _pointerDown = false;

            float held = Time.unscaledTime - _pointerDownTime;

            if (held >= _dragStartTime && _hasItem)
            {
                OnSlotDragStart?.Invoke(_slotIndex);
            }
            else if (_hasItem)
            {
                OnSlotTapped?.Invoke(_slotIndex);
            }
        }

        private void Update()
        {
            if (!_pointerDown || !_hasItem) return;

            float held = Time.unscaledTime - _pointerDownTime;
            if (held >= _dragStartTime)
            {
                _pointerDown = false;
                OnSlotDragStart?.Invoke(_slotIndex);
            }
        }
    }
}
