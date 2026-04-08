using System.Collections;
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

        // Слой статов (от PlayerStats / StatEffectApplier)
        private float _damageMultiplier = 1f;
        private float _attackSpeedMultiplier = 1f;
        private float _hitChance = 1f;

        // Слой мастерства оружия (от WeaponProficiencyApplier)
        private float _profDamageMultiplier = 1f;
        private float _profSpeedMultiplier = 1f;
        private float _profHitBonus = 0f;

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
            WeaponData weapon = _weaponEquip != null && _weaponEquip.HasWeapon
                ? _weaponEquip.CurrentWeapon
                : null;

            if (weapon != null)
            {
                damage = weapon.Damage;
                attackAnimSpeed = weapon.AttackSpeed;
                woundType = weapon.InflictedWoundType;
                woundSeverity = weapon.WoundSeverity;
            }
            else
            {
                damage = _config != null ? _config.UnarmedDamage : 5f;
            }

            // Итоговые множители с учётом обоих слоёв
            float finalDamageMul = _damageMultiplier * _profDamageMultiplier;
            float finalSpeedMul = _attackSpeedMultiplier * _profSpeedMultiplier;
            float finalHitChance = Mathf.Clamp01(_hitChance + _profHitBonus);

            bool isHit = Random.value < finalHitChance;
            float finalDamage = isHit
                ? damage * finalDamageMul
                : damage * finalDamageMul * 0.1f;

            RaiseAttackResult(isHit);

            if (_animator != null)
            {
                _animator.speed = attackAnimSpeed * finalSpeedMul;
                _animator.SetTrigger(AttackTrigger);
            }

            // Серия быстрых ударов (дага, коготь и т.п.)
            if (weapon != null && weapon.RapidHitCount > 1)
            {
                StartCoroutine(RapidHitCoroutine(finalDamage, woundType, woundSeverity, weapon.RapidHitCount));
            }
            else
            {
                var info = new DamageInfo(finalDamage, woundType, woundSeverity, gameObject);
                if (_hitbox != null) _hitbox.Activate(info);
                ApplyStun(weapon, isHit);
            }

            // В финале заменить на Animation Event; для прототипа — достаточно
            Invoke(nameof(EndAttack), 0.4f);

            RaiseAttackPerformed(weapon);
        }

        private IEnumerator RapidHitCoroutine(float totalDamage, WoundType woundType, float woundSeverity, int hitCount)
        {
            float damagePerHit = totalDamage / hitCount;
            var info = new DamageInfo(damagePerHit, woundType, woundSeverity, gameObject);

            for (int i = 0; i < hitCount; i++)
            {
                if (_hitbox != null) _hitbox.Activate(info);
                yield return new WaitForSeconds(0.15f);
            }
        }

        private void ApplyStun(WeaponData weapon, bool isHit)
        {
            if (weapon == null || !isHit || weapon.StunDuration <= 0f) return;
            if (_currentTarget == null) return;

            if (_currentTarget.TryGetComponent<StunEffect>(out var stunEffect))
            {
                stunEffect.Apply(weapon.StunDuration);
            }
        }

        // --- Static методы для внешних классов (RangedAttackHandler, Projectile, etc.) ---

        public static void RaiseAttackPerformed(WeaponData weapon)
        {
            OnAttackPerformed?.Invoke(weapon);
        }

        public static void RaiseAttackResult(bool isHit)
        {
            OnAttackResult?.Invoke(isHit);
        }

        // --- Set-методы слоя статов (используются StatEffectApplier) ---

        public void SetDamageMultiplier(float value) => _damageMultiplier = value;
        public void SetAttackSpeedMultiplier(float value) => _attackSpeedMultiplier = value;
        public void SetHitChance(float value) => _hitChance = Mathf.Clamp01(value);

        // --- Set-методы слоя мастерства оружия (используются WeaponProficiencyApplier) ---

        public void SetProficiencyDamageMultiplier(float value) => _profDamageMultiplier = value;
        public void SetProficiencySpeedMultiplier(float value) => _profSpeedMultiplier = value;
        public void SetProficiencyHitBonus(float value) => _profHitBonus = value;

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
