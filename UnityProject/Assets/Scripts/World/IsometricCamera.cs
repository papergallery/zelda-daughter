using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Isometric camera that follows the player with smooth movement.
    /// Orthographic projection, configurable angle and distance.
    /// </summary>
    public class IsometricCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;

        [Header("Camera Settings")]
        [SerializeField] private float _orthographicSize = 8f;
        [SerializeField] private float _followSpeed = 5f;
        [SerializeField] private float _cameraAngle = 35f;
        [SerializeField] private float _cameraDistance = 20f;
        [SerializeField] private float _cameraYRotation = 45f;

        [Header("Bounds (optional)")]
        [SerializeField] private bool _useBounds;
        [SerializeField] private Vector2 _boundsMin = new(-50f, -50f);
        [SerializeField] private Vector2 _boundsMax = new(50f, 50f);

        private Camera _camera;
        private Vector3 _offset;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = gameObject.AddComponent<Camera>();

            _camera.orthographic = true;
            _camera.orthographicSize = _orthographicSize;

            CalculateOffset();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            var targetPosition = _target.position + _offset;

            if (_useBounds)
                targetPosition = ClampToBounds(targetPosition);

            transform.position = Vector3.Lerp(transform.position, targetPosition, _followSpeed * Time.deltaTime);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void CalculateOffset()
        {
            var rotation = Quaternion.Euler(_cameraAngle, _cameraYRotation, 0f);
            _offset = rotation * new Vector3(0f, 0f, -_cameraDistance);
            transform.rotation = rotation;
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, _boundsMin.x, _boundsMax.x);
            position.z = Mathf.Clamp(position.z, _boundsMin.y, _boundsMax.y);
            return position;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CalculateOffset();
            if (_camera != null)
                _camera.orthographicSize = _orthographicSize;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_useBounds) return;
            Gizmos.color = Color.yellow;
            var center = new Vector3(
                (_boundsMin.x + _boundsMax.x) / 2f,
                0f,
                (_boundsMin.y + _boundsMax.y) / 2f
            );
            var size = new Vector3(
                _boundsMax.x - _boundsMin.x,
                0.1f,
                _boundsMax.y - _boundsMin.y
            );
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
