using UnityEngine;

namespace ZeldaDaughter.Inventory
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Item Data", fileName = "NewItemData")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private float _weight = 0.5f;
        [SerializeField] private bool _stackable = true;
        [SerializeField] private int _maxStack = 10;
        [SerializeField] private string _pickupLine = "";
        [SerializeField] private ItemType _itemType = ItemType.Generic;
        [SerializeField] private string _description = "";
        [SerializeField] private bool _isPlaceable;
        [SerializeField] private GameObject _worldPrefab;

        public string Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float Weight => _weight;
        public bool Stackable => _stackable;
        public int MaxStack => _maxStack;
        public string PickupLine => _pickupLine;
        public ItemType ItemType => _itemType;
        public string Description => _description;
        public bool IsPlaceable => _isPlaceable;
        public GameObject WorldPrefab => _worldPrefab;
    }
}
