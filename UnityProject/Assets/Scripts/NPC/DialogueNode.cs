using System;
using UnityEngine;

namespace ZeldaDaughter.NPC
{
    [Serializable]
    public struct DialogueOption
    {
        public string text;
        public Sprite icon;
        /// <summary>"" = конец диалога</summary>
        public string nextNodeId;
        public string effectKey;
        /// <summary>"" = всегда видна</summary>
        public string requiredCondition;
    }

    [Serializable]
    public struct DialogueNode
    {
        public string id;
        /// <summary>Будет scrambled через LanguageSystem в зависимости от уровня понимания языка.</summary>
        public string npcText;
        /// <summary>Иконки для режима непонимания (низкий уровень языка).</summary>
        public Sprite[] npcIcons;
        public DialogueOption[] options;
        /// <summary>"" = узел всегда доступен</summary>
        public string conditionKey;
        /// <summary>"" = нет эффекта при входе в узел</summary>
        public string effectKey;
        /// <summary>При true завершает диалог и открывает экран торговли.</summary>
        public bool startsTrade;
    }
}
