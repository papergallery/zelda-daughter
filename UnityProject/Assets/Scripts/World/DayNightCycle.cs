using UnityEngine;

namespace ZeldaDaughter.World
{
    public enum TimeOfDay
    {
        Dawn,
        Day,
        Dusk,
        Night
    }

    /// <summary>
    /// Manages the day/night cycle. Rotates directional light, changes sky color.
    /// Full cycle = configurable real-time minutes (default 25 min).
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static event System.Action<TimeOfDay> OnTimeOfDayChanged;

        [Header("Cycle Settings")]
        [SerializeField] private float _fullCycleMinutes = 25f;
        [SerializeField] private Light _directionalLight;

        [Header("Light Settings")]
        [SerializeField] private Gradient _lightColorGradient;
        [SerializeField] private AnimationCurve _lightIntensityCurve = AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f);

        [Header("Ambient")]
        [SerializeField] private Gradient _ambientColorGradient;

        [Header("Debug")]
        [SerializeField] [Range(0f, 1f)] private float _timeOfDayNormalized;

        private TimeOfDay _currentTimeOfDay;
        private float _cycleDurationSeconds;

        /// <summary>
        /// Normalized time: 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk
        /// </summary>
        public float TimeNormalized => _timeOfDayNormalized;
        public TimeOfDay CurrentTimeOfDay => _currentTimeOfDay;

        /// <summary>
        /// Current in-game hour (0-24)
        /// </summary>
        public float CurrentHour => _timeOfDayNormalized * 24f;

        private void Awake()
        {
            _cycleDurationSeconds = _fullCycleMinutes * 60f;
            _timeOfDayNormalized = 0.35f; // Start at morning
        }

        private void Update()
        {
            _timeOfDayNormalized += Time.deltaTime / _cycleDurationSeconds;
            if (_timeOfDayNormalized >= 1f)
                _timeOfDayNormalized -= 1f;

            UpdateLighting();
            UpdateTimeOfDay();
        }

        private void UpdateLighting()
        {
            if (_directionalLight == null) return;

            // Rotate sun: 0 = midnight (below horizon), 0.5 = noon (overhead)
            float sunAngle = (_timeOfDayNormalized - 0.25f) * 360f;
            _directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

            if (_lightColorGradient != null)
                _directionalLight.color = _lightColorGradient.Evaluate(_timeOfDayNormalized);

            _directionalLight.intensity = _lightIntensityCurve.Evaluate(_timeOfDayNormalized);

            if (_ambientColorGradient != null)
                RenderSettings.ambientLight = _ambientColorGradient.Evaluate(_timeOfDayNormalized);
        }

        private void UpdateTimeOfDay()
        {
            var newTimeOfDay = _timeOfDayNormalized switch
            {
                < 0.2f => TimeOfDay.Night,
                < 0.3f => TimeOfDay.Dawn,
                < 0.7f => TimeOfDay.Day,
                < 0.8f => TimeOfDay.Dusk,
                _ => TimeOfDay.Night
            };

            if (newTimeOfDay != _currentTimeOfDay)
            {
                _currentTimeOfDay = newTimeOfDay;
                OnTimeOfDayChanged?.Invoke(_currentTimeOfDay);
            }
        }

        /// <summary>
        /// Skip time forward (e.g., sleeping at tavern).
        /// </summary>
        public void AdvanceTime(float hours)
        {
            _timeOfDayNormalized += hours / 24f;
            if (_timeOfDayNormalized >= 1f)
                _timeOfDayNormalized -= 1f;
        }

        /// <summary>
        /// Set time to specific hour (0-24).
        /// </summary>
        public void SetTime(float hour)
        {
            _timeOfDayNormalized = Mathf.Clamp01(hour / 24f);
        }
    }
}
