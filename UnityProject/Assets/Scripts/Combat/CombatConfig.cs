using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Combat/Combat Config", fileName = "CombatConfig")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _naturalHealRate = 0.1f;
        [SerializeField] private float _restHealMultiplier = 3f;

        [Header("Knockout")]
        [SerializeField] private float _knockoutDuration = 5f;
        [SerializeField] private float _reviveHPRatio = 0.15f;

        [Header("Combat")]
        [SerializeField] private float _unarmedDamage = 5f;
        [SerializeField] private float _unarmedSpeed = 1f;
        [SerializeField] private float _attackApproachRange = 1.5f;
        [SerializeField] private float _attackCooldown = 0.8f;

        [Header("Hunger")]
        [SerializeField] private float _hungerMaxTime = 600f;
        [SerializeField] private float _hungerDegradationThreshold = 0.7f;
        [SerializeField] private float _hungerSpeedPenalty = 0.6f;

        [Header("Replies")]
        [SerializeField] private string[] _healthReplies = new[]
        {
            "Ничего страшного...",
            "Болит...",
            "Плохо дело...",
            "Не могу дальше..."
        };

        [SerializeField] private string[] _hungerReplies = new[]
        {
            "Есть хочется...",
            "В животе урчит...",
            "Нужно что-нибудь съесть..."
        };

        public float MaxHP => _maxHP;
        public float NaturalHealRate => _naturalHealRate;
        public float RestHealMultiplier => _restHealMultiplier;
        public float KnockoutDuration => _knockoutDuration;
        public float ReviveHPRatio => _reviveHPRatio;
        public float UnarmedDamage => _unarmedDamage;
        public float UnarmedSpeed => _unarmedSpeed;
        public float AttackApproachRange => _attackApproachRange;
        public float AttackCooldown => _attackCooldown;
        public float HungerMaxTime => _hungerMaxTime;
        public float HungerDegradationThreshold => _hungerDegradationThreshold;
        public float HungerSpeedPenalty => _hungerSpeedPenalty;
        public string[] HealthReplies => _healthReplies;
        public string[] HungerReplies => _hungerReplies;
    }
}
