using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class ItemInfoPopup : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _displayDuration = 3f;

        private float _hideTime;
        private PlayerInventory _inventory;

        private void Awake()
        {
            _inventory = FindObjectOfType<PlayerInventory>();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            InventorySlotUI.OnSlotTapped += HandleSlotTapped;
        }

        private void OnDisable()
        {
            InventorySlotUI.OnSlotTapped -= HandleSlotTapped;
        }

        private void Update()
        {
            if (_canvasGroup != null && _canvasGroup.alpha > 0f && Time.unscaledTime >= _hideTime)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void HandleSlotTapped(int slotIndex)
        {
            if (_inventory == null) return;

            var stack = _inventory.GetSlot(slotIndex);
            if (stack.Item == null) return;

            Show(stack.Item);
        }

        public void Show(ItemData item)
        {
            if (_nameText != null)
                _nameText.text = item.DisplayName;
            if (_descriptionText != null)
                _descriptionText.text = item.Description;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            _hideTime = Time.unscaledTime + _displayDuration;
        }

        public void Hide()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }
    }
}
