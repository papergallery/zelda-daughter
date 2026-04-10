using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZeldaDaughter.Debugging;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Save
{
    public class SaveManager : MonoBehaviour
    {
        [SerializeField] private float _autoSaveInterval = 120f;

        private static readonly List<ISaveable> _registry = new();

        public static SaveManager Instance { get; private set; }

        private static string _savePath;
        private static string SavePath => _savePath ??= Application.persistentDataPath + "/save.json";

        // Cache of all known ItemData, built at Load time from scene Pickupables + inventory
        private static readonly Dictionary<string, ItemData> _itemCache = new();

        public static void Register(ISaveable saveable)
        {
            if (!_registry.Contains(saveable))
                _registry.Add(saveable);
        }

        public static void Unregister(ISaveable saveable)
        {
            _registry.Remove(saveable);
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            Application.quitting += Save;
        }

        private void OnDisable()
        {
            Application.quitting -= Save;
        }

        private void Start()
        {
            Load();
            StartCoroutine(AutoSaveRoutine());
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Save();
        }

        public void Save()
        {
            var data = new SaveData();

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                data.PlayerPosition = playerObj.transform.position;
                data.playerRotY = playerObj.transform.rotation.eulerAngles.y;
            }

            if (playerObj != null && playerObj.TryGetComponent<PlayerInventory>(out var inventory))
            {
                var invList = new List<InventorySaveEntry>();
                foreach (var stack in inventory.Items)
                {
                    if (stack.Item == null) continue;
                    invList.Add(new InventorySaveEntry
                    {
                        itemId = stack.Item.Id,
                        amount = stack.Amount
                    });
                }
                data.inventoryItems = invList.ToArray();
            }

            var dayNight = Object.FindFirstObjectByType<DayNightCycle>();
            if (dayNight != null)
                data.timeOfDay = dayNight.TimeNormalized;

            var statesList = new List<SaveableEntry>();
            foreach (var saveable in _registry)
            {
                var state = saveable.CaptureState();
                if (state == null) continue;

                string json = JsonUtility.ToJson(state);
                statesList.Add(new SaveableEntry
                {
                    saveId = saveable.SaveId,
                    jsonState = json
                });
            }
            data.saveableStates = statesList.ToArray();

            string saveJson = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, saveJson);
            Debug.Log("[Save] Saved successfully");
            ZDLog.Log("Save", "AutoSave success");
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
                return;

            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogWarning("[Save] Failed to deserialize save file.");
                return;
            }

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerObj.transform.position = data.PlayerPosition;
                playerObj.transform.rotation = Quaternion.Euler(0f, data.playerRotY, 0f);
            }

            if (playerObj != null && playerObj.TryGetComponent<PlayerInventory>(out var inventory))
            {
                // Build item cache from scene Pickupables before restoring
                if (_itemCache.Count == 0)
                    BuildItemCache();

                inventory.Clear();
                foreach (var entry in data.inventoryItems)
                {
                    if (_itemCache.TryGetValue(entry.itemId, out var item))
                    {
                        inventory.AddItem(item, entry.amount);
                    }
                    else
                    {
                        Debug.LogWarning($"[Save] ItemData '{entry.itemId}' not found in item cache.");
                    }
                }
            }

            var dayNight = Object.FindFirstObjectByType<DayNightCycle>();
            if (dayNight != null)
                dayNight.SetTime(data.timeOfDay * 24f);

            foreach (var saveable in _registry)
            {
                var entry = System.Array.Find(data.saveableStates, e => e.saveId == saveable.SaveId);
                if (entry == null) continue;

                var state = JsonUtility.FromJson(entry.jsonState, saveable.CaptureState()?.GetType() ?? typeof(object));
                if (state != null)
                    saveable.RestoreState(state);
            }

            Debug.Log("[Save] Loaded successfully");
            ZDLog.Log("Save", "Loaded success");
        }

        /// <summary>
        /// Удаляет файл сохранения. Используется при старте новой игры.
        /// </summary>
        public void DeleteSave()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }

        private static void BuildItemCache()
        {
            _itemCache.Clear();
            // Scan all Pickupables in scene for their ItemData
            foreach (var pickup in Object.FindObjectsOfType<Pickupable>())
            {
                var itemData = pickup.GetItemData();
                if (itemData != null && !_itemCache.ContainsKey(itemData.Id))
                    _itemCache[itemData.Id] = itemData;
            }
            // Scan ResourceNodes for drop items (e.g. Wood from trees)
            foreach (var resNode in Object.FindObjectsOfType<ResourceNode>())
            {
                var nodeData = resNode.Data;
                if (nodeData?.Drops == null) continue;
                foreach (var drop in nodeData.Drops)
                {
                    if (drop.item != null && !_itemCache.ContainsKey(drop.item.Id))
                        _itemCache[drop.item.Id] = drop.item;
                }
            }
            // Also check existing inventory items
            var player = GameObject.FindWithTag("Player");
            if (player != null && player.TryGetComponent<PlayerInventory>(out var inv))
            {
                foreach (var stack in inv.Items)
                {
                    if (stack.Item != null && !_itemCache.ContainsKey(stack.Item.Id))
                        _itemCache[stack.Item.Id] = stack.Item;
                }
            }
            ZDLog.Log("Save", $"ItemCache built: {_itemCache.Count} items");
        }

        private IEnumerator AutoSaveRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_autoSaveInterval);
                Save();
            }
        }
    }
}
