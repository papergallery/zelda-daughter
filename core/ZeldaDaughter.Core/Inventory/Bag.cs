#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Data;

namespace ZeldaDaughter.Core.Inventory
{
    /// <summary>data/inventory.json (C-09).</summary>
    public sealed class InventorySettings
    {
        public int Slots { get; set; }
        public float BaseCapacity { get; set; }
        public float OverloadFrom { get; set; }
        /// <summary>Points (load ratio, speed multiplier), linear between, flat outside.</summary>
        public List<float[]> SpeedCurve { get; set; } = new List<float[]>();
    }

    public sealed class Stack
    {
        public string ItemId { get; }
        public int Count { get; internal set; }

        public Stack(string itemId, int count) { ItemId = itemId; Count = count; }
    }

    /// <summary>
    /// The hero's bag (project-design.md §7): limited slots, hidden weight. Add and Remove are all-or-nothing.
    /// Capacity = base × the carry_capacity effect of Skills (passed in as <c>capacityMultiplier</c>).
    /// </summary>
    public sealed class Bag
    {
        readonly InventorySettings _s;
        readonly IReadOnlyDictionary<string, ItemDef> _items;
        readonly List<Stack> _stacks = new List<Stack>();

        public Bag(InventorySettings settings, IReadOnlyDictionary<string, ItemDef> items)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<Stack> Stacks => _stacks;
        public int UsedSlots => _stacks.Count;

        public int Count(string itemId)
        {
            int n = 0;
            foreach (var s in _stacks) if (s.ItemId == itemId) n += s.Count;
            return n;
        }

        public float Weight
        {
            get
            {
                float w = 0;
                foreach (var s in _stacks) w += _items[s.ItemId].Weight * s.Count;
                return w;
            }
        }

        public bool CanAdd(string itemId, int count = 1)
        {
            if (count <= 0 || !_items.TryGetValue(itemId, out var def)) return false;
            int room = 0;
            foreach (var s in _stacks) if (s.ItemId == itemId) room += def.Stack - s.Count;
            int left = Math.Max(0, count - room);
            int newSlots = (left + def.Stack - 1) / def.Stack;
            return _stacks.Count + newSlots <= _s.Slots;
        }

        public bool Add(string itemId, int count = 1)
        {
            if (!CanAdd(itemId, count)) return false;
            var def = _items[itemId];
            foreach (var s in _stacks)
            {
                if (count == 0) break;
                if (s.ItemId != itemId) continue;
                int put = Math.Min(def.Stack - s.Count, count);
                s.Count += put;
                count -= put;
            }
            while (count > 0)
            {
                int put = Math.Min(def.Stack, count);
                _stacks.Add(new Stack(itemId, put));
                count -= put;
            }
            return true;
        }

        public bool Remove(string itemId, int count = 1)
        {
            if (count <= 0 || Count(itemId) < count) return false;
            for (int i = _stacks.Count - 1; i >= 0 && count > 0; i--)
            {
                var s = _stacks[i];
                if (s.ItemId != itemId) continue;
                int take = Math.Min(s.Count, count);
                s.Count -= take;
                count -= take;
                if (s.Count == 0) _stacks.RemoveAt(i);
            }
            return true;
        }

        public float Capacity(float capacityMultiplier) => _s.BaseCapacity * Math.Max(capacityMultiplier, 1e-3f);
        public float LoadRatio(float capacityMultiplier) => Weight / Capacity(capacityMultiplier);

        /// <summary>Overload starts where the penalty starts — and that is also where carrying trains (§6, §7).</summary>
        public bool IsOverloaded(float capacityMultiplier) => LoadRatio(capacityMultiplier) > _s.OverloadFrom;

        public float SpeedMultiplier(float capacityMultiplier)
        {
            float r = LoadRatio(capacityMultiplier);
            var c = _s.SpeedCurve;
            if (c.Count == 0 || r <= c[0][0]) return c.Count == 0 ? 1f : c[0][1];
            for (int i = 1; i < c.Count; i++)
                if (r <= c[i][0])
                    return c[i - 1][1] + (c[i][1] - c[i - 1][1]) * (r - c[i - 1][0]) / (c[i][0] - c[i - 1][0]);
            return c[c.Count - 1][1];
        }

        /// <summary>For save/load (C-13): stacks as they were; unknown ids are dropped.</summary>
        public void Restore(IEnumerable<Stack> stacks)
        {
            _stacks.Clear();
            foreach (var s in stacks) if (_items.ContainsKey(s.ItemId) && s.Count > 0) _stacks.Add(new Stack(s.ItemId, s.Count));
        }
    }
}
