using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Combat/Training Dummy Config", fileName = "TrainingDummyConfig")]
    public class TrainingDummyConfig : ScriptableObject
    {
        [SerializeField] private int _hitsRequired = 10;
        [SerializeField] private float _xpReward = 5f;
        [SerializeField] private string _completionReply = "Неплохо. Приходи ещё.";

        public int HitsRequired => _hitsRequired;
        public float XpReward => _xpReward;
        public string CompletionReply => _completionReply;
    }
}
