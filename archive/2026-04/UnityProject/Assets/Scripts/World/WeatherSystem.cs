using System;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// FSM-based weather controller. Advances through WeatherType states
    /// using probabilities and durations from WeatherConfig.
    /// Singleton — one per scene.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        /// <summary>Вызывается при смене погоды: (старая, новая).</summary>
        public static event Action<WeatherType, WeatherType> OnWeatherChanged;

        [SerializeField] private WeatherConfig _config;
        [SerializeField] private WeatherType _initialWeather = WeatherType.Clear;

        private WeatherType _currentWeather;
        private float _timeUntilNextTransition;

        public WeatherType CurrentWeather => _currentWeather;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _currentWeather = _initialWeather;
            _timeUntilNextTransition = GetDurationForCurrent();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            _timeUntilNextTransition -= Time.deltaTime;
            if (_timeUntilNextTransition <= 0f)
                TransitionToNext();
        }

        /// <summary>Принудительно устанавливает погоду (дебаг / тесты).</summary>
        public void ForceWeather(WeatherType type)
        {
            if (_currentWeather == type) return;

            var previous = _currentWeather;
            _currentWeather = type;
            _timeUntilNextTransition = GetDurationForCurrent();

            OnWeatherChanged?.Invoke(previous, _currentWeather);
        }

        private void TransitionToNext()
        {
            var next = _config != null
                ? _config.GetNextWeather(_currentWeather)
                : _currentWeather;

            var previous = _currentWeather;
            _currentWeather = next;
            _timeUntilNextTransition = GetDurationForCurrent();

            if (previous != _currentWeather)
                OnWeatherChanged?.Invoke(previous, _currentWeather);
        }

        private float GetDurationForCurrent()
        {
            if (_config != null)
                return _config.GetRandomDuration(_currentWeather);

            // Страховка если конфиг не назначен
            return 60f;
        }
    }
}
