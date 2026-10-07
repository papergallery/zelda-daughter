using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.NPC;
using ZeldaDaughter.Save;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Quest
{
    public class QuestManager : MonoBehaviour, ISaveable
    {
        [SerializeField] private QuestDatabase _database;
        [SerializeField] private PlayerInventory _playerInventory;

        private readonly HashSet<string> _activeQuests = new();
        private readonly HashSet<string> _completedQuests = new();
        private readonly Dictionary<string, int> _killCounts = new();

        /// <summary>Вызывается при принятии нового квеста.</summary>
        public static event Action<string> OnQuestStarted;

        /// <summary>Вызывается при успешном завершении квеста.</summary>
        public static event Action<string> OnQuestCompleted;

        /// <summary>Запрос на начало квеста (из DialogueEffectExecutor или NPC).</summary>
        public static event Action<string> OnQuestStartRequested;

        /// <summary>Запрос на попытку сдать квест.</summary>
        public static event Action<string> OnQuestCompleteRequested;

        public string SaveId => "quest_manager";

        private void OnEnable()
        {
            SaveManager.Register(this);
            OnQuestStartRequested += StartQuest;
            OnQuestCompleteRequested += TryCompleteQuest;
            EnemyHealth.OnDeath += HandleEnemyKilled;
            DialogueEffectExecutor.OnQuestStartRequested += StartQuest;
            DialogueEffectExecutor.OnQuestCompleteRequested += TryCompleteQuest;
        }

        private void OnDisable()
        {
            SaveManager.Unregister(this);
            OnQuestStartRequested -= StartQuest;
            OnQuestCompleteRequested -= TryCompleteQuest;
            EnemyHealth.OnDeath -= HandleEnemyKilled;
            DialogueEffectExecutor.OnQuestStartRequested -= StartQuest;
            DialogueEffectExecutor.OnQuestCompleteRequested -= TryCompleteQuest;
        }

        /// <summary>
        /// Начинает квест. Игнорирует повторный вызов если квест уже активен или завершён.
        /// </summary>
        public void StartQuest(string questId)
        {
            if (_activeQuests.Contains(questId) || _completedQuests.Contains(questId))
                return;

            var questData = _database != null ? _database.FindById(questId) : null;
            if (questData == null)
            {
                Debug.LogWarning($"[QuestManager] QuestData не найден для id='{questId}'");
                return;
            }

            _activeQuests.Add(questId);
            OnQuestStarted?.Invoke(questId);

            // Записываем задание в блокнот через событие
            if (!string.IsNullOrEmpty(questData.NotebookText))
                NotebookManager.RequestNotebookEntry("Quest", questData.NotebookText);
        }

        /// <summary>
        /// Пытается завершить квест. Проверяет все условия перед выдачей награды.
        /// </summary>
        public void TryCompleteQuest(string questId)
        {
            if (!_activeQuests.Contains(questId))
                return;

            if (!CheckConditions(questId))
                return;

            var questData = _database != null ? _database.FindById(questId) : null;

            _activeQuests.Remove(questId);
            _completedQuests.Add(questId);

            if (questData != null)
            {
                GiveReward(questData.Reward);

                if (!string.IsNullOrEmpty(questData.CompletionText))
                    NotebookManager.RequestNotebookEntry("Quest", questData.CompletionText);
            }

            OnQuestCompleted?.Invoke(questId);
        }

        public bool IsQuestActive(string questId) => _activeQuests.Contains(questId);
        public bool IsQuestComplete(string questId) => _completedQuests.Contains(questId);

        /// <summary>
        /// Проверяет все условия квеста. Возвращает true если все выполнены.
        /// </summary>
        public bool CheckConditions(string questId)
        {
            var questData = _database != null ? _database.FindById(questId) : null;
            if (questData == null)
                return false;

            var conditions = questData.Conditions;
            if (conditions == null || conditions.Length == 0)
                return true;

            var inventory = GetInventory();

            for (int i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                switch (condition.Type)
                {
                    case QuestConditionType.BringItem:
                        if (!CheckBringItem(condition, inventory))
                            return false;
                        break;

                    case QuestConditionType.KillEnemy:
                        if (!CheckKillEnemy(condition))
                            return false;
                        break;

                    case QuestConditionType.VisitLocation:
                        // TODO: Реализовать через триггер-зоны (LocationTrigger)
                        break;
                }
            }
            return true;
        }

        private bool CheckBringItem(QuestCondition condition, PlayerInventory inventory)
        {
            if (inventory == null) return false;

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                var stack = inventory.Items[i];
                if (stack.Item != null && stack.Item.Id == condition.TargetId)
                    return stack.Amount >= condition.RequiredCount;
            }
            return false;
        }

        private bool CheckKillEnemy(QuestCondition condition)
        {
            _killCounts.TryGetValue(condition.TargetId, out int count);
            return count >= condition.RequiredCount;
        }

        private void GiveReward(QuestReward reward)
        {
            var inventory = GetInventory();
            if (inventory == null) return;

            if (reward.RewardItems == null) return;

            int itemCount = reward.RewardItems.Length;
            for (int i = 0; i < itemCount; i++)
            {
                var item = reward.RewardItems[i];
                if (item == null) continue;

                int amount = (reward.RewardAmounts != null && i < reward.RewardAmounts.Length)
                    ? reward.RewardAmounts[i]
                    : 1;

                inventory.AddItem(item, amount);
            }

            // TODO: Реализовать GoldReward когда будет система валюты
        }

        private void HandleEnemyKilled(EnemyHealth enemy)
        {
            if (enemy == null) return;

            // Используем имя EnemyData как идентификатор типа врага
            string enemyTypeId = enemy.Data != null ? enemy.Data.name : enemy.name;

            if (!_killCounts.ContainsKey(enemyTypeId))
                _killCounts[enemyTypeId] = 0;

            _killCounts[enemyTypeId]++;

            // Проверяем, не выполнил ли этот Kill условие активного квеста
            CheckKillConditionsForEnemy(enemyTypeId);
        }

        private void CheckKillConditionsForEnemy(string enemyTypeId)
        {
            // Небольшая оптимизация: проверяем только если есть активные квесты
            if (_activeQuests.Count == 0) return;

            // Копируем коллекцию чтобы избежать изменения при итерации (TryComplete может завершить квест)
            var activeSnapshot = new List<string>(_activeQuests);
            for (int i = 0; i < activeSnapshot.Count; i++)
            {
                string questId = activeSnapshot[i];
                var questData = _database != null ? _database.FindById(questId) : null;
                if (questData?.Conditions == null) continue;

                for (int j = 0; j < questData.Conditions.Length; j++)
                {
                    var cond = questData.Conditions[j];
                    if (cond.Type == QuestConditionType.KillEnemy && cond.TargetId == enemyTypeId)
                    {
                        // Убийство связано с этим квестом — проверяем все условия
                        TryCompleteQuest(questId);
                        break;
                    }
                }
            }
        }

        private PlayerInventory GetInventory()
        {
            if (_playerInventory != null)
                return _playerInventory;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && playerObj.TryGetComponent(out PlayerInventory inv))
            {
                _playerInventory = inv;
                return inv;
            }
            return null;
        }

        // --- ISaveable ---

        [Serializable]
        private class SaveData
        {
            public List<string> ActiveQuests;
            public List<string> CompletedQuests;
            public List<KillCountEntry> KillCounts;
        }

        [Serializable]
        private struct KillCountEntry
        {
            public string EnemyTypeId;
            public int Count;
        }

        public object CaptureState()
        {
            var killList = new List<KillCountEntry>(_killCounts.Count);
            foreach (var pair in _killCounts)
                killList.Add(new KillCountEntry { EnemyTypeId = pair.Key, Count = pair.Value });

            return new SaveData
            {
                ActiveQuests = new List<string>(_activeQuests),
                CompletedQuests = new List<string>(_completedQuests),
                KillCounts = killList
            };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;

            _activeQuests.Clear();
            _completedQuests.Clear();
            _killCounts.Clear();

            if (data.ActiveQuests != null)
                for (int i = 0; i < data.ActiveQuests.Count; i++)
                    _activeQuests.Add(data.ActiveQuests[i]);

            if (data.CompletedQuests != null)
                for (int i = 0; i < data.CompletedQuests.Count; i++)
                    _completedQuests.Add(data.CompletedQuests[i]);

            if (data.KillCounts != null)
                for (int i = 0; i < data.KillCounts.Count; i++)
                    _killCounts[data.KillCounts[i].EnemyTypeId] = data.KillCounts[i].Count;
        }
    }
}
