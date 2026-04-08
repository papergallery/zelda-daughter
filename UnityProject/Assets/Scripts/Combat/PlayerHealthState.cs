using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.Combat
{
    public class PlayerHealthState : MonoBehaviour, ISaveable, IDamageable
    {
        [SerializeField] private CombatConfig _config;
        [SerializeField] private WoundConfig[] _woundConfigs;

        private float _currentHP;
        private List<Wound> _activeWounds = new();
        private bool _isResting;

        public static event Action<float> OnHealthChanged;
        public static event Action<Wound> OnWoundAdded;
        public static event Action<WoundType> OnWoundRemoved;
        public static event Action OnKnockout;
        public static event Action OnRevive;

        public float HealthRatio => _currentHP / _config.MaxHP;
        public bool IsAlive => _currentHP > 0f;
        public IReadOnlyList<Wound> ActiveWounds => _activeWounds;

        public string SaveId => "player_health";

        private void Awake()
        {
            _currentHP = _config.MaxHP;
        }

        private void Update()
        {
            if (!IsAlive) return;

            TickWounds();
            ApplyNaturalHealing();
        }

        private void TickWounds()
        {
            bool anyRemoved = false;

            for (int i = _activeWounds.Count - 1; i >= 0; i--)
            {
                var wound = _activeWounds[i];

                // Кровотечение от колотых ран
                WoundConfig cfg = FindConfig(wound.Type);
                if (cfg != null && cfg.HPDrainPerSecond > 0f)
                {
                    float drain = cfg.HPDrainPerSecond * wound.Severity * Time.deltaTime;
                    _currentHP -= drain;
                    _currentHP = Mathf.Max(_currentHP, 0f);
                }

                // Тик заживления
                float healRate = _isResting ? _config.RestHealMultiplier : 1f;
                wound.RemainingTime -= Time.deltaTime * healRate;

                if (wound.RemainingTime <= 0f)
                {
                    _activeWounds.RemoveAt(i);
                    OnWoundRemoved?.Invoke(wound.Type);
                    anyRemoved = true;
                }
                else
                {
                    _activeWounds[i] = wound;
                }
            }

            if (_currentHP <= 0f && IsAlive)
            {
                _currentHP = 0f;
                OnHealthChanged?.Invoke(0f);
                OnKnockout?.Invoke();
                return;
            }

            if (anyRemoved)
                OnHealthChanged?.Invoke(HealthRatio);
        }

        private void ApplyNaturalHealing()
        {
            if (_currentHP >= _config.MaxHP) return;

            float rate = _config.NaturalHealRate;
            if (_isResting) rate *= _config.RestHealMultiplier;

            _currentHP = Mathf.Min(_currentHP + rate * Time.deltaTime, _config.MaxHP);
            OnHealthChanged?.Invoke(HealthRatio);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            _currentHP -= info.Amount;
            _currentHP = Mathf.Max(_currentHP, 0f);

            if (info.WoundSeverity > 0f)
                AddWound(info.WoundType, info.WoundSeverity);

            OnHealthChanged?.Invoke(HealthRatio);

            if (_currentHP <= 0f)
                OnKnockout?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;

            _currentHP = Mathf.Min(_currentHP + amount, _config.MaxHP);
            OnHealthChanged?.Invoke(HealthRatio);
        }

        public void HealToRatio(float ratio)
        {
            float target = _config.MaxHP * Mathf.Clamp01(ratio);
            if (_currentHP < target)
                Heal(target - _currentHP);
        }

        public void AddWound(WoundType type, float severity)
        {
            WoundConfig cfg = FindConfig(type);
            if (cfg == null)
            {
                Debug.LogWarning($"[PlayerHealthState] WoundConfig not found for {type}");
                return;
            }

            // Заменяем существующую рану того же типа, если она менее серьёзная
            for (int i = 0; i < _activeWounds.Count; i++)
            {
                if (_activeWounds[i].Type == type)
                {
                    if (severity <= _activeWounds[i].Severity) return;
                    _activeWounds.RemoveAt(i);
                    break;
                }
            }

            var wound = new Wound(type, severity, cfg.HealTime);
            _activeWounds.Add(wound);
            OnWoundAdded?.Invoke(wound);
            OnHealthChanged?.Invoke(HealthRatio);
        }

        public void TreatWound(WoundType type)
        {
            for (int i = 0; i < _activeWounds.Count; i++)
            {
                if (_activeWounds[i].Type == type)
                {
                    _activeWounds.RemoveAt(i);
                    OnWoundRemoved?.Invoke(type);
                    OnHealthChanged?.Invoke(HealthRatio);
                    return;
                }
            }
        }

        public void SetResting(bool resting)
        {
            _isResting = resting;
        }

        public void Revive()
        {
            _currentHP = _config.MaxHP * _config.ReviveHPRatio;
            OnHealthChanged?.Invoke(HealthRatio);
            OnRevive?.Invoke();
        }

        private WoundConfig FindConfig(WoundType type)
        {
            if (_woundConfigs == null) return null;
            foreach (var cfg in _woundConfigs)
            {
                if (cfg != null && cfg.Type == type)
                    return cfg;
            }
            return null;
        }

        // --- ISaveable ---

        [Serializable]
        private struct SaveData
        {
            public float HP;
            public WoundSaveEntry[] Wounds;
        }

        [Serializable]
        private struct WoundSaveEntry
        {
            public WoundType Type;
            public float Severity;
            public float RemainingTime;
            public float MaxTime;
        }

        public object CaptureState()
        {
            var wounds = new WoundSaveEntry[_activeWounds.Count];
            for (int i = 0; i < _activeWounds.Count; i++)
            {
                wounds[i] = new WoundSaveEntry
                {
                    Type = _activeWounds[i].Type,
                    Severity = _activeWounds[i].Severity,
                    RemainingTime = _activeWounds[i].RemainingTime,
                    MaxTime = _activeWounds[i].MaxTime
                };
            }
            return new SaveData { HP = _currentHP, Wounds = wounds };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;

            _currentHP = Mathf.Clamp(data.HP, 0f, _config.MaxHP);
            _activeWounds.Clear();

            if (data.Wounds != null)
            {
                foreach (var entry in data.Wounds)
                {
                    var wound = new Wound(entry.Type, entry.Severity, entry.MaxTime);
                    wound.RemainingTime = entry.RemainingTime;
                    _activeWounds.Add(wound);
                }
            }

            OnHealthChanged?.Invoke(HealthRatio);
        }
    }
}
