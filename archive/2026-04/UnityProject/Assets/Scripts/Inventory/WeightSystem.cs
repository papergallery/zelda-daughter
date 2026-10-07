using UnityEngine;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Inventory
{
    public class WeightSystem : MonoBehaviour
    {
        [SerializeField] private InventoryConfig _config;
        [SerializeField] private CharacterMovement _characterMovement;
        [SerializeField] private float _replyInterval = 30f;

        private PlayerInventory _inventory;
        private float _lastReplyTime;
        private bool _isOverloaded;

        /// <summary>Fires when overload state changes. True = became overloaded, false = no longer overloaded.</summary>
        public static event System.Action<bool> OnOverloadChanged;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null)
                _inventory = GetComponentInParent<PlayerInventory>();
        }

        private void OnEnable()
        {
            PlayerInventory.OnWeightChanged += HandleWeightChanged;
        }

        private void OnDisable()
        {
            PlayerInventory.OnWeightChanged -= HandleWeightChanged;
        }

        private void HandleWeightChanged(float totalWeight)
        {
            if (_config == null || _characterMovement == null)
                return;

            float capacity = _config.BaseWeightCapacity;
            if (capacity <= 0f)
                return;

            float ratio = totalWeight / capacity;

            bool nowOverloaded = ratio > _config.OverloadThreshold;

            if (!nowOverloaded)
            {
                _characterMovement.SetWeightSpeedMultiplier(1f);

                if (_isOverloaded)
                {
                    _isOverloaded = false;
                    OnOverloadChanged?.Invoke(false);
                }
                return;
            }

            // Перегружен: линейная интерполяция от порога до 100%
            float overloadProgress = Mathf.InverseLerp(_config.OverloadThreshold, 1f, ratio);
            float speedMul = Mathf.Lerp(1f, _config.OverloadSpeedMultiplier, overloadProgress);
            _characterMovement.SetWeightSpeedMultiplier(speedMul);

            if (!_isOverloaded)
            {
                _isOverloaded = true;
                OnOverloadChanged?.Invoke(true);
            }

            // Реплика о перегрузе (не чаще чем раз в _replyInterval секунд)
            if (Time.time - _lastReplyTime >= _replyInterval && _config.OverloadReplies.Length > 0)
            {
                _lastReplyTime = Time.time;
                string reply = _config.OverloadReplies[Random.Range(0, _config.OverloadReplies.Length)];
                SpeechBubbleManager.Say(reply);
            }
        }
    }
}
