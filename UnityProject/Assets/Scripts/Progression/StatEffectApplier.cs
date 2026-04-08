using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Progression
{
    /// <summary>
    /// Применяет эффекты навыков к геймплейным системам.
    /// Реагирует на изменения статов и обновляет множители в CombatController,
    /// PlayerHealthState и PlayerInventory.
    /// </summary>
    public class StatEffectApplier : MonoBehaviour
    {
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private CombatController _combatController;
        [SerializeField] private PlayerHealthState _healthState;
        [SerializeField] private PlayerInventory _inventory;

        private void OnEnable()
        {
            PlayerStats.OnStatChanged += HandleStatChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            PlayerStats.OnStatChanged -= HandleStatChanged;
        }

        private void HandleStatChanged(StatType type, float oldVal, float newVal)
        {
            switch (type)
            {
                case StatType.Strength:    RefreshStrength();    break;
                case StatType.Toughness:   RefreshToughness();   break;
                case StatType.Agility:     RefreshAgility();     break;
                case StatType.Accuracy:    RefreshAccuracy();    break;
                case StatType.Endurance:   RefreshEndurance();   break;
                case StatType.CarryCapacity: RefreshCarryCapacity(); break;
            }
        }

        private void RefreshAll()
        {
            RefreshStrength();
            RefreshToughness();
            RefreshAgility();
            RefreshAccuracy();
            RefreshEndurance();
            RefreshCarryCapacity();
        }

        private void RefreshStrength()
        {
            if (_combatController == null || _playerStats == null) return;

            float norm = _playerStats.GetStatNormalized(StatType.Strength);
            _combatController.SetDamageMultiplier(_playerStats.Config.EffectConfig.GetDamageMultiplier(norm));
        }

        private void RefreshToughness()
        {
            if (_healthState == null || _playerStats == null) return;

            float norm = _playerStats.GetStatNormalized(StatType.Toughness);
            _healthState.SetDamageReduction(_playerStats.Config.EffectConfig.GetDamageReduction(norm));
        }

        private void RefreshAgility()
        {
            if (_combatController == null || _playerStats == null) return;

            float norm = _playerStats.GetStatNormalized(StatType.Agility);
            _combatController.SetAttackSpeedMultiplier(_playerStats.Config.EffectConfig.GetAttackSpeedMultiplier(norm));
        }

        private void RefreshAccuracy()
        {
            if (_combatController == null || _playerStats == null) return;

            float norm = _playerStats.GetStatNormalized(StatType.Accuracy);
            _combatController.SetHitChance(_playerStats.Config.EffectConfig.GetHitChance(norm));
        }

        private void RefreshEndurance()
        {
            if (_healthState == null || _playerStats == null) return;

            float norm = _playerStats.GetStatNormalized(StatType.Endurance);
            _healthState.SetHealRateMultiplier(_playerStats.Config.EffectConfig.GetHealMultiplier(norm));
        }

        private void RefreshCarryCapacity()
        {
            if (_inventory == null || _playerStats == null) return;

            float norm = _playerStats.GetStatNormalized(StatType.CarryCapacity);
            _inventory.SetCapacityMultiplier(_playerStats.Config.EffectConfig.GetCapacityMultiplier(norm));
        }
    }
}
