using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Progression;
using ZeldaDaughter.Quest;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.NPC
{
    public class DialogueManager : MonoBehaviour
    {
        [SerializeField] private LanguageSystem _languageSystem;
        [SerializeField] private DialoguePanelUI _dialoguePanel;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private QuestManager _questManager;

        private NPCProfile _currentNPC;
        private NPCSpeechBubble _currentBubble;
        private DialogueTree _currentTree;
        private string _currentNodeId;

        private DialogueConditionResolver _conditionResolver;
        private DialogueEffectExecutor _effectExecutor;
        private bool _isActive;

        // Кэш отфильтрованных опций текущего узла
        private DialogueOption[] _filteredOptions = Array.Empty<DialogueOption>();

        public static event Action OnDialogueStarted;
        public static event Action OnDialogueEnded;

        /// <summary>Вызывается каждый раз, когда NPC произносит реплику.</summary>
        public static event Action OnDialogueLine;

        public bool IsActive => _isActive;

        private void Awake()
        {
            _conditionResolver = new DialogueConditionResolver(
                _playerInventory, _languageSystem, _playerStats, _questManager);
            _effectExecutor = new DialogueEffectExecutor(_playerInventory, _languageSystem);
        }

        private void OnEnable()
        {
            _dialoguePanel.OnOptionSelected += HandleOptionSelected;
        }

        private void OnDisable()
        {
            _dialoguePanel.OnOptionSelected -= HandleOptionSelected;
        }

        public void StartDialogue(NPCProfile profile, NPCSpeechBubble bubble)
        {
            if (_isActive) return;
            if (profile == null || bubble == null) return;
            if (profile.DialogueTree == null) return;

            _currentNPC = profile;
            _currentBubble = bubble;
            _currentTree = profile.DialogueTree;
            _isActive = true;

            OnDialogueStarted?.Invoke();

            ShowNode(_currentTree.GetStartNode());
        }

        private void ShowNode(DialogueNode node)
        {
            // Проверяем условие входа в узел
            if (!string.IsNullOrEmpty(node.conditionKey) && !_conditionResolver.Check(node.conditionKey))
            {
                EndDialogue();
                return;
            }

            _currentNodeId = node.id;

            // Выполняем эффект узла
            if (!string.IsNullOrEmpty(node.effectKey))
                _effectExecutor.Execute(node.effectKey);

            // Опыт языка за каждую реплику
            if (_languageSystem != null)
                _languageSystem.AddDialogueExperience();

            OnDialogueLine?.Invoke();

            // Показываем реплику NPC
            if (_languageSystem != null && _languageSystem.IsIconMode
                && node.npcIcons != null && node.npcIcons.Length > 0)
            {
                _currentBubble.ShowIconSequence(node.npcIcons, 1f);
            }
            else
            {
                string processed = _languageSystem != null
                    ? _languageSystem.ProcessText(node.npcText)
                    : node.npcText;
                _currentBubble.ShowText(processed);
            }

            // Фильтруем опции по условиям
            _filteredOptions = FilterOptions(node.options);

            if (_filteredOptions.Length > 0)
            {
                _dialoguePanel.Show(
                    _filteredOptions,
                    _languageSystem != null && _languageSystem.IsIconMode,
                    _languageSystem != null ? _languageSystem.ProcessText : null
                );
            }
            else
            {
                _dialoguePanel.ShowEndButton();
            }

            // Торговля
            if (node.startsTrade)
                Debug.Log("[DialogueManager] Trade requested");
        }

        private void HandleOptionSelected(int index)
        {
            // index == -1 означает кнопку "..." — завершение диалога
            if (index < 0)
            {
                EndDialogue();
                return;
            }

            if (index >= _filteredOptions.Length)
            {
                Debug.LogWarning($"[DialogueManager] Выбран индекс {index}, но filteredOptions.Length = {_filteredOptions.Length}");
                EndDialogue();
                return;
            }

            DialogueOption selected = _filteredOptions[index];

            if (!string.IsNullOrEmpty(selected.effectKey))
                _effectExecutor.Execute(selected.effectKey);

            if (string.IsNullOrEmpty(selected.nextNodeId))
            {
                EndDialogue();
                return;
            }

            if (_currentTree.TryGetNode(selected.nextNodeId, out DialogueNode nextNode))
            {
                ShowNode(nextNode);
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] Узел '{selected.nextNodeId}' не найден в дереве.");
                EndDialogue();
            }
        }

        public void EndDialogue()
        {
            if (_currentBubble != null)
                _currentBubble.Hide();

            _dialoguePanel.Hide();
            _isActive = false;
            _currentNPC = null;
            _currentBubble = null;
            _currentTree = null;
            _currentNodeId = null;
            _filteredOptions = Array.Empty<DialogueOption>();

            OnDialogueEnded?.Invoke();
        }

        private DialogueOption[] FilterOptions(DialogueOption[] options)
        {
            if (options == null || options.Length == 0)
                return Array.Empty<DialogueOption>();

            var result = new List<DialogueOption>(options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                if (_conditionResolver.Check(options[i].requiredCondition))
                    result.Add(options[i]);
            }

            return result.ToArray();
        }

        public static void ClearEvents()
        {
            OnDialogueStarted = null;
            OnDialogueEnded = null;
            OnDialogueLine = null;
        }
    }
}
