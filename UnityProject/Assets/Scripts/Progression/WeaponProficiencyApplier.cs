using UnityEngine;
using ZeldaDaughter.Combat;

namespace ZeldaDaughter.Progression
{
    /// <summary>
    /// Применяет текущее мастерство оружия к боевым параметрам.
    /// При нулевом мастерстве: 70% урона, 60% скорости, -30% к шансу попадания.
    /// При максимальном мастерстве: 100% урона, 100% скорости, +0% к шансу попадания.
    /// </summary>
    public class WeaponProficiencyApplier : MonoBehaviour
    {
        [SerializeField] private WeaponProficiency _proficiency;
        [SerializeField] private CombatController _combatController;
        [SerializeField] private WeaponEquipSystem _weaponEquip;

        private void OnEnable()
        {
            WeaponProficiency.OnProficiencyChanged += HandleProficiencyChanged;
            WeaponEquipSystem.OnWeaponChanged += HandleWeaponChanged;
            RefreshMultipliers();
        }

        private void OnDisable()
        {
            WeaponProficiency.OnProficiencyChanged -= HandleProficiencyChanged;
            WeaponEquipSystem.OnWeaponChanged -= HandleWeaponChanged;
        }

        private void HandleProficiencyChanged(WeaponType changedType, float oldValue, float newValue)
        {
            // Обновляем только если изменилось мастерство текущего оружия
            if (_weaponEquip == null || !_weaponEquip.HasWeapon) return;
            if (_weaponEquip.CurrentWeapon.Type != changedType) return;

            RefreshMultipliers();
        }

        private void HandleWeaponChanged(WeaponData weapon)
        {
            RefreshMultipliers();
        }

        private void RefreshMultipliers()
        {
            if (_combatController == null) return;

            if (_proficiency == null || _weaponEquip == null || !_weaponEquip.HasWeapon)
            {
                // Без оружия или без данных о мастерстве — нейтральные значения
                _combatController.SetProficiencyDamageMultiplier(1f);
                _combatController.SetProficiencySpeedMultiplier(1f);
                _combatController.SetProficiencyHitBonus(0f);
                return;
            }

            var weapon = _weaponEquip.CurrentWeapon;
            float norm = _proficiency.GetProficiencyNormalized(weapon.Type);

            float damageMul = Mathf.Lerp(0.7f, 1.0f, norm);
            float speedMul = Mathf.Lerp(0.6f, 1.0f, norm);
            float hitBonus = Mathf.Lerp(-0.3f, 0.0f, norm);

            _combatController.SetProficiencyDamageMultiplier(damageMul);
            _combatController.SetProficiencySpeedMultiplier(speedMul);
            _combatController.SetProficiencyHitBonus(hitBonus);
        }
    }
}
