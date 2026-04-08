using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Combat/Wound Config", fileName = "NewWoundConfig")]
    public class WoundConfig : ScriptableObject
    {
        [SerializeField] private WoundType _type;
        [SerializeField] private float _healTime = 120f;
        [SerializeField] private float _hpDrainPerSecond = 0f;
        [SerializeField] private float _speedMultiplier = 1f;
        [SerializeField] private float _attackSpeedMultiplier = 1f;
        [SerializeField] private float _accuracyMultiplier = 1f;
        [SerializeField] private string _healingItemId = "";
        [SerializeField] private string[] _woundReplies = new string[0];

        public WoundType Type => _type;
        public float HealTime => _healTime;
        public float HPDrainPerSecond => _hpDrainPerSecond;
        public float SpeedMultiplier => _speedMultiplier;
        public float AttackSpeedMultiplier => _attackSpeedMultiplier;
        public float AccuracyMultiplier => _accuracyMultiplier;
        public string HealingItemId => _healingItemId;
        public string[] WoundReplies => _woundReplies;
    }
}
