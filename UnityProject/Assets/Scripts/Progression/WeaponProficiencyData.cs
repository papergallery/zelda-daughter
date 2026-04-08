using System;
using UnityEngine;
using ZeldaDaughter.Combat;

namespace ZeldaDaughter.Progression
{
    [Serializable]
    public struct WeaponProfEntry
    {
        public WeaponType Type;
        public float BaseGrowthRate;
        public float MaxValue;
        public float DecayExponent;
        public float FailureMultiplier;
    }

    [CreateAssetMenu(menuName = "ZeldaDaughter/Progression/Weapon Proficiency Data", fileName = "NewWeaponProficiencyData")]
    public class WeaponProficiencyData : ScriptableObject
    {
        [SerializeField] private WeaponProfEntry[] _entries;

        public WeaponProfEntry GetEntry(WeaponType type)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type == type)
                    return entry;
            }
            Debug.LogWarning($"[WeaponProficiencyData] No entry for {type}, returning default.");
            return default;
        }

        public float CalculateGrowth(WeaponType type, float currentValue, float rawAmount)
        {
            var entry = GetEntry(type);
            if (entry.MaxValue <= 0f) return 0f;
            return rawAmount * entry.BaseGrowthRate
                * Mathf.Pow(Mathf.Clamp01(1f - currentValue / entry.MaxValue), entry.DecayExponent);
        }
    }
}
