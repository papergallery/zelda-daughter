using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using TMPro;

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
        private const string BoarAnimatorPath      = "Assets/Animations/Controllers/BoarAnimator.controller";
        private const string WolfAnimatorPath      = "Assets/Animations/Controllers/WolfAnimator.controller";
        private const string WolfFbxPath           = "Assets/Models/Animals/Animal Pack Vol.2 by @Quaternius/FBX/Wolf.fbx";
        private const string PigFbxPath            = "Assets/Models/Animals/Farm Animals by @Quaternius/FBX/Pig.fbx";

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
            WireNPCs();
            WireAudio();
            EnsureDebugResetButton();
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
                EnemyDataBoarPath,
                PigFbxPath,
                BoarAnimatorPath);
        }

        private static GameObject EnsureWolfPrefab()
        {
            return EnsureEnemyPrefab(
                PrefabWolfPath,
                "Enemy_Wolf",
                new Vector3(1.2f, 1.2f, 1.2f),
                new Color(0.45f, 0.45f, 0.45f),
                EnemyDataWolfPath,
                WolfFbxPath,
                WolfAnimatorPath);
        }

        private static GameObject EnsureEnemyPrefab(
            string prefabPath,
            string goName,
            Vector3 scale,
            Color color,
            string enemyDataPath,
            string modelFbxPath,
            string animatorControllerPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                // Обновляем Animator на существующем prefab если не назначен
                UpdateExistingEnemyPrefabAnimator(prefabPath, animatorControllerPath);
                Debug.Log($"[DemoSceneWirer] Prefab уже существует: {prefabPath}");
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            var dir = Path.GetDirectoryName(prefabPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Root — пустой GameObject (не Capsule), чтобы не было лишнего MeshRenderer
            var root = new GameObject(goName);
            root.transform.localScale = scale;

            // Тег Enemy
            try { root.tag = "Enemy"; }
            catch { Debug.LogWarning($"[DemoSceneWirer] Тег 'Enemy' не существует — пропускаем для {goName}."); }

            // Collider на root
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.5f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 0.75f, 0f);

            // Animator на root — EnemyFSM использует GetComponentInChildren<Animator>()
            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorControllerPath);
            var animator = root.AddComponent<Animator>();
            if (animController != null)
            {
                animator.runtimeAnimatorController = animController;
                Debug.Log($"[DemoSceneWirer] {goName}: AnimatorController назначен: {animatorControllerPath}");
            }
            else
            {
                Debug.LogWarning($"[DemoSceneWirer] {goName}: AnimatorController не найден: {animatorControllerPath}. " +
                                 "Запустите ZeldaDaughter/Animation/Build Enemy Animators.");
            }

            // Визуальная модель — child "Visual"
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelFbxPath);
            if (modelAsset != null)
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                modelInstance.name = "Visual";
                modelInstance.transform.SetParent(root.transform);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                Debug.Log($"[DemoSceneWirer] {goName}: модель назначена из {modelFbxPath}");
            }
            else
            {
                // Fallback: капсула-placeholder
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Visual";
                capsule.transform.SetParent(root.transform);
                capsule.transform.localPosition = Vector3.zero;
                capsule.transform.localScale = Vector3.one;
                var capsuleCol = capsule.GetComponent<Collider>();
                if (capsuleCol != null) Object.DestroyImmediate(capsuleCol);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = color;
                capsule.GetComponent<Renderer>().sharedMaterial = mat;
                Debug.LogWarning($"[DemoSceneWirer] {goName}: модель не найдена ({modelFbxPath}), используется capsule-placeholder.");
            }

            // Игровые компоненты
            root.AddComponent<Combat.EnemyHealth>();
            root.AddComponent<Combat.EnemyFSM>();
            root.AddComponent<Combat.DeathToCarcass>();

            // Назначаем EnemyData
            var enemyData = AssetDatabase.LoadAssetAtPath<Combat.EnemyData>(enemyDataPath);
            if (enemyData != null)
            {
                var health = root.GetComponent<Combat.EnemyHealth>();
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

                var fsm = root.GetComponent<Combat.EnemyFSM>();
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

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
                Debug.Log($"[DemoSceneWirer] Prefab создан: {prefabPath}");
            else
                Debug.LogError($"[DemoSceneWirer] Не удалось создать prefab: {prefabPath}");

            return prefab;
        }

        /// <summary>
        /// Назначает AnimatorController на уже существующий prefab если Animator не настроен.
        /// </summary>
        private static void UpdateExistingEnemyPrefabAnimator(string prefabPath, string animatorControllerPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) return;

            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorControllerPath);
            if (animController == null)
            {
                Debug.LogWarning($"[DemoSceneWirer] AnimatorController не найден: {animatorControllerPath}");
                return;
            }

            // Редактируем prefab через PrefabUtility
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = editScope.prefabContentsRoot;
                var animator = root.GetComponent<Animator>();
                if (animator == null)
                    animator = root.AddComponent<Animator>();

                if (animator.runtimeAnimatorController != animController)
                {
                    animator.runtimeAnimatorController = animController;
                    Debug.Log($"[DemoSceneWirer] Обновлён AnimatorController на {prefabPath}: {animatorControllerPath}");
                }
            }
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
        // NPC wiring
        // ─────────────────────────────────────────────────────────────────────

        // KayKit GLB characters for NPC visuals (role → model path)
        private static readonly (string role, string modelPath)[] NpcModelMap = new[]
        {
            ("Guard",      "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Knight.glb"),
            ("Mage",       "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Mage.glb"),
            ("Rogue",      "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Rogue.glb"),
            ("Herbalist",  "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Mage.glb"),
            ("default",    "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Barbarian.glb"),
        };

        // FBX fallback if GLB is not importable
        private const string NpcFbxFallback = "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";

        private static void WireNPCs()
        {
            if (!System.IO.Directory.Exists("Assets/Prefabs/NPCs"))
                System.IO.Directory.CreateDirectory("Assets/Prefabs/NPCs");

            var dialogueMgr = Object.FindObjectOfType<NPC.DialogueManager>();
            if (dialogueMgr == null)
                Debug.LogWarning("[DemoSceneWirer] WireNPCs: DialogueManager не найден в сцене.");

            var npcs = Object.FindObjectsOfType<NPC.NPCInteractable>();
            int wired = 0;

            foreach (var npc in npcs)
            {
                var so = new SerializedObject(npc);
                string npcName = npc.gameObject.name;

                // ── 1. DialogueManager ──────────────────────────────────────
                var dmProp = so.FindProperty("_dialogueManager");
                if (dmProp != null && dmProp.objectReferenceValue == null && dialogueMgr != null)
                    dmProp.objectReferenceValue = dialogueMgr;

                // ── 2. NPCProfile ───────────────────────────────────────────
                var profileProp = so.FindProperty("_profile");
                NPC.NPCProfile profile = null;
                if (profileProp != null)
                {
                    profile = profileProp.objectReferenceValue as NPC.NPCProfile;
                    if (profile == null)
                    {
                        // Exact match first: NPC_Merchant.asset for "Merchant"
                        string exactPath = $"Assets/Data/NPC/Profiles/NPC_{npcName}.asset";
                        profile = AssetDatabase.LoadAssetAtPath<NPC.NPCProfile>(exactPath);

                        if (profile == null)
                        {
                            // Fuzzy search fallback
                            var guids = AssetDatabase.FindAssets($"t:ScriptableObject NPC_{npcName}",
                                new[] { "Assets/Data/NPC/Profiles" });
                            if (guids.Length > 0)
                                profile = AssetDatabase.LoadAssetAtPath<NPC.NPCProfile>(
                                    AssetDatabase.GUIDToAssetPath(guids[0]));
                        }

                        if (profile != null)
                            profileProp.objectReferenceValue = profile;
                        else
                            Debug.LogWarning($"[DemoSceneWirer] WireNPCs: профиль не найден для '{npcName}'");
                    }
                }

                // ── 3. SpeechBubble ─────────────────────────────────────────
                var sbProp = so.FindProperty("_speechBubble");
                if (sbProp != null && sbProp.objectReferenceValue == null)
                {
                    var existingBubble = npc.GetComponentInChildren<NPC.NPCSpeechBubble>();
                    if (existingBubble != null)
                    {
                        sbProp.objectReferenceValue = existingBubble;
                    }
                    else
                    {
                        // Create SpeechBubble child
                        var bubbleGO = new GameObject("SpeechBubble");
                        bubbleGO.transform.SetParent(npc.transform);
                        bubbleGO.transform.localPosition = new Vector3(0f, 2.8f, 0f);
                        bubbleGO.transform.localRotation = Quaternion.identity;
                        bubbleGO.transform.localScale = Vector3.one;

                        var canvas = bubbleGO.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.WorldSpace;

                        var canvasRect = bubbleGO.GetComponent<RectTransform>();
                        canvasRect.sizeDelta = new Vector2(200f, 80f);
                        canvasRect.localScale = Vector3.one * 0.01f;

                        var cg = bubbleGO.AddComponent<CanvasGroup>();
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;

                        // Background
                        var bgGO = new GameObject("Background");
                        bgGO.transform.SetParent(bubbleGO.transform);
                        bgGO.transform.localPosition = Vector3.zero;
                        bgGO.transform.localRotation = Quaternion.identity;
                        bgGO.transform.localScale = Vector3.one;
                        var bgRect = bgGO.AddComponent<RectTransform>();
                        bgRect.anchorMin = Vector2.zero;
                        bgRect.anchorMax = Vector2.one;
                        bgRect.offsetMin = Vector2.zero;
                        bgRect.offsetMax = Vector2.zero;
                        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
                        bgImg.color = new Color(0f, 0f, 0f, 0.7f);

                        // Text
                        var textGO = new GameObject("Text");
                        textGO.transform.SetParent(bubbleGO.transform);
                        textGO.transform.localPosition = Vector3.zero;
                        textGO.transform.localRotation = Quaternion.identity;
                        textGO.transform.localScale = Vector3.one;
                        var textRect = textGO.AddComponent<RectTransform>();
                        textRect.anchorMin = new Vector2(0.05f, 0.05f);
                        textRect.anchorMax = new Vector2(0.95f, 0.95f);
                        textRect.offsetMin = Vector2.zero;
                        textRect.offsetMax = Vector2.zero;
                        var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
                        tmp.fontSize = 14f;
                        tmp.alignment = TMPro.TextAlignmentOptions.Center;
                        tmp.color = Color.white;

                        // Icon
                        var iconGO = new GameObject("Icon");
                        iconGO.transform.SetParent(bubbleGO.transform);
                        iconGO.transform.localPosition = Vector3.zero;
                        iconGO.transform.localRotation = Quaternion.identity;
                        iconGO.transform.localScale = Vector3.one;
                        var iconRect = iconGO.AddComponent<RectTransform>();
                        iconRect.anchorMin = Vector2.zero;
                        iconRect.anchorMax = Vector2.one;
                        iconRect.offsetMin = Vector2.zero;
                        iconRect.offsetMax = Vector2.zero;
                        var iconImg = iconGO.AddComponent<UnityEngine.UI.Image>();
                        iconImg.preserveAspect = true;
                        iconGO.SetActive(false);

                        var speechBubble = bubbleGO.AddComponent<NPC.NPCSpeechBubble>();
                        var bubbleSo = new SerializedObject(speechBubble);
                        bubbleSo.FindProperty("_textField").objectReferenceValue = tmp;
                        bubbleSo.FindProperty("_iconImage").objectReferenceValue = iconImg;
                        bubbleSo.FindProperty("_canvasGroup").objectReferenceValue = cg;
                        bubbleSo.ApplyModifiedPropertiesWithoutUndo();

                        sbProp.objectReferenceValue = speechBubble;
                        Debug.Log($"[DemoSceneWirer] WireNPCs: создан SpeechBubble для '{npcName}'");
                    }
                }

                // ── 4. Visual model ─────────────────────────────────────────
                // Check if there's already a visual child (non-UI, non-SpeechBubble)
                bool hasVisual = false;
                for (int ci = 0; ci < npc.transform.childCount; ci++)
                {
                    var child = npc.transform.GetChild(ci);
                    if (child.name == "SpeechBubble" || child.name == "IconBubble")
                        continue;
                    if (child.GetComponent<Renderer>() != null || child.GetComponent<Animator>() != null)
                    {
                        hasVisual = true;
                        break;
                    }
                }

                if (!hasVisual)
                {
                    string modelPath = ResolveNpcModelPath(npcName);
                    if (modelPath != null)
                    {
                        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                        if (modelAsset != null)
                        {
                            var modelInstance = (GameObject)Object.Instantiate(modelAsset);
                            modelInstance.name = "Model";
                            modelInstance.transform.SetParent(npc.transform);
                            modelInstance.transform.localPosition = Vector3.zero;
                            modelInstance.transform.localRotation = Quaternion.identity;
                            modelInstance.transform.localScale = Vector3.one;
                            Debug.Log($"[DemoSceneWirer] WireNPCs: модель '{System.IO.Path.GetFileName(modelPath)}' добавлена к '{npcName}'");
                        }
                        else
                        {
                            Debug.LogWarning($"[DemoSceneWirer] WireNPCs: модель не загружена: {modelPath}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[DemoSceneWirer] WireNPCs: модель для '{npcName}' не найдена — NPC остаётся без визуала");
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                wired++;
            }

            Debug.Log($"[DemoSceneWirer] WireNPCs: {wired}/{npcs.Length} NPC обработано.");
        }

        private static string ResolveNpcModelPath(string npcName)
        {
            // Try role-based mapping first
            foreach (var (role, path) in NpcModelMap)
            {
                if (role == "default") continue;
                if (npcName.IndexOf(role, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                        return path;
                }
            }

            // Default KayKit character
            foreach (var (role, path) in NpcModelMap)
            {
                if (role == "default")
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                        return path;
                }
            }

            // FBX fallback
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NpcFbxFallback) != null)
                return NpcFbxFallback;

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Audio wiring
        // ─────────────────────────────────────────────────────────────────────

        private static void WireAudio()
        {
            // FootstepSystem on Player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var footstep = player.GetComponent<Audio.FootstepSystem>()
                              ?? player.GetComponentInChildren<Audio.FootstepSystem>();
                if (footstep == null)
                {
                    footstep = player.AddComponent<Audio.FootstepSystem>();
                    Debug.Log("[DemoSceneWirer] FootstepSystem добавлен на Player.");
                }
                if (footstep != null)
                {
                    var so = new SerializedObject(footstep);
                    AssignClipArray(so, "_grassClips", "Assets/Audio/SFX/Footsteps", "grass");
                    AssignClipArray(so, "_stoneClips", "Assets/Audio/SFX/Footsteps", "stone");
                    AssignClipArray(so, "_dirtClips", "Assets/Audio/SFX/Footsteps", "footstep0");
                    AssignClipArray(so, "_woodClips", "Assets/Audio/SFX/Footsteps", "wood");
                    AssignClipArray(so, "_waterClips", "Assets/Audio/SFX/Footsteps", "wet");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[DemoSceneWirer] FootstepSystem — аудио назначено.");
                }

                // Ensure AudioSource on Player
                if (player.GetComponent<AudioSource>() == null)
                    player.AddComponent<AudioSource>();
            }

            // AmbientZones
            var ambientZones = Object.FindObjectsOfType<Audio.AmbientZone>();
            foreach (var zone in ambientZones)
            {
                var so = new SerializedObject(zone);
                string zoneName = zone.gameObject.name.ToLower();

                if (zoneName.Contains("meadow") || zoneName.Contains("forest"))
                {
                    AssignSingleClip(so, "_dayClip", "Assets/Audio/SFX/Ambient", "birds");
                    AssignSingleClip(so, "_nightClip", "Assets/Audio/SFX/Ambient", "crickets");
                }
                else if (zoneName.Contains("city") || zoneName.Contains("town"))
                {
                    AssignSingleClip(so, "_dayClip", "Assets/Audio/SFX/Ambient", "town");
                    AssignSingleClip(so, "_nightClip", "Assets/Audio/SFX/Ambient", "night");
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log($"[DemoSceneWirer] AmbientZones: {ambientZones.Length} зон обработано.");
        }

        private static void AssignClipArray(SerializedObject so, string propName, string folder, string filter)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var guids = AssetDatabase.FindAssets($"t:AudioClip {filter}", new[] { folder });
            if (guids.Length == 0) return;

            int count = Mathf.Min(guids.Length, 4); // max 4 clips per surface
            prop.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }
        }

        private static void AssignSingleClip(SerializedObject so, string propName, string folder, string filter)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var guids = AssetDatabase.FindAssets($"t:AudioClip {filter}", new[] { folder });
            if (guids.Length == 0) return;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                prop.objectReferenceValue = clip;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Debug Reset Button
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureDebugResetButton()
        {
            if (Object.FindObjectOfType<UI.DebugResetButton>() != null)
            {
                Debug.Log("[DemoSceneWirer] DebugResetButton уже в сцене.");
                return;
            }

            var go = new GameObject("DebugResetButton");
            go.AddComponent<UI.DebugResetButton>();
            Debug.Log("[DemoSceneWirer] DebugResetButton добавлен в сцену.");

            // FPS Counter
            if (Object.FindObjectOfType<UI.FPSCounter>() == null)
            {
                var fps = new GameObject("FPSCounter");
                fps.AddComponent<UI.FPSCounter>();
                Debug.Log("[DemoSceneWirer] FPSCounter добавлен.");
            }
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
