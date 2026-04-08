using System;

namespace ZeldaDaughter.Quest
{
    public enum QuestConditionType
    {
        BringItem,
        KillEnemy,
        VisitLocation
    }

    [Serializable]
    public struct QuestCondition
    {
        public QuestConditionType Type;
        public string TargetId;
        public int RequiredCount;
    }
}
