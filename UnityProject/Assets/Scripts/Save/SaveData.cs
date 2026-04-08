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
        public List<InventorySaveEntry> inventoryItems = new();
        public List<SaveableEntry> saveableStates = new();

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
