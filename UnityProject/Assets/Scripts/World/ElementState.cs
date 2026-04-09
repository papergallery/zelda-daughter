using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Tracks active elemental states on a GameObject with per-element timers.
    /// Attach to any object that can be set on fire, wetted, electrified, etc.
    /// </summary>
    public class ElementState : MonoBehaviour
    {
        public event Action<ElementTag> OnElementApplied;
        public event Action<ElementTag> OnElementRemoved;

        [SerializeField] private ElementConfig _elementConfig;

        // Активные элементы → оставшееся время (секунды)
        private readonly Dictionary<ElementTag, float> _activeElements = new Dictionary<ElementTag, float>(4);

        // Буфер для удаления во время итерации Update (избегаем аллокации каждый кадр)
        private readonly List<ElementTag> _expiredElements = new List<ElementTag>(4);

        /// <summary>Побитовое OR всех активных элементов.</summary>
        public ElementTag ActiveElements
        {
            get
            {
                var result = ElementTag.None;
                foreach (var tag in _activeElements.Keys)
                    result |= tag;
                return result;
            }
        }

        private void Update()
        {
            if (_activeElements.Count == 0) return;

            float delta = Time.deltaTime;

            // Тикаем таймеры, собираем истёкшие
            foreach (var kvp in _activeElements)
            {
                float remaining = kvp.Value - delta;
                _activeElements[kvp.Key] = remaining;
                if (remaining <= 0f)
                    _expiredElements.Add(kvp.Key);
            }

            // Удаляем истёкшие вне итерации
            foreach (var tag in _expiredElements)
                RemoveElement(tag);

            _expiredElements.Clear();
        }

        /// <summary>Добавляет элемент с длительностью из конфига. Если уже активен — сбрасывает таймер.</summary>
        public void ApplyElement(ElementTag tag)
        {
            if (tag == ElementTag.None) return;

            float duration = GetDurationFromConfig(tag);
            bool isNew = !_activeElements.ContainsKey(tag);

            _activeElements[tag] = duration;

            if (isNew)
                OnElementApplied?.Invoke(tag);
        }

        /// <summary>Немедленно снимает элемент.</summary>
        public void RemoveElement(ElementTag tag)
        {
            if (!_activeElements.ContainsKey(tag)) return;

            _activeElements.Remove(tag);
            OnElementRemoved?.Invoke(tag);
        }

        /// <summary>Проверяет, активен ли элемент.</summary>
        public bool HasElement(ElementTag tag)
        {
            return _activeElements.ContainsKey(tag);
        }

        private float GetDurationFromConfig(ElementTag tag)
        {
            if (_elementConfig != null && _elementConfig.TryGetSettings(tag, out var settings))
                return settings.duration;

            // Страховка если конфиг не назначен
            return 5f;
        }
    }
}
