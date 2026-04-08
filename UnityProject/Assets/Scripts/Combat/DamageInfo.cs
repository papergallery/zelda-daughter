using UnityEngine;

namespace ZeldaDaughter.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public WoundType WoundType;
        public float WoundSeverity;
        public GameObject Source;

        public DamageInfo(float amount, WoundType woundType, float severity, GameObject source = null)
        {
            Amount = amount;
            WoundType = woundType;
            WoundSeverity = severity;
            Source = source;
        }
    }
}
