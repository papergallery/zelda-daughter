using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.UI
{
    public class DialoguePanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject _optionButtonPrefab;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 5f;

        public event Action<int> OnOptionSelected;

        private readonly List<GameObject> _spawnedButtons = new();
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }

        /// <summary>
        /// Показывает варианты ответов игрока.
        /// В iconMode показывает иконки вместо текста.
        /// textProcessor применяется к тексту опции (например, ProcessText языковой системы).
        /// </summary>
        public void Show(DialogueOption[] options, bool iconMode, Func<string, string> textProcessor)
        {
            ClearButtons();

            for (int i = 0; i < options.Length; i++)
            {
                int capturedIndex = i;
                DialogueOption option = options[i];

                GameObject buttonObj = Instantiate(_optionButtonPrefab, _container);
                _spawnedButtons.Add(buttonObj);

                // Текст кнопки
                var labelField = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                var iconImage = buttonObj.GetComponentInChildren<Image>();

                if (iconMode && option.icon != null)
                {
                    if (labelField != null) labelField.gameObject.SetActive(false);
                    if (iconImage != null)
                    {
                        iconImage.gameObject.SetActive(true);
                        iconImage.sprite = option.icon;
                    }
                }
                else
                {
                    if (iconImage != null) iconImage.gameObject.SetActive(false);
                    if (labelField != null)
                    {
                        labelField.gameObject.SetActive(true);
                        string displayText = textProcessor != null
                            ? textProcessor(option.text)
                            : option.text;
                        labelField.text = displayText;
                    }
                }

                if (buttonObj.TryGetComponent<Button>(out var button))
                    button.onClick.AddListener(() => HandleOptionClicked(capturedIndex));
            }

            FadeIn();
        }

        public void Hide()
        {
            FadeOut(destroyAfter: true);
        }

        /// <summary>
        /// Показывает единственную кнопку "..." для завершения диалога без опций.
        /// </summary>
        public void ShowEndButton()
        {
            ClearButtons();

            GameObject buttonObj = Instantiate(_optionButtonPrefab, _container);
            _spawnedButtons.Add(buttonObj);

            var labelField = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            var iconImage = buttonObj.GetComponentInChildren<Image>();

            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (labelField != null)
            {
                labelField.gameObject.SetActive(true);
                labelField.text = "...";
            }

            if (buttonObj.TryGetComponent<Button>(out var button))
                button.onClick.AddListener(() => HandleOptionClicked(-1));

            FadeIn();
        }

        private void HandleOptionClicked(int index)
        {
            OnOptionSelected?.Invoke(index);
        }

        private void FadeIn()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(1f, false));
        }

        private void FadeOut(bool destroyAfter)
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(0f, destroyAfter));
        }

        private IEnumerator FadeRoutine(float target, bool clearOnComplete)
        {
            if (_canvasGroup == null) yield break;

            if (target > 0f)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
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
                _canvasGroup.interactable = false;

                if (clearOnComplete)
                    ClearButtons();
            }

            _fadeCoroutine = null;
        }

        private void ClearButtons()
        {
            foreach (GameObject btn in _spawnedButtons)
            {
                if (btn != null)
                    Destroy(btn);
            }
            _spawnedButtons.Clear();
        }
    }
}
