using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Combat
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Combat/Loot Table", fileName = "NewLootTable")]
    public class LootTable : ScriptableObject
    {
        [System.Serializable]
        public struct LootEntry
        {
            public ItemData Item;
            public int MinAmount;
            public int MaxAmount;
            [Range(0f, 1f)] public float Chance;
            public bool RequiresTool;
            public string RequiredToolId;
        }

        [SerializeField] private LootEntry[] _minimalLoot;
        [SerializeField] private LootEntry[] _fullLoot;

        public List<(ItemData item, int amount)> RollLoot(bool hasTool)
        {
            var result = new List<(ItemData, int)>();
            var entries = hasTool ? _fullLoot : _minimalLoot;

            if (entries == null) return result;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.Item == null) continue;
                if (Random.value > e.Chance) continue;
                int amount = Random.Range(e.MinAmount, e.MaxAmount + 1);
                if (amount > 0)
                    result.Add((e.Item, amount));
            }

            return result;
        }
    }
}
