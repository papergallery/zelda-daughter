using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ZeldaDaughter.NPC
{
    public class IconResponsePanel : MonoBehaviour
    {
        [SerializeField] private Button[] _buttons;
        [SerializeField] private Image[] _buttonIcons;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 3f;

        public event System.Action<int> OnResponseSelected;

        private bool _waitingForResponse;
        private Coroutine _activeCoroutine;

        private void Awake()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = false;

            foreach (Button button in _buttons)
                button.gameObject.SetActive(false);
        }

        public void Show(Sprite[] options)
        {
            UnsubscribeButtons();

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (i < options.Length)
                {
                    _buttons[i].gameObject.SetActive(true);
                    if (i < _buttonIcons.Length)
                        _buttonIcons[i].sprite = options[i];

                    int capturedIndex = i;
                    _buttons[i].onClick.AddListener(() => OnButtonClicked(capturedIndex));
                }
                else
                {
                    _buttons[i].gameObject.SetActive(false);
                }
            }

            _waitingForResponse = true;

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            _waitingForResponse = false;
            UnsubscribeButtons();

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(FadeOut());
        }

        private void OnButtonClicked(int index)
        {
            OnResponseSelected?.Invoke(index);
            Hide();
        }

        private void UnsubscribeButtons()
        {
            foreach (Button button in _buttons)
                button.onClick.RemoveAllListeners();
        }

        private IEnumerator FadeIn()
        {
            _canvasGroup.interactable = true;
            while (_canvasGroup.alpha < 1f)
            {
                _canvasGroup.alpha += Time.deltaTime * _fadeSpeed;
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _activeCoroutine = null;
        }

        private IEnumerator FadeOut()
        {
            _canvasGroup.interactable = false;
            while (_canvasGroup.alpha > 0f)
            {
                _canvasGroup.alpha -= Time.deltaTime * _fadeSpeed;
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _activeCoroutine = null;
        }
    }
}
