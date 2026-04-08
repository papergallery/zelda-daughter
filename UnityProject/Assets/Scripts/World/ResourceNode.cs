using System.Collections;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Save;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    public class ResourceNode : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private ResourceNodeData _data;
        [SerializeField] private string _saveId;
        [SerializeField] private float _interactionRange = 1.5f;

        private int _currentHP;
        private bool _depleted;
        private Transform _cachedTransform;
        private Vector3 _originalScale;
        private Coroutine _wobbleCoroutine;

        public event System.Action<ResourceNode> OnDepleted;
        public event System.Action<ResourceNode, int> OnHit;

        public ResourceNodeData Data => _data;

        // IInteractable
        public string InteractionPrompt => _data != null ? "Добыть" : "";
        public Transform InteractionPoint => transform;
        public float InteractionRange => _interactionRange;
        public bool CanInteract() => !_depleted;
        public InteractionType Type => InteractionType.Resource;

        // ISaveable
        public string SaveId => _saveId;

        private void Awake()
        {
            _cachedTransform = transform;
            _originalScale = _cachedTransform.localScale;

            if (_data != null)
                _currentHP = _data.MaxHitPoints;
        }

        public void Interact(GameObject actor)
        {
            if (_depleted || _data == null)
                return;

            // Анимация удара на акторе
            if (actor.TryGetComponent<Animator>(out var animator))
                animator.SetTrigger("Attack");

            _currentHP--;
            OnHit?.Invoke(this, _currentHP);

            // Визуальная реакция — покачивание
            if (_wobbleCoroutine != null)
                StopCoroutine(_wobbleCoroutine);
            _wobbleCoroutine = StartCoroutine(WobbleCoroutine());

            // Визуальная деградация — scale пропорционально HP
            float hpRatio = (float)_currentHP / _data.MaxHitPoints;
            _cachedTransform.localScale = _originalScale * (0.5f + 0.5f * hpRatio);

            if (_currentHP <= 0)
                HandleDepleted(actor);
        }

        private void HandleDepleted(GameObject actor)
        {
            _depleted = true;

            // Дропнуть предметы в инвентарь актора
            if (_data.Drops != null && actor.TryGetComponent<PlayerInventory>(out var inventory))
            {
                foreach (var drop in _data.Drops)
                {
                    if (drop.item == null)
                        continue;

                    int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                    if (amount > 0)
                        inventory.AddItem(drop.item, amount);
                    // SpeechBubbleManager подхватит реплику автоматически через PlayerInventory.OnItemAdded
                }
            }

            OnDepleted?.Invoke(this);

            // Деактивировать визуал — рендереры, но не GO (чтобы респаунер мог вернуть)
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }

        public void Respawn()
        {
            _currentHP = _data.MaxHitPoints;
            _depleted = false;
            _cachedTransform.localScale = _originalScale;

            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = true;

            gameObject.SetActive(true);
        }

        private IEnumerator WobbleCoroutine()
        {
            float elapsed = 0f;
            float duration = 0.5f;
            float amplitude = 10f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float decay = 1f - t;
                float angle = Mathf.Sin(elapsed * 20f) * amplitude * decay;
                _cachedTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _cachedTransform.localRotation = Quaternion.identity;
            _wobbleCoroutine = null;
        }

        // ISaveable

        public object CaptureState()
        {
            return new ResourceSaveData
            {
                currentHP = _currentHP,
                depleted = _depleted
            };
        }

        public void RestoreState(object state)
        {
            if (state is not ResourceSaveData data)
                return;

            _currentHP = data.currentHP;
            _depleted = data.depleted;

            if (_depleted)
            {
                _cachedTransform.localScale = _originalScale * 0.5f;
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
            }
            else
            {
                float hpRatio = _data != null ? (float)_currentHP / _data.MaxHitPoints : 1f;
                _cachedTransform.localScale = _originalScale * (0.5f + 0.5f * hpRatio);
            }
        }

        [System.Serializable]
        public class ResourceSaveData
        {
            public int currentHP;
            public bool depleted;
        }
    }
}
