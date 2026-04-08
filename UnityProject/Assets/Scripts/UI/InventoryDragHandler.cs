using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class InventoryDragHandler : MonoBehaviour
    {
        [SerializeField] private Image _dragIcon;
        [SerializeField] private CanvasGroup _dragIconGroup;
        [SerializeField] private InventoryPanel _inventoryPanel;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private CraftRecipeDatabase _recipeDatabase;

        public static event System.Action<int> OnDragStarted;
        public static event System.Action OnDragCancelled;
        public static event System.Action<int, int> OnDropOnSlot;
        public static event System.Action<ItemData> OnDropOutsidePanel;

        private PlayerInventory _inventory;
        private bool _isDragging;
        private int _sourceSlot = -1;
        private int _hoveredSlot = -1;

        private void Awake()
        {
            _inventory = FindObjectOfType<PlayerInventory>();
            if (_dragIconGroup != null)
                _dragIconGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            InventorySlotUI.OnSlotDragStart += StartDrag;
        }

        private void OnDisable()
        {
            InventorySlotUI.OnSlotDragStart -= StartDrag;
        }

        private void Update()
        {
            if (!_isDragging) return;

            Vector2 pointerPos;
            bool pointerUp;

#if UNITY_EDITOR
            pointerPos = UnityEngine.Input.mousePosition;
            pointerUp = UnityEngine.Input.GetMouseButtonUp(0);
#else
            if (UnityEngine.Input.touchCount > 0)
            {
                var touch = UnityEngine.Input.GetTouch(0);
                pointerPos = touch.position;
                pointerUp = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }
            else
            {
                EndDrag();
                return;
            }
#endif

            if (_dragIcon != null)
                _dragIcon.rectTransform.position = pointerPos;

            UpdateHoveredSlot(pointerPos);

            if (pointerUp)
                EndDrag();
        }

        private void StartDrag(int slotIndex)
        {
            if (_inventory == null) return;

            var stack = _inventory.GetSlot(slotIndex);
            if (stack.Item == null) return;

            _isDragging = true;
            _sourceSlot = slotIndex;
            _hoveredSlot = -1;

            if (_dragIcon != null && stack.Item.Icon != null)
            {
                _dragIcon.sprite = stack.Item.Icon;
                _dragIcon.enabled = true;
            }

            if (_dragIconGroup != null)
                _dragIconGroup.alpha = 0.8f;

            var slots = _inventoryPanel != null ? _inventoryPanel.Slots : null;
            if (slots != null && slotIndex < slots.Length)
                slots[slotIndex].SetHighlight(true);

            OnDragStarted?.Invoke(slotIndex);
        }

        private void EndDrag()
        {
            if (!_isDragging) return;
            _isDragging = false;

            if (_dragIconGroup != null)
                _dragIconGroup.alpha = 0f;
            if (_dragIcon != null)
                _dragIcon.enabled = false;

            var slots = _inventoryPanel != null ? _inventoryPanel.Slots : null;
            if (slots != null)
            {
                if (_sourceSlot >= 0 && _sourceSlot < slots.Length)
                    slots[_sourceSlot].SetHighlight(false);
                if (_hoveredSlot >= 0 && _hoveredSlot < slots.Length)
                    slots[_hoveredSlot].SetHighlight(false);
            }

            if (_hoveredSlot >= 0 && _hoveredSlot != _sourceSlot)
            {
                HandleDropOnSlot(_sourceSlot, _hoveredSlot);
            }
            else if (_hoveredSlot < 0 && _panelRect != null)
            {
                Vector2 pointerPos;
#if UNITY_EDITOR
                pointerPos = UnityEngine.Input.mousePosition;
#else
                pointerPos = UnityEngine.Input.touchCount > 0
                    ? (Vector2)UnityEngine.Input.GetTouch(0).position
                    : Vector2.zero;
#endif
                if (!RectTransformUtility.RectangleContainsScreenPoint(_panelRect, pointerPos))
                    HandleDropOutside();
                else
                    OnDragCancelled?.Invoke();
            }
            else
            {
                OnDragCancelled?.Invoke();
            }

            _sourceSlot = -1;
            _hoveredSlot = -1;
        }

        private void HandleDropOnSlot(int from, int to)
        {
            if (_inventory == null) return;

            var stackFrom = _inventory.GetSlot(from);
            var stackTo = _inventory.GetSlot(to);

            // Целевой слот занят — пробуем полевой крафт
            if (stackTo.Item != null && _recipeDatabase != null)
            {
                var recipe = _recipeDatabase.FindFieldRecipe(stackFrom.Item, stackTo.Item);
                if (recipe != null)
                {
                    CraftingSystem.TryCraft(from, to, _inventory, _recipeDatabase);
                    OnDropOnSlot?.Invoke(from, to);
                    return;
                }
            }

            _inventory.SwapSlots(from, to);
            OnDropOnSlot?.Invoke(from, to);
        }

        private void HandleDropOutside()
        {
            if (_inventory == null || _sourceSlot < 0) return;

            var stack = _inventory.GetSlot(_sourceSlot);
            if (stack.Item != null && stack.Item.IsPlaceable)
                OnDropOutsidePanel?.Invoke(stack.Item);
            else
                OnDragCancelled?.Invoke();
        }

        private void UpdateHoveredSlot(Vector2 pointerPos)
        {
            var slots = _inventoryPanel != null ? _inventoryPanel.Slots : null;
            if (slots == null) return;

            int newHovered = -1;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i == _sourceSlot) continue;

                if (!slots[i].TryGetComponent<RectTransform>(out var rt)) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(rt, pointerPos))
                {
                    newHovered = i;
                    break;
                }
            }

            if (newHovered == _hoveredSlot) return;

            if (_hoveredSlot >= 0 && _hoveredSlot < slots.Length)
                slots[_hoveredSlot].SetHighlight(false);

            _hoveredSlot = newHovered;

            if (_hoveredSlot >= 0 && _hoveredSlot < slots.Length)
                slots[_hoveredSlot].SetHighlight(true);
        }
    }
}
