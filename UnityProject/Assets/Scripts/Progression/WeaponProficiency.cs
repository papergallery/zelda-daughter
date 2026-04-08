using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.Progression
{
    public class WeaponProficiency : MonoBehaviour, ISaveable
    {
        [SerializeField] private WeaponProficiencyData _data;

        private Dictionary<WeaponType, float> _values;

        /// <summary>Вызывается при изменении мастерства: тип оружия, старое значение, новое значение.</summary>
        public static event Action<WeaponType, float, float> OnProficiencyChanged;

        public string SaveId => "weapon_proficiency";

        public WeaponProficiencyData Data => _data;

        private void Awake()
        {
            var weaponTypes = (WeaponType[])Enum.GetValues(typeof(WeaponType));
            _values = new Dictionary<WeaponType, float>(weaponTypes.Length);
            foreach (var type in weaponTypes)
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

        public float GetProficiency(WeaponType type)
        {
            return _values.TryGetValue(type, out float value) ? value : 0f;
        }

        public float GetProficiencyNormalized(WeaponType type)
        {
            var entry = _data.GetEntry(type);
            if (entry.MaxValue <= 0f) return 0f;
            return GetProficiency(type) / entry.MaxValue;
        }

        public void AddExperience(WeaponType type, float rawAmount)
        {
            float current = GetProficiency(type);
            float growth = _data.CalculateGrowth(type, current, rawAmount);
            if (growth <= 0f) return;

            var entry = _data.GetEntry(type);
            float newValue = Mathf.Min(current + growth, entry.MaxValue);

            if (Mathf.Approximately(newValue, current)) return;

            _values[type] = newValue;
            OnProficiencyChanged?.Invoke(type, current, newValue);
        }

        // --- ISaveable ---

        [Serializable]
        private struct SaveData
        {
            public ProfEntry[] Entries;
        }

        [Serializable]
        private struct ProfEntry
        {
            public WeaponType Type;
            public float Value;
        }

        public object CaptureState()
        {
            var weaponTypes = (WeaponType[])Enum.GetValues(typeof(WeaponType));
            var entries = new ProfEntry[weaponTypes.Length];
            for (int i = 0; i < weaponTypes.Length; i++)
            {
                entries[i] = new ProfEntry
                {
                    Type = weaponTypes[i],
                    Value = GetProficiency(weaponTypes[i])
                };
            }
            return new SaveData { Entries = entries };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;
            if (data.Entries == null) return;

            foreach (var entry in data.Entries)
            {
                if (_values.ContainsKey(entry.Type))
                    _values[entry.Type] = entry.Value;
            }
        }
    }
}
