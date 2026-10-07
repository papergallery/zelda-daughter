using System;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.World
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Resource Node Data", fileName = "NewResourceNodeData")]
    public class ResourceNodeData : ScriptableObject
    {
        [Serializable]
        public struct ItemDrop
        {
            public ItemData item;
            public int minAmount;
            public int maxAmount;
        }

        [SerializeField] private int _maxHitPoints = 5;
        [SerializeField] private float _respawnTime = 300f;
        [SerializeField] private ItemDrop[] _drops;

        public int MaxHitPoints => _maxHitPoints;
        public float RespawnTime => _respawnTime;
        public ItemDrop[] Drops => _drops;
    }
}
