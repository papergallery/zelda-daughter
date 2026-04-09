using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Drives visual weather effects: rain particles, ambient light, fog density.
    /// Subscribes to WeatherSystem.OnWeatherChanged and transitions smoothly between states.
    /// </summary>
    public class WeatherVFXController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _rainParticles;
        [SerializeField] private Light _mainLight;
        [SerializeField] private WeatherConfig _config;
        [SerializeField] private float _transitionDuration = 3f;

        // Fallback values used when WeatherConfig has no entry for a type
        private const float FallbackFogClear    = 0.002f;
        private const float FallbackFogCloudy   = 0.015f;
        private const float FallbackFogRain     = 0.04f;
        private const float FallbackFogStorm    = 0.08f;
        private const float FallbackAmbientFull = 1.0f;
        private const float FallbackAmbientLow  = 0.45f;

        private Coroutine _transitionCoroutine;
        private float _baseLightIntensity;

        private void Awake()
        {
            if (_mainLight != null)
                _baseLightIntensity = _mainLight.intensity;
        }

        private void OnEnable()
        {
            WeatherSystem.OnWeatherChanged += HandleWeatherChanged;
        }

        private void OnDisable()
        {
            WeatherSystem.OnWeatherChanged -= HandleWeatherChanged;
        }

        private void Start()
        {
            // Sync with whatever weather is already active when the scene loads
            if (WeatherSystem.Instance != null)
                ApplyImmediate(WeatherSystem.Instance.CurrentWeather);
        }

        private void HandleWeatherChanged(WeatherType previous, WeatherType next)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _transitionCoroutine = StartCoroutine(TransitionTo(next));
        }

        private IEnumerator TransitionTo(WeatherType type)
        {
            GetTargetVisuals(type, out float targetFog, out float targetAmbient, out float targetRainIntensity);

            float startFog     = RenderSettings.fogDensity;
            float startAmbient = RenderSettings.ambientIntensity;
            float startRain    = GetCurrentEmissionRate();

            // Ensure particles are running if we need rain
            if (targetRainIntensity > 0f && _rainParticles != null && !_rainParticles.isPlaying)
                _rainParticles.Play();

            float elapsed = 0f;
            while (elapsed < _transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _transitionDuration;

                RenderSettings.fogDensity     = Mathf.Lerp(startFog, targetFog, t);
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbient, targetAmbient, t);
                SetEmissionRate(Mathf.Lerp(startRain, targetRainIntensity, t));

                yield return null;
            }

            // Snap to final values
            RenderSettings.fogDensity      = targetFog;
            RenderSettings.ambientIntensity = targetAmbient;
            SetEmissionRate(targetRainIntensity);

            // Stop particles completely if no rain needed
            if (targetRainIntensity <= 0f && _rainParticles != null)
                _rainParticles.Stop();

            _transitionCoroutine = null;
        }

        /// <summary>Instantly sets all visuals without lerping (used on scene start).</summary>
        private void ApplyImmediate(WeatherType type)
        {
            GetTargetVisuals(type, out float fog, out float ambient, out float rainIntensity);

            RenderSettings.fogDensity       = fog;
            RenderSettings.ambientIntensity  = ambient;

            if (_rainParticles != null)
            {
                SetEmissionRate(rainIntensity);
                if (rainIntensity > 0f)
                    _rainParticles.Play();
                else
                    _rainParticles.Stop();
            }
        }

        private void GetTargetVisuals(WeatherType type,
            out float fogDensity, out float ambientIntensity, out float rainIntensity)
        {
            if (_config != null && _config.TryGetVisuals(type, out var visuals))
            {
                fogDensity       = visuals.fogDensity;
                ambientIntensity = visuals.ambientIntensity;
                rainIntensity    = visuals.rainIntensity;
                return;
            }

            // Fallback defaults when config is missing or incomplete
            switch (type)
            {
                case WeatherType.Clear:
                    fogDensity = FallbackFogClear; ambientIntensity = FallbackAmbientFull; rainIntensity = 0f;
                    break;
                case WeatherType.Cloudy:
                    fogDensity = FallbackFogCloudy; ambientIntensity = FallbackAmbientLow; rainIntensity = 0f;
                    break;
                case WeatherType.Rain:
                    fogDensity = FallbackFogRain; ambientIntensity = FallbackAmbientLow; rainIntensity = 0.6f;
                    break;
                case WeatherType.Storm:
                    fogDensity = FallbackFogStorm; ambientIntensity = FallbackAmbientLow; rainIntensity = 1f;
                    break;
                default:
                    fogDensity = FallbackFogClear; ambientIntensity = FallbackAmbientFull; rainIntensity = 0f;
                    break;
            }
        }

        private float GetCurrentEmissionRate()
        {
            if (_rainParticles == null) return 0f;
            var emission = _rainParticles.emission;
            return emission.rateOverTime.constant;
        }

        private void SetEmissionRate(float normalizedRate)
        {
            if (_rainParticles == null) return;

            // normalizedRate is 0-1; map to a reasonable particle count range
            const float maxEmissionRate = 500f;

            var emission = _rainParticles.emission;
            emission.rateOverTime = normalizedRate * maxEmissionRate;

            // Also scale main light intensity relative to baseline
            if (_mainLight != null)
            {
                // During heavy rain light dims; normalizedRate == 0 → full brightness
                _mainLight.intensity = _baseLightIntensity * Mathf.Lerp(1f, 0.45f, normalizedRate);
            }
        }
    }
}
