using System.Collections;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Размещается на кровати в таверне. Позволяет спать: промотка времени + лечение до 70% HP.
    /// </summary>
    public class SleepInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private float _interactionRange = 1.5f;
        [SerializeField] private float _sleepHours = 8f;
        [SerializeField] private float _healRatio = 0.7f;
        [SerializeField] private float _fadeInDuration = 0.5f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        private DayNightCycle _dayNightCycle;

        public string InteractionPrompt => "Спать";
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;
        public float InteractionRange => _interactionRange;
        public InteractionType Type => InteractionType.Station;

        private void Awake()
        {
            _dayNightCycle = FindObjectOfType<DayNightCycle>();
        }

        public bool CanInteract() => true;

        public void Interact(GameObject actor)
        {
            PlayerHealthState healthState = actor.GetComponent<PlayerHealthState>();
            StartCoroutine(SleepRoutine(healthState));
        }

        private IEnumerator SleepRoutine(PlayerHealthState healthState)
        {
            // Затемнение экрана
            yield return StartCoroutine(FadeScreen(0f, 1f, _fadeInDuration));

            // Промотка времени
            if (_dayNightCycle != null)
                _dayNightCycle.AdvanceTime(_sleepHours);

            // Лечение до порога
            if (healthState != null)
                healthState.HealToRatio(_healRatio);

            // Задержка на "чёрном экране"
            yield return new WaitForSeconds(0.3f);

            // Возврат экрана
            yield return StartCoroutine(FadeScreen(1f, 0f, _fadeOutDuration));
        }

        private IEnumerator FadeScreen(float from, float to, float duration)
        {
            // Используем CanvasGroup если доступен ScreenFader, иначе просто ждём
            // Реальный фейд реализуется отдельным ScreenFader — здесь только таймаут
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
