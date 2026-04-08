using UnityEngine;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Размещается на Player. Определяет вход/выход из RestZone (костёр, привал).
    /// </summary>
    public class RestZoneDetector : MonoBehaviour
    {
        private PlayerHealthState _healthState;

        private void Awake()
        {
            TryGetComponent(out _healthState);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("RestZone")) return;

            if (_healthState != null)
                _healthState.SetResting(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("RestZone")) return;

            if (_healthState != null)
                _healthState.SetResting(false);
        }
    }
}
