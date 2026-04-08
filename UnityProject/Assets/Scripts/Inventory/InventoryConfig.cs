using UnityEngine;

namespace ZeldaDaughter.Inventory
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Inventory Config", fileName = "InventoryConfig")]
    public class InventoryConfig : ScriptableObject
    {
        [Header("Slots")]
        [SerializeField] private int _maxSlots = 20;

        [Header("Weight")]
        [SerializeField] private float _baseWeightCapacity = 50f;
        [SerializeField] private float _overloadThreshold = 0.8f;
        [SerializeField] private float _overloadSpeedMultiplier = 0.5f;

        [Header("Overload Replies")]
        [SerializeField] private string[] _overloadReplies = new[]
        {
            "Тяжело...",
            "Спина не выдержит...",
            "Надо что-то выбросить...",
            "Еле тащу..."
        };

        public int MaxSlots => _maxSlots;
        public float BaseWeightCapacity => _baseWeightCapacity;
        public float OverloadThreshold => _overloadThreshold;
        public float OverloadSpeedMultiplier => _overloadSpeedMultiplier;
        public string[] OverloadReplies => _overloadReplies;
    }
}
