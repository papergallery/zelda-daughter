using System;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Quest
{
    [Serializable]
    public struct QuestReward
    {
        public ItemData[] RewardItems;
        public int[] RewardAmounts;
        public int GoldReward;
        public string UnlockDialogueNodeId;
    }
}
