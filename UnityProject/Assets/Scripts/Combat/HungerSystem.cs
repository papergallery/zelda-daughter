using System;
using UnityEngine;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Combat
{
    public class HungerSystem : MonoBehaviour
    {
        [SerializeField] private CombatConfig _config;
        [SerializeField] private CharacterMovement _movement;

        private float _hunger = 0f; // 0 = сыт, 1 = голоден
        private bool _hungerPenaltyActive;

        private float _lastReplyTime = -60f;
        private const float ReplyInterval = 60f;
        private int _replyIndex;

        public static event Action<float> OnHungerChanged;

        private void Update()
        {
            _hunger += Time.deltaTime / _config.HungerMaxTime;
            _hunger = Mathf.Clamp01(_hunger);

            OnHungerChanged?.Invoke(_hunger);

            HandleHungerEffects();
        }

        private void HandleHungerEffects()
        {
            if (_hunger > _config.HungerDegradationThreshold)
            {
                if (!_hungerPenaltyActive)
                {
                    _hungerPenaltyActive = true;
                    ApplyHungerPenalty();
                }

                TrySayHungerReply();
            }
            else if (_hungerPenaltyActive)
            {
                _hungerPenaltyActive = false;
                RemoveHungerPenalty();
            }
        }

        private void ApplyHungerPenalty()
        {
            if (_movement == null) return;
            // Умножаем на штраф голода — другие множители (раны) применяются своим WoundEffectApplier
            // Голод отдельно не перезаписывает SpeedMultiplier напрямую, только через событие
            // Используем отдельный подход: снижаем через базовый множитель
            _movement.SetHungerSpeedMultiplier(_config.HungerSpeedPenalty);
        }

        private void RemoveHungerPenalty()
        {
            if (_movement == null) return;
            _movement.SetHungerSpeedMultiplier(1f);
        }

        private void TrySayHungerReply()
        {
            if (Time.time - _lastReplyTime < ReplyInterval) return;

            string[] replies = _config.HungerReplies;
            if (replies == null || replies.Length == 0) return;

            SpeechBubbleManager.Say(replies[_replyIndex % replies.Length]);
            _replyIndex++;
            _lastReplyTime = Time.time;
        }

        public void Feed(float amount)
        {
            _hunger = Mathf.Clamp01(_hunger - amount);
            OnHungerChanged?.Invoke(_hunger);

            if (_hungerPenaltyActive && _hunger <= _config.HungerDegradationThreshold)
            {
                _hungerPenaltyActive = false;
                RemoveHungerPenalty();
            }
        }
    }
}
