using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Editor.MapGen;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Editor script to build scenes programmatically.
    /// Called via -executeMethod ZeldaDaughter.Editor.SceneBuilder.CreateTestScene
    /// </summary>
    public static class SceneBuilder
    {
        [MenuItem("ZeldaDaughter/Create Test Scene")]
        public static void CreateTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Ground", new Color(0.3f, 0.5f, 0.2f));

            // Directional Light (Sun)
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(35f, 170f, 0f);
            // Add UniversalAdditionalLightData for URP
            if (lightGO.GetComponent<UniversalAdditionalLightData>() == null)
                lightGO.AddComponent<UniversalAdditionalLightData>();

            // Camera with isometric setup
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.75f, 0.9f);
            cameraGO.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            cameraGO.transform.position = Quaternion.Euler(35f, 45f, 0f) * new Vector3(0f, 0f, -20f);
            // Add UniversalAdditionalCameraData for URP
            if (cameraGO.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGO.AddComponent<UniversalAdditionalCameraData>();
            // Add IsometricCamera component
            cameraGO.AddComponent<World.IsometricCamera>();

            // Player placeholder (capsule)
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Player", Color.blue);

            // GameBootstrap
            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<World.GameBootstrap>();

            // DayNightCycle
            var dayNight = new GameObject("DayNightCycle");
            var cycle = dayNight.AddComponent<World.DayNightCycle>();

            // Some test objects to verify scale
            PlaceTestObject("Tree_Test", new Vector3(5f, 1.5f, 3f), new Color(0.2f, 0.6f, 0.1f), PrimitiveType.Cylinder);
            PlaceTestObject("Rock_Test", new Vector3(-3f, 0.5f, 7f), Color.gray, PrimitiveType.Sphere);
            PlaceTestObject("Building_Test", new Vector3(10f, 2f, -5f), new Color(0.6f, 0.4f, 0.2f), PrimitiveType.Cube);

            // Save scene
            string scenePath = "Assets/Scenes/TestScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SceneBuilder] Test scene created at {scenePath}");
        }

        [MenuItem("ZeldaDaughter/Scenes/Build Stage1 Scene")]
        public static void CreateStage1Scene()
        {
            // Убеждаемся что папка Scenes существует
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Directional Light (Sun) — используется DayNightCycle
            var lightGO = new GameObject("Directional Light");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, 170f, 0f);
            if (lightGO.GetComponent<UniversalAdditionalLightData>() == null)
                lightGO.AddComponent<UniversalAdditionalLightData>();
            // DayNightCycle на том же GO что и свет
            var dayNight = lightGO.AddComponent<World.DayNightCycle>();
            var dayNightSO = new SerializedObject(dayNight);
            dayNightSO.FindProperty("_directionalLight").objectReferenceValue = sun;
            dayNightSO.ApplyModifiedProperties();

            // Player
            const string playerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            GameObject player;
            var spawnPos = new Vector3(-40f, 0f, 0f);
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.position = spawnPos;
            }
            else
            {
                Debug.LogWarning($"[SceneBuilder] Player prefab not found at '{playerPrefabPath}', creating capsule placeholder.");
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                player.transform.position = spawnPos;
            }

            // Isometric Camera
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.75f, 0.9f);
            if (cameraGO.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGO.AddComponent<UniversalAdditionalCameraData>();
            var isoCam = cameraGO.AddComponent<World.IsometricCamera>();
            isoCam.SetTarget(player.transform);

            // GameBootstrap
            var bootstrapGO = new GameObject("GameBootstrap");
            bootstrapGO.AddComponent<World.GameBootstrap>();

            // GestureDispatcher
            var inputGO = new GameObject("InputSystem");
            inputGO.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();

            // Ambient zones
            CreateAmbientZone("AmbientZone_Forest", new Vector3(-35f, 0f, -20f), 25f);
            CreateAmbientZone("AmbientZone_River",  new Vector3(42f,  0f, 0f),   15f);
            CreateAmbientZone("AmbientZone_Town",   new Vector3(20f,  0f, 8f),   20f);
            CreateAmbientZone("AmbientZone_Meadow", new Vector3(-10f, 0f, 15f),  18f);

            // EventSystem для UI
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();

            // Размещаем регион
            // Application.dataPath = .../UnityProject/Assets; убираем "/Assets" чтобы получить корень проекта
            const string regionConfigPath = "Assets/Content/Configs/region_startmeadow.json";
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absoluteConfigPath = Path.GetFullPath(Path.Combine(projectRoot, regionConfigPath));
            if (File.Exists(absoluteConfigPath))
            {
                RegionPlacer.PlaceRegion(absoluteConfigPath);
            }
            else
            {
                Debug.LogWarning($"[SceneBuilder] Region config not found at '{absoluteConfigPath}'. Skipping region placement.");
            }

            // Сохраняем сцену
            const string scenePath = "Assets/Scenes/Stage1.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SceneBuilder] Stage1 scene saved at '{scenePath}'.");

            // Добавляем в Build Settings
            AddSceneToBuildSettings(scenePath);
        }

        [MenuItem("ZeldaDaughter/Scenes/Build Stage 3")]
        public static void CreateStage3Scene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // Загружаем Stage1 как базу — если есть
            const string stage1Path = "Assets/Scenes/Stage1.unity";
            if (File.Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), stage1Path))))
            {
                EditorSceneManager.OpenScene(stage1Path, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Debug.LogWarning("[SceneBuilder] Stage1.unity не найдена, создаём пустую сцену для Stage3.");
            }

            // --- Компоненты на Player ---
            var player = GameObject.Find("Player");
            if (player != null)
            {
                AddComponentIfMissing<ZeldaDaughter.Inventory.WeightSystem>(player);
                AddComponentIfMissing<ZeldaDaughter.UI.CraftFeedback>(player);
                AddComponentIfMissing<ZeldaDaughter.World.WorldInteractionSystem>(player);
                AddComponentIfMissing<ZeldaDaughter.World.WorldPlacement>(player);

                // Привязываем InventoryConfig к WeightSystem через SerializedObject
                var weightSys = player.GetComponent<ZeldaDaughter.Inventory.WeightSystem>();
                if (weightSys != null)
                {
                    var invConfig = AssetDatabase.LoadAssetAtPath<InventoryConfig>("Assets/Data/InventoryConfig.asset");
                    var charMove = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();
                    if (invConfig != null || charMove != null)
                    {
                        var so = new SerializedObject(weightSys);
                        if (invConfig != null)
                            so.FindProperty("_config").objectReferenceValue = invConfig;
                        if (charMove != null)
                            so.FindProperty("_characterMovement").objectReferenceValue = charMove;
                        so.ApplyModifiedProperties();
                    }
                }

                // Привязываем CraftRecipeDatabase к WorldPlacement
                var worldPlacement = player.GetComponent<ZeldaDaughter.World.WorldPlacement>();
                if (worldPlacement != null)
                {
                    var db = AssetDatabase.LoadAssetAtPath<CraftRecipeDatabase>("Assets/Data/Recipes/CraftRecipeDatabase.asset");
                    if (db != null)
                    {
                        var so = new SerializedObject(worldPlacement);
                        var dbProp = so.FindProperty("_recipeDatabase");
                        if (dbProp != null)
                        {
                            dbProp.objectReferenceValue = db;
                            so.ApplyModifiedProperties();
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] Player GameObject не найден в сцене. Пропускаем привязку компонентов.");
            }

            // EventSystem — добавляем если нет
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            // --- Canvas: RadialMenuCanvas ---
            var radialCanvas = CreateCanvas("RadialMenuCanvas", 100);
            var radialMenu = radialCanvas.gameObject.AddComponent<ZeldaDaughter.UI.RadialMenuController>();
            radialCanvas.gameObject.AddComponent<CanvasGroup>();

            // MenuRoot — дочерний RectTransform для сброса позиции/масштаба при анимации
            var menuRoot = new GameObject("MenuRoot").AddComponent<RectTransform>();
            menuRoot.SetParent(radialCanvas.transform, false);
            menuRoot.anchoredPosition = Vector2.zero;

            // Привязываем _menuRoot и _canvasGroup через SerializedObject
            {
                var so = new SerializedObject(radialMenu);
                so.FindProperty("_menuRoot").objectReferenceValue = menuRoot;
                so.FindProperty("_canvasGroup").objectReferenceValue = radialCanvas.GetComponent<CanvasGroup>();
                if (player != null)
                    so.FindProperty("_playerTransform").objectReferenceValue = player.transform;

                // Создаём 3 сектора
                var radialMenuConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.UI.RadialMenuConfig>("Assets/Data/RadialMenuConfig.asset");
                if (radialMenuConfig != null)
                    so.FindProperty("_config").objectReferenceValue = radialMenuConfig;

                var sectorsProp = so.FindProperty("_sectors");
                sectorsProp.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var sectorGO = new GameObject($"RadialMenuSector_{i}").AddComponent<RectTransform>();
                    sectorGO.SetParent(menuRoot, false);
                    var sectorImg = sectorGO.gameObject.AddComponent<Image>();
                    sectorImg.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
                    var sector = sectorGO.gameObject.AddComponent<ZeldaDaughter.UI.RadialMenuSector>();
                    sectorsProp.GetArrayElementAtIndex(i).objectReferenceValue = sector;
                }
                so.ApplyModifiedProperties();
            }

            // --- Canvas: InventoryCanvas ---
            var inventoryCanvas = CreateCanvas("InventoryCanvas", 101);

            // InventoryPanel
            var inventoryPanelGO = new GameObject("InventoryPanel");
            inventoryPanelGO.transform.SetParent(inventoryCanvas.transform, false);
            var inventoryPanel = inventoryPanelGO.AddComponent<ZeldaDaughter.UI.InventoryPanel>();
            var inventoryPanelGroup = inventoryPanelGO.AddComponent<CanvasGroup>();
            {
                var so = new SerializedObject(inventoryPanel);
                so.FindProperty("_canvasGroup").objectReferenceValue = inventoryPanelGroup;
                so.ApplyModifiedProperties();
            }

            // ItemInfoPopup
            var itemInfoGO = new GameObject("ItemInfoPopup");
            itemInfoGO.transform.SetParent(inventoryCanvas.transform, false);
            var itemInfoPopup = itemInfoGO.AddComponent<ZeldaDaughter.UI.ItemInfoPopup>();
            var itemInfoGroup = itemInfoGO.AddComponent<CanvasGroup>();
            {
                var so = new SerializedObject(itemInfoPopup);
                so.FindProperty("_canvasGroup").objectReferenceValue = itemInfoGroup;
                so.ApplyModifiedProperties();
            }

            // InventoryDragHandler с drag-иконкой
            var dragHandlerGO = new GameObject("InventoryDragHandler");
            dragHandlerGO.transform.SetParent(inventoryCanvas.transform, false);
            var dragHandler = dragHandlerGO.AddComponent<ZeldaDaughter.UI.InventoryDragHandler>();
            {
                // Drag-иконка: Image + CanvasGroup как дочерний объект
                var dragIconGO = new GameObject("DragIcon");
                dragIconGO.transform.SetParent(dragHandlerGO.transform, false);
                var dragImg = dragIconGO.AddComponent<Image>();
                dragImg.raycastTarget = false;
                var dragIconGroup = dragIconGO.AddComponent<CanvasGroup>();
                dragIconGroup.blocksRaycasts = false;

                var panelRect = inventoryPanelGO.GetComponent<RectTransform>();
                var db = AssetDatabase.LoadAssetAtPath<CraftRecipeDatabase>("Assets/Data/Recipes/CraftRecipeDatabase.asset");

                var so = new SerializedObject(dragHandler);
                so.FindProperty("_dragIcon").objectReferenceValue = dragImg;
                so.FindProperty("_dragIconGroup").objectReferenceValue = dragIconGroup;
                so.FindProperty("_inventoryPanel").objectReferenceValue = inventoryPanel;
                so.FindProperty("_panelRect").objectReferenceValue = panelRect;
                if (db != null)
                    so.FindProperty("_recipeDatabase").objectReferenceValue = db;
                so.ApplyModifiedProperties();
            }

            // --- Canvas: StationCanvas ---
            var stationCanvas = CreateCanvas("StationCanvas", 102);
            var stationUI = stationCanvas.gameObject.AddComponent<ZeldaDaughter.UI.StationUI>();
            var stationGroup = stationCanvas.gameObject.AddComponent<CanvasGroup>();
            {
                var db = AssetDatabase.LoadAssetAtPath<CraftRecipeDatabase>("Assets/Data/Recipes/CraftRecipeDatabase.asset");
                var so = new SerializedObject(stationUI);
                so.FindProperty("_canvasGroup").objectReferenceValue = stationGroup;
                if (db != null)
                    so.FindProperty("_recipeDatabase").objectReferenceValue = db;
                so.ApplyModifiedProperties();
            }

            // --- Canvas: LongPressCanvas ---
            var longPressCanvas = CreateCanvas("LongPressCanvas", 99);
            var longPressIndicator = longPressCanvas.gameObject.AddComponent<ZeldaDaughter.UI.LongPressIndicator>();
            var longPressGroup = longPressCanvas.gameObject.AddComponent<CanvasGroup>();
            {
                var so = new SerializedObject(longPressIndicator);
                so.FindProperty("_canvasGroup").objectReferenceValue = longPressGroup;
                if (player != null)
                    so.FindProperty("_followTarget").objectReferenceValue = player.transform;
                so.ApplyModifiedProperties();
            }

            // --- Станки в сцене ---
            PlaceStation(StationType.Smelter, GetSmeltPosition());
            PlaceStation(StationType.Anvil, GetAnvilPosition());

            // Сохраняем
            const string scenePath = "Assets/Scenes/Stage3.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            Debug.Log($"[SceneBuilder] Stage3 scene saved at '{scenePath}'.");
            AddSceneToBuildSettings(scenePath);
        }

        [MenuItem("ZeldaDaughter/Scenes/Build Stage 4")]
        public static void CreateStage4Scene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // Загружаем Stage3 как базу — если нет, берём Stage1
            const string stage3Path = "Assets/Scenes/Stage3.unity";
            const string stage1Path = "Assets/Scenes/Stage1.unity";

            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            if (File.Exists(Path.GetFullPath(Path.Combine(projectRoot, stage3Path))))
            {
                EditorSceneManager.OpenScene(stage3Path, OpenSceneMode.Single);
            }
            else if (File.Exists(Path.GetFullPath(Path.Combine(projectRoot, stage1Path))))
            {
                EditorSceneManager.OpenScene(stage1Path, OpenSceneMode.Single);
                Debug.LogWarning("[SceneBuilder] Stage3.unity не найдена, используем Stage1 как базу для Stage4.");
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Debug.LogWarning("[SceneBuilder] Ни Stage3, ни Stage1 не найдены. Создаём пустую сцену для Stage4.");
            }

            // --- Компоненты на Player ---
            var player = GameObject.Find("Player");
            if (player != null)
            {
                AddComponentIfMissing<ZeldaDaughter.Combat.PlayerHealthState>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.CombatController>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.WeaponEquipSystem>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.RestZoneDetector>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.HungerSystem>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.WoundEffectApplier>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.FoodConsumption>(player);
                AddComponentIfMissing<ZeldaDaughter.Combat.KnockoutSystem>(player);

                // Hitbox на дочернем объекте (удар игрока)
                var existingHitbox = player.transform.Find("PlayerHitbox");
                if (existingHitbox == null)
                {
                    var hitboxGo = new GameObject("PlayerHitbox");
                    hitboxGo.transform.SetParent(player.transform);
                    hitboxGo.transform.localPosition = new Vector3(0f, 0.8f, 0.7f);
                    var hitboxCol = hitboxGo.AddComponent<BoxCollider>();
                    hitboxCol.isTrigger = true;
                    hitboxCol.size = new Vector3(0.6f, 0.6f, 0.6f);
                    hitboxGo.AddComponent<ZeldaDaughter.Combat.HitboxTrigger>();
                    hitboxGo.SetActive(false); // активируется только во время удара
                }

                // Привязываем CombatConfig
                var combatConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.CombatConfig>("Assets/Data/Combat/CombatConfig.asset");
                if (combatConfig != null)
                {
                    var cfgHealthState = player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
                    if (cfgHealthState != null)
                    {
                        var soH = new SerializedObject(cfgHealthState);
                        var configProp = soH.FindProperty("_config");
                        if (configProp != null)
                        {
                            configProp.objectReferenceValue = combatConfig;
                            soH.ApplyModifiedProperties();
                        }
                    }

                    var cfgCombatCtrl = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                    if (cfgCombatCtrl != null)
                    {
                        var soC = new SerializedObject(cfgCombatCtrl);
                        var configProp = soC.FindProperty("_config");
                        if (configProp != null)
                        {
                            configProp.objectReferenceValue = combatConfig;
                            soC.ApplyModifiedProperties();
                        }
                    }

                    var cfgHungerSys = player.GetComponent<ZeldaDaughter.Combat.HungerSystem>();
                    if (cfgHungerSys != null)
                    {
                        var soHu = new SerializedObject(cfgHungerSys);
                        var configProp = soHu.FindProperty("_config");
                        if (configProp != null)
                        {
                            configProp.objectReferenceValue = combatConfig;
                            soHu.ApplyModifiedProperties();
                        }
                    }

                    var cfgKnockoutSys = player.GetComponent<ZeldaDaughter.Combat.KnockoutSystem>();
                    if (cfgKnockoutSys != null)
                    {
                        var soK = new SerializedObject(cfgKnockoutSys);
                        var configProp = soK.FindProperty("_config");
                        if (configProp != null)
                        {
                            configProp.objectReferenceValue = combatConfig;
                            soK.ApplyModifiedProperties();
                        }
                    }
                }

                // --- Привязка SerializedField ссылок ---

                // Загружаем WoundConfig SO
                var woundPuncture  = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>("Assets/Data/Combat/WoundConfig_Puncture.asset");
                var woundFracture  = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>("Assets/Data/Combat/WoundConfig_Fracture.asset");
                var woundBurn      = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>("Assets/Data/Combat/WoundConfig_Burn.asset");
                var woundPoison    = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>("Assets/Data/Combat/WoundConfig_Poison.asset");

                var charMovement = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();
                var charAutoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();

                // Найти GestureDispatcher — на InputSystem GO или на Player
                ZeldaDaughter.Input.GestureDispatcher gestureDispatcher = null;
                var inputSystemGO = GameObject.Find("InputSystem");
                if (inputSystemGO != null)
                    inputSystemGO.TryGetComponent(out gestureDispatcher);
                if (gestureDispatcher == null)
                    gestureDispatcher = player.GetComponentInChildren<ZeldaDaughter.Input.GestureDispatcher>();
                if (gestureDispatcher == null)
                    gestureDispatcher = Object.FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();

                // Hitbox дочерний объект
                var hitboxTransform = player.transform.Find("PlayerHitbox");
                var hitboxTrigger = hitboxTransform != null
                    ? hitboxTransform.GetComponent<ZeldaDaughter.Combat.HitboxTrigger>()
                    : null;

                var weaponEquip  = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
                var combatCtrl   = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                var healthState  = player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
                var woundApplier = player.GetComponent<ZeldaDaughter.Combat.WoundEffectApplier>();
                var hungerSys2   = player.GetComponent<ZeldaDaughter.Combat.HungerSystem>();
                var knockoutSys2 = player.GetComponent<ZeldaDaughter.Combat.KnockoutSystem>();

                // 1. CombatController._hitbox, _weaponEquip, _autoMove
                if (combatCtrl != null)
                {
                    var so = new SerializedObject(combatCtrl);
                    if (hitboxTrigger != null)
                        so.FindProperty("_hitbox").objectReferenceValue = hitboxTrigger;
                    else
                        Debug.LogWarning("[SceneBuilder] HitboxTrigger не найден на PlayerHitbox.");
                    if (weaponEquip != null)
                        so.FindProperty("_weaponEquip").objectReferenceValue = weaponEquip;
                    if (charAutoMove != null)
                        so.FindProperty("_autoMove").objectReferenceValue = charAutoMove;
                    so.ApplyModifiedProperties();
                }

                // 2. PlayerHealthState._woundConfigs
                if (healthState != null)
                {
                    var so = new SerializedObject(healthState);
                    var woundsProp = so.FindProperty("_woundConfigs");
                    woundsProp.arraySize = 4;
                    woundsProp.GetArrayElementAtIndex(0).objectReferenceValue = woundPuncture;
                    woundsProp.GetArrayElementAtIndex(1).objectReferenceValue = woundFracture;
                    woundsProp.GetArrayElementAtIndex(2).objectReferenceValue = woundBurn;
                    woundsProp.GetArrayElementAtIndex(3).objectReferenceValue = woundPoison;
                    so.ApplyModifiedProperties();

                    if (woundPuncture == null || woundFracture == null || woundBurn == null || woundPoison == null)
                        Debug.LogWarning("[SceneBuilder] Часть WoundConfig SO не найдена. Запустите ZeldaDaughter/Data/Build Combat Data.");
                }

                // 3. WoundEffectApplier._woundConfigs, _movement
                if (woundApplier != null)
                {
                    var so = new SerializedObject(woundApplier);
                    var woundsProp = so.FindProperty("_woundConfigs");
                    woundsProp.arraySize = 4;
                    woundsProp.GetArrayElementAtIndex(0).objectReferenceValue = woundPuncture;
                    woundsProp.GetArrayElementAtIndex(1).objectReferenceValue = woundFracture;
                    woundsProp.GetArrayElementAtIndex(2).objectReferenceValue = woundBurn;
                    woundsProp.GetArrayElementAtIndex(3).objectReferenceValue = woundPoison;
                    if (charMovement != null)
                        so.FindProperty("_movement").objectReferenceValue = charMovement;
                    so.ApplyModifiedProperties();
                }

                // 4. HungerSystem._movement
                if (hungerSys2 != null && charMovement != null)
                {
                    var so = new SerializedObject(hungerSys2);
                    so.FindProperty("_movement").objectReferenceValue = charMovement;
                    so.ApplyModifiedProperties();
                }

                // 5. KnockoutSystem._gestureDispatcher
                if (knockoutSys2 != null && gestureDispatcher != null)
                {
                    var so = new SerializedObject(knockoutSys2);
                    so.FindProperty("_gestureDispatcher").objectReferenceValue = gestureDispatcher;
                    so.ApplyModifiedProperties();
                }
                else if (knockoutSys2 != null)
                {
                    Debug.LogWarning("[SceneBuilder] GestureDispatcher не найден. KnockoutSystem._gestureDispatcher не привязан.");
                }

                // 6. TapInteractionManager._combatController
                var tapManager = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
                if (tapManager != null && combatCtrl != null)
                {
                    var so = new SerializedObject(tapManager);
                    so.FindProperty("_combatController").objectReferenceValue = combatCtrl;
                    so.ApplyModifiedProperties();
                }
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] Player GameObject не найден. Пропускаем Combat-компоненты.");
            }

            // --- FadeOverlay Canvas (для сна и нокаута) ---
            if (GameObject.Find("FadeOverlayCanvas") == null)
            {
                var fadeCanvas = CreateCanvas("FadeOverlayCanvas", 200);
                var fadeCanvasGroup = fadeCanvas.gameObject.AddComponent<CanvasGroup>();

                // Полноэкранный чёрный Image
                var fadeImageGo = new GameObject("FadeImage");
                fadeImageGo.transform.SetParent(fadeCanvas.transform, false);
                var fadeRect = fadeImageGo.AddComponent<RectTransform>();
                fadeRect.anchorMin = Vector2.zero;
                fadeRect.anchorMax = Vector2.one;
                fadeRect.offsetMin = Vector2.zero;
                fadeRect.offsetMax = Vector2.zero;
                var fadeImage = fadeImageGo.AddComponent<Image>();
                fadeImage.color = Color.black;
                fadeImage.raycastTarget = false;

                // Начальное состояние — скрыт
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;

                // FadeOverlay компонент + привязка _canvasGroup
                var fadeOverlay = fadeCanvas.gameObject.AddComponent<ZeldaDaughter.UI.FadeOverlay>();
                {
                    var so = new SerializedObject(fadeOverlay);
                    so.FindProperty("_canvasGroup").objectReferenceValue = fadeCanvasGroup;
                    so.ApplyModifiedProperties();
                }
            }

            // EventSystem — добавляем если нет
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            // --- Зоны спавна врагов ---
            PlaceEnemySpawnZone(
                "SpawnZone_Wolves",
                new Vector3(-20f, 0f, 15f),
                "Assets/Prefabs/Enemies/Wolf.prefab",
                "Assets/Data/Combat/EnemyData_Wolf.asset",
                maxEnemies: 3,
                spawnRadius: 12f
            );

            PlaceEnemySpawnZone(
                "SpawnZone_Boars",
                new Vector3(25f, 0f, 20f),
                "Assets/Prefabs/Enemies/Boar.prefab",
                "Assets/Data/Combat/EnemyData_Boar.asset",
                maxEnemies: 2,
                spawnRadius: 10f
            );

            // --- Кровать в таверне ---
            PlaceBed();

            // Сохраняем
            const string scenePath = "Assets/Scenes/Stage4.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            Debug.Log($"[SceneBuilder] Stage4 scene saved at '{scenePath}'.");
            AddSceneToBuildSettings(scenePath);
        }

        private static void PlaceEnemySpawnZone(
            string goName,
            Vector3 position,
            string enemyPrefabPath,
            string enemyDataPath,
            int maxEnemies,
            float spawnRadius)
        {
            // Не дублируем если уже есть
            if (GameObject.Find(goName) != null)
            {
                Debug.Log($"[SceneBuilder] {goName} уже присутствует, пропускаем.");
                return;
            }

            var zoneGo = new GameObject(goName);
            zoneGo.transform.position = position;

            var spawnZone = zoneGo.AddComponent<ZeldaDaughter.Combat.EnemySpawnZone>();

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath);
            var enemyData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.EnemyData>(enemyDataPath);

            var so = new SerializedObject(spawnZone);
            if (enemyPrefab != null)
                so.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab;
            else
                Debug.LogWarning($"[SceneBuilder] Enemy prefab не найден: {enemyPrefabPath}. Запустите ZeldaDaughter/Prefabs/Build Enemy Prefabs.");

            if (enemyData != null)
                so.FindProperty("_enemyData").objectReferenceValue = enemyData;
            else
                Debug.LogWarning($"[SceneBuilder] Enemy data не найдена: {enemyDataPath}. Запустите ZeldaDaughter/Data/Build Combat Data.");

            so.FindProperty("_maxEnemies").intValue = maxEnemies;
            so.FindProperty("_spawnRadius").floatValue = spawnRadius;
            so.ApplyModifiedProperties();

            Debug.Log($"[SceneBuilder] SpawnZone '{goName}' размещена в позиции {position}.");
        }

        private static void PlaceBed()
        {
            const string bedName = "Bed";
            if (GameObject.Find(bedName) != null)
            {
                Debug.Log("[SceneBuilder] Bed уже присутствует, пропускаем.");
                return;
            }

            // Ищем таверну по имени или берём позицию по умолчанию
            Vector3 bedPosition = GetBedPosition();

            // Пробуем загрузить prefab кровати
            const string bedPrefabPath = "Assets/Prefabs/World/Bed.prefab";
            var bedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bedPrefabPath);

            GameObject bedGo;
            if (bedPrefab != null)
            {
                bedGo = (GameObject)PrefabUtility.InstantiatePrefab(bedPrefab);
                bedGo.transform.position = bedPosition;
            }
            else
            {
                // Placeholder: плоский куб
                bedGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bedGo.name = bedName;
                bedGo.transform.position = bedPosition;
                bedGo.transform.localScale = new Vector3(1f, 0.4f, 2f);
                var mat = CreateMaterial("Bed", new Color(0.6f, 0.3f, 0.1f));
                bedGo.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // Точка взаимодействия
            var interPoint = new GameObject("InteractionPoint");
            interPoint.transform.SetParent(bedGo.transform);
            interPoint.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            // SleepInteraction
            var sleepInteraction = bedGo.AddComponent<ZeldaDaughter.Combat.SleepInteraction>();
            {
                var so = new SerializedObject(sleepInteraction);
                var interPointProp = so.FindProperty("_interactionPoint");
                if (interPointProp != null)
                {
                    interPointProp.objectReferenceValue = interPoint.transform;
                    so.ApplyModifiedProperties();
                }
            }

            // Highlight для интерактивности
            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(bedGo);

            Debug.Log($"[SceneBuilder] Кровать размещена в позиции {bedPosition}.");
        }

        private static Vector3 GetBedPosition()
        {
            // Ищем таверну по возможным именам
            string[] tavernNames = { "Tavern", "NPC_Barman", "Building_Tavern", "Inn" };
            foreach (var name in tavernNames)
            {
                var found = GameObject.Find(name);
                if (found != null)
                    return found.transform.position + new Vector3(2f, 0f, 1f);
            }

            // Дефолтная позиция рядом с городом
            return new Vector3(22f, 0f, 8f);
        }

        // Создаёт Canvas в режиме Screen Space Overlay
        private static Canvas CreateCanvas(string goName, int sortOrder)
        {
            var go = new GameObject(goName);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // Размещает станок в мировых координатах
        private static void PlaceStation(StationType type, Vector3 position)
        {
            string goName = type == StationType.Smelter ? "Smelter" : "Anvil";

            // Проверяем — вдруг станок уже есть
            if (GameObject.Find(goName) != null)
            {
                Debug.Log($"[SceneBuilder] {goName} уже присутствует в сцене, пропускаем.");
                return;
            }

            // Пробуем загрузить prefab из папки Prefabs
            string prefabPath = $"Assets/Prefabs/World/{goName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            GameObject stationGO;
            if (prefab != null)
            {
                stationGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                stationGO.transform.position = position;
            }
            else
            {
                stationGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stationGO.name = goName;
                stationGO.transform.position = position;
                stationGO.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
                stationGO.GetComponent<Renderer>().sharedMaterial = CreateStationMaterial(type);
            }

            AddComponentIfMissing<ZeldaDaughter.World.StationInteractable>(stationGO);
            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(stationGO);

            var stationComp = stationGO.GetComponent<ZeldaDaughter.World.StationInteractable>();
            if (stationComp != null)
            {
                var so = new SerializedObject(stationComp);
                so.FindProperty("_stationType").enumValueIndex = (int)type;
                so.FindProperty("_interactionRange").floatValue = 2.5f;
                so.ApplyModifiedProperties();
            }

            Debug.Log($"[SceneBuilder] Станок {goName} размещён в позиции {position}.");
        }

        // Позиция плавильни: рядом с кузнецом (NPC Blacksmith) или по умолчанию
        private static Vector3 GetSmeltPosition()
        {
            var blacksmith = GameObject.Find("NPC_Blacksmith");
            return blacksmith != null
                ? blacksmith.transform.position + new Vector3(2f, 0f, 0f)
                : new Vector3(15f, 0f, 5f);
        }

        private static Vector3 GetAnvilPosition()
        {
            var blacksmith = GameObject.Find("NPC_Blacksmith");
            return blacksmith != null
                ? blacksmith.transform.position + new Vector3(-2f, 0f, 0f)
                : new Vector3(17f, 0f, 5f);
        }

        private static Material CreateStationMaterial(StationType type)
        {
            var color = type == StationType.Smelter
                ? new Color(0.8f, 0.4f, 0.1f)   // оранжевый — плавильня
                : new Color(0.4f, 0.4f, 0.5f);  // серый металл — наковальня
            string name = type == StationType.Smelter ? "Smelter" : "Anvil";
            return CreateMaterial(name, color);
        }

        private static void AddComponentIfMissing<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();
        }

        private static void CreateAmbientZone(string goName, Vector3 position, float radius)
        {
            var go = new GameObject(goName);
            go.transform.position = position;
            // AmbientZone требует AudioSource и SphereCollider через [RequireComponent]
            // Добавляем их заранее, чтобы настроить до AddComponent<AmbientZone>
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.maxDistance = radius;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            var sphereCol = go.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = radius;
            go.AddComponent<AmbientZone>();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == scenePath) return; // уже есть
            }
            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(newScenes, 0);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"[SceneBuilder] Added '{scenePath}' to Build Settings.");
        }

        private static void PlaceTestObject(string name, Vector3 position, Color color, PrimitiveType type)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.position = position;
            obj.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name, color);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            // Use URP Lit shader
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard"); // Fallback
            var mat = new Material(shader) { color = color };
            string matPath = $"Assets/Materials/{name}_Mat.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }
    }
}
