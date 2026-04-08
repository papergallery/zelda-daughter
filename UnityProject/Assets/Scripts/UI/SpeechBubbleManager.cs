using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class SpeechBubbleManager : MonoBehaviour
    {
        [SerializeField] private SpeechBubble _speechBubble;
        [SerializeField] private float _defaultDuration = 3f;
        [SerializeField] private float _queueDelay = 0.5f;

        private readonly Queue<(string text, float duration)> _queue = new();
        private Coroutine _processCoroutine;

        public static SpeechBubbleManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            PlayerInventory.OnItemAdded += HandleItemAdded;
        }

        private void OnDisable()
        {
            PlayerInventory.OnItemAdded -= HandleItemAdded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Показать реплику над персонажем. duration=0 использует DefaultDuration.
        /// Если пузырь занят — встать в очередь.
        /// </summary>
        public static void Say(string text, float duration = 0f)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[SpeechBubbleManager] Instance not found. Make sure SpeechBubbleManager is in the scene.");
                return;
            }
            Instance.Enqueue(text, duration);
        }

        private void Enqueue(string text, float duration)
        {
            float resolvedDuration = duration <= 0f ? _defaultDuration : duration;

            if (_speechBubble.IsShowing || _queue.Count > 0)
            {
                _queue.Enqueue((text, resolvedDuration));
            }
            else
            {
                _speechBubble.Show(text, resolvedDuration);
                _processCoroutine = StartCoroutine(ProcessQueue(resolvedDuration));
            }
        }

        private IEnumerator ProcessQueue(float currentDuration)
        {
            yield return new WaitForSeconds(currentDuration + _queueDelay);

            while (_queue.Count > 0)
            {
                var (text, duration) = _queue.Dequeue();
                _speechBubble.Show(text, duration);
                yield return new WaitForSeconds(duration + _queueDelay);
            }

            _processCoroutine = null;
        }

        private void HandleItemAdded(ItemData item, int amount)
        {
            if (item == null)
                return;

            string line = item.PickupLine;
            if (!string.IsNullOrWhiteSpace(line))
                Enqueue(line, _defaultDuration);
        }
    }
}
