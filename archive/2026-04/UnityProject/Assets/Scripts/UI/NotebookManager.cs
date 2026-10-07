using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.NPC;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.UI
{
    public class NotebookManager : MonoBehaviour, ISaveable
    {
        [SerializeField] private NotebookConfig _config;

        private readonly List<NotebookEntryData> _entries = new();
        private readonly HashSet<string> _recordedRecipes = new();
        private readonly HashSet<string> _recordedTreatments = new();

        /// <summary>Вызывается при добавлении новой записи в блокнот.</summary>
        public static event Action<NotebookEntryData> OnEntryAdded;

        /// <summary>
        /// Запрос на добавление записи извне (например из DialogueEffectExecutor).
        /// Параметры: category (имя enum), text.
        /// </summary>
        public static event Action<string, string> OnNotebookEntryRequested;

        public static void RequestNotebookEntry(string category, string text)
        {
            OnNotebookEntryRequested?.Invoke(category, text);
        }

        public string SaveId => "notebook";

        public IReadOnlyList<NotebookEntryData> Entries => _entries;

        /// <summary>Количество непрочитанных записей. Сбрасывается через MarkAsRead().</summary>
        public int NewEntriesCount { get; private set; }

        private void OnEnable()
        {
            SaveManager.Register(this);
            OnNotebookEntryRequested += HandleNotebookRequest;
            CraftingSystem.OnCraftSuccess += HandleCraftSuccess;
            WoundTreatment.OnWoundTreated += HandleWoundTreated;
            DialogueEffectExecutor.OnNotebookEntryRequested += HandleNotebookRequest;
        }

        private void OnDisable()
        {
            SaveManager.Unregister(this);
            OnNotebookEntryRequested -= HandleNotebookRequest;
            CraftingSystem.OnCraftSuccess -= HandleCraftSuccess;
            WoundTreatment.OnWoundTreated -= HandleWoundTreated;
            DialogueEffectExecutor.OnNotebookEntryRequested -= HandleNotebookRequest;
        }

        /// <summary>Добавляет запись в блокнот.</summary>
        public void AddEntry(NotebookCategory category, string text)
        {
            var entry = new NotebookEntryData
            {
                Category = category,
                Text = text,
                GameTime = Time.time
            };

            _entries.Add(entry);

            int maxEntries = _config != null ? _config.MaxEntries : 100;
            while (_entries.Count > maxEntries)
                _entries.RemoveAt(0);

            NewEntriesCount++;
            OnEntryAdded?.Invoke(entry);
        }

        /// <summary>Сбрасывает счётчик непрочитанных записей (вызывать при открытии блокнота).</summary>
        public void MarkAsRead()
        {
            NewEntriesCount = 0;
        }

        private void HandleCraftSuccess(CraftRecipe recipe)
        {
            if (recipe == null) return;

            // Уникальный ключ — имя ScriptableObject (name унаследован от UnityEngine.Object)
            string key = recipe.name;
            if (_recordedRecipes.Contains(key)) return;
            _recordedRecipes.Add(key);

            string ingredientA = recipe.IngredientA != null ? recipe.IngredientA.name : "?";
            string ingredientB = recipe.IngredientB != null ? $" + {recipe.IngredientB.name}" : string.Empty;
            string result = recipe.Result != null ? recipe.Result.name : "?";

            AddEntry(NotebookCategory.Recipe, $"Рецепт: {ingredientA}{ingredientB} → {result}");
        }

        private void HandleWoundTreated(WoundType woundType)
        {
            string key = woundType.ToString();
            if (_recordedTreatments.Contains(key)) return;
            _recordedTreatments.Add(key);

            string woundName = GetWoundDisplayName(woundType);
            string treatment = GetTreatmentDisplayName(woundType);
            AddEntry(NotebookCategory.Medicine, $"Лечение: {woundName} → {treatment}");
        }

        private void HandleNotebookRequest(string categoryName, string text)
        {
            if (!Enum.TryParse<NotebookCategory>(categoryName, ignoreCase: true, out var category))
            {
                Debug.LogWarning($"[NotebookManager] Неизвестная категория: '{categoryName}', используется Lore.");
                category = NotebookCategory.Lore;
            }
            AddEntry(category, text);
        }

        private static string GetWoundDisplayName(WoundType type)
        {
            switch (type)
            {
                case WoundType.Puncture: return "Колотая рана";
                case WoundType.Fracture: return "Перелом";
                case WoundType.Burn:     return "Ожог";
                case WoundType.Poison:   return "Отравление";
                default:                 return type.ToString();
            }
        }

        private static string GetTreatmentDisplayName(WoundType type)
        {
            switch (type)
            {
                case WoundType.Puncture: return "бинт";
                case WoundType.Fracture: return "шина";
                case WoundType.Burn:     return "мазь";
                case WoundType.Poison:   return "антидот";
                default:                 return "лекарство";
            }
        }

        // --- ISaveable ---

        [Serializable]
        private class SaveData
        {
            public List<NotebookEntryData> Entries;
            public List<string> RecordedRecipes;
            public List<string> RecordedTreatments;
            public int NewEntriesCount;
        }

        public object CaptureState()
        {
            return new SaveData
            {
                Entries = new List<NotebookEntryData>(_entries),
                RecordedRecipes = new List<string>(_recordedRecipes),
                RecordedTreatments = new List<string>(_recordedTreatments),
                NewEntriesCount = NewEntriesCount
            };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;

            _entries.Clear();
            _recordedRecipes.Clear();
            _recordedTreatments.Clear();

            if (data.Entries != null)
                _entries.AddRange(data.Entries);

            if (data.RecordedRecipes != null)
                for (int i = 0; i < data.RecordedRecipes.Count; i++)
                    _recordedRecipes.Add(data.RecordedRecipes[i]);

            if (data.RecordedTreatments != null)
                for (int i = 0; i < data.RecordedTreatments.Count; i++)
                    _recordedTreatments.Add(data.RecordedTreatments[i]);

            NewEntriesCount = data.NewEntriesCount;
        }
    }
}
