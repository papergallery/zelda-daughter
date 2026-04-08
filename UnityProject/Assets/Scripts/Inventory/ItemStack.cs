using UnityEngine;

namespace ZeldaDaughter.Inventory
{
    [System.Serializable]
    public struct ItemStack
    {
        [SerializeField] private ItemData _item;
        [SerializeField] private int _amount;

        public ItemData Item => _item;
        public int Amount => _amount;

        public ItemStack(ItemData item, int amount)
        {
            _item = item;
            _amount = amount;
        }

        public ItemStack WithAmount(int newAmount) => new(_item, newAmount);
    }
}
