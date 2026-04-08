using System;
using UnityEngine;

namespace ZeldaDaughter.Combat
{
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] private TrainingDummyConfig _config;
        [SerializeField] private Animator _animator;

        private int _hitCount;
        private bool _sessionActive;

        public static event Action<TrainingDummy> OnTrainingComplete;
        public static event Action<TrainingDummy> OnTrainingHit;

        // IDamageable: манекен бессмертен
        public bool IsAlive => true;

        public void StartSession()
        {
            _hitCount = 0;
            _sessionActive = true;
        }

        public void EndSession()
        {
            _sessionActive = false;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!_sessionActive) return;

            _hitCount++;

            if (_animator != null)
                _animator.SetTrigger("Hit");

            OnTrainingHit?.Invoke(this);

            if (_hitCount >= _config.HitsRequired)
            {
                OnTrainingComplete?.Invoke(this);
                EndSession();
            }
        }
    }
}
