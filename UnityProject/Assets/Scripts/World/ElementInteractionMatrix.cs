using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.World
{
    [Serializable]
    public struct ElementInteraction
    {
        [Tooltip("Первый элемент взаимодействия (порядок не важен)")]
        public ElementTag elementA;

        [Tooltip("Второй элемент взаимодействия (порядок не важен)")]
        public ElementTag elementB;

        [Tooltip("Элементарные теги, которые добавляются в результате")]
        public ElementTag resultAdd;

        [Tooltip("Элементарные теги, которые снимаются в результате")]
        public ElementTag resultRemove;

        [Tooltip("Множитель урона при этом взаимодействии (1.0 = без изменений)")]
        public float damageMultiplier;
    }

    [CreateAssetMenu(menuName = "ZeldaDaughter/World/Element Interaction Matrix", fileName = "ElementInteractionMatrix")]
    public class ElementInteractionMatrix : ScriptableObject
    {
        [SerializeField] private ElementInteraction[] _interactions;

        // Ключ: упорядоченная пара (min, max) чтобы поиск не зависел от порядка
        private Dictionary<(ElementTag, ElementTag), ElementInteraction> _lookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<(ElementTag, ElementTag), ElementInteraction>(_interactions?.Length ?? 0);
            if (_interactions == null) return;
            foreach (var interaction in _interactions)
            {
                var key = MakeKey(interaction.elementA, interaction.elementB);
                _lookup[key] = interaction;
            }
        }

        /// <summary>Ищет взаимодействие для пары элементов в любом порядке.</summary>
        public bool TryGetInteraction(ElementTag a, ElementTag b, out ElementInteraction result)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(MakeKey(a, b), out result);
        }

        private static (ElementTag, ElementTag) MakeKey(ElementTag a, ElementTag b)
        {
            // Сортируем чтобы (Fire, Wet) и (Wet, Fire) давали один ключ
            return a <= b ? (a, b) : (b, a);
        }
    }
}
