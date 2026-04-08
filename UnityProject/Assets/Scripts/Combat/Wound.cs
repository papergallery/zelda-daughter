namespace ZeldaDaughter.Combat
{
    [System.Serializable]
    public struct Wound
    {
        public WoundType Type;
        public float Severity;       // 0-1
        public float RemainingTime;  // секунды до заживления
        public float MaxTime;        // изначальное время заживления

        public Wound(WoundType type, float severity, float healTime)
        {
            Type = type;
            Severity = severity;
            RemainingTime = healTime;
            MaxTime = healTime;
        }

        public bool IsHealed => RemainingTime <= 0f;
        public float Progress => MaxTime > 0f ? 1f - (RemainingTime / MaxTime) : 1f;
    }
}
