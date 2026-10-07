using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.UI
{
    public class OnboardingHint : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeInDuration = 0.5f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseMinScale = 0.9f;
        [SerializeField] private float _pulseMaxScale = 1.1f;

        private bool _isVisible;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (_canvasGroup == null)
                TryGetComponent(out _canvasGroup);

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isVisible)
                return;

            float t = Mathf.PingPong(Time.time * _pulseSpeed, 1f);
            float scale = Mathf.Lerp(_pulseMinScale, _pulseMaxScale, t);
            transform.localScale = Vector3.one * scale;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _isVisible = false;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / _fadeInDuration);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _isVisible = true;
            _fadeCoroutine = null;
        }

        private IEnumerator FadeOut()
        {
            _isVisible = false;
            // Reset scale so it doesn't freeze mid-pulse when hidden again
            transform.localScale = Vector3.one;

            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeOutDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            _fadeCoroutine = null;
        }
    }
}
