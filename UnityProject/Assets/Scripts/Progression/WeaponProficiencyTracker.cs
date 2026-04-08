using UnityEngine;
using ZeldaDaughter.Combat;

namespace ZeldaDaughter.Progression
{
    /// <summary>
    /// Отслеживает боевые события и начисляет опыт мастерства оружия.
    /// Подписывается на CombatController и EnemyHealth.
    /// </summary>
    public class WeaponProficiencyTracker : MonoBehaviour
    {
        [SerializeField] private WeaponProficiency _proficiency;
        [SerializeField] private WeaponEquipSystem _weaponEquip;

        private void OnEnable()
        {
            CombatController.OnAttackResult += HandleAttackResult;
            EnemyHealth.OnDeath += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            CombatController.OnAttackResult -= HandleAttackResult;
            EnemyHealth.OnDeath -= HandleEnemyKilled;
        }

        private void HandleAttackResult(bool hit)
        {
            if (_proficiency == null || _weaponEquip == null) return;
            if (!_weaponEquip.HasWeapon) return;

            var weapon = _weaponEquip.CurrentWeapon;
            float amount;

            if (hit)
            {
                amount = 1f;
            }
            else
            {
                // Опыт за промах меньше, но всё равно начисляется — Kenshi-логика
                var entry = _proficiency.Data.GetEntry(weapon.Type);
                amount = entry.FailureMultiplier;
            }

            _proficiency.AddExperience(weapon.Type, amount);
        }

        private void HandleEnemyKilled(EnemyHealth enemy)
        {
            if (_proficiency == null || _weaponEquip == null) return;
            if (!_weaponEquip.HasWeapon) return;

            var weapon = _weaponEquip.CurrentWeapon;
            _proficiency.AddExperience(weapon.Type, 3f);
        }
    }
}
