using UnityEngine;

namespace ZeldaDaughter.Progression
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Progression/Stat Growth Curve", fileName = "NewStatGrowthCurve")]
    public class StatGrowthCurve : ScriptableObject
    {
        [SerializeField] private StatType _statType;
        [SerializeField] private float _baseGrowthRate = 1f;
        [SerializeField] private float _decayExponent = 0.5f;
        [SerializeField] private float _maxValue = 100f;
        [SerializeField] private float _failureMultiplier = 0.3f;
        [SerializeField] private float _victoryBonus = 3f;

        public StatType StatType => _statType;
        public float BaseGrowthRate => _baseGrowthRate;
        public float DecayExponent => _decayExponent;
        public float MaxValue => _maxValue;
        public float FailureMultiplier => _failureMultiplier;
        public float VictoryBonus => _victoryBonus;

        // Рост замедляется по мере приближения к потолку (sqrt-кривая по умолчанию)
        public float CalculateGrowth(float currentValue, float rawAmount)
        {
            float normalized = Mathf.Clamp01(currentValue / _maxValue);
            return rawAmount * _baseGrowthRate * Mathf.Pow(1f - normalized, _decayExponent);
        }
    }
}
