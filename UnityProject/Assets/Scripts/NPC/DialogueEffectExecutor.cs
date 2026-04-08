using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.NPC
{
    public class DialogueEffectExecutor
    {
        private readonly PlayerInventory _inventory;
        private readonly LanguageSystem _language;

        public DialogueEffectExecutor(PlayerInventory inventory, LanguageSystem language)
        {
            _inventory = inventory;
            _language = language;
        }

        /// <summary>
        /// Выполняет эффект по ключу.
        /// Форматы:
        ///   "give_item:itemId:amount"   — добавить предмет в инвентарь
        ///   "remove_item:itemId:amount" — убрать предмет из инвентаря
        ///   "language_xp:amount"        — добавить опыт языка (float)
        ///   "start_trade"               — обрабатывается DialogueManager, здесь игнорируется
        /// Пустая строка или null → ничего не делать.
        /// </summary>
        public void Execute(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey))
                return;

            string[] parts = effectKey.Split(':');
            string effectType = parts[0];

            switch (effectType)
            {
                case "give_item":
                    ExecuteGiveItem(parts, effectKey);
                    break;

                case "remove_item":
                    ExecuteRemoveItem(parts, effectKey);
                    break;

                case "language_xp":
                    ExecuteLanguageXp(parts, effectKey);
                    break;

                case "start_trade":
                    // Обрабатывается DialogueManager при ShowNode
                    break;

                default:
                    Debug.LogWarning($"[DialogueEffectExecutor] Неизвестный эффект: '{effectKey}'");
                    break;
            }
        }

        private void ExecuteGiveItem(string[] parts, string raw)
        {
            if (_inventory == null)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] PlayerInventory не задан, эффект '{raw}' пропущен.");
                return;
            }

            if (parts.Length < 3)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат give_item: '{raw}'");
                return;
            }

            string itemId = parts[1];
            if (!int.TryParse(parts[2], out int amount) || amount <= 0)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверное количество в '{raw}'");
                return;
            }

            var item = Resources.Load<ItemData>(itemId);
            if (item == null)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] ItemData '{itemId}' не найден в Resources.");
                return;
            }

            _inventory.AddItem(item, amount);
        }

        private void ExecuteRemoveItem(string[] parts, string raw)
        {
            if (_inventory == null)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] PlayerInventory не задан, эффект '{raw}' пропущен.");
                return;
            }

            if (parts.Length < 3)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат remove_item: '{raw}'");
                return;
            }

            string itemId = parts[1];
            if (!int.TryParse(parts[2], out int amount) || amount <= 0)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверное количество в '{raw}'");
                return;
            }

            var items = _inventory.Items;
            ItemData found = null;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item != null && items[i].Item.Id == itemId)
                {
                    found = items[i].Item;
                    break;
                }
            }

            if (found == null)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Предмет '{itemId}' не найден в инвентаре для remove_item.");
                return;
            }

            _inventory.RemoveItem(found, amount);
        }

        private void ExecuteLanguageXp(string[] parts, string raw)
        {
            if (_language == null)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] LanguageSystem не задан, эффект '{raw}' пропущен.");
                return;
            }

            if (parts.Length < 2)
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат language_xp: '{raw}'");
                return;
            }

            // Вызываем AddDialogueExperience нужное число раз
            if (int.TryParse(parts[1], out int times) && times > 0)
            {
                for (int i = 0; i < times; i++)
                    _language.AddDialogueExperience();
            }
            else
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверное значение language_xp в '{raw}'");
            }
        }
    }
}
