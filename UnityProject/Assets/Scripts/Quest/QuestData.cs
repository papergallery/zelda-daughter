using UnityEngine;

namespace ZeldaDaughter.Quest
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Quest/Quest Data", fileName = "NewQuestData")]
    public class QuestData : ScriptableObject
    {
        [SerializeField] private string _questId;
        [SerializeField] private string _questGiverNpcId;
        [SerializeField] [TextArea] private string _notebookText;
        [SerializeField] [TextArea] private string _completionText;
        [SerializeField] private QuestCondition[] _conditions;
        [SerializeField] private QuestReward _reward;

        public string QuestId => _questId;
        public string QuestGiverNpcId => _questGiverNpcId;
        public string NotebookText => _notebookText;
        public string CompletionText => _completionText;
        public QuestCondition[] Conditions => _conditions;
        public QuestReward Reward => _reward;
    }
}
