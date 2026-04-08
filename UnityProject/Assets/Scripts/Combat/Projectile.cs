using UnityEngine;
using ZeldaDaughter.Core;

namespace ZeldaDaughter.Combat
{
    [RequireComponent(typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private float _damageMultiplier;
        private GameObjectPool _pool;
        private float _lifetime = 5f;
        private float _timer;
        private bool _spent;

        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
        }

        private void OnEnable()
        {
            _spent = false;
            _timer = 0f;
        }

        public void Launch(Vector3 direction, float speed, float damage, float damageMultiplier, GameObjectPool pool)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _damageMultiplier = damageMultiplier;
            _pool = pool;
            _timer = 0f;
            _spent = false;
        }

        private void Update()
        {
            if (_spent) return;

            transform.position += _direction * _speed * Time.deltaTime;

            if (_direction.sqrMagnitude > 0.001f)
                transform.forward = _direction;

            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
                ReturnToPool();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_spent) return;

            // Пропускаем самого игрока
            if (other.CompareTag("Player")) return;

            // Пытаемся нанести урон врагу
            var damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                var info = new DamageInfo(_damage * _damageMultiplier, WoundType.Puncture, 0.2f, gameObject);
                damageable.TakeDamage(info);
                CombatController.RaiseAttackResult(true);
                ReturnToPool();
                return;
            }

            // Попали в нетриггерное препятствие (стена, земля)
            if (!other.isTrigger)
                ReturnToPool();
        }

        private void ReturnToPool()
        {
            _spent = true;
            if (_pool != null)
                _pool.Release(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
