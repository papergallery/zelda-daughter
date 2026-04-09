using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Connects WeatherSystem events to the elemental state of world objects.
    /// Rain/Storm extinguishes fire on flammable objects and makes terrain muddy.
    /// Clear weather dries mud with a delay.
    /// </summary>
    public class WeatherElementBridge : MonoBehaviour
    {
        [Header("Rain Effects")]
        [SerializeField] private float _muddyApplyRadius = 30f;
        [SerializeField] private float _muddyApplyDelay = 3f;

        [Header("Dry Effects")]
        [SerializeField] private float _muddyRemoveDelay = 60f;

        [Header("Layers")]
        [SerializeField] private LayerMask _groundLayers;

        private static readonly Collider[] _overlapBuffer = new Collider[64];

        private Coroutine _currentWeatherCoroutine;

        private void OnEnable()
        {
            WeatherSystem.OnWeatherChanged += HandleWeatherChanged;
        }

        private void OnDisable()
        {
            WeatherSystem.OnWeatherChanged -= HandleWeatherChanged;
        }

        private void HandleWeatherChanged(WeatherType previous, WeatherType next)
        {
            if (_currentWeatherCoroutine != null)
                StopCoroutine(_currentWeatherCoroutine);

            switch (next)
            {
                case WeatherType.Rain:
                    _currentWeatherCoroutine = StartCoroutine(ApplyRainEffects());
                    break;
                case WeatherType.Storm:
                    _currentWeatherCoroutine = StartCoroutine(ApplyStormEffects());
                    break;
                case WeatherType.Clear:
                case WeatherType.Cloudy:
                    _currentWeatherCoroutine = StartCoroutine(RemoveMuddyDelayed());
                    break;
            }
        }

        private IEnumerator ApplyRainEffects()
        {
            yield return new WaitForSeconds(_muddyApplyDelay);

            ExtinguishFireOnFlammables();
            ApplyMuddyToGround();
        }

        private IEnumerator ApplyStormEffects()
        {
            // Storm acts faster and more aggressively than regular rain
            yield return new WaitForSeconds(_muddyApplyDelay * 0.5f);

            ExtinguishFireOnFlammables();
            ApplyMuddyToGround();
        }

        private IEnumerator RemoveMuddyDelayed()
        {
            yield return new WaitForSeconds(_muddyRemoveDelay);

            RemoveMuddyFromGround();
        }

        private void ExtinguishFireOnFlammables()
        {
            // Find all FlammableTag components in the scene
            var flammables = FindObjectsByType<FlammableTag>(FindObjectsSortMode.None);

            foreach (var flammable in flammables)
            {
                if (flammable.TryGetComponent<ElementState>(out var elementState))
                {
                    if (elementState.HasElement(ElementTag.Fire))
                        elementState.RemoveElement(ElementTag.Fire);
                }
            }
        }

        private void ApplyMuddyToGround()
        {
            Vector3 center = transform.position;
            int count = Physics.OverlapSphereNonAlloc(center, _muddyApplyRadius, _overlapBuffer, _groundLayers);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (!col.CompareTag("Terrain") && !IsOnGroundLayer(col.gameObject)) continue;

                if (col.TryGetComponent<ElementState>(out var elementState))
                    elementState.ApplyElement(ElementTag.Muddy);
            }
        }

        private void RemoveMuddyFromGround()
        {
            Vector3 center = transform.position;
            int count = Physics.OverlapSphereNonAlloc(center, _muddyApplyRadius, _overlapBuffer, _groundLayers);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (!col.CompareTag("Terrain") && !IsOnGroundLayer(col.gameObject)) continue;

                if (col.TryGetComponent<ElementState>(out var elementState))
                {
                    if (elementState.HasElement(ElementTag.Muddy))
                        elementState.RemoveElement(ElementTag.Muddy);
                }
            }
        }

        private bool IsOnGroundLayer(GameObject go)
        {
            return (_groundLayers.value & (1 << go.layer)) != 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _muddyApplyRadius);
        }
#endif
    }
}
