using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ZeldaDaughter.World
{
    [Serializable]
    public struct WeatherTransition
    {
        public WeatherType from;
        public WeatherType to;

        [Range(0f, 1f)]
        [Tooltip("Вероятность перехода (0-1). Переходы из одного состояния нормализуются автоматически.")]
        public float probability;
    }

    [Serializable]
    public struct WeatherDuration
    {
        public WeatherType weatherType;

        [Tooltip("Минимальная длительность (секунды)")]
        public float minDuration;

        [Tooltip("Максимальная длительность (секунды)")]
        public float maxDuration;
    }

    [Serializable]
    public struct WeatherVisuals
    {
        public WeatherType weatherType;

        [Range(0f, 0.1f)]
        public float fogDensity;

        [Range(0f, 1f)]
        public float ambientIntensity;

        [Range(0f, 1f)]
        [Tooltip("Интенсивность системы частиц дождя (0 = без дождя)")]
        public float rainIntensity;
    }

    [CreateAssetMenu(menuName = "ZeldaDaughter/World/Weather Config", fileName = "WeatherConfig")]
    public class WeatherConfig : ScriptableObject
    {
        [SerializeField] private WeatherTransition[] _transitions;
        [SerializeField] private WeatherDuration[] _durations;
        [SerializeField] private WeatherVisuals[] _visuals;

        private Dictionary<WeatherType, WeatherDuration> _durationLookup;
        private Dictionary<WeatherType, WeatherVisuals> _visualsLookup;

        private void OnEnable()
        {
            BuildLookups();
        }

        private void BuildLookups()
        {
            _durationLookup = new Dictionary<WeatherType, WeatherDuration>(_durations?.Length ?? 0);
            if (_durations != null)
                foreach (var d in _durations)
                    _durationLookup[d.weatherType] = d;

            _visualsLookup = new Dictionary<WeatherType, WeatherVisuals>(_visuals?.Length ?? 0);
            if (_visuals != null)
                foreach (var v in _visuals)
                    _visualsLookup[v.weatherType] = v;
        }

        /// <summary>Выбирает следующее состояние погоды по вероятностям переходов из текущего.</summary>
        public WeatherType GetNextWeather(WeatherType current)
        {
            if (_transitions == null || _transitions.Length == 0)
                return current;

            // Собираем кандидатов и суммарную вероятность
            float totalWeight = 0f;
            var candidates = new List<WeatherTransition>(4);
            foreach (var t in _transitions)
            {
                if (t.from == current && t.probability > 0f)
                {
                    candidates.Add(t);
                    totalWeight += t.probability;
                }
            }

            if (candidates.Count == 0 || totalWeight <= 0f)
                return current;

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            foreach (var candidate in candidates)
            {
                cumulative += candidate.probability;
                if (roll <= cumulative)
                    return candidate.to;
            }

            // Страховка: вернуть последнего кандидата
            return candidates[candidates.Count - 1].to;
        }

        /// <summary>Возвращает случайную длительность для типа погоды.</summary>
        public float GetRandomDuration(WeatherType type)
        {
            if (_durationLookup == null) BuildLookups();
            if (_durationLookup.TryGetValue(type, out var d))
                return Random.Range(d.minDuration, d.maxDuration);
            return 60f;
        }

        /// <summary>Возвращает визуальные настройки для типа погоды. False если не задано.</summary>
        public bool TryGetVisuals(WeatherType type, out WeatherVisuals visuals)
        {
            if (_visualsLookup == null) BuildLookups();
            return _visualsLookup.TryGetValue(type, out visuals);
        }
    }
}
