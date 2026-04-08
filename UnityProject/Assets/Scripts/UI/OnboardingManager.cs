using System.Collections;
using UnityEngine;
using ZeldaDaughter.Input;
using ZeldaDaughter.Inventory;

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
            GestureDispatcher.OnMoveInput -= OnFirstSwipe;
            GestureDispatcher.OnTap -= OnFirstTap;
            GestureDispatcher.OnLongPressStart -= OnFirstLongPress;
            PlayerInventory.OnItemAdded -= OnFirstItemPickup;
        }

        private IEnumerator ShowSwipeHintAfterDelay()
        {
            yield return new WaitForSeconds(_initialDelay);

            if (_swipeHint != null)
            {
                _swipeHint.Show();
                GestureDispatcher.OnMoveInput += OnFirstSwipe;
            }
        }

        private void OnFirstSwipe(Vector2 direction, float intensity)
        {
            GestureDispatcher.OnMoveInput -= OnFirstSwipe;

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
                GestureDispatcher.OnTap += OnFirstTap;
            }
        }

        private void OnFirstTap(Vector2 screenPos)
        {
            GestureDispatcher.OnTap -= OnFirstTap;

            if (_tapHint != null)
                _tapHint.Hide();

            PlayerInventory.OnItemAdded += OnFirstItemPickup;
        }

        private void OnFirstItemPickup(ItemData item, int amount)
        {
            PlayerInventory.OnItemAdded -= OnFirstItemPickup;

            if (_longPressHint != null)
            {
                _longPressHint.Show();
                GestureDispatcher.OnLongPressStart += OnFirstLongPress;
            }
        }

        private void OnFirstLongPress()
        {
            GestureDispatcher.OnLongPressStart -= OnFirstLongPress;

            if (_longPressHint != null)
                _longPressHint.Hide();
        }
    }
}
