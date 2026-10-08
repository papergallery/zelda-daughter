using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Static isometric camera tied to the hero (project-design.md §1 «Камера»). Angles and distance come from the
    /// scene config (scenes/*.json → SceneBuilder). April IsometricCamera.cs, with SmoothDamp instead of a
    /// frame-rate dependent Lerp (docs/april-review.md §3).
    /// </summary>
    public sealed class IsoCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _pitch = 35f;
        [SerializeField] private float _yaw = 45f;
        [SerializeField] private float _distance = 20f;
        [SerializeField] private float _smoothTime = 0.15f;

        private Vector3 _velocity;

        public float Yaw => _yaw;

        public void Configure(Transform target, float pitch, float yaw, float distance, float smoothTime)
        {
            _target = target;
            _pitch = pitch;
            _yaw = yaw;
            _distance = distance;
            _smoothTime = smoothTime;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref _velocity, _smoothTime);
        }

        public void SnapToTarget()
        {
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _velocity = Vector3.zero;
            if (_target != null) transform.position = Desired();
        }

        private Vector3 Desired() => _target.position + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * _distance;
    }
}
