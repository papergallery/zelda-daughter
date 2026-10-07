using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Attached to bushes/grass. Reacts to Player contact with a squash-stretch
    /// and rotation wobble, driven by coroutine + AnimationCurve (no DOTween).
    /// Requires a SphereCollider with isTrigger=true on the same GameObject.
    /// </summary>
    public class EnvironmentReactor : MonoBehaviour
    {
        [Header("Wobble")]
        [SerializeField] private float _amplitude = 15f;
        [SerializeField] private float _duration = 1f;
        [SerializeField] private float _frequency = 3f;

        [Header("Squash-Stretch")]
        [SerializeField] private float _squashAmount = 0.15f;

        [Header("Curves")]
        [SerializeField] private AnimationCurve _decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private Transform _transform;
        private Vector3 _initialScale;
        private Quaternion _initialRotation;
        private bool _isReacting;

        private void Awake()
        {
            _transform = transform;
            _initialScale = _transform.localScale;
            _initialRotation = _transform.localRotation;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isReacting) return;
            if (!other.CompareTag("Player")) return;

            StartCoroutine(ReactCoroutine());
        }

        private IEnumerator ReactCoroutine()
        {
            _isReacting = true;

            float elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _duration;
                float decay = _decayCurve.Evaluate(t);

                // Rotation oscillation around Z axis
                float angle = Mathf.Sin(elapsed * _frequency * Mathf.PI * 2f) * _amplitude * decay;
                _transform.localRotation = _initialRotation * Quaternion.Euler(0f, 0f, angle);

                // Squash-stretch: compress X/Z, stretch Y at peak, then restore
                float squash = Mathf.Abs(Mathf.Sin(elapsed * _frequency * Mathf.PI * 2f)) * _squashAmount * decay;
                _transform.localScale = new Vector3(
                    _initialScale.x * (1f - squash),
                    _initialScale.y * (1f + squash),
                    _initialScale.z * (1f - squash)
                );

                yield return null;
            }

            _transform.localRotation = _initialRotation;
            _transform.localScale = _initialScale;
            _isReacting = false;
        }
    }
}
