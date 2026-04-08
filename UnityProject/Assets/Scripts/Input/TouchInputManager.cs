using UnityEngine;
using UnityEngine.EventSystems;

namespace ZeldaDaughter.Input
{
    /// <summary>
    /// Processes touch/mouse input and outputs a movement direction + intensity.
    /// Swipe = movement. Tap and long-press are detected but delegated to other systems.
    /// </summary>
    public class TouchInputManager : MonoBehaviour
    {
        public static event System.Action<Vector2, float> OnMoveInput;
        public static event System.Action OnMoveStop;
        public static event System.Action<Vector2> OnTap;
        public static event System.Action OnLongPressStart;
        public static event System.Action OnLongPressEnd;

        [Header("Settings")]
        [SerializeField] private float _longPressTime = 0.5f;
        [SerializeField] private float _longPressMaxDrift = 20f;
        [SerializeField] private float _tapMaxDuration = 0.3f;
        [SerializeField] private float _maxSwipeDistance = 80f;

        private Vector2 _touchStart;
        private float _touchStartTime;
        private bool _isDragging;
        private bool _isLongPress;
        private bool _longPressOnCharacter;
        private Camera _mainCamera;
        private Camera MainCamera => _mainCamera != null ? _mainCamera : (_mainCamera = Camera.main);

        private void Update()
        {
            #if UNITY_EDITOR
            HandleMouseInput();
            #else
            HandleTouchInput();
            #endif
        }

        private void HandleTouchInput()
        {
            if (UnityEngine.Input.touchCount == 0)
                return;

            var touch = UnityEngine.Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        return;
                    OnPointerDown(touch.position);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    OnPointerHeld(touch.position);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnPointerUp(touch.position);
                    break;
            }
        }

        private void HandleMouseInput()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
                OnPointerDown(UnityEngine.Input.mousePosition);
            else if (UnityEngine.Input.GetMouseButton(0))
                OnPointerHeld(UnityEngine.Input.mousePosition);
            else if (UnityEngine.Input.GetMouseButtonUp(0))
                OnPointerUp(UnityEngine.Input.mousePosition);
        }

        private void OnPointerDown(Vector2 screenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            _touchStart = screenPos;
            _touchStartTime = Time.unscaledTime;
            _isDragging = false;
            _isLongPress = false;
            _longPressOnCharacter = IsPointerOnCharacter(screenPos);
        }

        private void OnPointerHeld(Vector2 screenPos)
        {
            float drift = Vector2.Distance(screenPos, _touchStart);
            float elapsed = Time.unscaledTime - _touchStartTime;

            // Long-press detection: only on character, held long enough, minimal drift
            if (!_isLongPress && !_isDragging
                && _longPressOnCharacter
                && elapsed >= _longPressTime
                && drift <= _longPressMaxDrift)
            {
                _isLongPress = true;
                OnLongPressStart?.Invoke();
                return;
            }

            if (_isLongPress)
                return;

            // If finger moved enough, it's a swipe (movement)
            if (drift > _longPressMaxDrift || _isDragging)
            {
                _isDragging = true;
                Vector2 delta = screenPos - _touchStart;
                Vector2 direction = delta.normalized;
                float intensity = Mathf.Clamp01(delta.magnitude / _maxSwipeDistance);
                OnMoveInput?.Invoke(direction, intensity);
            }
        }

        private void OnPointerUp(Vector2 screenPos)
        {
            if (_isLongPress)
            {
                _isLongPress = false;
                OnLongPressEnd?.Invoke();
                return;
            }

            if (_isDragging)
            {
                _isDragging = false;
                OnMoveStop?.Invoke();
                return;
            }

            // Short touch without drag = tap
            float elapsed = Time.unscaledTime - _touchStartTime;
            if (elapsed <= _tapMaxDuration)
            {
                OnTap?.Invoke(screenPos);
            }
            else
            {
                OnMoveStop?.Invoke();
            }
        }

        private bool IsPointerOnCharacter(Vector2 screenPos)
        {
            var ray = MainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                return hit.collider.CompareTag("Player");
            }
            return false;
        }
    }
}
