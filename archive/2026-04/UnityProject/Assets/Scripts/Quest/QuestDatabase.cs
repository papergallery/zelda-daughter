using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Quest
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Quest/Quest Database", fileName = "QuestDatabase")]
    public class QuestDatabase : ScriptableObject
    {
        [SerializeField] private QuestData[] _quests;

        public IReadOnlyList<QuestData> Quests => _quests;

        public QuestData FindById(string id)
        {
            if (_quests == null) return null;
            foreach (var quest in _quests)
            {
                if (quest != null && quest.QuestId == id)
                    return quest;
            }
            return null;
        }
    }
}
