using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.Inventory
{
    public class PlayerInventory : MonoBehaviour, ISaveable
    {
        [SerializeField] private int _maxSlots = 20;
        [SerializeField] private InventoryConfig _config;

        private readonly List<ItemStack> _items = new();

        public static event System.Action<ItemData, int> OnItemAdded;
        public static event System.Action<ItemData, int> OnItemRemoved;
        public static event System.Action OnInventoryChanged;
        public static event System.Action<float> OnWeightChanged;

        public IReadOnlyList<ItemStack> Items => _items;
        public int SlotCount => _items.Count;
        public int MaxSlots => _config != null ? _config.MaxSlots : _maxSlots;
        public bool IsFull => _items.Count >= MaxSlots;

        public float TotalWeight
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _items.Count; i++)
                    total += _items[i].Item.Weight * _items[i].Amount;
                return total;
            }
        }

        public float WeightRatio => _config != null && _config.BaseWeightCapacity > 0
            ? TotalWeight / _config.BaseWeightCapacity
            : 0f;

        public bool IsOverloaded => _config != null && WeightRatio > _config.OverloadThreshold;

        public string SaveId => "player_inventory";

        /// <summary>
        /// Добавляет предмет. Если stackable — докладывает в существующий стак до maxStack,
        /// затем создаёт новые слоты. Возвращает false если нет свободного места.
        /// </summary>
        public bool AddItem(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0)
                return false;

            int remaining = amount;

            if (item.Stackable)
            {
                // Докладываем в существующие стаки
                for (int i = 0; i < _items.Count && remaining > 0; i++)
                {
                    if (_items[i].Item != item)
                        continue;

                    int space = item.MaxStack - _items[i].Amount;
                    if (space <= 0)
                        continue;

                    int toAdd = Mathf.Min(space, remaining);
                    _items[i] = _items[i].WithAmount(_items[i].Amount + toAdd);
                    remaining -= toAdd;
                }
            }

            // Создаём новые слоты для остатка
            while (remaining > 0)
            {
                if (IsFull)
                    break;

                int toAdd = item.Stackable ? Mathf.Min(item.MaxStack, remaining) : 1;
                _items.Add(new ItemStack(item, toAdd));
                remaining -= toAdd;
            }

            bool anyAdded = remaining < amount;
            if (anyAdded)
            {
                OnItemAdded?.Invoke(item, amount - remaining);
                OnInventoryChanged?.Invoke();
                NotifyWeightChanged();
            }

            // Если remaining > 0 — часть предметов не поместилась
            return remaining == 0;
        }

        /// <summary>
        /// Убирает указанное количество предмета. Возвращает false если предмета недостаточно.
        /// </summary>
        public bool RemoveItem(ItemData item, int amount = 1)
        {
            if (!HasItem(item, amount))
                return false;

            int remaining = amount;

            for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_items[i].Item != item)
                    continue;

                int toRemove = Mathf.Min(_items[i].Amount, remaining);
                int newAmount = _items[i].Amount - toRemove;

                if (newAmount == 0)
                    _items.RemoveAt(i);
                else
                    _items[i] = _items[i].WithAmount(newAmount);

                remaining -= toRemove;
            }

            OnItemRemoved?.Invoke(item, amount);
            OnInventoryChanged?.Invoke();
            NotifyWeightChanged();
            return true;
        }

        public bool HasItem(ItemData item, int amount = 1)
        {
            return GetItemCount(item) >= amount;
        }

        public int GetItemCount(ItemData item)
        {
            int total = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Item == item)
                    total += _items[i].Amount;
            }
            return total;
        }

        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= _items.Count)
                return default;
            return _items[index];
        }

        public void SwapSlots(int from, int to)
        {
            if (from < 0 || from >= _items.Count || to < 0 || to >= _items.Count || from == to)
                return;
            (_items[from], _items[to]) = (_items[to], _items[from]);
            OnInventoryChanged?.Invoke();
        }

        public bool RemoveFromSlot(int slotIndex, int amount = 1)
        {
            if (slotIndex < 0 || slotIndex >= _items.Count)
                return false;

            var stack = _items[slotIndex];
            if (stack.Amount < amount)
                return false;

            int newAmount = stack.Amount - amount;
            if (newAmount == 0)
                _items.RemoveAt(slotIndex);
            else
                _items[slotIndex] = stack.WithAmount(newAmount);

            OnItemRemoved?.Invoke(stack.Item, amount);
            OnInventoryChanged?.Invoke();
            NotifyWeightChanged();
            return true;
        }

        public void Clear()
        {
            _items.Clear();
            OnInventoryChanged?.Invoke();
            NotifyWeightChanged();
        }

        // ISaveable

        public object CaptureState()
        {
            var entries = new List<InventorySaveEntry>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Item == null)
                    continue;

                entries.Add(new InventorySaveEntry
                {
                    itemId = _items[i].Item.Id,
                    amount = _items[i].Amount
                });
            }
            return entries;
        }

        public void RestoreState(object state)
        {
            if (state is not List<InventorySaveEntry> entries)
                return;

            _items.Clear();

            foreach (var entry in entries)
            {
                var item = Resources.Load<ItemData>(entry.itemId);
                if (item == null)
                {
                    Debug.LogWarning($"[PlayerInventory] RestoreState: ItemData с id '{entry.itemId}' не найден в Resources.");
                    continue;
                }

                _items.Add(new ItemStack(item, entry.amount));
            }

            OnInventoryChanged?.Invoke();
            NotifyWeightChanged();
        }

        private void NotifyWeightChanged()
        {
            OnWeightChanged?.Invoke(TotalWeight);
        }

        [System.Serializable]
        public class InventorySaveEntry
        {
            public string itemId;
            public int amount;
        }
    }
}
