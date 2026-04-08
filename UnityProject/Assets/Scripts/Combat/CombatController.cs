using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.Combat
{
    public class CombatController : MonoBehaviour
    {
        [SerializeField] private CombatConfig _config;
        [SerializeField] private WeaponEquipSystem _weaponEquip;
        [SerializeField] private HitboxTrigger _hitbox;
        [SerializeField] private CharacterAutoMove _autoMove;

        private Animator _animator;
        private float _lastAttackTime;
        private GameObject _currentTarget;
        private bool _isAttacking;

        private float _damageMultiplier = 1f;
        private float _attackSpeedMultiplier = 1f;
        private float _hitChance = 1f;

        public static event System.Action<WeaponData> OnAttackPerformed;
        public static event System.Action<bool> OnAttackResult; // true = hit, false = glancing blow
        public bool IsAttacking => _isAttacking;

        private static readonly int AttackTrigger = Animator.StringToHash("Attack");

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_hitbox != null) _hitbox.Setup(gameObject);
        }

        public void AttackTarget(GameObject target)
        {
            if (target == null) return;

            float cooldown = _config != null ? _config.AttackCooldown : 0.8f;
            if (Time.time - _lastAttackTime < cooldown) return;

            _currentTarget = target;

            float range = _weaponEquip != null && _weaponEquip.HasWeapon
                ? _weaponEquip.CurrentWeapon.AttackRange
                : (_config != null ? _config.AttackApproachRange : 1.5f);

            if (_autoMove != null)
            {
                _autoMove.MoveTo(target.transform.position, range, OnReachedTarget);
            }
            else
            {
                PerformAttack();
            }
        }

        private void OnReachedTarget()
        {
            if (_currentTarget == null) return;

            var dir = (_currentTarget.transform.position - transform.position).normalized;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            PerformAttack();
        }

        private void PerformAttack()
        {
            _isAttacking = true;
            _lastAttackTime = Time.time;

            float damage;
            float attackAnimSpeed = 1f;
            WoundType woundType = WoundType.Puncture;
            float woundSeverity = 0.2f;

            if (_weaponEquip != null && _weaponEquip.HasWeapon)
            {
                var weapon = _weaponEquip.CurrentWeapon;
                damage = weapon.Damage;
                attackAnimSpeed = weapon.AttackSpeed;
                woundType = weapon.InflictedWoundType;
                woundSeverity = weapon.WoundSeverity;
            }
            else
            {
                damage = _config != null ? _config.UnarmedDamage : 5f;
            }

            float finalDamage;
            if (Random.value < _hitChance)
            {
                finalDamage = damage * _damageMultiplier;
                OnAttackResult?.Invoke(true);
            }
            else
            {
                finalDamage = damage * _damageMultiplier * 0.1f;
                OnAttackResult?.Invoke(false);
            }

            var info = new DamageInfo(finalDamage, woundType, woundSeverity, gameObject);

            if (_animator != null)
            {
                _animator.speed = attackAnimSpeed * _attackSpeedMultiplier;
                _animator.SetTrigger(AttackTrigger);
            }

            if (_hitbox != null)
                _hitbox.Activate(info);

            // В финале заменить на Animation Event; для прототипа — достаточно
            Invoke(nameof(EndAttack), 0.4f);

            OnAttackPerformed?.Invoke(_weaponEquip?.CurrentWeapon);
        }

        public void SetDamageMultiplier(float value) => _damageMultiplier = value;
        public void SetAttackSpeedMultiplier(float value) => _attackSpeedMultiplier = value;
        public void SetHitChance(float value) => _hitChance = Mathf.Clamp01(value);

        private void EndAttack()
        {
            _isAttacking = false;
            if (_animator != null)
                _animator.speed = 1f;
            if (_hitbox != null)
                _hitbox.Deactivate();
            _currentTarget = null;
        }
    }
}
