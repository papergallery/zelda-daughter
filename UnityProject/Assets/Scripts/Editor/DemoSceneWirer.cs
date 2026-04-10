using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace ZeldaDaughter.Editor
{
    public static class DemoSceneWirer
    {
        private const string CombatConfigPath      = "Assets/Data/Combat/CombatConfig.asset";
        private const string ProgressionConfigPath = "Assets/Data/Progression/ProgressionConfig.asset";
        private const string WoundPuncturePath     = "Assets/Data/Combat/WoundConfig_Puncture.asset";
        private const string WoundFracturePath     = "Assets/Data/Combat/WoundConfig_Fracture.asset";
        private const string WoundBurnPath         = "Assets/Data/Combat/WoundConfig_Burn.asset";
        private const string WoundPoisonPath       = "Assets/Data/Combat/WoundConfig_Poison.asset";
        private const string EnemyDataBoarPath     = "Assets/Data/Combat/EnemyData_Boar.asset";
        private const string EnemyDataWolfPath     = "Assets/Data/Combat/EnemyData_Wolf.asset";
        private const string ResourceTreePath      = "Assets/Content/Resources/Resource_Tree.asset";
        private const string ResourceRockPath      = "Assets/Content/Resources/Resource_Rock.asset";
        private const string PrefabBoarPath        = "Assets/Prefabs/Enemies/Enemy_Boar.prefab";
        private const string PrefabWolfPath        = "Assets/Prefabs/Enemies/Enemy_Wolf.prefab";

        [MenuItem("ZeldaDaughter/Scene/Wire DemoScene References")]
        public static void WireAll()
        {
            // Load DemoScene if not already open (needed for batch mode)
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name != "DemoScene")
            {
                var scenePath = "Assets/Scenes/DemoScene.unity";
                if (System.IO.File.Exists(scenePath))
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    Debug.Log("[DemoSceneWirer] DemoScene загружена.");
                }
                else
                {
                    Debug.LogError("[DemoSceneWirer] DemoScene.unity не найдена!");
                    return;
                }
            }

            Debug.Log("[DemoSceneWirer] Начинаем wire DemoScene...");

            WirePlayer();
            WireEnemySpawnZones();
            WireResourceNodes();
            WirePickupables();
            WireRadialMenu();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[DemoSceneWirer] Готово. Все references назначены. Сцена сохранена.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. Player
        // ─────────────────────────────────────────────────────────────────────

        private static void WirePlayer()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[DemoSceneWirer] WirePlayer: объект с тегом 'Player' не найден.");
                return;
            }

            var combatConfig = AssetDatabase.LoadAssetAtPath<Combat.CombatConfig>(CombatConfigPath);
            if (combatConfig == null)
                Debug.LogWarning($"[DemoSceneWirer] WirePlayer: CombatConfig не найден по пути {CombatConfigPath}");

            var progressionConfig = AssetDatabase.LoadAssetAtPath<Progression.ProgressionConfig>(ProgressionConfigPath);
            if (progressionConfig == null)
                Debug.LogWarning($"[DemoSceneWirer] WirePlayer: ProgressionConfig не найден по пути {ProgressionConfigPath}");

            var woundPuncture = AssetDatabase.LoadAssetAtPath<Combat.WoundConfig>(WoundPuncturePath);
            var woundFracture = AssetDatabase.LoadAssetAtPath<Combat.WoundConfig>(WoundFracturePath);
            var woundBurn     = AssetDatabase.LoadAssetAtPath<Combat.WoundConfig>(WoundBurnPath);
            var woundPoison   = AssetDatabase.LoadAssetAtPath<Combat.WoundConfig>(WoundPoisonPath);

            if (woundPuncture == null || woundFracture == null || woundBurn == null || woundPoison == null)
                Debug.LogWarning("[DemoSceneWirer] WirePlayer: часть WoundConfig SO не найдена. Запустите ZeldaDaughter/Data/Build Combat Data.");

            var charMovement  = player.GetComponent<Input.CharacterMovement>();
            var charAutoMove  = player.GetComponent<Input.CharacterAutoMove>();
            var hitboxTrigger = player.GetComponentInChildren<Combat.HitboxTrigger>();
            var gestureDisp   = Object.FindObjectOfType<Input.GestureDispatcher>();

            // PlayerHealthState
            if (player.TryGetComponent<Combat.PlayerHealthState>(out var healthState))
            {
                var so = new SerializedObject(healthState);
                if (combatConfig != null)
                    so.FindProperty("_config").objectReferenceValue = combatConfig;
                SetWoundConfigsArray(so, "_woundConfigs", woundPuncture, woundFracture, woundBurn, woundPoison);
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WirePlayer: PlayerHealthState — готово.");
            }

            // CombatController
            if (player.TryGetComponent<Combat.CombatController>(out var combatCtrl))
            {
                var so = new SerializedObject(combatCtrl);
                if (combatConfig != null)  so.FindProperty("_config").objectReferenceValue  = combatConfig;
                if (hitboxTrigger != null) so.FindProperty("_hitbox").objectReferenceValue  = hitboxTrigger;
                if (charAutoMove != null)  so.FindProperty("_autoMove").objectReferenceValue = charAutoMove;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WirePlayer: CombatController — готово.");
            }

            // HungerSystem
            if (player.TryGetComponent<Combat.HungerSystem>(out var hungerSys))
            {
                var so = new SerializedObject(hungerSys);
                if (combatConfig != null)  so.FindProperty("_config").objectReferenceValue    = combatConfig;
                if (charMovement != null)  so.FindProperty("_movement").objectReferenceValue  = charMovement;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WirePlayer: HungerSystem — готово.");
            }

            // KnockoutSystem
            if (player.TryGetComponent<Combat.KnockoutSystem>(out var knockoutSys))
            {
                var so = new SerializedObject(knockoutSys);
                if (combatConfig != null)  so.FindProperty("_config").objectReferenceValue             = combatConfig;
                if (gestureDisp != null)   so.FindProperty("_gestureDispatcher").objectReferenceValue  = gestureDisp;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WirePlayer: KnockoutSystem — готово.");
            }

            // PlayerStats
            if (player.TryGetComponent<Progression.PlayerStats>(out var playerStats))
            {
                var so = new SerializedObject(playerStats);
                if (progressionConfig != null)
                    so.FindProperty("_config").objectReferenceValue = progressionConfig;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WirePlayer: PlayerStats — готово.");
            }

            // WoundEffectApplier
            if (player.TryGetComponent<Combat.WoundEffectApplier>(out var woundApplier))
            {
                var so = new SerializedObject(woundApplier);
                SetWoundConfigsArray(so, "_woundConfigs", woundPuncture, woundFracture, woundBurn, woundPoison);
                if (charMovement != null) so.FindProperty("_movement").objectReferenceValue = charMovement;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WirePlayer: WoundEffectApplier — готово.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. EnemySpawnZones
        // ─────────────────────────────────────────────────────────────────────

        private static void WireEnemySpawnZones()
        {
            var boarData = AssetDatabase.LoadAssetAtPath<Combat.EnemyData>(EnemyDataBoarPath);
            var wolfData = AssetDatabase.LoadAssetAtPath<Combat.EnemyData>(EnemyDataWolfPath);

            if (boarData == null)
                Debug.LogWarning($"[DemoSceneWirer] WireEnemySpawnZones: EnemyData_Boar не найден по {EnemyDataBoarPath}");
            if (wolfData == null)
                Debug.LogWarning($"[DemoSceneWirer] WireEnemySpawnZones: EnemyData_Wolf не найден по {EnemyDataWolfPath}");

            var boarPrefab = EnsureEnemyPrefabs();
            var wolfPrefab = EnsureWolfPrefab();

            WireSpawnZone("SpawnZone_Boars_Meadow", boarData, boarPrefab);
            WireSpawnZone("SpawnZone_Wolves_Meadow", wolfData, wolfPrefab);
            WireSpawnZone("SpawnZone_Wolves_Road",   wolfData, wolfPrefab);

            Debug.Log("[DemoSceneWirer] WireEnemySpawnZones — готово.");
        }

        private static void WireSpawnZone(string zoneName, Combat.EnemyData enemyData, GameObject enemyPrefab)
        {
            var zoneGo = GameObject.Find(zoneName);
            if (zoneGo == null)
            {
                Debug.LogWarning($"[DemoSceneWirer] SpawnZone не найдена: {zoneName}");
                return;
            }

            if (!zoneGo.TryGetComponent<Combat.EnemySpawnZone>(out var zone))
            {
                Debug.LogWarning($"[DemoSceneWirer] EnemySpawnZone компонент отсутствует на {zoneName}");
                return;
            }

            var so = new SerializedObject(zone);
            if (enemyData   != null) so.FindProperty("_enemyData").objectReferenceValue    = enemyData;
            if (enemyPrefab != null) so.FindProperty("_enemyPrefab").objectReferenceValue  = enemyPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[DemoSceneWirer] SpawnZone '{zoneName}' — готово.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. EnsureEnemyPrefabs
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureEnemyPrefabs()
        {
            return EnsureEnemyPrefab(
                PrefabBoarPath,
                "Enemy_Boar",
                new Vector3(1.5f, 1.5f, 1.5f),
                new Color(0.8f, 0.15f, 0.1f),
                EnemyDataBoarPath);
        }

        private static GameObject EnsureWolfPrefab()
        {
            return EnsureEnemyPrefab(
                PrefabWolfPath,
                "Enemy_Wolf",
                new Vector3(1.2f, 1.2f, 1.2f),
                new Color(0.45f, 0.45f, 0.45f),
                EnemyDataWolfPath);
        }

        private static GameObject EnsureEnemyPrefab(
            string prefabPath,
            string goName,
            Vector3 scale,
            Color color,
            string enemyDataPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                Debug.Log($"[DemoSceneWirer] Prefab уже существует: {prefabPath}");
                return existing;
            }

            var dir = Path.GetDirectoryName(prefabPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Создаём временный GO в сцене для сохранения в prefab
            var tempGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tempGo.name = goName;
            tempGo.transform.localScale = scale;

            // Материал-placeholder
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            tempGo.GetComponent<Renderer>().sharedMaterial = mat;

            // Тег Enemy
            try { tempGo.tag = "Enemy"; }
            catch { Debug.LogWarning($"[DemoSceneWirer] Тег 'Enemy' не существует — пропускаем назначение тега для {goName}."); }

            // CapsuleCollider уже создан CreatePrimitive — ничего не добавляем
            tempGo.AddComponent<Combat.EnemyHealth>();
            tempGo.AddComponent<Combat.EnemyFSM>();
            tempGo.AddComponent<Combat.DeathToCarcass>();

            // Назначаем EnemyData через SerializedObject до сохранения prefab
            var enemyData = AssetDatabase.LoadAssetAtPath<Combat.EnemyData>(enemyDataPath);
            if (enemyData != null)
            {
                var health = tempGo.GetComponent<Combat.EnemyHealth>();
                if (health != null)
                {
                    var so = new SerializedObject(health);
                    var dataProp = so.FindProperty("_data");
                    if (dataProp != null)
                    {
                        dataProp.objectReferenceValue = enemyData;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                var fsm = tempGo.GetComponent<Combat.EnemyFSM>();
                if (fsm != null)
                {
                    var so = new SerializedObject(fsm);
                    var dataProp = so.FindProperty("_data");
                    if (dataProp != null)
                    {
                        dataProp.objectReferenceValue = enemyData;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[DemoSceneWirer] EnemyData не найдена по {enemyDataPath} — prefab создан без данных.");
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);
            Object.DestroyImmediate(tempGo);

            if (prefab != null)
                Debug.Log($"[DemoSceneWirer] Prefab создан: {prefabPath}");
            else
                Debug.LogError($"[DemoSceneWirer] Не удалось создать prefab: {prefabPath}");

            return prefab;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. ResourceNodes
        // ─────────────────────────────────────────────────────────────────────

        private static void WireResourceNodes()
        {
            var treeData = AssetDatabase.LoadAssetAtPath<World.ResourceNodeData>(ResourceTreePath);
            var rockData = AssetDatabase.LoadAssetAtPath<World.ResourceNodeData>(ResourceRockPath);

            if (treeData == null)
                Debug.LogWarning($"[DemoSceneWirer] WireResourceNodes: Resource_Tree не найден по {ResourceTreePath}");
            if (rockData == null)
                Debug.LogWarning($"[DemoSceneWirer] WireResourceNodes: Resource_Rock не найден по {ResourceRockPath}");

            var nodes = Object.FindObjectsOfType<World.ResourceNode>();
            int wiredCount = 0;

            foreach (var node in nodes)
            {
                var name = node.gameObject.name;
                World.ResourceNodeData targetData = null;

                if (name.Contains("Tree"))
                    targetData = treeData;
                else if (name.Contains("Stone") || name.Contains("Ore"))
                    targetData = rockData;

                if (targetData == null)
                    continue;

                var so = new SerializedObject(node);
                var dataProp = so.FindProperty("_data");
                if (dataProp != null)
                {
                    dataProp.objectReferenceValue = targetData;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    wiredCount++;
                }
            }

            Debug.Log($"[DemoSceneWirer] WireResourceNodes: {wiredCount}/{nodes.Length} нод — готово.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. Pickupables
        // ─────────────────────────────────────────────────────────────────────

        private static void WirePickupables()
        {
            // Собираем доступные ItemData — приоритет Content/Items, затем Data/Items
            var itemSources = new (string path, Inventory.ItemData data)[]
            {
                ("Assets/Content/Items/Item_Stick.asset",    null),
                ("Assets/Content/Items/Item_Stone.asset",    null),
                ("Assets/Content/Items/Item_Berry.asset",    null),
                ("Assets/Data/Items/item_stick.asset",       null),
                ("Assets/Data/Items/item_herbs.asset",       null),
                ("Assets/Data/Items/item_cloth.asset",       null),
                ("Assets/Data/Items/item_knife.asset",       null),
            };

            // Загружаем существующие
            var available = new System.Collections.Generic.List<Inventory.ItemData>();
            foreach (var (path, _) in itemSources)
            {
                var item = AssetDatabase.LoadAssetAtPath<Inventory.ItemData>(path);
                if (item != null)
                    available.Add(item);
            }

            if (available.Count == 0)
            {
                Debug.LogWarning("[DemoSceneWirer] WirePickupables: не найдено ни одного ItemData — пропускаем.");
                return;
            }

            var pickupables = Object.FindObjectsOfType<World.Pickupable>();
            int wiredCount = 0;

            for (int i = 0; i < pickupables.Length; i++)
            {
                var pickupable = pickupables[i];
                var so = new SerializedObject(pickupable);

                // round-robin по доступным items
                var itemData = available[i % available.Count];

                var itemProp   = so.FindProperty("_itemData");
                var amountProp = so.FindProperty("_amount");
                var saveIdProp = so.FindProperty("_saveId");

                if (itemProp   != null) itemProp.objectReferenceValue = itemData;
                if (amountProp != null) amountProp.intValue           = 1;
                if (saveIdProp != null && string.IsNullOrEmpty(saveIdProp.stringValue))
                    saveIdProp.stringValue = pickupable.gameObject.name;

                so.ApplyModifiedPropertiesWithoutUndo();
                wiredCount++;
            }

            Debug.Log($"[DemoSceneWirer] WirePickupables: {wiredCount}/{pickupables.Length} объектов — готово.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. RadialMenu
        // ─────────────────────────────────────────────────────────────────────

        private static void WireRadialMenu()
        {
            var radialMenu = Object.FindObjectOfType<UI.RadialMenuController>();
            if (radialMenu == null)
            {
                Debug.LogWarning("[DemoSceneWirer] WireRadialMenu: RadialMenuController не найден в сцене.");
                return;
            }

            const string radialConfigPath = "Assets/Data/UI/RadialMenuConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<UI.RadialMenuConfig>(radialConfigPath);
            if (config == null)
            {
                // Create default RadialMenuConfig
                if (!Directory.Exists("Assets/Data/UI"))
                    AssetDatabase.CreateFolder("Assets/Data", "UI");
                config = ScriptableObject.CreateInstance<UI.RadialMenuConfig>();
                AssetDatabase.CreateAsset(config, radialConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[DemoSceneWirer] Создан RadialMenuConfig.asset по {radialConfigPath}");
            }

            var so = new SerializedObject(radialMenu);
            var configProp = so.FindProperty("_config");
            if (configProp != null)
            {
                configProp.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[DemoSceneWirer] WireRadialMenu — готово.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void SetWoundConfigsArray(
            SerializedObject so,
            string propertyName,
            Combat.WoundConfig puncture,
            Combat.WoundConfig fracture,
            Combat.WoundConfig burn,
            Combat.WoundConfig poison)
        {
            var arrayProp = so.FindProperty(propertyName);
            if (arrayProp == null)
                return;

            arrayProp.arraySize = 4;
            arrayProp.GetArrayElementAtIndex(0).objectReferenceValue = puncture;
            arrayProp.GetArrayElementAtIndex(1).objectReferenceValue = fracture;
            arrayProp.GetArrayElementAtIndex(2).objectReferenceValue = burn;
            arrayProp.GetArrayElementAtIndex(3).objectReferenceValue = poison;
        }
    }
}
