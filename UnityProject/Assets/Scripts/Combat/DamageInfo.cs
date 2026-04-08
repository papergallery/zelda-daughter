using UnityEngine;

namespace ZeldaDaughter.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public WoundType WoundType;
        public float WoundSeverity;
        public GameObject Source;
        /// <summary>Длительность оглушения в секундах. 0 = без оглушения.</summary>
        public float StunDuration;

        public DamageInfo(float amount, WoundType woundType, float severity, GameObject source = null, float stunDuration = 0f)
        {
            Amount = amount;
            WoundType = woundType;
            WoundSeverity = severity;
            Source = source;
            StunDuration = stunDuration;
        }
    }
}
