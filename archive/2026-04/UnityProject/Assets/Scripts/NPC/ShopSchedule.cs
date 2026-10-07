using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// Placed on a shop building. Opens/closes based on time of day and controls window light.
    /// </summary>
    public class ShopSchedule : MonoBehaviour
    {
        [SerializeField] private TimeOfDay[] _openTimes = { TimeOfDay.Dawn, TimeOfDay.Day };

        [Tooltip("Light object in shop window — active when shop is closed (night light).")]
        [SerializeField] private GameObject _lightObject;

        [Tooltip("The merchant NPC inside — disabled while shop is closed.")]
        [SerializeField] private NPCInteractable _linkedNPC;

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            // Apply initial state based on current cycle if available
            var cycle = FindObjectOfType<DayNightCycle>();
            if (cycle != null)
                ApplyState(cycle.CurrentTimeOfDay);
        }

        private void OnEnable()
        {
            DayNightCycle.OnTimeOfDayChanged += HandleTimeChanged;
        }

        private void OnDisable()
        {
            DayNightCycle.OnTimeOfDayChanged -= HandleTimeChanged;
        }

        private void HandleTimeChanged(TimeOfDay newTime)
        {
            ApplyState(newTime);
        }

        private void ApplyState(TimeOfDay time)
        {
            _isOpen = System.Array.Exists(_openTimes, t => t == time);

            // Window light is on when shop is closed (candle/torch in the evening)
            if (_lightObject != null)
                _lightObject.SetActive(!_isOpen);

            // Allow or block interaction with the merchant
            if (_linkedNPC != null)
                _linkedNPC.enabled = _isOpen;
        }
    }
}
