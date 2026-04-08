using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Combat/Enemy Data", fileName = "NewEnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float _maxHP = 50f;

        [Header("Combat")]
        [SerializeField] private float _damage = 15f;
        [SerializeField] private WoundType _inflictedWoundType = WoundType.Puncture;
        [SerializeField] private float _woundSeverity = 0.5f;
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private float _attackCooldown = 2f;
        [SerializeField] private float _windupTime = 0.8f;
        [SerializeField] private float _staggerDuration = 1f;
        [SerializeField] private float _staggerThreshold = 0.3f; // % HP за один удар для стаггера

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _chaseSpeed = 5f;
        [SerializeField] private float _aggroRange = 8f;
        [SerializeField] private float _wanderRadius = 10f;

        [Header("Behavior")]
        [SerializeField] private bool _aggroOnSight = true;
        [SerializeField] private bool _aggroOnDamage = true;

        [Header("Loot")]
        [SerializeField] private LootTable _lootTable;
        [SerializeField] private float _respawnTime = 60f;

        public float MaxHP => _maxHP;
        public float Damage => _damage;
        public WoundType InflictedWoundType => _inflictedWoundType;
        public float WoundSeverity => _woundSeverity;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float WindupTime => _windupTime;
        public float StaggerDuration => _staggerDuration;
        public float StaggerThreshold => _staggerThreshold;
        public float MoveSpeed => _moveSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float AggroRange => _aggroRange;
        public float WanderRadius => _wanderRadius;
        public bool AggroOnSight => _aggroOnSight;
        public bool AggroOnDamage => _aggroOnDamage;
        public LootTable LootTable => _lootTable;
        public float RespawnTime => _respawnTime;
    }
}
