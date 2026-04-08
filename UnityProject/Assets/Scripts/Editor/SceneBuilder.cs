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
