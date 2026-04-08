using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Обрабатывает поедание пищи. Вызывается внешним кодом (InventoryDragHandler или UI-кнопка).
    /// </summary>
    public class FoodConsumption : MonoBehaviour
    {
        [SerializeField] private PlayerHealthState _healthState;
        [SerializeField] private HungerSystem _hungerSystem;
        [SerializeField] private Animator _animator;

        private static readonly int AnimEat = Animator.StringToHash("Eat");

        private void Awake()
        {
            if (_healthState == null)
                TryGetComponent(out _healthState);

            if (_hungerSystem == null)
                TryGetComponent(out _hungerSystem);

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// Публичный API: съесть предмет. Проверяет тип перед применением.
        /// </summary>
        public void ConsumeFood(ItemData food)
        {
            if (food == null) return;
            if (food.ItemType != ItemType.Consumable) return;

            if (_healthState != null && food.HealAmount > 0f)
                _healthState.Heal(food.HealAmount);

            if (_hungerSystem != null && food.HungerRestore > 0f)
                _hungerSystem.Feed(food.HungerRestore);

            if (_animator != null)
                _animator.SetTrigger(AnimEat);
        }
    }
}
