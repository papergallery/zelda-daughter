using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZeldaDaughter.NPC
{
    public class NPCSpeechBubble : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _textField;
        [SerializeField] private Image _iconImage;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 5f;
        [SerializeField] private float _typewriterSpeed = 30f;

        private Coroutine _currentRoutine;

        public bool IsShowing { get; private set; }

        private void Awake()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            SetTextVisible(false);
            SetIconVisible(false);
        }

        public void ShowText(string text)
        {
            StopCurrentRoutine();
            SetIconVisible(false);
            SetTextVisible(true);
            _currentRoutine = StartCoroutine(TypewriterRoutine(text));
        }

        public void ShowIcon(Sprite icon)
        {
            StopCurrentRoutine();
            SetTextVisible(false);
            _iconImage.sprite = icon;
            SetIconVisible(true);
            _currentRoutine = StartCoroutine(FadeRoutine(1f));
        }

        public void ShowIconSequence(Sprite[] icons, float interval)
        {
            if (icons == null || icons.Length == 0) return;
            StopCurrentRoutine();
            SetTextVisible(false);
            SetIconVisible(true);
            _currentRoutine = StartCoroutine(IconSequenceRoutine(icons, interval));
        }

        public void Hide()
        {
            StopCurrentRoutine();
            _currentRoutine = StartCoroutine(FadeRoutine(0f));
        }

        private IEnumerator TypewriterRoutine(string text)
        {
            _textField.text = string.Empty;
            IsShowing = true;

            // Плавное появление пузыря
            yield return FadeRoutine(1f);

            float interval = _typewriterSpeed > 0f ? 1f / _typewriterSpeed : 0f;

            for (int i = 0; i < text.Length; i++)
            {
                _textField.text = text[..( i + 1)];
                yield return new WaitForSeconds(interval);
            }

            _currentRoutine = null;
        }

        private IEnumerator IconSequenceRoutine(Sprite[] icons, float interval)
        {
            IsShowing = true;

            foreach (Sprite icon in icons)
            {
                _iconImage.sprite = icon;
                yield return FadeRoutine(1f);
                yield return new WaitForSeconds(interval);
                yield return FadeRoutine(0f);
            }

            IsShowing = false;
            _currentRoutine = null;
        }

        private IEnumerator FadeRoutine(float target)
        {
            if (_canvasGroup == null) yield break;

            if (target > 0f)
            {
                _canvasGroup.blocksRaycasts = true;
                IsShowing = true;
            }

            while (!Mathf.Approximately(_canvasGroup.alpha, target))
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, Time.deltaTime * _fadeSpeed);
                yield return null;
            }

            _canvasGroup.alpha = target;

            if (target <= 0f)
            {
                _canvasGroup.blocksRaycasts = false;
                IsShowing = false;
            }
        }

        private void StopCurrentRoutine()
        {
            if (_currentRoutine != null)
            {
                StopCoroutine(_currentRoutine);
                _currentRoutine = null;
            }
        }

        private void SetTextVisible(bool visible)
        {
            if (_textField != null)
                _textField.gameObject.SetActive(visible);
        }

        private void SetIconVisible(bool visible)
        {
            if (_iconImage != null)
                _iconImage.gameObject.SetActive(visible);
        }
    }
}
