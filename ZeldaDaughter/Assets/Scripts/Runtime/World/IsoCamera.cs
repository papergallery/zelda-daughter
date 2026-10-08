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
        private Vector3 _base;                 // where the camera is without the shake (SmoothDamp works on this one)
        private bool _haveBase;
        private Camera _cam;
        private float _baseOrtho, _widen = 1f, _appliedOrtho;
        private Vector2 _shake;                // metres on the camera's right / up axes (D-26)

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

        /// <summary>The ortho size the scene was built with (before the widening of D-26); 0 for a perspective camera.</summary>
        public float BaseOrtho { get { TrackOrtho(); return _baseOrtho; } }
        /// <summary>The camera's ortho size now as a multiple of the base one (1 = as built; D-26 widens it by a fifth at night and in a fight).</summary>
        public float Widen => _widen;

        /// <summary>D-26: the shake of the frame, metres along the camera's right and up axes. Zero = none.</summary>
        public void SetShake(float right, float up) => _shake = new Vector2(right, up);

        /// <summary>D-26: ortho size = base × factor (1…1.2). A perspective camera is left alone.</summary>
        public void SetWiden(float factor)
        {
            _widen = Mathf.Max(0.5f, factor);
            TrackOrtho();
            if (_cam != null && _cam.orthographic && _baseOrtho > 0f)
            {
                float want = _baseOrtho * _widen;
                if (Mathf.Approximately(want, _cam.orthographicSize)) { _appliedOrtho = _cam.orthographicSize; return; }   // not every frame: a new size rebuilds the projection
                _appliedOrtho = want;
                _cam.orthographicSize = want;
            }
        }

        // the scene builder (or a test) may set the size itself: what is there that we did not put there is the new base
        private void TrackOrtho()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null || !_cam.orthographic) return;
            if (_baseOrtho <= 0f || !Mathf.Approximately(_cam.orthographicSize, _appliedOrtho))
            {
                _baseOrtho = _cam.orthographicSize / _widen;
                _appliedOrtho = _cam.orthographicSize;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            if (!_haveBase) { _base = transform.position; _haveBase = true; }
            _base = Vector3.SmoothDamp(_base, Desired(), ref _velocity, _smoothTime);
            transform.position = _base + ShakeOffset();
        }

        private Vector3 ShakeOffset() => _shake == Vector2.zero ? Vector3.zero : transform.right * _shake.x + transform.up * _shake.y;

        public void SnapToTarget()
        {
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _velocity = Vector3.zero;
            if (_target != null) { _base = Desired(); _haveBase = true; transform.position = _base + ShakeOffset(); }
        }

        private Vector3 Desired() => _target.position + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * _distance;
    }
}
