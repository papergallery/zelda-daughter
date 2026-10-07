using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.UI
{
    public class FadeOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        private static FadeOverlay _instance;

        private void Awake()
        {
            _instance = this;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static Coroutine FadeToBlack(float duration)
        {
            if (_instance == null) return null;
            return _instance.StartCoroutine(_instance.FadeCoroutine(0f, 1f, duration));
        }

        public static Coroutine FadeFromBlack(float duration)
        {
            if (_instance == null) return null;
            return _instance.StartCoroutine(_instance.FadeCoroutine(1f, 0f, duration));
        }

        public static Coroutine FlickerEffect(float duration, int count)
        {
            if (_instance == null) return null;
            return _instance.StartCoroutine(_instance.FlickerCoroutine(duration, count));
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            if (_canvasGroup != null)
                _canvasGroup.alpha = to;
        }

        private IEnumerator FlickerCoroutine(float duration, int count)
        {
            float interval = duration / (count * 2);
            for (int i = 0; i < count; i++)
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0.3f;
                yield return new WaitForSecondsRealtime(interval);
                if (_canvasGroup != null) _canvasGroup.alpha = 1f;
                yield return new WaitForSecondsRealtime(interval);
            }
        }
    }
}
