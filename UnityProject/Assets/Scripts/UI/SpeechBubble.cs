using System.Collections;
using TMPro;
using UnityEngine;

namespace ZeldaDaughter.UI
{
    public class SpeechBubble : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private Vector3 _offset = new(0f, 2.5f, 0f);

        private Transform _followTarget;
        private Coroutine _activeCoroutine;
        private Camera _mainCamera;

        public bool IsShowing { get; private set; }

        private void Awake()
        {
            _mainCamera = Camera.main;
            _followTarget = transform.parent;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            IsShowing = false;
        }

        private void LateUpdate()
        {
            if (_followTarget != null)
                transform.position = _followTarget.position + _offset;

            if (_mainCamera != null)
                transform.rotation = _mainCamera.transform.rotation;
        }

        public void Show(string text, float duration)
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _text.text = text;
            _activeCoroutine = StartCoroutine(ShowRoutine(duration));
        }

        public void Hide()
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeOut());
        }

        private IEnumerator ShowRoutine(float duration)
        {
            yield return FadeIn();

            IsShowing = true;
            yield return new WaitForSeconds(duration);

            yield return FadeOut();
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeOutDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            IsShowing = false;
            _activeCoroutine = null;
        }
    }
}
