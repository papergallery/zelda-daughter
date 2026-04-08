using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [RequireComponent(typeof(Collider))]
    public class HitboxTrigger : MonoBehaviour
    {
        private Collider _collider;
        private DamageInfo _currentDamage;
        private bool _isActive;
        private readonly HashSet<int> _hitTargets = new();
        private GameObject _owner;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _collider.enabled = false;
        }

        public void Setup(GameObject owner)
        {
            _owner = owner;
        }

        public void Activate(DamageInfo info)
        {
            _currentDamage = info;
            _hitTargets.Clear();
            _collider.enabled = true;
            _isActive = true;
        }

        public void Deactivate()
        {
            _collider.enabled = false;
            _isActive = false;
            _hitTargets.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;

            int id = other.gameObject.GetInstanceID();
            if (_hitTargets.Contains(id)) return;
            if (_owner != null && other.gameObject == _owner) return;
            // Не бить самого себя через child collider
            if (_owner != null && other.transform.root == _owner.transform.root) return;

            var damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            _hitTargets.Add(id);
            damageable.TakeDamage(_currentDamage);
        }
    }
}
