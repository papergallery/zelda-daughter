using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.NPC
{
    public class DialogueConditionResolver
    {
        private readonly PlayerInventory _inventory;
        private readonly LanguageSystem _language;
        private readonly PlayerStats _stats;

        public DialogueConditionResolver(PlayerInventory inventory, LanguageSystem language, PlayerStats stats)
        {
            _inventory = inventory;
            _language = language;
            _stats = stats;
        }

        /// <summary>
        /// Проверяет условие по ключу.
        /// Форматы:
        ///   "has_item:itemId"            — наличие предмета в инвентаре
        ///   "language_level:0.5"         — уровень понимания языка >= значения
        ///   "stat:Strength:50"           — значение навыка >= порога
        /// Пустая строка или null → всегда true.
        /// Неизвестный ключ → true (заглушка для будущих квестов).
        /// </summary>
        public bool Check(string conditionKey)
        {
            if (string.IsNullOrEmpty(conditionKey))
                return true;

            string[] parts = conditionKey.Split(':');
            string conditionType = parts[0];

            switch (conditionType)
            {
                case "has_item":
                    return CheckHasItem(parts);

                case "language_level":
                    return CheckLanguageLevel(parts);

                case "stat":
                    return CheckStat(parts);

                default:
                    // Заглушка для квестовых флагов и будущих условий
                    return true;
            }
        }

        private bool CheckHasItem(string[] parts)
        {
            if (parts.Length < 2 || _inventory == null)
                return true;

            string itemId = parts[1];
            var items = _inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item != null && items[i].Item.Id == itemId)
                    return true;
            }
            return false;
        }

        private bool CheckLanguageLevel(string[] parts)
        {
            if (parts.Length < 2 || _language == null)
                return true;

            if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float threshold))
            {
                return _language.Comprehension >= threshold;
            }

            Debug.LogWarning($"[DialogueConditionResolver] Невалидный порог языка: '{parts[1]}'");
            return true;
        }

        private bool CheckStat(string[] parts)
        {
            if (parts.Length < 3 || _stats == null)
                return true;

            if (!System.Enum.TryParse(parts[1], out StatType statType))
            {
                Debug.LogWarning($"[DialogueConditionResolver] Неизвестный тип стата: '{parts[1]}'");
                return true;
            }

            if (float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float threshold))
            {
                return _stats.GetStat(statType) >= threshold;
            }

            Debug.LogWarning($"[DialogueConditionResolver] Невалидный порог стата: '{parts[2]}'");
            return true;
        }
    }
}
