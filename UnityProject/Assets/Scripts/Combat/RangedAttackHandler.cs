using UnityEngine;
using ZeldaDaughter.Core;

namespace ZeldaDaughter.Combat
{
    /// <summary>Обработчик дальнобойных атак (лук). Дополняет CombatController, не заменяет его.</summary>
    public class RangedAttackHandler : MonoBehaviour
    {
        [SerializeField] private CombatController _combatController;
        [SerializeField] private GameObjectPool _projectilePool;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private float _maxRange = 15f;

        private WeaponEquipSystem _weaponEquip;

        private void Awake()
        {
            _weaponEquip = GetComponent<WeaponEquipSystem>();
        }

        /// <summary>Возвращает true, если текущее оружие — дальнобойное.</summary>
        public bool CanHandleRanged()
        {
            if (_weaponEquip == null || !_weaponEquip.HasWeapon) return false;
            return _weaponEquip.CurrentWeapon.IsRanged;
        }

        /// <summary>Выпустить снаряд в направлении targetPosition.</summary>
        public void PerformRangedAttack(Vector3 targetPosition)
        {
            if (!CanHandleRanged()) return;
            if (_projectilePool == null) return;

            var origin = _firePoint != null ? _firePoint.position : transform.position;

            var direction = targetPosition - origin;
            direction.y = 0f;

            // Цель слишком далеко
            if (direction.magnitude > _maxRange)
                direction = direction.normalized * _maxRange;

            direction.Normalize();

            var go = _projectilePool.Get();
            if (go == null) return;

            go.transform.position = origin;
            go.transform.forward = direction;

            if (go.TryGetComponent<Projectile>(out var proj))
            {
                var weapon = _weaponEquip.CurrentWeapon;
                // damageMultiplier = 1f: прогрессия будет расширять это через CombatController
                proj.Launch(direction, weapon.ProjectileSpeed, weapon.Damage, 1f, _projectilePool);
            }

            CombatController.RaiseAttackPerformed(_weaponEquip.CurrentWeapon);
        }
    }
}
