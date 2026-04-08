using System.Collections;
using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.UI
{
    public class OnboardingManager : MonoBehaviour
    {
        [SerializeField] private OnboardingHint _swipeHint;
        [SerializeField] private OnboardingHint _tapHint;
        [SerializeField] private OnboardingHint _longPressHint; // Этап 3

        [SerializeField] private float _initialDelay = 2f;
        [SerializeField] private float _tapHintDelay = 5f;

        private void Start()
        {
            StartCoroutine(ShowSwipeHintAfterDelay());
        }

        private void OnDestroy()
        {
            // Safety unsubscribe in case the object is destroyed before gestures fire
            TouchInputManager.OnMoveInput -= OnFirstSwipe;
            TouchInputManager.OnTap -= OnFirstTap;
        }

        private IEnumerator ShowSwipeHintAfterDelay()
        {
            yield return new WaitForSeconds(_initialDelay);

            if (_swipeHint != null)
            {
                _swipeHint.Show();
                TouchInputManager.OnMoveInput += OnFirstSwipe;
            }
        }

        private void OnFirstSwipe(Vector2 direction, float intensity)
        {
            TouchInputManager.OnMoveInput -= OnFirstSwipe;

            if (_swipeHint != null)
                _swipeHint.Hide();

            StartCoroutine(ShowTapHintAfterDelay());
        }

        private IEnumerator ShowTapHintAfterDelay()
        {
            yield return new WaitForSeconds(_tapHintDelay);

            if (_tapHint != null)
            {
                _tapHint.Show();
                TouchInputManager.OnTap += OnFirstTap;
            }
        }

        private void OnFirstTap(Vector2 screenPos)
        {
            TouchInputManager.OnTap -= OnFirstTap;

            if (_tapHint != null)
                _tapHint.Hide();
        }
    }
}
