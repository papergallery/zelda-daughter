using UnityEngine;
using UnityEngine.EventSystems;

namespace ZeldaDaughter.Input
{
    public class GestureDispatcher : MonoBehaviour
    {
        public static event System.Action<Vector2, float> OnMoveInput;
        public static event System.Action OnMoveStop;
        public static event System.Action<Vector2> OnTap;
        public static event System.Action OnLongPressStart;
        public static event System.Action OnLongPressEnd;
        public static event System.Action<float> OnLongPressProgress; // 0..1 нормализованный прогресс

        [Header("Settings")]
        [SerializeField] private float _longPressTime = 0.5f;
        [SerializeField] private float _longPressMaxDrift = 20f;
        [SerializeField] private float _tapMaxDuration = 0.3f;
        [SerializeField] private float _maxSwipeDistance = 80f;

        private enum GestureState { None, PotentialLongPress, Swiping, LongPressActive }

        private GestureState _state = GestureState.None;
        private Vector2 _touchStart;
        private float _touchStartTime;
        private bool _longPressOnCharacter;
        private Camera _mainCamera;
        private Camera MainCamera => _mainCamera != null ? _mainCamera : (_mainCamera = Camera.main);

        private int _diagCount = 0;
        private void Update()
        {
            // Diagnostic: raw Debug.Log to verify GestureDispatcher runs
            _diagCount++;
            if (_diagCount == 120) // every 2 sec at 60fps
            {
                int tc = UnityEngine.Input.touchCount;
                var mp = UnityEngine.Input.mousePosition;
                bool mb = UnityEngine.Input.GetMouseButton(0);
                UnityEngine.Debug.Log($"[GD:Diag] tc={tc} mb={mb} mpos=({mp.x:F0},{mp.y:F0})");
                _diagCount = 0;
            }

            // Touch first (real device), fallback to mouse (editor + emulator)
            if (UnityEngine.Input.touchCount > 0)
                HandleTouchInput();
            else
                HandleMouseInput();
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
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            UnityEngine.Debug.Log($"[GD:Down] pos=({screenPos.x:F0},{screenPos.y:F0}) overUI={overUI}");
            if (overUI)
                return;

            _touchStart = screenPos;
            _touchStartTime = Time.unscaledTime;
            _longPressOnCharacter = IsPointerOnCharacter(screenPos);
            _state = _longPressOnCharacter ? GestureState.PotentialLongPress : GestureState.None;
        }

        private void OnPointerHeld(Vector2 screenPos)
        {
            float drift = Vector2.Distance(screenPos, _touchStart);
            float elapsed = Time.unscaledTime - _touchStartTime;

            switch (_state)
            {
                case GestureState.PotentialLongPress:
                    if (drift > _longPressMaxDrift)
                    {
                        // Палец сдвинулся — это свайп
                        _state = GestureState.Swiping;
                        EmitSwipe(screenPos);
                        return;
                    }
                    // Ещё держит — отправляем прогресс
                    float progress = Mathf.Clamp01(elapsed / _longPressTime);
                    OnLongPressProgress?.Invoke(progress);
                    if (elapsed >= _longPressTime)
                    {
                        _state = GestureState.LongPressActive;
                        OnLongPressStart?.Invoke();
                    }
                    break;

                case GestureState.LongPressActive:
                    // Лонг-пресс активен, ничего не делаем (меню управляет)
                    break;

                case GestureState.Swiping:
                    EmitSwipe(screenPos);
                    break;

                case GestureState.None:
                    // Не на персонаже — сразу свайп при смещении
                    if (drift > _longPressMaxDrift)
                    {
                        _state = GestureState.Swiping;
                        EmitSwipe(screenPos);
                    }
                    // Emulator fallback: если держим палец (активное касание),
                    // свайпим в направлении от центра экрана к точке касания
                    else if (_touchStartTime > 0.1f && elapsed > 0.05f && elapsed < 5f && drift < 5f)
                    {
                        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                        Vector2 dirFromCenter = (screenPos - screenCenter).normalized;
                        float distFromCenter = Vector2.Distance(screenPos, screenCenter);
                        if (distFromCenter > 50f) // достаточно далеко от центра
                        {
                            float intensity = Mathf.Clamp01(distFromCenter / (Screen.width * 0.3f));
                            OnMoveInput?.Invoke(dirFromCenter, intensity);
                        }
                    }
                    break;
            }
        }

        private void OnPointerUp(Vector2 screenPos)
        {
            switch (_state)
            {
                case GestureState.LongPressActive:
                    OnLongPressEnd?.Invoke();
                    break;

                case GestureState.Swiping:
                    OnMoveStop?.Invoke();
                    break;

                case GestureState.PotentialLongPress:
                    // Не дождался лонг-пресса и не свайпнул — сброс прогресса
                    OnLongPressProgress?.Invoke(0f);
                    float elapsed = Time.unscaledTime - _touchStartTime;
                    if (elapsed <= _tapMaxDuration)
                        OnTap?.Invoke(screenPos);
                    else
                        OnMoveStop?.Invoke();
                    break;

                case GestureState.None:
                    float el = Time.unscaledTime - _touchStartTime;
                    if (el <= _tapMaxDuration)
                        OnTap?.Invoke(screenPos);
                    else
                        OnMoveStop?.Invoke();
                    break;
            }

            _state = GestureState.None;
        }

        private void EmitSwipe(Vector2 screenPos)
        {
            Vector2 delta = screenPos - _touchStart;
            Vector2 direction = delta.normalized;
            float intensity = Mathf.Clamp01(delta.magnitude / _maxSwipeDistance);
            UnityEngine.Debug.Log($"[GD:Swipe] dir=({direction.x:F2},{direction.y:F2}) intensity={intensity:F2} drift={delta.magnitude:F0}");
            OnMoveInput?.Invoke(direction, intensity);
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
