using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.Progression
{
    public class PlayerStats : MonoBehaviour, ISaveable
    {
        [SerializeField] private ProgressionConfig _config;

        private Dictionary<StatType, float> _values;

        /// <summary>Вызывается при изменении любого навыка: тип, старое значение, новое значение.</summary>
        public static event Action<StatType, float, float> OnStatChanged;

        /// <summary>Вызывается при переходе навыка на новый тир: тип, индекс тира.</summary>
        public static event Action<StatType, int> OnTierReached;

        public ProgressionConfig Config => _config;

        public string SaveId => "player_stats";

        private void Awake()
        {
            var statTypes = (StatType[])Enum.GetValues(typeof(StatType));
            _values = new Dictionary<StatType, float>(statTypes.Length);
            foreach (var type in statTypes)
                _values[type] = 0f;
        }

        private void OnEnable()
        {
            SaveManager.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Unregister(this);
        }

        public float GetStat(StatType type)
        {
            return _values.TryGetValue(type, out float value) ? value : 0f;
        }

        public float GetStatNormalized(StatType type)
        {
            var curve = _config.GetCurve(type);
            if (curve == null || curve.MaxValue <= 0f) return 0f;
            return GetStat(type) / curve.MaxValue;
        }

        public int GetTier(StatType type)
        {
            return _config.EffectConfig.GetTier(GetStat(type));
        }

        public void AddExperience(StatType type, float rawAmount)
        {
            var curve = _config.GetCurve(type);
            if (curve == null)
            {
                Debug.LogWarning($"[PlayerStats] No growth curve for {type}");
                return;
            }

            float currentValue = GetStat(type);
            int oldTier = _config.EffectConfig.GetTier(currentValue);

            float growth = curve.CalculateGrowth(currentValue, rawAmount);
            float newValue = Mathf.Min(currentValue + growth, curve.MaxValue);

            if (Mathf.Approximately(newValue, currentValue)) return;

            _values[type] = newValue;
            OnStatChanged?.Invoke(type, currentValue, newValue);
            Debugging.ZDLog.Log("Progression", $"SkillUp type={type} value={newValue:F2} (was {currentValue:F2})");

            int newTier = _config.EffectConfig.GetTier(newValue);
            if (newTier != oldTier)
            {
                OnTierReached?.Invoke(type, newTier);
                Debugging.ZDLog.Log("Progression", $"TierReached type={type} tier={newTier}");
            }
        }

        // --- Debug / Test helpers ---

        public static void ClearEvents()
        {
            OnStatChanged = null;
            OnTierReached = null;
        }

#if UNITY_EDITOR
        public void DebugResetAll()
        {
            var statTypes = (StatType[])System.Enum.GetValues(typeof(StatType));
            foreach (var type in statTypes)
            {
                float old = _values[type];
                _values[type] = 0f;
                OnStatChanged?.Invoke(type, old, 0f);
            }
        }

        public void DebugMaxAll()
        {
            var statTypes = (StatType[])System.Enum.GetValues(typeof(StatType));
            foreach (var type in statTypes)
            {
                float old = _values[type];
                var curve = _config?.GetCurve(type);
                float max = curve != null ? curve.MaxValue : 100f;
                _values[type] = max;
                OnStatChanged?.Invoke(type, old, max);
            }
        }

        public void DebugAddToAll(float amount)
        {
            var statTypes = (StatType[])System.Enum.GetValues(typeof(StatType));
            foreach (var type in statTypes)
                AddExperience(type, amount);
        }
#endif

        // --- ISaveable ---

        [Serializable]
        private struct SaveData
        {
            public StatEntry[] Stats;
        }

        [Serializable]
        private struct StatEntry
        {
            public StatType Type;
            public float Value;
        }

        public object CaptureState()
        {
            var statTypes = (StatType[])Enum.GetValues(typeof(StatType));
            var entries = new StatEntry[statTypes.Length];
            for (int i = 0; i < statTypes.Length; i++)
            {
                entries[i] = new StatEntry
                {
                    Type = statTypes[i],
                    Value = GetStat(statTypes[i])
                };
            }
            return new SaveData { Stats = entries };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;

            if (data.Stats == null) return;

            foreach (var entry in data.Stats)
            {
                if (_values.ContainsKey(entry.Type))
                    _values[entry.Type] = entry.Value;
            }
        }
    }
}
