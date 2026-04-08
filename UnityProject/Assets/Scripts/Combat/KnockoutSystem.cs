using System.Collections;
using UnityEngine;
using ZeldaDaughter.UI;
using ZeldaDaughter.Input;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Combat
{
    public class KnockoutSystem : MonoBehaviour
    {
        [SerializeField] private CombatConfig _config;
        [SerializeField] private GestureDispatcher _gestureDispatcher;

        private PlayerHealthState _healthState;
        private Animator _animator;
        private DayNightCycle _dayNightCycle;
        private bool _isKnockedOut;

        private static readonly int DefeatTrigger = Animator.StringToHash("Defeat");

        public bool IsKnockedOut => _isKnockedOut;

        private void Awake()
        {
            _healthState = GetComponent<PlayerHealthState>();
            _animator = GetComponentInChildren<Animator>();
            _dayNightCycle = FindObjectOfType<DayNightCycle>();
        }

        private void OnEnable()
        {
            PlayerHealthState.OnKnockout += TriggerKnockout;
        }

        private void OnDisable()
        {
            PlayerHealthState.OnKnockout -= TriggerKnockout;
        }

        public void TriggerKnockout()
        {
            if (_isKnockedOut) return;
            StartCoroutine(KnockoutSequence());
        }

        /// <summary>
        /// Сон в таверне — затемнение, промотка времени, хил до указанного соотношения HP.
        /// </summary>
        public void Sleep(float healToRatio, float hoursToAdvance)
        {
            StartCoroutine(SleepSequence(healToRatio, hoursToAdvance));
        }

        private IEnumerator KnockoutSequence()
        {
            _isKnockedOut = true;

            if (_gestureDispatcher != null)
                _gestureDispatcher.enabled = false;

            if (_animator != null)
                _animator.SetTrigger(DefeatTrigger);

            yield return FadeOverlay.FadeToBlack(1f);

            float knockoutDuration = _config != null ? _config.KnockoutDuration : 5f;
            yield return FadeOverlay.FlickerEffect(knockoutDuration, 3);

            // Revive() внутри хилит до ReviveHPRatio и вызывает OnRevive
            if (_healthState != null)
                _healthState.Revive();

            yield return FadeOverlay.FadeFromBlack(1.5f);

            if (_gestureDispatcher != null)
                _gestureDispatcher.enabled = true;

            _isKnockedOut = false;
        }

        private IEnumerator SleepSequence(float healToRatio, float hoursToAdvance)
        {
            if (_gestureDispatcher != null)
                _gestureDispatcher.enabled = false;

            yield return FadeOverlay.FadeToBlack(1f);

            if (_dayNightCycle != null)
                _dayNightCycle.AdvanceTime(hoursToAdvance);

            // Небольшая пауза — визуальный эффект пробуждения
            yield return new WaitForSecondsRealtime(1f);

            if (_healthState != null)
                _healthState.HealToRatio(healToRatio);

            yield return FadeOverlay.FadeFromBlack(1.5f);

            if (_gestureDispatcher != null)
                _gestureDispatcher.enabled = true;
        }
    }
}
