using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Progression
{
    /// <summary>
    /// Отслеживает действия игрока и начисляет опыт в соответствующие навыки.
    /// Подписывается на статические события боевых систем.
    /// </summary>
    public class ActionTracker : MonoBehaviour
    {
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private PlayerInventory _playerInventory;

        private Vector3 _lastPosition;
        private float _distanceAccumulator;

        private const float DISTANCE_THRESHOLD = 5f;

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            CombatController.OnAttackResult += HandleAttackResult;
            PlayerHealthState.OnDamageTaken += HandleDamageTaken;
            EnemyHealth.OnDeath += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            CombatController.OnAttackResult -= HandleAttackResult;
            PlayerHealthState.OnDamageTaken -= HandleDamageTaken;
            EnemyHealth.OnDeath -= HandleEnemyKilled;
        }

        private void Update()
        {
            float dist = Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;
            _distanceAccumulator += dist;

            if (_distanceAccumulator >= DISTANCE_THRESHOLD)
            {
                float units = _distanceAccumulator / DISTANCE_THRESHOLD;
                _playerStats.AddExperience(StatType.Endurance, units);

                if (_playerInventory != null && _playerInventory.WeightRatio > 1f)
                    _playerStats.AddExperience(StatType.CarryCapacity, units);

                _distanceAccumulator = 0f;
            }
        }

        private void HandleAttackResult(bool hit)
        {
            _playerStats.AddExperience(StatType.Strength, 1f);

            if (hit)
            {
                _playerStats.AddExperience(StatType.Accuracy, 1f);
            }
            else
            {
                var accuracyCurve = _playerStats.Config.GetCurve(StatType.Accuracy);
                float failureMultiplier = accuracyCurve != null ? accuracyCurve.FailureMultiplier : 0.3f;
                _playerStats.AddExperience(StatType.Accuracy, failureMultiplier);
            }

            _playerStats.AddExperience(StatType.Agility, 0.5f);
        }

        private void HandleDamageTaken(float amount)
        {
            _playerStats.AddExperience(StatType.Toughness, amount * 0.1f);
        }

        private void HandleEnemyKilled(EnemyHealth enemy)
        {
            var strengthCurve = _playerStats.Config.GetCurve(StatType.Strength);
            float bonus = strengthCurve != null ? strengthCurve.VictoryBonus : 3f;

            _playerStats.AddExperience(StatType.Strength, bonus);
            _playerStats.AddExperience(StatType.Accuracy, bonus);
        }
    }
}
