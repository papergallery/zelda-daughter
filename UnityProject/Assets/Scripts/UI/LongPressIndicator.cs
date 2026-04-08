using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.UI
{
    public class LongPressIndicator : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Vector3 _offset = new(0f, 2.5f, 0f);
        [SerializeField] private float _fadeSpeed = 8f;

        private Camera _mainCamera;
        private bool _isShowing;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            if (_fillImage != null)
                _fillImage.fillAmount = 0f;
        }

        private void OnEnable()
        {
            GestureDispatcher.OnLongPressProgress += HandleProgress;
            GestureDispatcher.OnLongPressStart += HandleLongPressStart;
            GestureDispatcher.OnMoveInput += HandleSwipe;
        }

        private void OnDisable()
        {
            GestureDispatcher.OnLongPressProgress -= HandleProgress;
            GestureDispatcher.OnLongPressStart -= HandleLongPressStart;
            GestureDispatcher.OnMoveInput -= HandleSwipe;
        }

        private void LateUpdate()
        {
            if (_followTarget == null || _mainCamera == null)
                return;

            // Следуем за персонажем в screen space
            var worldPos = _followTarget.position + _offset;
            transform.position = _mainCamera.WorldToScreenPoint(worldPos);

            // Fade
            if (_canvasGroup != null)
            {
                float target = _isShowing ? 1f : 0f;
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, _fadeSpeed * Time.unscaledDeltaTime);
            }
        }

        private void HandleProgress(float normalized)
        {
            if (normalized > 0f && normalized < 1f)
            {
                _isShowing = true;
                if (_fillImage != null)
                    _fillImage.fillAmount = normalized;
            }
            else
            {
                Hide();
            }
        }

        private void HandleLongPressStart()
        {
            Hide();
        }

        private void HandleSwipe(Vector2 dir, float intensity)
        {
            Hide();
        }

        private void Hide()
        {
            _isShowing = false;
            if (_fillImage != null)
                _fillImage.fillAmount = 0f;
        }
    }
}
