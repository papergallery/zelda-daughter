using UnityEngine;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Combat
{
    public class WoundEffectApplier : MonoBehaviour
    {
        [SerializeField] private CharacterMovement _movement;
        [SerializeField] private WoundConfig[] _woundConfigs;

        private const float ReplyInterval = 30f;
        private float _lastReplyTime = -ReplyInterval;

        private void OnEnable()
        {
            PlayerHealthState.OnWoundAdded += HandleWoundAdded;
            PlayerHealthState.OnWoundRemoved += HandleWoundRemoved;
            PlayerHealthState.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            PlayerHealthState.OnWoundAdded -= HandleWoundAdded;
            PlayerHealthState.OnWoundRemoved -= HandleWoundRemoved;
            PlayerHealthState.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleWoundAdded(Wound wound)
        {
            RecalculateSpeedMultiplier(wound.Type, added: true);
        }

        private void HandleWoundRemoved(WoundType type)
        {
            RecalculateSpeedMultiplier(type, added: false);
        }

        private void HandleHealthChanged(float ratio)
        {
            if (Time.time - _lastReplyTime < ReplyInterval) return;

            string reply = ratio switch
            {
                <= 0.3f => "Не могу дальше...",
                <= 0.7f => "Плохо дело...",
                _ => null
            };

            if (reply != null)
            {
                SpeechBubbleManager.Say(reply);
                _lastReplyTime = Time.time;
            }
        }

        private void RecalculateSpeedMultiplier(WoundType changedType, bool added)
        {
            if (_movement == null) return;

            // Запрашиваем текущий список ран через PlayerHealthState
            var healthState = GetComponentInParent<PlayerHealthState>();
            if (healthState == null)
                healthState = FindObjectOfType<PlayerHealthState>();
            if (healthState == null) return;

            float combined = 1f;
            foreach (var wound in healthState.ActiveWounds)
            {
                var cfg = FindConfig(wound.Type);
                if (cfg != null)
                    combined *= cfg.SpeedMultiplier;
            }

            _movement.SetWoundSpeedMultiplier(combined);
        }

        private WoundConfig FindConfig(WoundType type)
        {
            if (_woundConfigs == null) return null;
            foreach (var cfg in _woundConfigs)
            {
                if (cfg != null && cfg.Type == type)
                    return cfg;
            }
            return null;
        }
    }
}
