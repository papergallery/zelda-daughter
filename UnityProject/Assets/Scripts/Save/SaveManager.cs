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
                foreach (var stack in inventory.Items)
                {
                    if (stack.Item == null) continue;
                    data.inventoryItems.Add(new InventorySaveEntry
                    {
                        itemId = stack.Item.Id,
                        amount = stack.Amount
                    });
                }
            }

            var dayNight = Object.FindFirstObjectByType<DayNightCycle>();
            if (dayNight != null)
                data.timeOfDay = dayNight.TimeNormalized;

            foreach (var saveable in _registry)
            {
                var state = saveable.CaptureState();
                if (state == null) continue;

                string json = JsonUtility.ToJson(state);
                data.saveableStates.Add(new SaveableEntry
                {
                    saveId = saveable.SaveId,
                    jsonState = json
                });
            }

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
                inventory.Clear();
                foreach (var entry in data.inventoryItems)
                {
                    var item = Resources.Load<ItemData>(entry.itemId);
                    if (item == null)
                    {
                        Debug.LogWarning($"[Save] ItemData '{entry.itemId}' not found in Resources.");
                        continue;
                    }
                    inventory.AddItem(item, entry.amount);
                }
            }

            var dayNight = Object.FindFirstObjectByType<DayNightCycle>();
            if (dayNight != null)
                dayNight.SetTime(data.timeOfDay * 24f);

            foreach (var saveable in _registry)
            {
                var entry = data.saveableStates.Find(e => e.saveId == saveable.SaveId);
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
