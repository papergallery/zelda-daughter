using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.NPC
{
    public class IconBubble : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _iconImage;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 3f;
        [SerializeField] private Vector3 _offset = new(0f, 2.5f, 0f);

        private Transform _followTarget;
        private Camera _mainCamera;
        private Coroutine _activeCoroutine;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _followTarget = transform.parent;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private void LateUpdate()
        {
            if (_followTarget != null)
                transform.position = _followTarget.position + _offset;

            if (_mainCamera != null)
                transform.rotation = _mainCamera.transform.rotation;
        }

        public void ShowIcon(Sprite icon)
        {
            _iconImage.sprite = icon;

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeIn());
        }

        public void ShowSequence(Sprite[] icons, float interval)
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(SequenceRoutine(icons, interval));
        }

        public void Hide()
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeOut());
        }

        private IEnumerator SequenceRoutine(Sprite[] icons, float interval)
        {
            foreach (Sprite icon in icons)
            {
                _iconImage.sprite = icon;
                yield return FadeIn();
                yield return new WaitForSeconds(interval);
                yield return FadeOut();
            }

            _activeCoroutine = null;
        }

        private IEnumerator FadeIn()
        {
            while (_canvasGroup.alpha < 1f)
            {
                _canvasGroup.alpha += Time.deltaTime * _fadeSpeed;
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            while (_canvasGroup.alpha > 0f)
            {
                _canvasGroup.alpha -= Time.deltaTime * _fadeSpeed;
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }
    }
}
