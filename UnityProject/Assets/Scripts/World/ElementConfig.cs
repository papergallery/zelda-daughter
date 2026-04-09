using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.World
{
    [Serializable]
    public struct ElementSettings
    {
        [Tooltip("Как долго держится эффект (секунды)")]
        public float duration;

        [Tooltip("Радиус распространения на соседние объекты")]
        public float propagationRadius;

        [Tooltip("Задержка перед распространением (секунды)")]
        public float propagationDelay;

        [Tooltip("Максимальная глубина цепного распространения")]
        public int maxPropagationDepth;

        [Tooltip("Урон в секунду пока эффект активен")]
        public float damagePerSecond;
    }

    [Serializable]
    public struct ElementEntry
    {
        public ElementTag tag;
        public ElementSettings settings;
    }

    [CreateAssetMenu(menuName = "ZeldaDaughter/World/Element Config", fileName = "ElementConfig")]
    public class ElementConfig : ScriptableObject
    {
        [SerializeField] private ElementEntry[] _entries;

        private Dictionary<ElementTag, ElementSettings> _lookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<ElementTag, ElementSettings>(_entries?.Length ?? 0);
            if (_entries == null) return;
            foreach (var entry in _entries)
                _lookup[entry.tag] = entry.settings;
        }

        /// <summary>Возвращает настройки для элемента. False если элемент не зарегистрирован.</summary>
        public bool TryGetSettings(ElementTag tag, out ElementSettings settings)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(tag, out settings);
        }
    }
}
