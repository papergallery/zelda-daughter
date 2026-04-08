using System;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.NPC
{
    public class DialogueEffectExecutor
    {
        private readonly PlayerInventory _inventory;
        private readonly LanguageSystem _language;

        /// <summary>Открыть маркер на карте. Параметр: markerId.</summary>
        public static event Action<string> OnMapMarkerRequested;

        /// <summary>Добавить запись в блокнот. Параметры: category, text.</summary>
        public static event Action<string, string> OnNotebookEntryRequested;

        /// <summary>Начать квест. Параметр: questId.</summary>
        public static event Action<string> OnQuestStartRequested;

        /// <summary>Попытаться завершить квест. Параметр: questId.</summary>
        public static event Action<string> OnQuestCompleteRequested;

        public DialogueEffectExecutor(PlayerInventory inventory, LanguageSystem language)
        {
            _inventory = inventory;
            _language = language;
        }

        /// <summary>
        /// Выполняет эффект по ключу.
        /// Форматы:
        ///   "give_item:itemId:amount"              — добавить предмет в инвентарь
        ///   "remove_item:itemId:amount"             — убрать предмет из инвентаря
        ///   "language_xp:amount"                   — добавить опыт языка
        ///   "start_trade"                          — обрабатывается DialogueManager, здесь игнорируется
        ///   "add_map_marker:markerId"              — открыть маркер на карте
        ///   "add_notebook_entry:category:текст"    — добавить запись в блокнот
        ///   "start_quest:questId"                  — начать квест
        ///   "complete_quest:questId"               — завершить квест
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

                case "add_map_marker":
                    ExecuteAddMapMarker(parts, effectKey);
                    break;

                case "add_notebook_entry":
                    ExecuteAddNotebookEntry(parts, effectKey);
                    break;

                case "start_quest":
                    ExecuteStartQuest(parts, effectKey);
                    break;

                case "complete_quest":
                    ExecuteCompleteQuest(parts, effectKey);
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

        // "add_map_marker:markerId"
        private void ExecuteAddMapMarker(string[] parts, string raw)
        {
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат add_map_marker: '{raw}'");
                return;
            }
            OnMapMarkerRequested?.Invoke(parts[1]);
        }

        // "add_notebook_entry:category:текст записи (может содержать двоеточия)"
        private void ExecuteAddNotebookEntry(string[] parts, string raw)
        {
            // parts[0] = "add_notebook_entry", parts[1] = category, parts[2..] = текст
            if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]))
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат add_notebook_entry: '{raw}'");
                return;
            }

            string category = parts[1];
            // Текст может содержать ':', собираем всё с третьей части
            string text = string.Join(":", parts, 2, parts.Length - 2);
            OnNotebookEntryRequested?.Invoke(category, text);
        }

        // "start_quest:questId"
        private void ExecuteStartQuest(string[] parts, string raw)
        {
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат start_quest: '{raw}'");
                return;
            }
            OnQuestStartRequested?.Invoke(parts[1]);
        }

        // "complete_quest:questId"
        private void ExecuteCompleteQuest(string[] parts, string raw)
        {
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                Debug.LogWarning($"[DialogueEffectExecutor] Неверный формат complete_quest: '{raw}'");
                return;
            }
            OnQuestCompleteRequested?.Invoke(parts[1]);
        }
    }
}
