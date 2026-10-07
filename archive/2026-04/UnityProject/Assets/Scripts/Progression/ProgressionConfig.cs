using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Progression
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Progression/Progression Config", fileName = "ProgressionConfig")]
    public class ProgressionConfig : ScriptableObject
    {
        [SerializeField] private StatGrowthCurve[] _growthCurves;
        [SerializeField] private StatEffectConfig _effectConfig;

        public StatEffectConfig EffectConfig => _effectConfig;
        public IReadOnlyList<StatGrowthCurve> GrowthCurves => _growthCurves;

        public StatGrowthCurve GetCurve(StatType type)
        {
            foreach (var curve in _growthCurves)
            {
                if (curve.StatType == type)
                    return curve;
            }
            return null;
        }
    }
}
