using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Save
{
    [Serializable]
    public class SaveData
    {
        public float playerPosX, playerPosY, playerPosZ;
        public float playerRotY;
        public float timeOfDay;
        // Arrays instead of List<T> — JsonUtility cannot serialize List<T> fields
        public InventorySaveEntry[] inventoryItems = Array.Empty<InventorySaveEntry>();
        public SaveableEntry[] saveableStates = Array.Empty<SaveableEntry>();

        public Vector3 PlayerPosition
        {
            get => new(playerPosX, playerPosY, playerPosZ);
            set { playerPosX = value.x; playerPosY = value.y; playerPosZ = value.z; }
        }
    }

    [Serializable]
    public class InventorySaveEntry
    {
        public string itemId;
        public int amount;
    }

    [Serializable]
    public class SaveableEntry
    {
        public string saveId;
        public string jsonState; // сериализованное состояние объекта
    }
}
