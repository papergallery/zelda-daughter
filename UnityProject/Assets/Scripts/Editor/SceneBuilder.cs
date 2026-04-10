using System.IO;
using TMPro;
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
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Editor script to build scenes programmatically.
    /// Called via -executeMethod ZeldaDaughter.Editor.SceneBuilder.CreateTestScene
    /// </summary>
    public static class SceneBuilder
    {
        // FBX paths for world objects
        private const string FbxBasePath = "Assets/Models/Kenney/NatureKit/Models/FBX format/";
        private const string FbxTownPath = "Assets/Models/Kenney/FantasyTownKit/Models/FBX format/";
        private const string FbxCharacterPath = "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        [MenuItem("ZeldaDaughter/Create Test Scene")]
        public static void CreateTestScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Ground", new Color(0.3f, 0.5f, 0.2f));

            // Directional Light (Sun)
            var lightGO = new GameObject("Directional Light");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(35f, 170f, 0f);
            if (lightGO.GetComponent<UniversalAdditionalLightData>() == null)
                lightGO.AddComponent<UniversalAdditionalLightData>();

            // Player — prefab → FBX → capsule fallback
            var player = CreatePlayer();

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
            isoCam.SetScreenOffset(new Vector3(0f, 3f, 0f));

            // EventSystem (обязательно для GestureDispatcher)
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();

            // InputSystem
            var inputGO = new GameObject("InputSystem");
            inputGO.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();

            // GameBootstrap
            var bootstrapGO = new GameObject("GameBootstrap");
            bootstrapGO.AddComponent<World.GameBootstrap>();

            // DayNightCycle
            var dayNightGO = new GameObject("DayNightCycle");
            var cycle = dayNightGO.AddComponent<World.DayNightCycle>();
            var cycleSO = new SerializedObject(cycle);
            cycleSO.FindProperty("_directionalLight").objectReferenceValue = sun;
            cycleSO.ApplyModifiedProperties();

            // World objects: trees, stones, buildings from real FBX
            PlaceWorldObjects();

            // Save scene
            const string scenePath = "Assets/Scenes/TestScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            Debug.Log($"[SceneBuilder] Test scene created at {scenePath}");
        }

        /// <summary>Создаёт Player: prefab → FBX персонажа → capsule fallback. Все компоненты гарантированы.</summary>
        private static GameObject CreatePlayer()
        {
            EnsurePlayerTag();

            GameObject player = null;

            // Попытка 1: готовый prefab
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.position = new Vector3(0f, 0f, 0f);
                Debug.Log("[SceneBuilder] Player prefab загружен из Assets/Prefabs/Player/Player.prefab");
            }

            // Попытка 2: FBX персонажа
            if (player == null)
            {
                // Убедиться что FBX импортирует материалы
                var importer = AssetImporter.GetAtPath(FbxCharacterPath) as ModelImporter;
                if (importer != null && importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.SaveAndReimport();
                }

                var characterFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxCharacterPath);
                if (characterFbx != null)
                {
                    player = new GameObject("Player");
                    player.transform.position = new Vector3(0f, 0.05f, 0f);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(characterFbx);
                    model.name = "Model";
                    model.transform.SetParent(player.transform);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;

                    // Проверить размер модели и масштабировать если слишком маленькая
                    var bounds = CalculateBounds(model);
                    if (bounds.size.y < 0.5f && bounds.size.y > 0.001f)
                    {
                        float targetHeight = 1.8f;
                        float scale = targetHeight / bounds.size.y;
                        model.transform.localScale = Vector3.one * scale;
                        Debug.Log($"[SceneBuilder] Model масштабирован: {scale:F1}x (высота была {bounds.size.y:F3})");
                    }

                    // Назначить URP материал если модель пришла без материалов
                    AssignUrpMaterialIfMissing(model);

                    Debug.Log($"[SceneBuilder] Player создан из FBX: {FbxCharacterPath}");
                }
            }

            // Fallback: капсула
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                player.transform.position = new Vector3(0f, 1f, 0f);
                player.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Player", Color.blue);
                Debug.LogWarning("[SceneBuilder] Player FBX не найден, используется capsule fallback.");
            }

            player.tag = "Player";

            // CharacterController (требуется CharacterMovement)
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.radius = 0.3f;
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.slopeLimit = 45f;
                cc.stepOffset = 0.3f;
            }

            // Gameplay-компоненты (только если prefab не принёс их)
            AddComponentIfMissing<ZeldaDaughter.Input.CharacterMovement>(player);
            AddComponentIfMissing<ZeldaDaughter.Input.CharacterAutoMove>(player);
            AddComponentIfMissing<ZeldaDaughter.World.SurfaceDetector>(player);
            AddComponentIfMissing<ZeldaDaughter.Inventory.PlayerInventory>(player);

            // Progression — только если ScriptableObject'ы существуют
            var progressionConfig = AssetDatabase.LoadAssetAtPath<ProgressionConfig>("Assets/Data/Progression/ProgressionConfig.asset");
            var playerStats = player.GetComponent<PlayerStats>() ?? player.AddComponent<PlayerStats>();
            if (progressionConfig != null)
            {
                var so = new SerializedObject(playerStats);
                so.FindProperty("_config").objectReferenceValue = progressionConfig;
                so.ApplyModifiedProperties();
            }

            var inventory = player.GetComponent<PlayerInventory>();
            var actionTracker = player.GetComponent<ActionTracker>() ?? player.AddComponent<ActionTracker>();
            {
                var so = new SerializedObject(actionTracker);
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                so.FindProperty("_playerInventory").objectReferenceValue = inventory;
                so.ApplyModifiedProperties();
            }

            var effectConfig = AssetDatabase.LoadAssetAtPath<StatEffectConfig>("Assets/Data/Progression/StatEffectConfig.asset");
            var effectApplier = player.GetComponent<StatEffectApplier>() ?? player.AddComponent<StatEffectApplier>();
            {
                var so = new SerializedObject(effectApplier);
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                so.FindProperty("_inventory").objectReferenceValue = inventory;
                if (effectConfig != null)
                    so.FindProperty("_effectConfig").objectReferenceValue = effectConfig;
                so.ApplyModifiedProperties();
            }

            var feedback = player.GetComponent<ProgressionFeedback>() ?? player.AddComponent<ProgressionFeedback>();
            {
                var so = new SerializedObject(feedback);
                if (effectConfig != null)
                    so.FindProperty("_effectConfig").objectReferenceValue = effectConfig;
                var anim = player.GetComponent<Animator>() ?? player.GetComponentInChildren<Animator>();
                if (anim != null)
                    so.FindProperty("_animator").objectReferenceValue = anim;
                so.ApplyModifiedProperties();
            }

            return player;
        }

        /// <summary>Размещает объекты мира из реальных FBX. Fallback на примитивы если FBX недоступен.</summary>
        private static void PlaceWorldObjects()
        {
            // Деревья — варианты в порядке приоритета
            string[] treeModels = {
                FbxBasePath + "tree_simple_fall.fbx",
                FbxBasePath + "tree_fat.fbx",
                FbxBasePath + "tree_pineTallC.fbx",
                FbxBasePath + "tree_pineTallA_detailed.fbx",
                FbxBasePath + "tree_cone_fall.fbx",
            };

            // Камни
            string[] stoneModels = {
                FbxBasePath + "stone_largeC.fbx",
                FbxBasePath + "stone_largeE.fbx",
                FbxBasePath + "stone_tallA.fbx",
                FbxBasePath + "stone_smallB.fbx",
                FbxBasePath + "stone_smallF.fbx",
            };

            // Здания из FantasyTownKit
            string[] buildingModels = {
                FbxTownPath + "wall.fbx",
                FbxTownPath + "roof-high.fbx",
                FbxTownPath + "roof-point.fbx",
            };

            // Группа деревьев
            var treePositions = new Vector3[] {
                new Vector3(5f, 0f, 3f),
                new Vector3(-6f, 0f, 4f),
                new Vector3(8f, 0f, -2f),
                new Vector3(-4f, 0f, -7f),
                new Vector3(12f, 0f, 5f),
                new Vector3(-10f, 0f, 2f),
                new Vector3(3f, 0f, 10f),
                new Vector3(-7f, 0f, 9f),
                new Vector3(15f, 0f, -3f),
                new Vector3(-13f, 0f, -5f),
                new Vector3(6f, 0f, -9f),
                new Vector3(-2f, 0f, -12f),
            };

            var treeParent = new GameObject("Trees");
            for (int i = 0; i < treePositions.Length; i++)
            {
                string modelPath = treeModels[i % treeModels.Length];
                var go = SpawnFbxOrPrimitive(modelPath, $"Tree_{i}", treePositions[i],
                    PrimitiveType.Cylinder, new Color(0.2f, 0.6f, 0.1f));
                go.transform.SetParent(treeParent.transform);
                EnsureCollider(go);
            }

            // Группа камней
            var stonePositions = new Vector3[] {
                new Vector3(-3f, 0f, 7f),
                new Vector3(9f, 0f, -6f),
                new Vector3(-8f, 0f, -3f),
                new Vector3(4f, 0f, -5f),
                new Vector3(-11f, 0f, 7f),
                new Vector3(13f, 0f, 8f),
            };

            var stoneParent = new GameObject("Stones");
            for (int i = 0; i < stonePositions.Length; i++)
            {
                string modelPath = stoneModels[i % stoneModels.Length];
                var go = SpawnFbxOrPrimitive(modelPath, $"Stone_{i}", stonePositions[i],
                    PrimitiveType.Sphere, Color.gray);
                go.transform.SetParent(stoneParent.transform);
                EnsureCollider(go);
            }

            // Строения
            var buildingParent = new GameObject("Buildings");
            SpawnFbxOrPrimitive(buildingModels[0], "Building_Wall", new Vector3(18f, 0f, 0f),
                PrimitiveType.Cube, new Color(0.6f, 0.4f, 0.2f))
                .transform.SetParent(buildingParent.transform);
            SpawnFbxOrPrimitive(buildingModels[1 % buildingModels.Length], "Building_Roof", new Vector3(18f, 2f, 0f),
                PrimitiveType.Cube, new Color(0.5f, 0.3f, 0.15f))
                .transform.SetParent(buildingParent.transform);
        }

        /// <summary>
        /// Пытается загрузить FBX по пути. Если не найден — создаёт примитив с материалом.
        /// Добавляет MeshCollider (или BoxCollider) если у объекта нет коллайдера.
        /// </summary>
        private static GameObject SpawnFbxOrPrimitive(
            string fbxPath, string goName, Vector3 position,
            PrimitiveType fallbackPrimitive, Color fallbackColor)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (asset != null)
            {
                var go = Object.Instantiate(asset, position, Quaternion.identity);
                go.name = goName;
                AssignUrpMaterials(go);
                return go;
            }

            // Fallback: примитив
            var primitive = GameObject.CreatePrimitive(fallbackPrimitive);
            primitive.name = goName;
            primitive.transform.position = position;
            primitive.GetComponent<Renderer>().sharedMaterial = CreateMaterial(goName, fallbackColor);
            return primitive;
        }

        /// <summary>Назначает URP/Lit материал всем Renderer в иерархии (FBX-ы часто приходят с Built-in шейдерами).</summary>
        private static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AssignUrpMaterialIfMissing(GameObject root)
        {
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urpShader == null) return;

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = rend.sharedMaterials;
                bool needsFix = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        mats[i] = new Material(urpShader) { color = Color.gray };
                        needsFix = true;
                    }
                    else if (mats[i].shader.name.Contains("Standard") || mats[i].shader.name.Contains("Hidden"))
                    {
                        var oldColor = mats[i].HasProperty("_Color") ? mats[i].color : Color.gray;
                        mats[i] = new Material(urpShader) { color = oldColor };
                        needsFix = true;
                    }
                }
                if (needsFix) rend.sharedMaterials = mats;
            }
        }

        private static void AssignUrpMaterials(GameObject root)
        {
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) return;

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = rend.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == urpShader) continue;
                    var newMat = new Material(urpShader);
                    newMat.color = mats[i].HasProperty("_Color") ? mats[i].color : Color.white;
                    mats[i] = newMat;
                    changed = true;
                }
                if (changed) rend.sharedMaterials = mats;
            }
        }

        /// <summary>Добавляет BoxCollider если у объекта нет ни одного коллайдера.</summary>
        private static void EnsureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        /// <summary>Добавляет тег "Player" если отсутствует (дублирует логику PlayerPrefabBuilder).</summary>
        private static void EnsurePlayerTag()
        {
            const string playerTag = "Player";
            foreach (string tag in UnityEditorInternal.InternalEditorUtility.tags)
                if (tag == playerTag) return;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var el = tagsProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = playerTag;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log("[SceneBuilder] Тег 'Player' добавлен в TagManager.");
                    return;
                }
            }
            tagsProp.arraySize++;
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = playerTag;
            tagManager.ApplyModifiedProperties();
            Debug.Log("[SceneBuilder] Тег 'Player' добавлен в TagManager.");
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
            isoCam.SetScreenOffset(new Vector3(0f, 3f, 0f));

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

        // ─────────────────────────────────────────────────────────────────────
        // DEMO SCENE — полная демо-сцена со всеми механиками
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("ZeldaDaughter/Scenes/Build Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = SetupDemoScene();
            var player = SetupDemoPlayer();
            SetupDemoCamera(player);
            SetupDemoCoreSystems(sun);
            SetupDemoNPCSystems(player);
            SetupDemoQuestSystems(player);
            SetupDemoUI(player);

            var meadowParent = PlaceDemoMeadow();
            PlaceDemoRoad();
            PlaceDemoCity();

            WireDemoReferences();

            const string scenePath = "Assets/Scenes/DemoScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            Debug.Log($"[SceneBuilder] DemoScene создана: {scenePath}");
        }

        // 1. SetupDemoScene — земля + свет
        private static Light SetupDemoScene()
        {
            // Поляна (зелёная)
            var meadowGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            meadowGround.name = "Ground_Meadow";
            meadowGround.transform.position = new Vector3(-25f, 0f, 0f);
            meadowGround.transform.localScale = new Vector3(8f, 1f, 8f);
            meadowGround.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial("Ground_Meadow", new Color(0.25f, 0.52f, 0.18f));

            // Город (серый)
            var cityGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            cityGround.name = "Ground_City";
            cityGround.transform.position = new Vector3(21f, 0f, 7f);
            cityGround.transform.localScale = new Vector3(8f, 1f, 8f);
            cityGround.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial("Ground_City", new Color(0.48f, 0.46f, 0.42f));

            // Directional Light (Sun)
            var lightGO = new GameObject("Directional Light");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, 170f, 0f);
            if (lightGO.GetComponent<UniversalAdditionalLightData>() == null)
                lightGO.AddComponent<UniversalAdditionalLightData>();

            return sun;
        }

        // 2. SetupDemoPlayer — Player со всеми компонентами
        private static GameObject SetupDemoPlayer()
        {
            EnsurePlayerTag();

            var spawnPos = new Vector3(-40f, 0f, 0f);
            GameObject player = null;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.position = spawnPos;
                Debug.Log("[SceneBuilder] DemoScene: Player загружен из prefab.");
            }

            if (player == null)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxCharacterPath);
                if (fbx != null)
                {
                    player = new GameObject("Player");
                    player.transform.position = spawnPos;
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                    model.name = "Model";
                    model.transform.SetParent(player.transform);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;
                    AssignUrpMaterialIfMissing(model);
                }
            }

            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                player.transform.position = spawnPos;
                player.GetComponent<Renderer>().sharedMaterial =
                    CreateMaterial("Player", new Color(0.2f, 0.4f, 0.8f));
                Debug.LogWarning("[SceneBuilder] DemoScene: Player FBX не найден, используется capsule.");
            }

            player.tag = "Player";

            // CharacterController
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.radius = 0.3f;
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.slopeLimit = 45f;
                cc.stepOffset = 0.3f;
            }

            // Animator
            if (player.GetComponent<Animator>() == null)
            {
                var anim = player.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Animations/Controllers/PlayerAnimator.controller");
                if (controller != null)
                    anim.runtimeAnimatorController = controller;
            }

            // Core movement
            AddComponentIfMissing<ZeldaDaughter.Input.CharacterMovement>(player);
            AddComponentIfMissing<ZeldaDaughter.Input.CharacterAutoMove>(player);
            AddComponentIfMissing<ZeldaDaughter.World.SurfaceDetector>(player);

            // Inventory
            AddComponentIfMissing<ZeldaDaughter.Inventory.PlayerInventory>(player);
            AddComponentIfMissing<ZeldaDaughter.Inventory.WeightSystem>(player);

            var inventory = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            var charMovement = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();

            var invConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.InventoryConfig>(
                "Assets/Data/InventoryConfig.asset");
            var weightSys = player.GetComponent<ZeldaDaughter.Inventory.WeightSystem>();
            if (weightSys != null && (invConfig != null || charMovement != null))
            {
                var so = new SerializedObject(weightSys);
                if (invConfig != null)
                    so.FindProperty("_config").objectReferenceValue = invConfig;
                if (charMovement != null)
                    so.FindProperty("_characterMovement").objectReferenceValue = charMovement;
                so.ApplyModifiedProperties();
            }

            // Crafting
            AddComponentIfMissing<ZeldaDaughter.UI.CraftFeedback>(player);
            AddComponentIfMissing<ZeldaDaughter.World.WorldInteractionSystem>(player);
            AddComponentIfMissing<ZeldaDaughter.World.WorldPlacement>(player);

            var worldPlacement = player.GetComponent<ZeldaDaughter.World.WorldPlacement>();
            if (worldPlacement != null)
            {
                var db = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.CraftRecipeDatabase>(
                    "Assets/Data/Recipes/CraftRecipeDatabase.asset");
                if (db != null)
                {
                    var so = new SerializedObject(worldPlacement);
                    var prop = so.FindProperty("_recipeDatabase");
                    if (prop != null) { prop.objectReferenceValue = db; so.ApplyModifiedProperties(); }
                }
            }

            // Progression
            var progressionConfig = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(
                "Assets/Data/Progression/ProgressionConfig.asset");
            var effectConfig = AssetDatabase.LoadAssetAtPath<StatEffectConfig>(
                "Assets/Data/Progression/StatEffectConfig.asset");

            var playerStats = player.GetComponent<PlayerStats>() ?? player.AddComponent<PlayerStats>();
            if (progressionConfig != null)
            {
                var so = new SerializedObject(playerStats);
                so.FindProperty("_config").objectReferenceValue = progressionConfig;
                so.ApplyModifiedProperties();
            }

            var actionTracker = player.GetComponent<ActionTracker>() ?? player.AddComponent<ActionTracker>();
            {
                var so = new SerializedObject(actionTracker);
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                so.FindProperty("_playerInventory").objectReferenceValue = inventory;
                so.ApplyModifiedProperties();
            }

            var effectApplier = player.GetComponent<StatEffectApplier>() ?? player.AddComponent<StatEffectApplier>();
            {
                var so = new SerializedObject(effectApplier);
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                so.FindProperty("_inventory").objectReferenceValue = inventory;
                if (effectConfig != null)
                    so.FindProperty("_effectConfig").objectReferenceValue = effectConfig;
                so.ApplyModifiedProperties();
            }

            var feedback = player.GetComponent<ProgressionFeedback>() ?? player.AddComponent<ProgressionFeedback>();
            {
                var so = new SerializedObject(feedback);
                if (effectConfig != null)
                    so.FindProperty("_effectConfig").objectReferenceValue = effectConfig;
                var anim = player.GetComponent<Animator>() ?? player.GetComponentInChildren<Animator>();
                if (anim != null)
                    so.FindProperty("_animator").objectReferenceValue = anim;
                so.ApplyModifiedProperties();
            }

            // Combat
            AddComponentIfMissing<ZeldaDaughter.Combat.PlayerHealthState>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.CombatController>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.WeaponEquipSystem>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.RestZoneDetector>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.HungerSystem>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.WoundEffectApplier>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.FoodConsumption>(player);
            AddComponentIfMissing<ZeldaDaughter.Combat.KnockoutSystem>(player);

            // PlayerHitbox
            if (player.transform.Find("PlayerHitbox") == null)
            {
                var hitboxGo = new GameObject("PlayerHitbox");
                hitboxGo.transform.SetParent(player.transform);
                hitboxGo.transform.localPosition = new Vector3(0f, 0.8f, 0.7f);
                var hCol = hitboxGo.AddComponent<BoxCollider>();
                hCol.isTrigger = true;
                hCol.size = new Vector3(0.6f, 0.6f, 0.6f);
                hitboxGo.AddComponent<ZeldaDaughter.Combat.HitboxTrigger>();
                hitboxGo.SetActive(false);
            }

            var combatConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.CombatConfig>(
                "Assets/Data/Combat/CombatConfig.asset");

            // Привязываем CombatConfig к Combat-компонентам
            DemoWireCombatConfig(player, combatConfig);

            // WeaponBone — точка крепления оружия к персонажу
            if (player.transform.Find("WeaponBone") == null)
            {
                var weaponBoneGO = new GameObject("WeaponBone");
                weaponBoneGO.transform.SetParent(player.transform);
                weaponBoneGO.transform.localPosition = new Vector3(0.4f, 1.2f, 0.2f);
            }

            // WeaponEquipSystem._weaponBoneAttach
            var weaponEquipSys = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
            if (weaponEquipSys != null)
            {
                var weaponBone = player.transform.Find("WeaponBone");
                if (weaponBone != null)
                {
                    var so = new SerializedObject(weaponEquipSys);
                    var prop = so.FindProperty("_weaponBoneAttach");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = weaponBone;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            // FoodConsumption — привязать _healthState, _hungerSystem, _animator
            var foodConsumption = player.GetComponent<ZeldaDaughter.Combat.FoodConsumption>();
            if (foodConsumption != null)
            {
                var healthState = player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
                var hungerSystem = player.GetComponent<ZeldaDaughter.Combat.HungerSystem>();
                var anim = player.GetComponent<Animator>() ?? player.GetComponentInChildren<Animator>();
                var so = new SerializedObject(foodConsumption);
                if (healthState != null)  so.FindProperty("_healthState").objectReferenceValue = healthState;
                if (hungerSystem != null) so.FindProperty("_hungerSystem").objectReferenceValue = hungerSystem;
                if (anim != null)         so.FindProperty("_animator").objectReferenceValue = anim;
                so.ApplyModifiedProperties();
            }

            // Weapon Proficiency
            AddComponentIfMissing<ZeldaDaughter.Progression.WeaponProficiency>(player);
            AddComponentIfMissing<ZeldaDaughter.Progression.WeaponProficiencyTracker>(player);
            AddComponentIfMissing<ZeldaDaughter.Progression.WeaponProficiencyApplier>(player);

            var proficiency = player.GetComponent<ZeldaDaughter.Progression.WeaponProficiency>();
            var profData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Progression.WeaponProficiencyData>(
                "Assets/Data/Progression/WeaponProficiencyData.asset");
            if (proficiency != null && profData != null)
            {
                var so = new SerializedObject(proficiency);
                so.FindProperty("_data").objectReferenceValue = profData;
                so.ApplyModifiedProperties();
            }

            var profTracker = player.GetComponent<ZeldaDaughter.Progression.WeaponProficiencyTracker>();
            if (profTracker != null)
            {
                var weaponEquip = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
                var so = new SerializedObject(profTracker);
                so.FindProperty("_proficiency").objectReferenceValue = proficiency;
                if (weaponEquip != null)
                    so.FindProperty("_weaponEquip").objectReferenceValue = weaponEquip;
                so.ApplyModifiedProperties();
            }

            var profApplier = player.GetComponent<ZeldaDaughter.Progression.WeaponProficiencyApplier>();
            if (profApplier != null)
            {
                var combatCtrl = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                var weaponEquip = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
                var so = new SerializedObject(profApplier);
                so.FindProperty("_proficiency").objectReferenceValue = proficiency;
                if (combatCtrl != null)
                    so.FindProperty("_combatController").objectReferenceValue = combatCtrl;
                if (weaponEquip != null)
                    so.FindProperty("_weaponEquip").objectReferenceValue = weaponEquip;
                so.ApplyModifiedProperties();
            }

            Debug.Log("[SceneBuilder] DemoScene: Player настроен.");
            return player;
        }

        // Привязывает CombatConfig и WoundConfig к Combat-компонентам Player
        private static void DemoWireCombatConfig(GameObject player, ZeldaDaughter.Combat.CombatConfig combatConfig)
        {
            var charMovement = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();
            var charAutoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
            var weaponEquip  = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
            var combatCtrl   = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
            var healthState  = player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
            var woundApplier = player.GetComponent<ZeldaDaughter.Combat.WoundEffectApplier>();
            var hungerSys    = player.GetComponent<ZeldaDaughter.Combat.HungerSystem>();
            var knockoutSys  = player.GetComponent<ZeldaDaughter.Combat.KnockoutSystem>();

            var hitboxTransform = player.transform.Find("PlayerHitbox");
            var hitboxTrigger = hitboxTransform != null
                ? hitboxTransform.GetComponent<ZeldaDaughter.Combat.HitboxTrigger>() : null;

            var woundPuncture = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                "Assets/Data/Combat/WoundConfig_Puncture.asset");
            var woundFracture = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                "Assets/Data/Combat/WoundConfig_Fracture.asset");
            var woundBurn = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                "Assets/Data/Combat/WoundConfig_Burn.asset");
            var woundPoison = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                "Assets/Data/Combat/WoundConfig_Poison.asset");

            if (combatCtrl != null)
            {
                var so = new SerializedObject(combatCtrl);
                if (combatConfig != null) so.FindProperty("_config").objectReferenceValue = combatConfig;
                if (hitboxTrigger != null) so.FindProperty("_hitbox").objectReferenceValue = hitboxTrigger;
                if (weaponEquip != null) so.FindProperty("_weaponEquip").objectReferenceValue = weaponEquip;
                if (charAutoMove != null) so.FindProperty("_autoMove").objectReferenceValue = charAutoMove;
                so.ApplyModifiedProperties();
            }

            if (healthState != null)
            {
                var so = new SerializedObject(healthState);
                if (combatConfig != null) so.FindProperty("_config").objectReferenceValue = combatConfig;
                var woundsProp = so.FindProperty("_woundConfigs");
                if (woundsProp != null)
                {
                    woundsProp.arraySize = 4;
                    woundsProp.GetArrayElementAtIndex(0).objectReferenceValue = woundPuncture;
                    woundsProp.GetArrayElementAtIndex(1).objectReferenceValue = woundFracture;
                    woundsProp.GetArrayElementAtIndex(2).objectReferenceValue = woundBurn;
                    woundsProp.GetArrayElementAtIndex(3).objectReferenceValue = woundPoison;
                }
                so.ApplyModifiedProperties();
            }

            if (woundApplier != null)
            {
                var so = new SerializedObject(woundApplier);
                var woundsProp = so.FindProperty("_woundConfigs");
                if (woundsProp != null)
                {
                    woundsProp.arraySize = 4;
                    woundsProp.GetArrayElementAtIndex(0).objectReferenceValue = woundPuncture;
                    woundsProp.GetArrayElementAtIndex(1).objectReferenceValue = woundFracture;
                    woundsProp.GetArrayElementAtIndex(2).objectReferenceValue = woundBurn;
                    woundsProp.GetArrayElementAtIndex(3).objectReferenceValue = woundPoison;
                }
                if (charMovement != null) so.FindProperty("_movement").objectReferenceValue = charMovement;
                so.ApplyModifiedProperties();
            }

            if (hungerSys != null)
            {
                var so = new SerializedObject(hungerSys);
                if (combatConfig != null) so.FindProperty("_config").objectReferenceValue = combatConfig;
                if (charMovement != null) so.FindProperty("_movement").objectReferenceValue = charMovement;
                so.ApplyModifiedProperties();
            }

            if (knockoutSys != null && combatConfig != null)
            {
                var so = new SerializedObject(knockoutSys);
                so.FindProperty("_config").objectReferenceValue = combatConfig;
                so.ApplyModifiedProperties();
            }
        }

        // 3. SetupDemoCamera — изометрическая камера на Player
        private static void SetupDemoCamera(GameObject player)
        {
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.75f, 0.9f);
            if (cameraGO.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGO.AddComponent<UniversalAdditionalCameraData>();
            var isoCam = cameraGO.AddComponent<ZeldaDaughter.World.IsometricCamera>();
            isoCam.SetTarget(player.transform);
            // Смещаем камеру вверх по мировому Y, чтобы персонаж был в нижней трети экрана
            isoCam.SetScreenOffset(new Vector3(0f, 3f, 0f));
        }

        // 4. SetupDemoCoreSystems — EventSystem, GestureDispatcher, DayNightCycle, WeatherSystem, SaveManager
        private static void SetupDemoCoreSystems(Light sun)
        {
            var systemsParent = new GameObject("Systems");

            // EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var evGO = new GameObject("EventSystem");
                evGO.transform.SetParent(systemsParent.transform);
                evGO.AddComponent<EventSystem>();
                evGO.AddComponent<StandaloneInputModule>();
            }

            // GestureDispatcher
            var inputGO = new GameObject("InputSystem");
            inputGO.transform.SetParent(systemsParent.transform);
            inputGO.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();

            // GameBootstrap
            var bootstrapGO = new GameObject("GameBootstrap");
            bootstrapGO.transform.SetParent(systemsParent.transform);
            bootstrapGO.AddComponent<ZeldaDaughter.World.GameBootstrap>();

            // DayNightCycle
            var dayNightGO = new GameObject("DayNightCycle");
            dayNightGO.transform.SetParent(systemsParent.transform);
            var dayNight = dayNightGO.AddComponent<ZeldaDaughter.World.DayNightCycle>();
            {
                var so = new SerializedObject(dayNight);
                so.FindProperty("_directionalLight").objectReferenceValue = sun;
                so.ApplyModifiedProperties();
            }

            // WeatherSystem
            var weatherGO = new GameObject("WeatherSystem");
            weatherGO.transform.SetParent(systemsParent.transform);
            var weatherSys = weatherGO.AddComponent<ZeldaDaughter.World.WeatherSystem>();
            {
                var weatherConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.WeatherConfig>(
                    "Assets/Data/World/WeatherConfig.asset");
                if (weatherConfig != null)
                {
                    var so = new SerializedObject(weatherSys);
                    so.FindProperty("_config").objectReferenceValue = weatherConfig;
                    so.ApplyModifiedProperties();
                }
            }

            // TapInteractionManager — создаётся здесь; ссылки на Player пробрасываются в WireDemoReferences
            var tapSystemGO = new GameObject("TapSystem");
            tapSystemGO.transform.SetParent(systemsParent.transform);
            tapSystemGO.AddComponent<ZeldaDaughter.World.TapInteractionManager>();

            // SaveManager
            var saveGO = new GameObject("SaveManager");
            saveGO.transform.SetParent(systemsParent.transform);
            saveGO.AddComponent<ZeldaDaughter.Save.SaveManager>();

            // Ambient zones
            CreateAmbientZone("AmbientZone_Meadow",  new Vector3(-35f, 0f, 0f),   25f);
            CreateAmbientZone("AmbientZone_City",    new Vector3(21f,  0f, 7f),   20f);
            CreateAmbientZone("AmbientZone_Road",    new Vector3(-5f,  0f, 3f),   18f);

            Debug.Log("[SceneBuilder] DemoScene: CoreSystems настроены.");
        }

        // 5. SetupDemoNPCSystems — LanguageSystem, DialogueManager, TradeManager
        private static void SetupDemoNPCSystems(GameObject player)
        {
            var npcSystemsGO = new GameObject("NPCSystems");

            var playerInventory = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            var playerStats     = player.GetComponent<PlayerStats>();

            // LanguageSystem
            var langGO = new GameObject("LanguageSystem");
            langGO.transform.SetParent(npcSystemsGO.transform);
            var langSys = langGO.AddComponent<ZeldaDaughter.NPC.LanguageSystem>();
            {
                var langConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.LanguageConfig>(
                    "Assets/Data/NPC/LanguageConfig.asset");
                var so = new SerializedObject(langSys);
                if (langConfig != null) so.FindProperty("_config").objectReferenceValue = langConfig;
                if (playerStats != null) so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                so.ApplyModifiedProperties();
            }

            // DialogueManager
            var dialogueGO = new GameObject("DialogueManager");
            dialogueGO.transform.SetParent(npcSystemsGO.transform);
            var dialogueMgr = dialogueGO.AddComponent<ZeldaDaughter.NPC.DialogueManager>();
            {
                var questMgr = Object.FindObjectOfType<ZeldaDaughter.Quest.QuestManager>();
                var so = new SerializedObject(dialogueMgr);
                so.FindProperty("_languageSystem").objectReferenceValue = langSys;
                if (playerInventory != null) so.FindProperty("_playerInventory").objectReferenceValue = playerInventory;
                if (playerStats != null)     so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                if (questMgr != null)        so.FindProperty("_questManager").objectReferenceValue = questMgr;
                so.ApplyModifiedProperties();
            }

            // TradeManager
            var tradeGO = new GameObject("TradeManager");
            tradeGO.transform.SetParent(npcSystemsGO.transform);
            var tradeMgr = tradeGO.AddComponent<ZeldaDaughter.NPC.TradeManager>();
            {
                var so = new SerializedObject(tradeMgr);
                so.FindProperty("_languageSystem").objectReferenceValue = langSys;
                if (playerInventory != null) so.FindProperty("_playerInventory").objectReferenceValue = playerInventory;
                so.ApplyModifiedProperties();
            }

            Debug.Log("[SceneBuilder] DemoScene: NPCSystems настроены.");
        }

        // 6. SetupDemoQuestSystems — QuestManager, MapManager, NotebookManager
        private static void SetupDemoQuestSystems(GameObject player)
        {
            var questSystemsGO = new GameObject("QuestSystems");

            var playerInventory = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();

            // QuestManager
            var questGO = new GameObject("QuestManager");
            questGO.transform.SetParent(questSystemsGO.transform);
            var questMgr = questGO.AddComponent<ZeldaDaughter.Quest.QuestManager>();
            {
                var questDb = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Quest.QuestDatabase>(
                    "Assets/Data/Quests/QuestDatabase.asset");
                var so = new SerializedObject(questMgr);
                if (questDb != null)         so.FindProperty("_database").objectReferenceValue = questDb;
                if (playerInventory != null) so.FindProperty("_playerInventory").objectReferenceValue = playerInventory;
                so.ApplyModifiedProperties();
            }

            // MapManager
            var mapGO = new GameObject("MapManager");
            mapGO.transform.SetParent(questSystemsGO.transform);
            var mapMgr = mapGO.AddComponent<ZeldaDaughter.World.MapManager>();
            {
                var so = new SerializedObject(mapMgr);
                so.FindProperty("_playerTransform").objectReferenceValue = player.transform;
                if (playerInventory != null) so.FindProperty("_playerInventory").objectReferenceValue = playerInventory;

                // Загрузить MapRegionData если существуют
                var regionsProp = so.FindProperty("_regions");
                if (regionsProp != null)
                {
                    var meadowRegion = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.MapRegionData>(
                        "Assets/Data/World/MapRegion_StartMeadow.asset");
                    var cityRegion = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.MapRegionData>(
                        "Assets/Data/World/MapRegion_HumanCity.asset");

                    int count = 0;
                    if (meadowRegion != null) count++;
                    if (cityRegion != null) count++;

                    if (count > 0)
                    {
                        regionsProp.arraySize = count;
                        int idx = 0;
                        if (meadowRegion != null) regionsProp.GetArrayElementAtIndex(idx++).objectReferenceValue = meadowRegion;
                        if (cityRegion != null)   regionsProp.GetArrayElementAtIndex(idx).objectReferenceValue = cityRegion;
                    }
                }
                so.ApplyModifiedProperties();
            }

            // NotebookManager
            var notebookGO = new GameObject("NotebookManager");
            notebookGO.transform.SetParent(questSystemsGO.transform);
            var notebookMgr = notebookGO.AddComponent<ZeldaDaughter.UI.NotebookManager>();
            {
                var notebookConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.UI.NotebookConfig>(
                    "Assets/Data/NotebookConfig.asset");
                var so = new SerializedObject(notebookMgr);
                if (notebookConfig != null) so.FindProperty("_config").objectReferenceValue = notebookConfig;
                so.ApplyModifiedProperties();
            }

            Debug.Log("[SceneBuilder] DemoScene: QuestSystems настроены.");
        }

        // 7. SetupDemoUI — все Canvas'ы
        private static void SetupDemoUI(GameObject player)
        {
            var uiParent = new GameObject("UI");

            var recipeDb = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.CraftRecipeDatabase>(
                "Assets/Data/Recipes/CraftRecipeDatabase.asset");

            // --- RadialMenuCanvas (99) ---
            var radialCanvas = CreateCanvas("RadialMenuCanvas", 99);
            radialCanvas.transform.SetParent(uiParent.transform);
            var radialMenu = radialCanvas.gameObject.AddComponent<ZeldaDaughter.UI.RadialMenuController>();
            var radialCG = radialCanvas.gameObject.AddComponent<CanvasGroup>();

            var menuRoot = new GameObject("MenuRoot").AddComponent<RectTransform>();
            menuRoot.SetParent(radialCanvas.transform, false);
            menuRoot.anchoredPosition = Vector2.zero;

            {
                var radialMenuConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.UI.RadialMenuConfig>(
                    "Assets/Data/RadialMenuConfig.asset");
                var so = new SerializedObject(radialMenu);
                so.FindProperty("_menuRoot").objectReferenceValue = menuRoot;
                so.FindProperty("_canvasGroup").objectReferenceValue = radialCG;
                so.FindProperty("_playerTransform").objectReferenceValue = player.transform;
                if (radialMenuConfig != null)
                    so.FindProperty("_config").objectReferenceValue = radialMenuConfig;
                var sectorsProp = so.FindProperty("_sectors");
                sectorsProp.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var sGO = new GameObject($"RadialMenuSector_{i}").AddComponent<RectTransform>();
                    sGO.SetParent(menuRoot, false);
                    sGO.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
                    var sector = sGO.gameObject.AddComponent<ZeldaDaughter.UI.RadialMenuSector>();
                    sectorsProp.GetArrayElementAtIndex(i).objectReferenceValue = sector;
                }
                so.ApplyModifiedProperties();
            }

            // --- InventoryCanvas (100) ---
            var inventoryCanvas = CreateCanvas("InventoryCanvas", 100);
            inventoryCanvas.transform.SetParent(uiParent.transform);

            var inventoryPanelGO = new GameObject("InventoryPanel");
            inventoryPanelGO.transform.SetParent(inventoryCanvas.transform, false);
            var inventoryPanel = inventoryPanelGO.AddComponent<ZeldaDaughter.UI.InventoryPanel>();
            var inventoryPanelCG = inventoryPanelGO.AddComponent<CanvasGroup>();

            // SlotsGrid — родитель для слотов
            var slotsGridGO = new GameObject("SlotsGrid");
            slotsGridGO.transform.SetParent(inventoryPanelGO.transform, false);
            var slotsGridRect = slotsGridGO.AddComponent<RectTransform>();
            slotsGridRect.anchorMin = Vector2.zero;
            slotsGridRect.anchorMax = Vector2.one;
            slotsGridRect.offsetMin = Vector2.zero;
            slotsGridRect.offsetMax = Vector2.zero;

            // SlotPrefab — шаблон слота инвентаря
            var slotPrefabGO = new GameObject("SlotPrefab");
            slotPrefabGO.transform.SetParent(inventoryPanelGO.transform, false);
            var slotPrefabRect = slotPrefabGO.AddComponent<RectTransform>();
            slotPrefabRect.sizeDelta = new Vector2(64f, 64f);
            slotPrefabGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            slotPrefabGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();
            slotPrefabGO.SetActive(false); // Шаблон — не отображается напрямую

            {
                var so = new SerializedObject(inventoryPanel);
                so.FindProperty("_canvasGroup").objectReferenceValue = inventoryPanelCG;
                so.FindProperty("_slotsParent").objectReferenceValue = slotsGridRect;
                so.FindProperty("_slotPrefab").objectReferenceValue = slotPrefabGO;
                so.ApplyModifiedProperties();
            }

            var itemInfoGO = new GameObject("ItemInfoPopup");
            itemInfoGO.transform.SetParent(inventoryCanvas.transform, false);
            var itemInfoPopup = itemInfoGO.AddComponent<ZeldaDaughter.UI.ItemInfoPopup>();
            var itemInfoCG = itemInfoGO.AddComponent<CanvasGroup>();
            itemInfoGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            {
                var nameTextGO = new GameObject("NameText");
                nameTextGO.transform.SetParent(itemInfoGO.transform, false);
                var nameRect = nameTextGO.AddComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0f, 0.55f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.offsetMin = new Vector2(8f, 0f);
                nameRect.offsetMax = new Vector2(-8f, 0f);
                var nameText = nameTextGO.AddComponent<Text>();
                nameText.fontSize = 16;
                nameText.color = Color.white;
                nameText.alignment = TextAnchor.MiddleCenter;

                var descTextGO = new GameObject("DescriptionText");
                descTextGO.transform.SetParent(itemInfoGO.transform, false);
                var descRect = descTextGO.AddComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0f, 0f);
                descRect.anchorMax = new Vector2(1f, 0.5f);
                descRect.offsetMin = new Vector2(8f, 4f);
                descRect.offsetMax = new Vector2(-8f, 0f);
                var descText = descTextGO.AddComponent<Text>();
                descText.fontSize = 13;
                descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                descText.alignment = TextAnchor.UpperLeft;

                var so = new SerializedObject(itemInfoPopup);
                so.FindProperty("_canvasGroup").objectReferenceValue = itemInfoCG;
                so.FindProperty("_nameText").objectReferenceValue = nameText;
                so.FindProperty("_descriptionText").objectReferenceValue = descText;
                so.ApplyModifiedProperties();
            }

            var dragHandlerGO = new GameObject("InventoryDragHandler");
            dragHandlerGO.transform.SetParent(inventoryCanvas.transform, false);
            var dragHandler = dragHandlerGO.AddComponent<ZeldaDaughter.UI.InventoryDragHandler>();
            {
                var dragIconGO = new GameObject("DragIcon");
                dragIconGO.transform.SetParent(dragHandlerGO.transform, false);
                var dragImg = dragIconGO.AddComponent<Image>();
                dragImg.raycastTarget = false;
                var dragIconCG = dragIconGO.AddComponent<CanvasGroup>();
                dragIconCG.blocksRaycasts = false;

                var playerGO = GameObject.Find("Player");
                var weaponEquipForDrag = playerGO != null
                    ? playerGO.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>()
                    : null;

                var so = new SerializedObject(dragHandler);
                so.FindProperty("_dragIcon").objectReferenceValue = dragImg;
                so.FindProperty("_dragIconGroup").objectReferenceValue = dragIconCG;
                so.FindProperty("_inventoryPanel").objectReferenceValue = inventoryPanel;
                so.FindProperty("_panelRect").objectReferenceValue =
                    inventoryPanelGO.GetComponent<RectTransform>();
                if (recipeDb != null)
                    so.FindProperty("_recipeDatabase").objectReferenceValue = recipeDb;
                if (weaponEquipForDrag != null)
                    so.FindProperty("_weaponEquipSystem").objectReferenceValue = weaponEquipForDrag;
                so.ApplyModifiedProperties();
            }

            // --- StationCanvas (101) ---
            var stationCanvas = CreateCanvas("StationCanvas", 101);
            stationCanvas.transform.SetParent(uiParent.transform);
            var stationUI = stationCanvas.gameObject.AddComponent<ZeldaDaughter.UI.StationUI>();
            var stationCG = stationCanvas.gameObject.AddComponent<CanvasGroup>();
            {
                // Title
                var titleGO = new GameObject("TitleText");
                titleGO.transform.SetParent(stationCanvas.transform, false);
                var titleRect = titleGO.AddComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0.1f, 0.85f);
                titleRect.anchorMax = new Vector2(0.9f, 0.98f);
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
                var titleText = titleGO.AddComponent<Text>();
                titleText.fontSize = 20;
                titleText.color = Color.white;
                titleText.alignment = TextAnchor.MiddleCenter;

                // SlotA
                var slotAGO = new GameObject("SlotA");
                slotAGO.transform.SetParent(stationCanvas.transform, false);
                var slotARect = slotAGO.AddComponent<RectTransform>();
                slotARect.anchorMin = new Vector2(0.1f, 0.5f);
                slotARect.anchorMax = new Vector2(0.35f, 0.8f);
                slotARect.offsetMin = Vector2.zero;
                slotARect.offsetMax = Vector2.zero;
                slotAGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                var slotAUI = slotAGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();

                // SlotB
                var slotBGO = new GameObject("SlotB");
                slotBGO.transform.SetParent(stationCanvas.transform, false);
                var slotBRect = slotBGO.AddComponent<RectTransform>();
                slotBRect.anchorMin = new Vector2(0.4f, 0.5f);
                slotBRect.anchorMax = new Vector2(0.65f, 0.8f);
                slotBRect.offsetMin = Vector2.zero;
                slotBRect.offsetMax = Vector2.zero;
                slotBGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                var slotBUI = slotBGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();

                // ResultSlot
                var resultGO = new GameObject("ResultSlot");
                resultGO.transform.SetParent(stationCanvas.transform, false);
                var resultRect = resultGO.AddComponent<RectTransform>();
                resultRect.anchorMin = new Vector2(0.65f, 0.5f);
                resultRect.anchorMax = new Vector2(0.9f, 0.8f);
                resultRect.offsetMin = Vector2.zero;
                resultRect.offsetMax = Vector2.zero;
                resultGO.AddComponent<Image>().color = new Color(0.15f, 0.3f, 0.15f, 0.8f);
                var resultSlotUI = resultGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();

                // CraftButton
                var craftBtnGO = new GameObject("CraftButton");
                craftBtnGO.transform.SetParent(stationCanvas.transform, false);
                var craftBtnRect = craftBtnGO.AddComponent<RectTransform>();
                craftBtnRect.anchorMin = new Vector2(0.35f, 0.15f);
                craftBtnRect.anchorMax = new Vector2(0.65f, 0.35f);
                craftBtnRect.offsetMin = Vector2.zero;
                craftBtnRect.offsetMax = Vector2.zero;
                craftBtnGO.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 1f);
                var craftBtn = craftBtnGO.AddComponent<Button>();

                // ProgressBar (filled Image)
                var progressGO = new GameObject("ProgressBar");
                progressGO.transform.SetParent(stationCanvas.transform, false);
                var progressRect = progressGO.AddComponent<RectTransform>();
                progressRect.anchorMin = new Vector2(0.1f, 0.05f);
                progressRect.anchorMax = new Vector2(0.9f, 0.13f);
                progressRect.offsetMin = Vector2.zero;
                progressRect.offsetMax = Vector2.zero;
                var progressImg = progressGO.AddComponent<Image>();
                progressImg.color = new Color(0.2f, 0.7f, 0.9f, 1f);
                progressImg.type = Image.Type.Filled;
                progressImg.fillMethod = Image.FillMethod.Horizontal;
                progressImg.fillAmount = 0f;

                var so = new SerializedObject(stationUI);
                so.FindProperty("_canvasGroup").objectReferenceValue = stationCG;
                so.FindProperty("_titleText").objectReferenceValue = titleText;
                so.FindProperty("_slotA").objectReferenceValue = slotAUI;
                so.FindProperty("_slotB").objectReferenceValue = slotBUI;
                so.FindProperty("_resultSlot").objectReferenceValue = resultSlotUI;
                so.FindProperty("_craftButton").objectReferenceValue = craftBtn;
                so.FindProperty("_progressBar").objectReferenceValue = progressImg;
                if (recipeDb != null)
                    so.FindProperty("_recipeDatabase").objectReferenceValue = recipeDb;
                so.ApplyModifiedProperties();
            }

            // --- DialogueCanvas (102) ---
            var dialogueCanvas = CreateCanvas("DialogueCanvas", 102);
            dialogueCanvas.transform.SetParent(uiParent.transform);

            var dialoguePanelGO = new GameObject("DialoguePanel");
            dialoguePanelGO.transform.SetParent(dialogueCanvas.transform, false);
            var dialoguePanelUI = dialoguePanelGO.AddComponent<ZeldaDaughter.UI.DialoguePanelUI>();
            var dialogueCG = dialoguePanelGO.AddComponent<CanvasGroup>();
            {
                var containerGO = new GameObject("Container");
                containerGO.transform.SetParent(dialoguePanelGO.transform, false);
                var containerRect = containerGO.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0f, 0f);
                containerRect.anchorMax = new Vector2(1f, 0.3f);
                containerRect.offsetMin = Vector2.zero;
                containerRect.offsetMax = Vector2.zero;
                containerGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

                // OptionButton prefab — шаблон кнопки диалога
                var optionBtnGO = new GameObject("OptionButtonPrefab");
                optionBtnGO.transform.SetParent(dialoguePanelGO.transform, false);
                var optionBtnRect = optionBtnGO.AddComponent<RectTransform>();
                optionBtnRect.sizeDelta = new Vector2(280f, 48f);
                optionBtnGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                optionBtnGO.AddComponent<Button>();
                var optionLabelGO = new GameObject("Label");
                optionLabelGO.transform.SetParent(optionBtnGO.transform, false);
                var optionLabelRect = optionLabelGO.AddComponent<RectTransform>();
                optionLabelRect.anchorMin = Vector2.zero;
                optionLabelRect.anchorMax = Vector2.one;
                optionLabelRect.offsetMin = new Vector2(8f, 4f);
                optionLabelRect.offsetMax = new Vector2(-8f, -4f);
                var optionTmp = optionLabelGO.AddComponent<TextMeshProUGUI>();
                optionTmp.fontSize = 14f;
                optionTmp.color = Color.white;
                optionTmp.alignment = TextAlignmentOptions.MidlineLeft;
                optionBtnGO.SetActive(false); // шаблон скрыт

                var so = new SerializedObject(dialoguePanelUI);
                so.FindProperty("_container").objectReferenceValue = containerRect;
                so.FindProperty("_canvasGroup").objectReferenceValue = dialogueCG;
                so.FindProperty("_optionButtonPrefab").objectReferenceValue = optionBtnGO;
                so.ApplyModifiedProperties();
            }

            // --- TradeCanvas (103) ---
            var tradeCanvas = CreateCanvas("TradeCanvas", 103);
            tradeCanvas.transform.SetParent(uiParent.transform);
            var tradeUI = tradeCanvas.gameObject.AddComponent<ZeldaDaughter.UI.TradeUI>();
            var tradeCG = tradeCanvas.gameObject.AddComponent<CanvasGroup>();
            {
                // Merchant panel
                var merchantPanelGO = new GameObject("MerchantPanel");
                merchantPanelGO.transform.SetParent(tradeCanvas.transform, false);
                var merchantRect = merchantPanelGO.AddComponent<RectTransform>();
                merchantRect.anchorMin = new Vector2(0f, 0.1f);
                merchantRect.anchorMax = new Vector2(0.45f, 0.9f);
                merchantRect.offsetMin = Vector2.zero;
                merchantRect.offsetMax = Vector2.zero;
                merchantPanelGO.AddComponent<Image>().color = new Color(0.15f, 0.1f, 0.05f, 0.85f);

                // Player panel
                var playerPanelGO = new GameObject("PlayerPanel");
                playerPanelGO.transform.SetParent(tradeCanvas.transform, false);
                var playerRect = playerPanelGO.AddComponent<RectTransform>();
                playerRect.anchorMin = new Vector2(0.55f, 0.1f);
                playerRect.anchorMax = new Vector2(1f, 0.9f);
                playerRect.offsetMin = Vector2.zero;
                playerRect.offsetMax = Vector2.zero;
                playerPanelGO.AddComponent<Image>().color = new Color(0.05f, 0.1f, 0.15f, 0.85f);

                // Trade zone
                var tradeZoneGO = new GameObject("TradeZone");
                tradeZoneGO.transform.SetParent(tradeCanvas.transform, false);
                var tradeZoneRect = tradeZoneGO.AddComponent<RectTransform>();
                tradeZoneRect.anchorMin = new Vector2(0.35f, 0.1f);
                tradeZoneRect.anchorMax = new Vector2(0.65f, 0.9f);
                tradeZoneRect.offsetMin = Vector2.zero;
                tradeZoneRect.offsetMax = Vector2.zero;
                tradeZoneGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

                // Balance indicator
                var balanceGO = new GameObject("BalanceIndicator");
                balanceGO.transform.SetParent(tradeCanvas.transform, false);
                var balanceRect = balanceGO.AddComponent<RectTransform>();
                balanceRect.anchorMin = new Vector2(0.3f, 0.02f);
                balanceRect.anchorMax = new Vector2(0.7f, 0.08f);
                balanceRect.offsetMin = Vector2.zero;
                balanceRect.offsetMax = Vector2.zero;
                var balanceImage = balanceGO.AddComponent<Image>();
                balanceImage.color = new Color(0.6f, 0.8f, 0.2f, 0.9f);

                // Confirm / Cancel buttons
                var confirmGO = new GameObject("ConfirmButton");
                confirmGO.transform.SetParent(tradeCanvas.transform, false);
                var confirmRect = confirmGO.AddComponent<RectTransform>();
                confirmRect.anchorMin = new Vector2(0.35f, 0.02f);
                confirmRect.anchorMax = new Vector2(0.5f, 0.09f);
                confirmRect.offsetMin = Vector2.zero;
                confirmRect.offsetMax = Vector2.zero;
                confirmGO.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.2f, 1f);
                var confirmBtn = confirmGO.AddComponent<Button>();

                var cancelGO = new GameObject("CancelButton");
                cancelGO.transform.SetParent(tradeCanvas.transform, false);
                var cancelRect = cancelGO.AddComponent<RectTransform>();
                cancelRect.anchorMin = new Vector2(0.5f, 0.02f);
                cancelRect.anchorMax = new Vector2(0.65f, 0.09f);
                cancelRect.offsetMin = Vector2.zero;
                cancelRect.offsetMax = Vector2.zero;
                cancelGO.AddComponent<Image>().color = new Color(0.7f, 0.2f, 0.2f, 1f);
                var cancelBtn = cancelGO.AddComponent<Button>();

                // Trade item slot prefab
                var tradeSlotGO = new GameObject("TradeSlotPrefab");
                tradeSlotGO.transform.SetParent(tradeCanvas.transform, false);
                var tradeSlotRect = tradeSlotGO.AddComponent<RectTransform>();
                tradeSlotRect.sizeDelta = new Vector2(56f, 56f);
                tradeSlotGO.AddComponent<Image>().color = new Color(0.2f, 0.18f, 0.14f, 0.9f);
                tradeSlotGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();
                tradeSlotGO.SetActive(false); // шаблон скрыт

                var so = new SerializedObject(tradeUI);
                so.FindProperty("_merchantPanel").objectReferenceValue = merchantRect;
                so.FindProperty("_playerPanel").objectReferenceValue = playerRect;
                so.FindProperty("_tradeZone").objectReferenceValue = tradeZoneRect;
                so.FindProperty("_balanceIndicator").objectReferenceValue = balanceImage;
                so.FindProperty("_confirmButton").objectReferenceValue = confirmBtn;
                so.FindProperty("_cancelButton").objectReferenceValue = cancelBtn;
                so.FindProperty("_canvasGroup").objectReferenceValue = tradeCG;
                so.FindProperty("_itemSlotPrefab").objectReferenceValue = tradeSlotGO;
                so.ApplyModifiedProperties();
            }

            // --- MapCanvas (104) ---
            var mapCanvas = CreateCanvas("MapCanvas", 104);
            mapCanvas.transform.SetParent(uiParent.transform);
            var mapPanelGO = new GameObject("MapPanel");
            mapPanelGO.transform.SetParent(mapCanvas.transform, false);
            var mapRect = mapPanelGO.AddComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0.05f, 0.05f);
            mapRect.anchorMax = new Vector2(0.95f, 0.95f);
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;
            mapPanelGO.AddComponent<Image>().color = new Color(0.18f, 0.14f, 0.08f, 0.95f);
            var mapPanelUI = mapPanelGO.AddComponent<ZeldaDaughter.UI.MapPanelUI>();
            {
                var so = new SerializedObject(mapPanelUI);
                so.FindProperty("_panel").objectReferenceValue = mapPanelGO;
                so.ApplyModifiedProperties();
            }

            // --- NotebookCanvas (105) ---
            var notebookCanvas = CreateCanvas("NotebookCanvas", 105);
            notebookCanvas.transform.SetParent(uiParent.transform);
            var notebookPanelGO = new GameObject("NotebookPanel");
            notebookPanelGO.transform.SetParent(notebookCanvas.transform, false);
            var notebookRect = notebookPanelGO.AddComponent<RectTransform>();
            notebookRect.anchorMin = new Vector2(0.05f, 0.05f);
            notebookRect.anchorMax = new Vector2(0.95f, 0.95f);
            notebookRect.offsetMin = Vector2.zero;
            notebookRect.offsetMax = Vector2.zero;
            notebookPanelGO.AddComponent<Image>().color = new Color(0.94f, 0.90f, 0.78f, 0.97f);
            var notebookPanelUI = notebookPanelGO.AddComponent<ZeldaDaughter.UI.NotebookPanelUI>();
            {
                var so = new SerializedObject(notebookPanelUI);
                so.FindProperty("_panel").objectReferenceValue = notebookPanelGO;
                so.ApplyModifiedProperties();
            }

            // --- LongPressCanvas (98) ---
            var longPressCanvas = CreateCanvas("LongPressCanvas", 98);
            longPressCanvas.transform.SetParent(uiParent.transform);
            var longPressIndicator = longPressCanvas.gameObject.AddComponent<ZeldaDaughter.UI.LongPressIndicator>();
            var longPressCG = longPressCanvas.gameObject.AddComponent<CanvasGroup>();
            {
                // FillImage — заливочное изображение прогресса лонг-пресса
                var fillImageGO = new GameObject("FillImage");
                fillImageGO.transform.SetParent(longPressCanvas.transform, false);
                var fillRect = fillImageGO.AddComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0.35f, 0.35f);
                fillRect.anchorMax = new Vector2(0.65f, 0.65f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                var fillImg = fillImageGO.AddComponent<Image>();
                fillImg.color = new Color(1f, 1f, 1f, 0.85f);
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Radial360;
                fillImg.fillAmount = 0f;

                var so = new SerializedObject(longPressIndicator);
                so.FindProperty("_canvasGroup").objectReferenceValue = longPressCG;
                so.FindProperty("_followTarget").objectReferenceValue = player.transform;
                so.FindProperty("_fillImage").objectReferenceValue = fillImg;
                so.ApplyModifiedProperties();
            }

            // --- FadeOverlayCanvas (200) ---
            var fadeCanvas = CreateCanvas("FadeOverlayCanvas", 200);
            fadeCanvas.transform.SetParent(uiParent.transform);
            var fadeCG = fadeCanvas.gameObject.AddComponent<CanvasGroup>();
            fadeCG.alpha = 0f;
            fadeCG.blocksRaycasts = false;

            var fadeImageGO = new GameObject("FadeImage");
            fadeImageGO.transform.SetParent(fadeCanvas.transform, false);
            var fadeRect = fadeImageGO.AddComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            var fadeImage = fadeImageGO.AddComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;

            var fadeOverlay = fadeCanvas.gameObject.AddComponent<ZeldaDaughter.UI.FadeOverlay>();
            {
                var so = new SerializedObject(fadeOverlay);
                so.FindProperty("_canvasGroup").objectReferenceValue = fadeCG;
                so.ApplyModifiedProperties();
            }

            // Скрыть все UI-панели по умолчанию (они открываются через код при взаимодействии)
            void HideCanvasGroup(CanvasGroup cg) { if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; } }
            HideCanvasGroup(radialCG);
            HideCanvasGroup(inventoryPanelCG);
            HideCanvasGroup(itemInfoCG);
            HideCanvasGroup(stationCG);
            HideCanvasGroup(dialogueCG);
            HideCanvasGroup(tradeCG);
            HideCanvasGroup(longPressCG);
            // MapPanel и NotebookPanel — скрыть через CanvasGroup для консистентности
            var mapPanelCG = mapPanelGO.AddComponent<CanvasGroup>();
            HideCanvasGroup(mapPanelCG);
            var notebookPanelCG = notebookPanelGO.AddComponent<CanvasGroup>();
            HideCanvasGroup(notebookPanelCG);

            Debug.Log("[SceneBuilder] DemoScene: UI настроен (все панели скрыты).");
        }

        // 8. PlaceDemoMeadow — поляна
        private static GameObject PlaceDemoMeadow()
        {
            UnityEngine.Random.InitState(42);

            var meadowParent = new GameObject("Meadow");
            meadowParent.transform.position = new Vector3(-40f, 0f, 0f);

            // 12+ деревьев
            string[] treeModels = {
                FbxBasePath + "tree_oak.fbx",
                FbxBasePath + "tree_fat.fbx",
                FbxBasePath + "tree_simple.fbx",
                FbxBasePath + "tree_pineTallA.fbx",
                FbxBasePath + "tree_pineTallC.fbx",
                FbxBasePath + "tree_default.fbx",
                FbxBasePath + "tree_detailed.fbx",
                FbxBasePath + "tree_cone.fbx",
                FbxBasePath + "tree_small.fbx",
                FbxBasePath + "tree_tall.fbx",
                FbxBasePath + "tree_thin.fbx",
                FbxBasePath + "tree_blocks.fbx",
            };

            var treeParent = new GameObject("Trees");
            treeParent.transform.SetParent(meadowParent.transform);
            for (int i = 0; i < 14; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f);
                float dist  = UnityEngine.Random.Range(6f, 18f);
                var pos = new Vector3(
                    -40f + Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist);
                string modelPath = treeModels[i % treeModels.Length];
                var go = SpawnFbxOrPrimitive(modelPath, $"Tree_{i}", pos,
                    PrimitiveType.Cylinder, new Color(0.2f, 0.55f, 0.1f));
                go.transform.SetParent(treeParent.transform);
                go.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                EnsureCollider(go);
            }

            // 6+ камней
            string[] stoneModels = {
                FbxBasePath + "stone_largeA.fbx",
                FbxBasePath + "stone_largeC.fbx",
                FbxBasePath + "stone_largeE.fbx",
                FbxBasePath + "stone_smallB.fbx",
                FbxBasePath + "stone_smallF.fbx",
                FbxBasePath + "stone_tallA.fbx",
            };

            var stoneParent = new GameObject("Stones");
            stoneParent.transform.SetParent(meadowParent.transform);
            for (int i = 0; i < 8; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f);
                float dist  = UnityEngine.Random.Range(4f, 16f);
                var pos = new Vector3(
                    -40f + Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist);
                string modelPath = stoneModels[i % stoneModels.Length];
                var go = SpawnFbxOrPrimitive(modelPath, $"Stone_{i}", pos,
                    PrimitiveType.Sphere, Color.gray);
                go.transform.SetParent(stoneParent.transform);
                EnsureCollider(go);
            }

            // 8 кустов с EnvironmentReactor
            string[] bushModels = {
                FbxBasePath + "plant_bush.fbx",
                FbxBasePath + "plant_bushSmall.fbx",
                FbxBasePath + "plant_bushDetailed.fbx",
                FbxBasePath + "plant_bushLarge.fbx",
            };

            var bushParent = new GameObject("Bushes");
            bushParent.transform.SetParent(meadowParent.transform);
            for (int i = 0; i < 8; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f);
                float dist  = UnityEngine.Random.Range(3f, 14f);
                var pos = new Vector3(
                    -40f + Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist);
                string modelPath = bushModels[i % bushModels.Length];
                var go = SpawnFbxOrPrimitive(modelPath, $"Bush_{i}", pos,
                    PrimitiveType.Sphere, new Color(0.2f, 0.5f, 0.15f));
                go.transform.localScale = Vector3.one * 0.8f;
                go.transform.SetParent(bushParent.transform);
                go.AddComponent<ZeldaDaughter.World.EnvironmentReactor>();
                // SphereCollider с isTrigger — обязателен для OnTriggerEnter в EnvironmentReactor
                var bushCol = go.AddComponent<SphereCollider>();
                bushCol.isTrigger = true;
                bushCol.radius = 0.6f;
            }

            // 5 ResourceNode
            var resourceParent = new GameObject("ResourceNodes");
            resourceParent.transform.SetParent(meadowParent.transform);
            PlaceDemoResourceNode(resourceParent, "ResourceNode_Tree0",
                new Vector3(-35f, 0f, 8f), "Assets/Data/World/ResourceNode_Tree.asset", "tree_oak.fbx",
                PrimitiveType.Cylinder, new Color(0.35f, 0.65f, 0.2f));
            PlaceDemoResourceNode(resourceParent, "ResourceNode_Tree1",
                new Vector3(-45f, 0f, -6f), "Assets/Data/World/ResourceNode_Tree.asset", "tree_fat.fbx",
                PrimitiveType.Cylinder, new Color(0.3f, 0.6f, 0.15f));
            PlaceDemoResourceNode(resourceParent, "ResourceNode_Stone0",
                new Vector3(-32f, 0f, -10f), "Assets/Data/World/ResourceNode_Stone.asset", "stone_largeB.fbx",
                PrimitiveType.Sphere, new Color(0.5f, 0.5f, 0.5f));
            PlaceDemoResourceNode(resourceParent, "ResourceNode_Stone1",
                new Vector3(-50f, 0f, 5f), "Assets/Data/World/ResourceNode_Stone.asset", "stone_largeD.fbx",
                PrimitiveType.Sphere, new Color(0.55f, 0.52f, 0.48f));
            PlaceDemoResourceNode(resourceParent, "ResourceNode_Ore",
                new Vector3(-48f, 0f, -8f), "Assets/Data/World/ResourceNode_Ore.asset", "stone_tallB.fbx",
                PrimitiveType.Sphere, new Color(0.7f, 0.55f, 0.3f));

            // 8 Pickupable
            var pickupParent = new GameObject("Pickupables");
            pickupParent.transform.SetParent(meadowParent.transform);
            string[] pickupDataPaths = {
                "Assets/Data/Items/Item_Stick.asset",
                "Assets/Data/Items/Item_Stone.asset",
                "Assets/Data/Items/Item_Berries.asset",
                "Assets/Data/Items/Item_Flint.asset",
                "Assets/Data/Items/Item_Stick.asset",
                "Assets/Data/Items/Item_Stone.asset",
                "Assets/Data/Items/Item_Berries.asset",
                "Assets/Data/Items/Item_Flint.asset",
            };
            Vector3[] pickupPositions = {
                new Vector3(-38f, 0f, 4f),
                new Vector3(-42f, 0f, -3f),
                new Vector3(-36f, 0f, -7f),
                new Vector3(-44f, 0f, 9f),
                new Vector3(-33f, 0f, 2f),
                new Vector3(-47f, 0f, -5f),
                new Vector3(-41f, 0f, 11f),
                new Vector3(-37f, 0f, -12f),
            };
            for (int i = 0; i < 8; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Pickup_{i}";
                go.transform.position = pickupPositions[i];
                go.transform.localScale = Vector3.one * 0.25f;
                go.transform.SetParent(pickupParent.transform);
                go.GetComponent<Renderer>().sharedMaterial =
                    CreateMaterial($"Pickup_{i}", new Color(0.8f, 0.7f, 0.2f));
                var pickupable = go.AddComponent<ZeldaDaughter.World.Pickupable>();
                var itemData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.ItemData>(
                    pickupDataPaths[i]);
                var so = new SerializedObject(pickupable);
                if (itemData != null) so.FindProperty("_itemData").objectReferenceValue = itemData;
                so.FindProperty("_amount").intValue = 1;
                so.FindProperty("_saveId").stringValue = $"pickup_meadow_{i}";
                so.ApplyModifiedProperties();
            }

            // Костёр
            var campfireParent = new GameObject("Campfire");
            campfireParent.transform.SetParent(meadowParent.transform);
            campfireParent.transform.position = new Vector3(-40f, 0f, -3f);

            var campfireFbxPath = FbxBasePath + "campfire_logs.fbx";
            var campfireVisual = SpawnFbxOrPrimitive(campfireFbxPath, "CampfireVisual",
                campfireParent.transform.position, PrimitiveType.Sphere,
                new Color(0.8f, 0.35f, 0.05f));
            campfireVisual.transform.SetParent(campfireParent.transform);
            campfireVisual.transform.localScale = Vector3.one * 0.5f;

            // SphereCollider (trigger) — для зоны взаимодействия/отдыха у костра
            var campfireCollider = campfireParent.AddComponent<SphereCollider>();
            campfireCollider.isTrigger = true;
            campfireCollider.radius = 2f;
            campfireCollider.center = new Vector3(0f, 0.5f, 0f);

            var campfireObj = campfireParent.AddComponent<ZeldaDaughter.World.CampfireObject>();
            {
                var campfireConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.CampfireConfig>(
                    "Assets/Data/World/CampfireConfig.asset");
                var so = new SerializedObject(campfireObj);
                if (campfireConfig != null) so.FindProperty("_config").objectReferenceValue = campfireConfig;
                so.FindProperty("_unlitVisual").objectReferenceValue = campfireVisual;
                so.FindProperty("_litVisual").objectReferenceValue = campfireVisual;

                // Точка взаимодействия
                var interPointGO = new GameObject("InteractionPoint");
                interPointGO.transform.SetParent(campfireParent.transform);
                interPointGO.transform.localPosition = new Vector3(0f, 0.5f, 0.8f);
                so.FindProperty("_interactionPoint").objectReferenceValue = interPointGO.transform;
                so.ApplyModifiedProperties();

                AddComponentIfMissing<ZeldaDaughter.Combat.RestZoneDetector>(campfireParent);
            }

            // Свет от костра
            var fireLightGO = new GameObject("FireLight");
            fireLightGO.transform.SetParent(campfireParent.transform);
            fireLightGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var fireLight = fireLightGO.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.6f, 0.1f);
            fireLight.intensity = 2f;
            fireLight.range = 5f;
            {
                var so = new SerializedObject(campfireObj);
                so.FindProperty("_fireLight").objectReferenceValue = fireLight;
                so.ApplyModifiedProperties();
            }

            // 2 EnemySpawnZone
            PlaceEnemySpawnZone("SpawnZone_Boars_Meadow",
                new Vector3(-52f, 0f, 8f),
                "Assets/Prefabs/Enemies/Boar.prefab",
                "Assets/Data/Combat/EnemyData_Boar.asset",
                maxEnemies: 2, spawnRadius: 10f);

            PlaceEnemySpawnZone("SpawnZone_Wolves_Meadow",
                new Vector3(-30f, 0f, -15f),
                "Assets/Prefabs/Enemies/Wolf.prefab",
                "Assets/Data/Combat/EnemyData_Wolf.asset",
                maxEnemies: 2, spawnRadius: 12f);

            Debug.Log("[SceneBuilder] DemoScene: Поляна размещена.");
            return meadowParent;
        }

        // Вспомогательный метод: ResourceNode
        private static void PlaceDemoResourceNode(
            GameObject parent, string goName, Vector3 pos,
            string dataPath, string fbxName,
            PrimitiveType fallback, Color color)
        {
            string fbxPath = FbxBasePath + fbxName;
            var go = SpawnFbxOrPrimitive(fbxPath, goName, pos, fallback, color);
            go.transform.SetParent(parent.transform);
            EnsureCollider(go);
            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(go);

            var node = go.AddComponent<ZeldaDaughter.World.ResourceNode>();
            var data = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.ResourceNodeData>(dataPath);
            var so = new SerializedObject(node);
            if (data != null) so.FindProperty("_data").objectReferenceValue = data;
            so.FindProperty("_saveId").stringValue = $"resourcenode_{goName.ToLower()}";
            so.ApplyModifiedProperties();
        }

        // 9. PlaceDemoRoad — дорога от поляны к городу
        private static void PlaceDemoRoad()
        {
            var roadParent = new GameObject("Road");
            roadParent.transform.position = new Vector3(-12f, 0f, 3f);

            string[] roadModels = {
                FbxTownPath + "road.fbx",
                FbxTownPath + "road-bend.fbx",
                FbxTownPath + "road-corner.fbx",
            };
            // Прямые тайлы от X=-30 до X=14, Z≈4
            string straightPath = FbxTownPath + "road.fbx";
            for (int i = 0; i < 11; i++)
            {
                float x = -30f + i * 4f;
                float z = 3f + (i < 5 ? 0f : (i - 5) * 0.4f);
                string modelPath = (i % 4 == 3) ? FbxTownPath + "road-bend.fbx" : straightPath;
                var tile = SpawnFbxOrPrimitive(modelPath, $"Road_{i}",
                    new Vector3(x, 0f, z), PrimitiveType.Cube,
                    new Color(0.55f, 0.52f, 0.45f));
                tile.transform.SetParent(roadParent.transform);
                tile.transform.localScale = new Vector3(1f, 0.05f, 1f);
            }

            // EnemySpawnZone на дороге
            PlaceEnemySpawnZone("SpawnZone_Wolves_Road",
                new Vector3(-10f, 0f, 5f),
                "Assets/Prefabs/Enemies/Wolf.prefab",
                "Assets/Data/Combat/EnemyData_Wolf.asset",
                maxEnemies: 1, spawnRadius: 6f);

            Debug.Log("[SceneBuilder] DemoScene: Дорога размещена.");
        }

        // 10. PlaceDemoCity — город
        private static void PlaceDemoCity()
        {
            var cityCenter = new Vector3(21f, 0f, 7f);
            var cityParent = new GameObject("City");
            cityParent.transform.position = cityCenter;

            // Здания (5 штук, модульная сборка стен + крыши)
            var buildingsParent = new GameObject("Buildings");
            buildingsParent.transform.SetParent(cityParent.transform);
            PlaceDemoBuilding(buildingsParent, "Building_Tavern",
                new Vector3(21f, 0f, 14f), 3, 3, false);
            PlaceDemoBuilding(buildingsParent, "Building_Smithy",
                new Vector3(14f, 0f, 5f), 2, 2, false);
            PlaceDemoBuilding(buildingsParent, "Building_Shop",
                new Vector3(28f, 0f, 8f), 2, 2, false);
            PlaceDemoBuilding(buildingsParent, "Building_Herbalist",
                new Vector3(24f, 0f, 0f), 2, 2, false);
            PlaceDemoBuilding(buildingsParent, "Building_House1",
                new Vector3(16f, 0f, 15f), 2, 2, false);

            // Фонтан в центре
            var fountainGO = SpawnFbxOrPrimitive(FbxTownPath + "fountain-round.fbx",
                "Fountain", new Vector3(21f, 0f, 7f),
                PrimitiveType.Cylinder, new Color(0.6f, 0.65f, 0.7f));
            fountainGO.transform.SetParent(cityParent.transform);
            EnsureCollider(fountainGO);

            // Фонари
            var lanternPath = FbxTownPath + "lantern.fbx";
            var lanternPositions = new Vector3[] {
                new Vector3(18f, 0f, 7f), new Vector3(24f, 0f, 7f),
                new Vector3(21f, 0f, 4f), new Vector3(21f, 0f, 10f),
            };
            var lanternParent = new GameObject("Lanterns");
            lanternParent.transform.SetParent(cityParent.transform);
            for (int i = 0; i < lanternPositions.Length; i++)
            {
                var lanternGO = SpawnFbxOrPrimitive(lanternPath, $"Lantern_{i}",
                    lanternPositions[i], PrimitiveType.Cylinder,
                    new Color(0.9f, 0.8f, 0.1f));
                lanternGO.transform.SetParent(lanternParent.transform);
                lanternGO.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
                // Точечный свет от фонаря
                var pLight = new GameObject("LanternLight");
                pLight.transform.SetParent(lanternGO.transform);
                pLight.transform.localPosition = new Vector3(0f, 1f, 0f);
                var pl = pLight.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1f, 0.85f, 0.5f);
                pl.intensity = 1.5f;
                pl.range = 6f;
            }

            // Торговые стойки
            var stallPath = FbxTownPath + "stall.fbx";
            var stallParent = new GameObject("Stalls");
            stallParent.transform.SetParent(cityParent.transform);
            SpawnFbxOrPrimitive(stallPath, "Stall_Merchant",
                new Vector3(28f, 0f, 6f), PrimitiveType.Cube,
                new Color(0.7f, 0.4f, 0.15f)).transform.SetParent(stallParent.transform);
            SpawnFbxOrPrimitive(FbxTownPath + "stall-green.fbx", "Stall_Herbalist",
                new Vector3(25f, 0f, 1f), PrimitiveType.Cube,
                new Color(0.3f, 0.6f, 0.3f)).transform.SetParent(stallParent.transform);

            // Заборы
            var fencePath = FbxTownPath + "fence.fbx";
            var fenceParent = new GameObject("Fences");
            fenceParent.transform.SetParent(cityParent.transform);
            for (int i = 0; i < 6; i++)
            {
                var fGO = SpawnFbxOrPrimitive(fencePath, $"Fence_{i}",
                    new Vector3(12f + i * 2f, 0f, 0f), PrimitiveType.Cube,
                    new Color(0.5f, 0.35f, 0.2f));
                fGO.transform.SetParent(fenceParent.transform);
                fGO.transform.localScale = new Vector3(0.15f, 0.8f, 2f);
                EnsureCollider(fGO);
            }

            // Тренировочный манекен
            var dummyGO = new GameObject("TrainingDummy");
            dummyGO.transform.SetParent(cityParent.transform);
            dummyGO.transform.position = new Vector3(15f, 0f, 8f);
            // CapsuleCollider на parent — нужен для raycast/hitbox при атаке
            var dummyCollider = dummyGO.AddComponent<CapsuleCollider>();
            dummyCollider.height = 1.8f;
            dummyCollider.radius = 0.35f;
            dummyCollider.center = new Vector3(0f, 0.9f, 0f);
            var dummyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyVisual.name = "DummyVisual";
            dummyVisual.transform.SetParent(dummyGO.transform);
            dummyVisual.transform.localPosition = Vector3.zero;
            dummyVisual.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial("TrainingDummy", new Color(0.6f, 0.45f, 0.2f));
            var dummyComp = dummyGO.AddComponent<ZeldaDaughter.Combat.TrainingDummy>();
            {
                var dummyConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.TrainingDummyConfig>(
                    "Assets/Data/Combat/TrainingDummyConfig.asset");
                var so = new SerializedObject(dummyComp);
                if (dummyConfig != null) so.FindProperty("_config").objectReferenceValue = dummyConfig;
                var anim = dummyVisual.GetComponent<Animator>();
                if (anim != null) so.FindProperty("_animator").objectReferenceValue = anim;
                so.ApplyModifiedProperties();
            }
            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(dummyGO);

            // Станки (плавильня, наковальня)
            PlaceStation(StationType.Smelter, new Vector3(13f, 0f, 4f));
            PlaceStation(StationType.Anvil,   new Vector3(15f, 0f, 4f));

            // Кровать в таверне
            PlaceBed();

            // 7 NPC
            var npcPositions = new Vector3[] {
                new Vector3(28f, 0f, 9f),   // Merchant
                new Vector3(14f, 0f, 6f),   // Blacksmith
                new Vector3(21f, 0f, 13f),  // Bartender
                new Vector3(24f, 0f, 2f),   // Herbalist
                new Vector3(19f, 0f, 3f),   // Guard
                new Vector3(25f, 0f, 12f),  // Villager1
                new Vector3(17f, 0f, 11f),  // Villager2
            };
            string[] npcNames = {
                "Merchant", "Blacksmith", "Bartender", "Herbalist",
                "Guard", "Villager1", "Villager2"
            };
            var npcParent = new GameObject("NPCs");
            npcParent.transform.SetParent(cityParent.transform);
            for (int i = 0; i < npcNames.Length; i++)
            {
                PlaceDemoNPC(npcParent, npcNames[i], npcPositions[i]);
            }

            // Также крестьянин на дороге к городу
            PlaceDemoNPC(cityParent, "Peasant", new Vector3(0f, 0f, 3f));

            // Waypoints для NPC
            var waypointsParent = new GameObject("Waypoints");
            waypointsParent.transform.SetParent(cityParent.transform);
            PlaceDemoWaypoint(waypointsParent, "WP_Fountain",    new Vector3(21f, 0f, 7f),   "fountain");
            PlaceDemoWaypoint(waypointsParent, "WP_Market",      new Vector3(28f, 0f, 8f),   "market");
            PlaceDemoWaypoint(waypointsParent, "WP_Smithy",      new Vector3(14f, 0f, 5f),   "smithy");
            PlaceDemoWaypoint(waypointsParent, "WP_Gate",        new Vector3(12f, 0f, 7f),   "gate");
            PlaceDemoWaypoint(waypointsParent, "WP_TavernDoor",  new Vector3(21f, 0f, 11f),  "tavern");
            PlaceDemoWaypoint(waypointsParent, "WP_Patrol1",     new Vector3(16f, 0f, 3f),   "patrol");
            PlaceDemoWaypoint(waypointsParent, "WP_Patrol2",     new Vector3(26f, 0f, 12f),  "patrol");

            Debug.Log("[SceneBuilder] DemoScene: Город размещён.");
        }

        // Создаёт модульное здание из wall.fbx + roof.fbx
        private static void PlaceDemoBuilding(
            GameObject parent, string buildingName,
            Vector3 origin, int widthTiles, int depthTiles,
            bool hasChimney)
        {
            var buildingGO = new GameObject(buildingName);
            buildingGO.transform.SetParent(parent.transform);
            buildingGO.transform.position = origin;

            float tileSize = 2f;

            // Стены — по периметру
            for (int x = 0; x < widthTiles; x++)
            {
                // Передняя стена
                var wallF = SpawnFbxOrPrimitive(FbxTownPath + "wall-wood.fbx",
                    $"WallFront_{x}", origin + new Vector3(x * tileSize, 0f, 0f),
                    PrimitiveType.Cube, new Color(0.55f, 0.38f, 0.22f));
                wallF.transform.SetParent(buildingGO.transform);
                wallF.transform.localScale = new Vector3(tileSize, tileSize, 0.2f);
                EnsureCollider(wallF);

                // Задняя стена
                var wallB = SpawnFbxOrPrimitive(FbxTownPath + "wall-wood.fbx",
                    $"WallBack_{x}", origin + new Vector3(x * tileSize, 0f, depthTiles * tileSize),
                    PrimitiveType.Cube, new Color(0.5f, 0.35f, 0.2f));
                wallB.transform.SetParent(buildingGO.transform);
                wallB.transform.localScale = new Vector3(tileSize, tileSize, 0.2f);
                EnsureCollider(wallB);
            }
            for (int z = 0; z < depthTiles; z++)
            {
                // Левая стена
                var wallL = SpawnFbxOrPrimitive(FbxTownPath + "wall-wood-corner.fbx",
                    $"WallLeft_{z}", origin + new Vector3(-0.1f, 0f, z * tileSize),
                    PrimitiveType.Cube, new Color(0.5f, 0.35f, 0.2f));
                wallL.transform.SetParent(buildingGO.transform);
                wallL.transform.localScale = new Vector3(0.2f, tileSize, tileSize);
                EnsureCollider(wallL);

                // Правая стена
                var wallR = SpawnFbxOrPrimitive(FbxTownPath + "wall-wood-corner.fbx",
                    $"WallRight_{z}", origin + new Vector3(widthTiles * tileSize + 0.1f, 0f, z * tileSize),
                    PrimitiveType.Cube, new Color(0.5f, 0.35f, 0.2f));
                wallR.transform.SetParent(buildingGO.transform);
                wallR.transform.localScale = new Vector3(0.2f, tileSize, tileSize);
                EnsureCollider(wallR);
            }

            // Крыша
            for (int x = 0; x < widthTiles; x++)
            {
                var roofTile = SpawnFbxOrPrimitive(FbxTownPath + "roof-high.fbx",
                    $"Roof_{x}", origin + new Vector3(x * tileSize, tileSize * 1.5f, depthTiles * tileSize * 0.5f),
                    PrimitiveType.Cube, new Color(0.55f, 0.18f, 0.12f));
                roofTile.transform.SetParent(buildingGO.transform);
                roofTile.transform.localScale = new Vector3(tileSize, tileSize * 0.8f, depthTiles * tileSize);
            }

            // Дымоход (опционально)
            if (hasChimney)
            {
                var chimney = SpawnFbxOrPrimitive(FbxTownPath + "chimney.fbx",
                    $"{buildingName}_Chimney",
                    origin + new Vector3(widthTiles * tileSize * 0.5f, tileSize * 2f, depthTiles * tileSize * 0.5f),
                    PrimitiveType.Cylinder, new Color(0.4f, 0.38f, 0.35f));
                chimney.transform.SetParent(buildingGO.transform);
            }
        }

        // Размещает NPC: prefab → capsule fallback
        private static void PlaceDemoNPC(GameObject parent, string npcName, Vector3 position)
        {
            string prefabPath = $"Assets/Prefabs/NPCs/{npcName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            GameObject npcGO;
            if (prefab != null)
            {
                npcGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                npcGO.transform.position = position;
                Debug.Log($"[SceneBuilder] DemoScene: NPC {npcName} из prefab.");
            }
            else
            {
                // Fallback: capsule с компонентами
                npcGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npcGO.name = npcName;
                npcGO.transform.position = position;
                npcGO.GetComponent<Renderer>().sharedMaterial =
                    CreateMaterial($"NPC_{npcName}", new Color(0.7f, 0.5f, 0.3f));
                var npcInteractable = npcGO.AddComponent<ZeldaDaughter.NPC.NPCInteractable>();

                // Загрузить NPCProfile если доступен
                var npcProfile = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.NPCProfile>(
                    $"Assets/Data/NPC/Profiles/NPC_{npcName}.asset");
                if (npcProfile != null)
                {
                    var so = new SerializedObject(npcInteractable);
                    var profileProp = so.FindProperty("_profile");
                    if (profileProp != null)
                    {
                        profileProp.objectReferenceValue = npcProfile;
                        so.ApplyModifiedProperties();
                    }
                }
                Debug.LogWarning($"[SceneBuilder] DemoScene: NPC prefab не найден ({prefabPath}), используется capsule.");
            }

            npcGO.transform.SetParent(parent.transform);
        }

        // Waypoint для NPCScheduler
        private static void PlaceDemoWaypoint(GameObject parent, string goName, Vector3 position, string waypointId)
        {
            var wpGO = new GameObject(goName);
            wpGO.transform.SetParent(parent.transform);
            wpGO.transform.position = position;
            var wp = wpGO.AddComponent<ZeldaDaughter.NPC.NPCWaypoint>();
            var so = new SerializedObject(wp);
            so.FindProperty("_waypointId").stringValue = waypointId;
            so.ApplyModifiedProperties();
        }

        // 11. WireDemoReferences — финальная перелинковка
        private static void WireDemoReferences()
        {
            var player       = GameObject.Find("Player");
            var dialogueMgr  = Object.FindObjectOfType<ZeldaDaughter.NPC.DialogueManager>();
            var tradeMgr     = Object.FindObjectOfType<ZeldaDaughter.NPC.TradeManager>();
            var questMgr     = Object.FindObjectOfType<ZeldaDaughter.Quest.QuestManager>();
            var gestureDisp  = Object.FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            var tapManager   = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            var dialogueUI   = Object.FindObjectOfType<ZeldaDaughter.UI.DialoguePanelUI>();
            var tradeUI      = Object.FindObjectOfType<ZeldaDaughter.UI.TradeUI>();
            var mapPanelUI   = Object.FindObjectOfType<ZeldaDaughter.UI.MapPanelUI>();
            var notebookUI   = Object.FindObjectOfType<ZeldaDaughter.UI.NotebookPanelUI>();
            var mapMgr       = Object.FindObjectOfType<ZeldaDaughter.World.MapManager>();
            var notebookMgr  = Object.FindObjectOfType<ZeldaDaughter.UI.NotebookManager>();

            // DialogueManager ↔ DialoguePanelUI
            if (dialogueMgr != null && dialogueUI != null)
            {
                var so = new SerializedObject(dialogueMgr);
                var panelProp = so.FindProperty("_dialoguePanel");
                if (panelProp != null)
                {
                    panelProp.objectReferenceValue = dialogueUI;
                    so.ApplyModifiedProperties();
                }
            }

            // TradeManager ↔ TradeUI
            if (tradeMgr != null && tradeUI != null)
            {
                var so = new SerializedObject(tradeMgr);
                var uiProp = so.FindProperty("_tradeUI");
                if (uiProp != null)
                {
                    uiProp.objectReferenceValue = tradeUI;
                    so.ApplyModifiedProperties();
                }
            }

            // QuestManager ↔ DialogueManager (уже задан в SetupDemoNPCSystems, но уточняем)
            if (dialogueMgr != null && questMgr != null)
            {
                var so = new SerializedObject(dialogueMgr);
                var qProp = so.FindProperty("_questManager");
                if (qProp != null)
                {
                    qProp.objectReferenceValue = questMgr;
                    so.ApplyModifiedProperties();
                }
            }

            // KnockoutSystem ↔ GestureDispatcher
            if (player != null && gestureDisp != null)
            {
                var knockout = player.GetComponent<ZeldaDaughter.Combat.KnockoutSystem>();
                if (knockout != null)
                {
                    var so = new SerializedObject(knockout);
                    var gProp = so.FindProperty("_gestureDispatcher");
                    if (gProp != null)
                    {
                        gProp.objectReferenceValue = gestureDisp;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            // TapInteractionManager ↔ Player, AutoMove, CombatController
            if (tapManager != null && player != null)
            {
                var autoMove   = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                var combatCtrl = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                var so = new SerializedObject(tapManager);
                so.FindProperty("_player").objectReferenceValue = player;
                if (autoMove != null)
                {
                    var amProp = so.FindProperty("_autoMove");
                    if (amProp != null) amProp.objectReferenceValue = autoMove;
                }
                if (combatCtrl != null)
                {
                    var ccProp = so.FindProperty("_combatController");
                    if (ccProp != null) ccProp.objectReferenceValue = combatCtrl;
                }
                so.ApplyModifiedProperties();
            }

            // NPCInteractable на всех NPC → DialogueManager
            var allNPCs = Object.FindObjectsOfType<ZeldaDaughter.NPC.NPCInteractable>();
            foreach (var npc in allNPCs)
            {
                var so = new SerializedObject(npc);
                var dmProp = so.FindProperty("_dialogueManager");
                if (dmProp != null && dialogueMgr != null)
                {
                    dmProp.objectReferenceValue = dialogueMgr;
                    so.ApplyModifiedProperties();
                }
            }

            // WaterZone — река между поляной (x=-40) и городом (x=+21)
            if (GameObject.Find("WaterZone") == null)
            {
                var waterZoneGO = new GameObject("WaterZone");
                waterZoneGO.transform.position = new Vector3(42f, 0f, 0f);
                var waterVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                waterVisual.name = "WaterVisual";
                waterVisual.transform.SetParent(waterZoneGO.transform);
                waterVisual.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                waterVisual.transform.localScale = new Vector3(6f, 0.3f, 40f);
                waterVisual.GetComponent<Renderer>().sharedMaterial =
                    CreateMaterial("Water", new Color(0.1f, 0.35f, 0.75f, 0.7f));
                // Убрать стандартный коллайдер с визуала
                Object.DestroyImmediate(waterVisual.GetComponent<BoxCollider>());

                var boxCol = waterZoneGO.AddComponent<BoxCollider>();
                boxCol.isTrigger = true;
                boxCol.size = new Vector3(6f, 2f, 40f);
                boxCol.center = new Vector3(0f, 0f, 0f);

                waterZoneGO.AddComponent<ZeldaDaughter.World.WaterZone>();
            }

            Debug.Log("[SceneBuilder] DemoScene: WireReferences завершён.");
        }
    }
}
