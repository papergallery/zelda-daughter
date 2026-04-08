using UnityEngine;

namespace ZeldaDaughter.Combat
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private EnemyData _data;

        private float _currentHP;

        public static event System.Action<EnemyHealth, float> OnDamaged; // enemy, hpRatio
        public static event System.Action<EnemyHealth> OnStagger;
        public static event System.Action<EnemyHealth> OnDeath;

        public float HealthRatio => _data != null ? _currentHP / _data.MaxHP : 0f;
        public bool IsAlive => _currentHP > 0f;
        public EnemyData Data => _data;

        private void Awake()
        {
            if (_data != null)
                _currentHP = _data.MaxHP;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            _currentHP -= info.Amount;

            OnDamaged?.Invoke(this, HealthRatio);

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                OnDeath?.Invoke(this);
                return;
            }

            // Стаггер если урон превышает порог относительно максимального HP
            if (_data != null && info.Amount / _data.MaxHP >= _data.StaggerThreshold)
            {
                OnStagger?.Invoke(this);
            }
        }

        public void Initialize(EnemyData data)
        {
            _data = data;
            _currentHP = data.MaxHP;
        }
    }
}
