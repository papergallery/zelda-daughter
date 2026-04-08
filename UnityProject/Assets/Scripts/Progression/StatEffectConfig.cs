using UnityEngine;

namespace ZeldaDaughter.Progression
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Progression/Stat Effect Config", fileName = "StatEffectConfig")]
    public class StatEffectConfig : ScriptableObject
    {
        [Header("Damage")]
        [SerializeField] private float _maxDamageBonus = 1.5f;
        [SerializeField] private float _maxDamageReduction = 0.5f;

        [Header("Combat Speed")]
        [SerializeField] private float _maxAttackSpeedBonus = 0.8f;

        [Header("Accuracy")]
        [SerializeField] private float _baseHitChance = 0.5f;

        [Header("Recovery")]
        [SerializeField] private float _maxHealBonus = 1f;

        [Header("Carry")]
        [SerializeField] private float _maxCapacityBonus = 2f;

        [Header("Tiers")]
        [SerializeField] private float[] _tierThresholds = { 0f, 25f, 50f, 80f };
        [SerializeField] private float _tierReplicaCooldown = 30f;

        public float MaxDamageBonus => _maxDamageBonus;
        public float MaxDamageReduction => _maxDamageReduction;
        public float MaxAttackSpeedBonus => _maxAttackSpeedBonus;
        public float BaseHitChance => _baseHitChance;
        public float MaxHealBonus => _maxHealBonus;
        public float MaxCapacityBonus => _maxCapacityBonus;
        public float[] TierThresholds => _tierThresholds;
        public float TierReplicaCooldown => _tierReplicaCooldown;

        // Возвращает индекс тира (0–3): чем выше значение навыка, тем выше тир
        public int GetTier(float statValue)
        {
            int tier = 0;
            for (int i = 0; i < _tierThresholds.Length; i++)
            {
                if (statValue >= _tierThresholds[i])
                    tier = i;
            }
            return tier;
        }

        // strengthNorm — значение навыка, нормализованное [0..1]
        public float GetDamageMultiplier(float strengthNorm)
        {
            return 1f + strengthNorm * _maxDamageBonus;
        }

        public float GetDamageReduction(float toughnessNorm)
        {
            return toughnessNorm * _maxDamageReduction;
        }

        public float GetAttackSpeedMultiplier(float agilityNorm)
        {
            return 1f + agilityNorm * _maxAttackSpeedBonus;
        }

        // При Accuracy=0 шанс полного попадания = _baseHitChance; при Accuracy=max → 1.0
        public float GetHitChance(float accuracyNorm)
        {
            return _baseHitChance + accuracyNorm * (1f - _baseHitChance);
        }

        public float GetHealMultiplier(float enduranceNorm)
        {
            return 1f + enduranceNorm * _maxHealBonus;
        }

        public float GetCapacityMultiplier(float carryNorm)
        {
            return 1f + carryNorm * _maxCapacityBonus;
        }
    }
}
