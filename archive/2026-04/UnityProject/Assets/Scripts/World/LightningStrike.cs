using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Spawns lightning strikes at random positions around the player during Storm weather.
    /// Applies Electrified element to nearby objects and deals increased damage in water.
    /// </summary>
    public class LightningStrike : MonoBehaviour
    {
        [SerializeField] private float _minInterval = 8f;
        [SerializeField] private float _maxInterval = 20f;
        [SerializeField] private float _strikeRadius = 30f;
        [SerializeField] private float _damageRadius = 3f;
        [SerializeField] private float _damage = 40f;
        [SerializeField] private float _waterDamageRadiusMultiplier = 2.5f;

        [SerializeField] private ParticleSystem _lightningVFX;
        [SerializeField] private AudioClip _thunderSound;
        [SerializeField] private Light _flashLight;

        // Flash parameters
        private const float FlashDuration = 0.1f;
        private const float FlashIntensity = 8f;

        private Transform _player;
        private AudioSource _audioSource;
        private Coroutine _strikeLoopCoroutine;

        private bool _isActive;

        // Reusable overlap buffer — avoids per-strike alloc
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private void Awake()
        {
            if (!TryGetComponent(out _audioSource))
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.spatialBlend = 0f; // 2D — thunder is heard everywhere
            _audioSource.playOnAwake  = false;

            if (_flashLight != null)
                _flashLight.enabled = false;
        }

        private void Start()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                _player = playerObj.transform;

            // Sync with current weather on scene start
            if (WeatherSystem.Instance != null)
                SetActive(WeatherSystem.Instance.CurrentWeather == WeatherType.Storm);
        }

        private void OnEnable()
        {
            WeatherSystem.OnWeatherChanged += HandleWeatherChanged;
        }

        private void OnDisable()
        {
            WeatherSystem.OnWeatherChanged -= HandleWeatherChanged;
            StopStrikeLoop();
        }

        private void HandleWeatherChanged(WeatherType previous, WeatherType next)
        {
            SetActive(next == WeatherType.Storm);
        }

        private void SetActive(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;

            if (_isActive)
                _strikeLoopCoroutine = StartCoroutine(StrikeLoop());
            else
                StopStrikeLoop();
        }

        private void StopStrikeLoop()
        {
            if (_strikeLoopCoroutine != null)
            {
                StopCoroutine(_strikeLoopCoroutine);
                _strikeLoopCoroutine = null;
            }
        }

        private IEnumerator StrikeLoop()
        {
            while (_isActive)
            {
                float interval = Random.Range(_minInterval, _maxInterval);
                yield return new WaitForSeconds(interval);

                if (_player == null) continue;

                if (TryGetStrikePoint(out Vector3 strikePoint))
                    yield return StartCoroutine(ExecuteStrike(strikePoint));
            }
        }

        private bool TryGetStrikePoint(out Vector3 point)
        {
            // Random direction in the horizontal disc around the player
            Vector2 randomDisc = Random.insideUnitCircle * _strikeRadius;
            Vector3 candidate  = _player.position + new Vector3(randomDisc.x, 50f, randomDisc.y);

            // Raycast downward to land on terrain/geometry
            if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 100f))
            {
                point = hit.point;
                return true;
            }

            // Fallback: flat projection at player height
            point = _player.position + new Vector3(randomDisc.x, 0f, randomDisc.y);
            return true;
        }

        private IEnumerator ExecuteStrike(Vector3 strikePoint)
        {
            // Visual flash
            yield return StartCoroutine(FlashLight());

            // Spawn VFX at strike point
            if (_lightningVFX != null)
            {
                _lightningVFX.transform.position = strikePoint;
                _lightningVFX.Play();
            }

            // Play thunder sound (slight random pitch for variety)
            if (_thunderSound != null)
            {
                _audioSource.pitch = Random.Range(0.85f, 1.15f);
                _audioSource.PlayOneShot(_thunderSound);
            }

            // Determine effective damage radius (increased if strike lands in water)
            float effectiveRadius = _damageRadius;
            if (IsInWater(strikePoint))
                effectiveRadius *= _waterDamageRadiusMultiplier;

            ApplyElectrifiedInRadius(strikePoint, effectiveRadius);
        }

        private IEnumerator FlashLight()
        {
            if (_flashLight == null) yield break;

            _flashLight.intensity = FlashIntensity;
            _flashLight.enabled   = true;

            yield return new WaitForSeconds(FlashDuration);

            _flashLight.enabled = false;
        }

        private void ApplyElectrifiedInRadius(Vector3 center, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i].TryGetComponent<ElementState>(out var elementState))
                    elementState.ApplyElement(ElementTag.Electrified);
            }
        }

        /// <summary>
        /// Returns true if there is a WaterZone trigger overlapping the strike point.
        /// Falls back to layer-based check ("Water" layer) when no WaterZone component is found.
        /// </summary>
        private bool IsInWater(Vector3 point)
        {
            int waterLayer = LayerMask.NameToLayer("Water");
            int layerMask  = waterLayer >= 0 ? (1 << waterLayer) : 0;

            // Small probe sphere to detect water colliders
            int count = Physics.OverlapSphereNonAlloc(point, 0.5f, _overlapBuffer,
                layerMask, QueryTriggerInteraction.Collide);

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_overlapBuffer[i].TryGetComponent<WaterZone>(out _))
                        return true;

                    // Layer match is enough if WaterZone component is absent
                    if (waterLayer >= 0 && _overlapBuffer[i].gameObject.layer == waterLayer)
                        return true;
                }
            }

            // Broad fallback: check all triggers at point regardless of layer
            count = Physics.OverlapSphereNonAlloc(point, 0.5f, _overlapBuffer,
                ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i].TryGetComponent<WaterZone>(out _))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_player == null) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(_player.position, _strikeRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(_player.position, _damageRadius);
        }
#endif
    }
}
