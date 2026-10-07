using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class StationUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private InventorySlotUI _slotA;
        [SerializeField] private InventorySlotUI _slotB;
        [SerializeField] private InventorySlotUI _resultSlot;
        [SerializeField] private Button _craftButton;
        [SerializeField] private CraftRecipeDatabase _recipeDatabase;

        [Header("Smelter")]
        [SerializeField] private float _smeltDuration = 5f;
        [SerializeField] private Image _progressBar;

        private static StationUI _instance;

        private StationType _currentType;
        private PlayerInventory _inventory;
        private ItemData _inputA;
        private ItemData _inputB;
        private bool _isOpen;
        private bool _isSmelting;
        private float _smeltTimer;

        private void Awake()
        {
            _instance = this;
            _inventory = FindObjectOfType<PlayerInventory>();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void Open(StationType type)
        {
            if (_instance != null)
                _instance.OpenInternal(type);
        }

        private void OpenInternal(StationType type)
        {
            _currentType = type;
            _isOpen = true;
            _inputA = null;
            _inputB = null;
            _isSmelting = false;

            if (_titleText != null)
            {
                _titleText.text = type switch
                {
                    StationType.Smelter => "Плавильня",
                    StationType.Anvil   => "Наковальня",
                    _                   => "Станок"
                };
            }

            if (_slotA != null)      _slotA.Setup(-1, default);
            if (_slotB != null)      _slotB.Setup(-1, default);
            if (_resultSlot != null) _resultSlot.Setup(-1, default);
            if (_progressBar != null) _progressBar.fillAmount = 0f;

            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveAllListeners();
                _craftButton.onClick.AddListener(OnCraftButtonPressed);
            }

            SetVisible(true);
            Time.timeScale = 0f;
        }

        public void Close()
        {
            _isOpen = false;
            _isSmelting = false;
            SetVisible(false);

            if (Time.timeScale == 0f)
                Time.timeScale = 1f;
        }

        private void Update()
        {
            if (!_isOpen || !_isSmelting || _currentType != StationType.Smelter) return;

            _smeltTimer += Time.unscaledDeltaTime;

            if (_progressBar != null)
                _progressBar.fillAmount = _smeltTimer / _smeltDuration;

            if (_smeltTimer >= _smeltDuration)
            {
                _isSmelting = false;
                CompleteCraft();
            }
        }

        /// <summary>Установить предмет в слот A (вызывается из внешнего drag-handler).</summary>
        public void SetInputA(ItemData item)
        {
            _inputA = item;
            if (_slotA != null && item != null)
                _slotA.Setup(-1, new ItemStack(item, 1));
        }

        /// <summary>Установить предмет в слот B (вызывается из внешнего drag-handler).</summary>
        public void SetInputB(ItemData item)
        {
            _inputB = item;
            if (_slotB != null && item != null)
                _slotB.Setup(-1, new ItemStack(item, 1));
        }

        private void OnCraftButtonPressed()
        {
            if (_inventory == null || _recipeDatabase == null || _inputA == null) return;

            if (_currentType == StationType.Smelter)
            {
                // Проверить рецепт перед запуском таймера
                var recipe = _inputB != null
                    ? _recipeDatabase.FindStationRecipe(_inputA, _inputB, StationType.Smelter)
                    : _recipeDatabase.FindSingleIngredientRecipe(_inputA, StationType.Smelter);

                if (recipe == null)
                {
                    SpeechBubbleManager.Say("Это нельзя расплавить...");
                    return;
                }

                _isSmelting = true;
                _smeltTimer = 0f;
            }
            else
            {
                CompleteCraft();
            }
        }

        private void CompleteCraft()
        {
            if (_inventory == null || _recipeDatabase == null) return;

            bool success = CraftingSystem.TryStationCraft(
                _inputA, _inputB, _currentType, _inventory, _recipeDatabase);

            if (success)
            {
                _inputA = null;
                _inputB = null;
                if (_slotA != null)      _slotA.Setup(-1, default);
                if (_slotB != null)      _slotB.Setup(-1, default);
            }

            if (_progressBar != null)
                _progressBar.fillAmount = 0f;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable   = visible;
        }
    }
}
