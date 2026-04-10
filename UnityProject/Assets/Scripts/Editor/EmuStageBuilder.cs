using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Builds lightweight scenes for each TESTING_GUIDE stage.
    /// Each scene contains ONLY the mechanics needed for that stage's tests.
    /// Rule: ≤15 root objects to avoid SwiftShader SIGSEGV.
    /// </summary>
    public static class EmuStageBuilder
    {
        private static Shader _shader;

        private static void Init()
        {
            _shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        }

        private static Material Mat(string name, Color c)
        {
            var path = $"Assets/Materials/Emu_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = c;
                return existing;
            }
            var mat = new Material(_shader);
            mat.name = $"Emu_{name}";
            mat.SetColor("_Color", c);
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ============================================================
        // Stage 1: Запуск + Движение (TESTING_GUIDE секции 1-2)
        // Нужно: Ground, Player(+movement), Camera(isometric), Trees, Bushes, Light
        // ============================================================
        [MenuItem("ZeldaDaughter/Scenes/Build EmuStage1 (Launch+Movement)")]
        public static void BuildStage1()
        {
            SetupStage1Base();
            const string path = "Assets/Scenes/EmuStage1.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage 1 created: {path}");
        }

        private static void SetupStage1Base()
        {
            Init();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // === Ground (большая поляна) ===
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10); // 100x100 — big enough
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Ground", new Color(0.3f, 0.55f, 0.2f));

            // === Directional Light ===
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(50, 170, 0);

            // === Player ===
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0, 0.05f, 0); // Just above ground
            player.GetComponent<Renderer>().sharedMaterial = Mat("Player", new Color(0.2f, 0.4f, 0.8f));
            // Remove default CapsuleCollider, use CharacterController only
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);
            player.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            player.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();
            player.AddComponent<ZeldaDaughter.World.SurfaceDetector>();

            // === Camera (isometric, follows player) ===
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8;
            cam.backgroundColor = new Color(0.53f, 0.76f, 0.96f); // Light sky blue
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            // Isometric position: 30-45 degree angle
            camGo.transform.position = new Vector3(0, 12, -12);
            camGo.transform.rotation = Quaternion.Euler(45, 0, 0);
            var isoCam = camGo.AddComponent<ZeldaDaughter.World.IsometricCamera>();

            // === GameBootstrap ===
            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<ZeldaDaughter.World.GameBootstrap>();

            // === Input System ===
            var inputGo = new GameObject("InputSystem");
            inputGo.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();

            // === EventSystem (needed for touch input) ===
            var eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<EventSystem>();
            eventSys.AddComponent<StandaloneInputModule>();

            // === Trees (with colliders — for collision test) ===
            var treesParent = new GameObject("Trees");
            Vector3[] treePositions = {
                new(-5, 0, 8), new(-3, 0, -6), new(4, 0, 5),
                new(7, 0, -3), new(-8, 0, -4), new(6, 0, 9)
            };
            foreach (var pos in treePositions)
            {
                CreateTree(treesParent.transform, pos);
            }

            // === Bushes (with EnvironmentReactor — sway when player passes) ===
            var bushesParent = new GameObject("Bushes");
            Vector3[] bushPositions = {
                new(-2, 0.3f, 3), new(3, 0.3f, -2), new(-6, 0.3f, 1), new(5, 0.3f, 7)
            };
            foreach (var pos in bushPositions)
            {
                CreateBush(bushesParent.transform, pos);
            }

            // === Stones (as obstacles) ===
            var stonesParent = new GameObject("Stones");
            Vector3[] stonePositions = {
                new(2, 0.2f, -5), new(-4, 0.15f, 6), new(8, 0.25f, 2)
            };
            foreach (var pos in stonePositions)
            {
                var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = "Stone";
                stone.transform.SetParent(stonesParent.transform);
                stone.transform.position = pos;
                stone.transform.localScale = Vector3.one * Random.Range(0.5f, 0.9f);
                stone.GetComponent<Renderer>().sharedMaterial = Mat("Stone", new Color(0.5f, 0.5f, 0.52f));
                // BoxCollider for blocking
                Object.DestroyImmediate(stone.GetComponent<SphereCollider>());
                var box = stone.AddComponent<BoxCollider>();
            }

            // Wire IsometricCamera to Player
            WireIsometricCamera(isoCam, player.transform);
        }

        // ============================================================
        // Stage 2: Взаимодействие с миром (TESTING_GUIDE секции 3-4)
        // Нужно: Pickupable предметы, ResourceNode, TapInteractionManager, PlayerInventory
        // ============================================================
        [MenuItem("ZeldaDaughter/Scenes/Build EmuStage2 (Interaction)")]
        public static void BuildStage2()
        {
            Init();

            // Start from Stage1 base (without saving)
            SetupStage1Base();

            // Add TapInteractionManager
            var tapSys = new GameObject("TapSystem");
            tapSys.AddComponent<ZeldaDaughter.World.TapInteractionManager>();

            // Add PlayerInventory to Player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>() == null)
                    player.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            }

            // Add SaveManager (needed for save IDs on pickupables)
            var saveSys = new GameObject("SaveManager");
            saveSys.AddComponent<ZeldaDaughter.Save.SaveManager>();

            // Place Pickupable items near player (within view)
            var pickupParent = new GameObject("Pickupables");
            string[] itemPaths = {
                "Assets/Content/Items/Item_Stick.asset",
                "Assets/Content/Items/Item_Stone.asset",
                "Assets/Content/Items/Item_Berry.asset",
            };
            Vector3[] pickupPositions = {
                new(3, 1f, 3),     // stick - close to player, high enough for raycast
                new(-4, 1f, 2),    // stone - left of player
                new(2, 1f, -4),    // berry - behind player
            };

            for (int i = 0; i < itemPaths.Length; i++)
            {
                var itemData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.ItemData>(itemPaths[i]);
                if (itemData == null)
                {
                    Debug.LogWarning($"[EmuStageBuilder] ItemData not found: {itemPaths[i]}");
                    continue;
                }

                var pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pickup.name = $"Pickup_{itemData.name}";
                pickup.transform.SetParent(pickupParent.transform);
                pickup.transform.position = pickupPositions[i];
                pickup.transform.localScale = Vector3.one * 1.0f; // Big enough for raycast hit
                pickup.GetComponent<Renderer>().sharedMaterial = Mat("Pickup", new Color(0.9f, 0.8f, 0.2f));

                // Keep SphereCollider (from CreatePrimitive) for raycast detection
                // Scale 0.5 + default collider = good hit area

                var pickupComp = pickup.AddComponent<ZeldaDaughter.World.Pickupable>();
                // Wire ItemData via SerializedObject
                var so = new SerializedObject(pickupComp);
                var itemProp = so.FindProperty("_itemData");
                if (itemProp != null) itemProp.objectReferenceValue = itemData;
                var amountProp = so.FindProperty("_amount");
                if (amountProp != null) amountProp.intValue = 1;
                var saveProp = so.FindProperty("_saveId");
                if (saveProp != null) saveProp.stringValue = $"pickup_{i}";
                so.ApplyModifiedPropertiesWithoutUndo();

                // Add InteractableHighlight if it exists
                var highlightType = System.Type.GetType("ZeldaDaughter.World.InteractableHighlight, Assembly-CSharp");
                if (highlightType != null)
                    pickup.AddComponent(highlightType);
            }

            // Place ResourceNode trees (harvestable)
            var resParent = new GameObject("ResourceNodes");
            var treeData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.ResourceNodeData>("Assets/Content/Resources/Resource_Tree.asset");
            if (treeData != null)
            {
                Vector3[] resTreePositions = { new(5, 0, 3), new(-5, 0, -5) };
                for (int i = 0; i < resTreePositions.Length; i++)
                {
                    var resTree = new GameObject($"ResourceTree_{i}");
                    resTree.transform.SetParent(resParent.transform);
                    resTree.transform.position = resTreePositions[i];

                    // Visual
                    CreateTree(resTree.transform, resTreePositions[i]);

                    // ResourceNode component
                    var resNode = resTree.AddComponent<ZeldaDaughter.World.ResourceNode>();
                    var resSo = new SerializedObject(resNode);
                    var dataProp = resSo.FindProperty("_data");
                    if (dataProp != null) dataProp.objectReferenceValue = treeData;
                    var resSaveProp = resSo.FindProperty("_saveId");
                    if (resSaveProp != null) resSaveProp.stringValue = $"res_tree_{i}";
                    resSo.ApplyModifiedPropertiesWithoutUndo();

                    // Collider for tap detection
                    var resCol = resTree.AddComponent<CapsuleCollider>();
                    resCol.radius = 0.5f;
                    resCol.height = 3f;
                    resCol.center = new Vector3(0, 1.5f, 0);

                    if (System.Type.GetType("ZeldaDaughter.World.InteractableHighlight, Assembly-CSharp") is var ht && ht != null)
                        resTree.AddComponent(ht);
                }
            }

            // Wire TapInteractionManager
            var tapMgr = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            if (tapMgr != null && player != null)
            {
                var tapSo = new SerializedObject(tapMgr);
                var playerProp = tapSo.FindProperty("_player");
                if (playerProp != null) playerProp.objectReferenceValue = player; // GameObject, not Transform
                var autoMoveProp = tapSo.FindProperty("_autoMove");
                var autoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                if (autoMoveProp != null && autoMove != null) autoMoveProp.objectReferenceValue = autoMove;
                tapSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Add OnboardingManager with hint canvases
            SetupOnboarding();

            // Overwrite scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            const string path = "Assets/Scenes/EmuStage2.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage 2 created: {path}");
        }

        // ============================================================
        // Onboarding Setup
        // ============================================================

        private static void SetupOnboarding()
        {
            // Canvas for onboarding hints
            var canvasGo = new GameObject("OnboardingCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            // Swipe hint
            var swipeHintGo = CreateHintUI(canvasGo.transform, "SwipeHint",
                new Vector2(0, -150), new Vector2(120, 120),
                new Color(1f, 1f, 1f, 0.8f), "^", 60);
            var swipeHint = swipeHintGo.AddComponent<ZeldaDaughter.UI.OnboardingHint>();

            // Tap hint
            var tapHintGo = CreateHintUI(canvasGo.transform, "TapHint",
                new Vector2(0, -250), new Vector2(100, 100),
                new Color(1f, 0.9f, 0.4f, 0.8f), "O", 40);
            var tapHint = tapHintGo.AddComponent<ZeldaDaughter.UI.OnboardingHint>();

            // OnboardingManager
            var mgrGo = new GameObject("OnboardingManager");
            var mgr = mgrGo.AddComponent<ZeldaDaughter.UI.OnboardingManager>();
            var mgrSo = new SerializedObject(mgr);
            var swipeProp = mgrSo.FindProperty("_swipeHint");
            if (swipeProp != null) swipeProp.objectReferenceValue = swipeHint;
            var tapProp = mgrSo.FindProperty("_tapHint");
            if (tapProp != null) tapProp.objectReferenceValue = tapHint;
            mgrSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static void CreateTree(Transform parent, Vector3 pos)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent);
            tree.transform.position = pos;

            // Trunk
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = new Vector3(0, 0.75f, 0);
            trunk.transform.localScale = new Vector3(0.25f, 0.75f, 0.25f);
            trunk.GetComponent<Renderer>().sharedMaterial = Mat("Trunk", new Color(0.45f, 0.3f, 0.15f));
            Object.DestroyImmediate(trunk.GetComponent<CapsuleCollider>());

            // Crown
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(tree.transform);
            crown.transform.localPosition = new Vector3(0, 2f, 0);
            crown.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
            crown.GetComponent<Renderer>().sharedMaterial = Mat("Foliage", new Color(0.15f, 0.5f, 0.1f));
            Object.DestroyImmediate(crown.GetComponent<SphereCollider>());

            // Collider on the tree root (blocks player)
            var col = tree.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 3f;
            col.center = new Vector3(0, 1.5f, 0);
        }

        private static GameObject CreateHintUI(Transform canvasParent, string name,
            Vector2 position, Vector2 size, Color color, string label, int fontSize)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            go.transform.SetParent(canvasParent, false);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.AddComponent<CanvasGroup>().alpha = 0f;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            // Label
            var labelGo = new GameObject("Label");
            labelGo.AddComponent<RectTransform>();
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.AddComponent<UnityEngine.UI.Text>();
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            var lrt = labelGo.GetComponent<RectTransform>() ?? labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return go;
        }

        private static void CreateBush(Transform parent, Vector3 pos)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush";
            bush.transform.SetParent(parent);
            bush.transform.position = pos;
            bush.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            bush.GetComponent<Renderer>().sharedMaterial = Mat("Bush", new Color(0.2f, 0.55f, 0.15f));

            // Trigger collider for EnvironmentReactor
            Object.DestroyImmediate(bush.GetComponent<SphereCollider>());
            var trigger = bush.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;

            // EnvironmentReactor — sways when player passes through
            var reactorType = System.Type.GetType("ZeldaDaughter.World.EnvironmentReactor, Assembly-CSharp");
            if (reactorType != null)
                bush.AddComponent(reactorType);
        }

        // ============================================================
        // Stage 3: Инвентарь и крафт (TESTING_GUIDE секции 6-7)
        // Нужно: PlayerInventory, WeightSystem, RadialMenu, InventoryPanel,
        //        LongPressIndicator, CraftingSystem, CraftFeedback, Pickupables
        // ============================================================
        [MenuItem("ZeldaDaughter/Scenes/Build EmuStage3 (Inventory+Craft)")]
        public static void BuildStage3()
        {
            Init();
            SetupStage1Base();

            // === PlayerInventory + WeightSystem on Player ===
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>() == null)
                    player.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();

                var weightSys = player.AddComponent<ZeldaDaughter.Inventory.WeightSystem>();
                var movement = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();
                if (movement != null)
                {
                    var wso = new SerializedObject(weightSys);
                    var moveProp = wso.FindProperty("_characterMovement");
                    if (moveProp != null) moveProp.objectReferenceValue = movement;
                    wso.ApplyModifiedPropertiesWithoutUndo();
                }

                // CraftFeedback также живёт на Player
                player.AddComponent<ZeldaDaughter.UI.CraftFeedback>();
            }

            // === TapInteractionManager ===
            var tapSys = new GameObject("TapSystem");
            tapSys.AddComponent<ZeldaDaughter.World.TapInteractionManager>();

            // Wire TapInteractionManager
            var tapMgr = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            if (tapMgr != null && player != null)
            {
                var tapSo = new SerializedObject(tapMgr);
                var playerProp = tapSo.FindProperty("_player");
                if (playerProp != null) playerProp.objectReferenceValue = player;
                var autoMoveProp = tapSo.FindProperty("_autoMove");
                var autoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                if (autoMoveProp != null && autoMove != null) autoMoveProp.objectReferenceValue = autoMove;
                tapSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // === RadialMenu Canvas (sortingOrder=99) ===
            var radialCanvasGo = new GameObject("RadialMenuCanvas");
            var radialCanvas = radialCanvasGo.AddComponent<Canvas>();
            radialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            radialCanvas.sortingOrder = 99;
            radialCanvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            radialCanvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var radialMenuGo = new GameObject("RadialMenu");
            radialMenuGo.AddComponent<RectTransform>().SetParent(radialCanvasGo.transform, false);
            var radialController = radialMenuGo.AddComponent<ZeldaDaughter.UI.RadialMenuController>();

            // CanvasGroup для радиального меню
            var radialCg = radialMenuGo.AddComponent<CanvasGroup>();
            radialCg.alpha = 0f;

            // Wire _playerTransform и _canvasGroup в RadialMenuController
            if (player != null)
            {
                var rso = new SerializedObject(radialController);
                var ptProp = rso.FindProperty("_playerTransform");
                if (ptProp != null) ptProp.objectReferenceValue = player.transform;
                var cgProp = rso.FindProperty("_canvasGroup");
                if (cgProp != null) cgProp.objectReferenceValue = radialCg;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            // === Inventory Canvas (sortingOrder=100) ===
            var invCanvasGo = new GameObject("InventoryCanvas");
            var invCanvas = invCanvasGo.AddComponent<Canvas>();
            invCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            invCanvas.sortingOrder = 100;
            invCanvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            invCanvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var invPanelGo = new GameObject("InventoryPanel");
            invPanelGo.AddComponent<RectTransform>().SetParent(invCanvasGo.transform, false);
            var invCg = invPanelGo.AddComponent<CanvasGroup>();
            invCg.alpha = 0f;
            var invPanel = invPanelGo.AddComponent<ZeldaDaughter.UI.InventoryPanel>();

            // Wire _canvasGroup в InventoryPanel
            {
                var iso = new SerializedObject(invPanel);
                var cgProp = iso.FindProperty("_canvasGroup");
                if (cgProp != null) cgProp.objectReferenceValue = invCg;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }

            // === LongPress Canvas (sortingOrder=98) ===
            var lpCanvasGo = new GameObject("LongPressCanvas");
            var lpCanvas = lpCanvasGo.AddComponent<Canvas>();
            lpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lpCanvas.sortingOrder = 98;

            var lpIndicatorGo = new GameObject("LongPressIndicator");
            var lpRt = lpIndicatorGo.AddComponent<RectTransform>();
            lpRt.SetParent(lpCanvasGo.transform, false);
            var lpCg = lpIndicatorGo.AddComponent<CanvasGroup>();
            lpCg.alpha = 0f;
            var lpIndicator = lpIndicatorGo.AddComponent<ZeldaDaughter.UI.LongPressIndicator>();

            // Дочерний Image для _fillImage
            var fillImageGo = new GameObject("FillImage");
            fillImageGo.AddComponent<RectTransform>().SetParent(lpIndicatorGo.transform, false);
            var fillImg = fillImageGo.AddComponent<UnityEngine.UI.Image>();
            fillImg.type = UnityEngine.UI.Image.Type.Filled;
            fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            fillImg.fillAmount = 0f;
            fillImg.color = new Color(1f, 0.9f, 0.2f, 0.85f);

            // Wire _fillImage, _canvasGroup, _followTarget в LongPressIndicator
            {
                var lpso = new SerializedObject(lpIndicator);
                var fillProp = lpso.FindProperty("_fillImage");
                if (fillProp != null) fillProp.objectReferenceValue = fillImg;
                var cgProp = lpso.FindProperty("_canvasGroup");
                if (cgProp != null) cgProp.objectReferenceValue = lpCg;
                if (player != null)
                {
                    var targetProp = lpso.FindProperty("_followTarget");
                    if (targetProp != null) targetProp.objectReferenceValue = player.transform;
                }
                lpso.ApplyModifiedPropertiesWithoutUndo();
            }

            // === SaveManager (дочерний к TapSystem — экономим root object, лимит 15) ===
            var saveSys = new GameObject("SaveManager");
            saveSys.transform.SetParent(tapSys.transform);
            saveSys.AddComponent<ZeldaDaughter.Save.SaveManager>();

            // === Pickupable предметы (те же три, что в Stage2) ===
            var pickupParent = new GameObject("Pickupables");
            string[] itemPaths = {
                "Assets/Content/Items/Item_Stick.asset",
                "Assets/Content/Items/Item_Stone.asset",
                "Assets/Content/Items/Item_Berry.asset",
            };
            Vector3[] pickupPositions = {
                new(3, 1f, 3),
                new(-4, 1f, 2),
                new(2, 1f, -4),
            };

            for (int i = 0; i < itemPaths.Length; i++)
            {
                var itemData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.ItemData>(itemPaths[i]);
                if (itemData == null)
                {
                    Debug.LogWarning($"[EmuStageBuilder] Stage3: ItemData not found: {itemPaths[i]}");
                    continue;
                }

                var pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pickup.name = $"Pickup_{itemData.name}";
                pickup.transform.SetParent(pickupParent.transform);
                pickup.transform.position = pickupPositions[i];
                pickup.transform.localScale = Vector3.one;
                pickup.GetComponent<Renderer>().sharedMaterial = Mat("Pickup", new Color(0.9f, 0.8f, 0.2f));

                var pickupComp = pickup.AddComponent<ZeldaDaughter.World.Pickupable>();
                var pso = new SerializedObject(pickupComp);
                var itemProp = pso.FindProperty("_itemData");
                if (itemProp != null) itemProp.objectReferenceValue = itemData;
                var amountProp = pso.FindProperty("_amount");
                if (amountProp != null) amountProp.intValue = 1;
                var saveProp = pso.FindProperty("_saveId");
                if (saveProp != null) saveProp.stringValue = $"s3_pickup_{i}";
                pso.ApplyModifiedPropertiesWithoutUndo();

                var highlightType = System.Type.GetType("ZeldaDaughter.World.InteractableHighlight, Assembly-CSharp");
                if (highlightType != null)
                    pickup.AddComponent(highlightType);
            }

            // === CraftRecipeDatabase — загружаем SO и добавляем хранителя в сцену ===
            var recipeDb = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.CraftRecipeDatabase>(
                "Assets/Data/Recipes/CraftRecipeDatabase.asset");
            if (recipeDb == null)
                Debug.LogWarning("[EmuStageBuilder] Stage3: CraftRecipeDatabase not found at Assets/Data/Recipes/CraftRecipeDatabase.asset");

            // CraftingSystem — статический класс, но CraftFeedback уже на Player.
            // Добавляем CraftSystemBridge-объект для хранения ссылки на БД в сцене (если компонент существует)
            var craftBridgeType = System.Type.GetType("ZeldaDaughter.Inventory.CraftSystemBridge, Assembly-CSharp");
            if (craftBridgeType != null)
            {
                var craftBridgeGo = new GameObject("CraftSystemBridge");
                var bridge = craftBridgeGo.AddComponent(craftBridgeType) as MonoBehaviour;
                if (bridge != null && recipeDb != null)
                {
                    var bso = new SerializedObject(bridge);
                    var dbProp = bso.FindProperty("_recipeDatabase");
                    if (dbProp != null) dbProp.objectReferenceValue = recipeDb;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else if (recipeDb != null)
            {
                // Нет отдельного bridge-компонента — логируем что БД найдена, крафт работает через статику
                Debug.Log($"[EmuStageBuilder] Stage3: CraftRecipeDatabase loaded ({recipeDb.Recipes.Count} recipes). Wire manually if CraftSystemBridge exists.");
            }

            // === InventoryConfig — загружаем если есть ===
            var invConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.InventoryConfig>(
                "Assets/Data/InventoryConfig.asset");
            if (invConfig == null)
                invConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.InventoryConfig>(
                    "Assets/Data/Inventory/InventoryConfig.asset");

            if (invConfig == null)
                Debug.LogWarning("[EmuStageBuilder] Stage3: InventoryConfig not found — PlayerInventory will use defaults.");

            // Сохраняем сцену
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            const string path = "Assets/Scenes/EmuStage3.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage 3 created: {path}");
        }

        // ============================================================
        // Stage 4: Боевая система (TESTING_GUIDE секция 8)
        // Нужно: PlayerHealthState, CombatController, WeaponEquipSystem,
        //        KnockoutSystem, HungerSystem, WoundEffectApplier,
        //        PlayerHitbox (trigger+HitboxTrigger+WeaponBone),
        //        TapInteractionManager, PlayerInventory, SaveManager,
        //        Enemy Boar (Capsule, EnemyHealth+EnemyFSM+EnemyAttackSignal)
        // Root objects: base(10) + TapSystem + SaveManager + Boar = 13. OK.
        // ============================================================
        [MenuItem("ZeldaDaughter/Scenes/Build EmuStage4 (Combat)")]
        public static void BuildStage4()
        {
            Init();
            SetupStage1Base();

            var player = GameObject.FindGameObjectWithTag("Player");

            // === Боевые компоненты на Player ===
            if (player != null)
            {
                // Добавляем только если ещё нет (SetupStage1Base не добавляет боевые)
                if (player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.PlayerHealthState>();
                if (player.GetComponent<ZeldaDaughter.Combat.CombatController>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.CombatController>();
                if (player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
                if (player.GetComponent<ZeldaDaughter.Combat.KnockoutSystem>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.KnockoutSystem>();
                if (player.GetComponent<ZeldaDaughter.Combat.HungerSystem>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.HungerSystem>();
                if (player.GetComponent<ZeldaDaughter.Combat.WoundEffectApplier>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.WoundEffectApplier>();

                // FoodConsumption для еды
                var foodType = System.Type.GetType("ZeldaDaughter.Combat.FoodConsumption, Assembly-CSharp");
                if (foodType != null && player.GetComponent(foodType) == null)
                    player.AddComponent(foodType);

                // Inventory для лута
                if (player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>() == null)
                    player.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();

                // === PlayerHitbox (дочерний) ===
                if (player.transform.Find("PlayerHitbox") == null)
                {
                    var hitboxGo = new GameObject("PlayerHitbox");
                    hitboxGo.transform.SetParent(player.transform);
                    hitboxGo.transform.localPosition = new Vector3(0, 0.9f, 0);

                    var col = hitboxGo.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = new Vector3(0.6f, 0.6f, 0.6f);

                    hitboxGo.AddComponent<ZeldaDaughter.Combat.HitboxTrigger>();

                    // WeaponBone — дочерний к PlayerHitbox
                    var weaponBoneGo = new GameObject("WeaponBone");
                    weaponBoneGo.transform.SetParent(hitboxGo.transform);
                    weaponBoneGo.transform.localPosition = new Vector3(0.4f, 0.3f, 0.2f);
                }

                // === Загружаем боевые конфиги ===
                var combatConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.CombatConfig>(
                    "Assets/Data/Combat/CombatConfig.asset");
                if (combatConfig == null)
                    Debug.LogWarning("[EmuStageBuilder] Stage4: CombatConfig not found at Assets/Data/Combat/CombatConfig.asset");

                var woundPuncture = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                    "Assets/Data/Combat/WoundConfig_Puncture.asset");
                var woundFracture = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                    "Assets/Data/Combat/WoundConfig_Fracture.asset");
                var woundBurn = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                    "Assets/Data/Combat/WoundConfig_Burn.asset");
                var woundPoison = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.WoundConfig>(
                    "Assets/Data/Combat/WoundConfig_Poison.asset");

                if (woundPuncture == null || woundFracture == null || woundBurn == null || woundPoison == null)
                    Debug.LogWarning("[EmuStageBuilder] Stage4: Часть WoundConfig SO не найдена. Запустите ZeldaDaughter/Data/Build Combat Data.");

                var charMovement = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();
                var charAutoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                var hitboxTransform = player.transform.Find("PlayerHitbox");
                var hitboxTrigger = hitboxTransform != null
                    ? hitboxTransform.GetComponent<ZeldaDaughter.Combat.HitboxTrigger>() : null;
                var gestureDispatcher = Object.FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();

                // Wiring PlayerHealthState
                var healthState = player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
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
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // Wiring CombatController
                var combatCtrl = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                if (combatCtrl != null)
                {
                    var so = new SerializedObject(combatCtrl);
                    if (combatConfig != null) so.FindProperty("_config").objectReferenceValue = combatConfig;
                    if (hitboxTrigger != null) so.FindProperty("_hitbox").objectReferenceValue = hitboxTrigger;
                    if (charAutoMove != null) so.FindProperty("_autoMove").objectReferenceValue = charAutoMove;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // Wiring WeaponEquipSystem
                var weaponEquip = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();
                if (weaponEquip != null && hitboxTransform != null)
                {
                    var weaponBone = hitboxTransform.Find("WeaponBone");
                    if (weaponBone != null)
                    {
                        var so = new SerializedObject(weaponEquip);
                        var boneProp = so.FindProperty("_weaponBoneAttach");
                        if (boneProp != null) boneProp.objectReferenceValue = weaponBone;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                // Wiring KnockoutSystem
                var knockoutSys = player.GetComponent<ZeldaDaughter.Combat.KnockoutSystem>();
                if (knockoutSys != null)
                {
                    var so = new SerializedObject(knockoutSys);
                    if (combatConfig != null) so.FindProperty("_config").objectReferenceValue = combatConfig;
                    if (gestureDispatcher != null) so.FindProperty("_gestureDispatcher").objectReferenceValue = gestureDispatcher;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // Wiring HungerSystem
                var hungerSys = player.GetComponent<ZeldaDaughter.Combat.HungerSystem>();
                if (hungerSys != null)
                {
                    var so = new SerializedObject(hungerSys);
                    if (combatConfig != null) so.FindProperty("_config").objectReferenceValue = combatConfig;
                    if (charMovement != null) so.FindProperty("_movement").objectReferenceValue = charMovement;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // Wiring WoundEffectApplier
                var woundApplier = player.GetComponent<ZeldaDaughter.Combat.WoundEffectApplier>();
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
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // === TapInteractionManager ===
            var tapSys = new GameObject("TapSystem");
            tapSys.AddComponent<ZeldaDaughter.World.TapInteractionManager>();

            var tapMgr = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            if (tapMgr != null && player != null)
            {
                var so = new SerializedObject(tapMgr);
                var playerProp = so.FindProperty("_player");
                if (playerProp != null) playerProp.objectReferenceValue = player;
                var autoMoveProp = so.FindProperty("_autoMove");
                var autoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                if (autoMoveProp != null && autoMove != null) autoMoveProp.objectReferenceValue = autoMove;
                var combatCtrlRef = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                var combatProp = so.FindProperty("_combatController");
                if (combatProp != null && combatCtrlRef != null) combatProp.objectReferenceValue = combatCtrlRef;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // === SaveManager ===
            var saveSys = new GameObject("SaveManager");
            saveSys.AddComponent<ZeldaDaughter.Save.SaveManager>();

            // === Enemy: Boar (красная капсула рядом с игроком) ===
            var boarGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boarGo.name = "Enemy_Boar";
            boarGo.transform.position = new Vector3(2, 0.05f, 0); // Right next to player
            boarGo.transform.localScale = Vector3.one * 1.5f; // Big for easy tap
            boarGo.GetComponent<Renderer>().sharedMaterial = Mat("Enemy", new Color(0.8f, 0.15f, 0.1f));

            // Тег Enemy если существует
            try { boarGo.tag = "Enemy"; }
            catch { boarGo.tag = "Untagged"; }

            // CapsuleCollider уже добавлен CreatePrimitive — оставляем, настраиваем
            var boarCollider = boarGo.GetComponent<CapsuleCollider>();
            if (boarCollider != null)
            {
                boarCollider.radius = 0.6f;
                boarCollider.height = 2f;
                boarCollider.center = new Vector3(0, 1f, 0);
            }

            // Грузим EnemyData_Boar
            var boarData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.EnemyData>(
                "Assets/Data/Combat/EnemyData_Boar.asset");
            if (boarData == null)
                Debug.LogWarning("[EmuStageBuilder] Stage4: EnemyData_Boar not found at Assets/Data/Combat/EnemyData_Boar.asset");

            // EnemyHealth
            var enemyHealth = boarGo.AddComponent<ZeldaDaughter.Combat.EnemyHealth>();
            if (boarData != null)
            {
                var so = new SerializedObject(enemyHealth);
                var dataProp = so.FindProperty("_data");
                if (dataProp != null) dataProp.objectReferenceValue = boarData;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // EnemyFSM
            var enemyFsm = boarGo.AddComponent<ZeldaDaughter.Combat.EnemyFSM>();
            if (boarData != null)
            {
                var so = new SerializedObject(enemyFsm);
                var dataProp = so.FindProperty("_data");
                if (dataProp != null) dataProp.objectReferenceValue = boarData;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // EnemyAttackSignal (опционально)
            var attackSignalType = System.Type.GetType("ZeldaDaughter.Combat.EnemyAttackSignal, Assembly-CSharp");
            if (attackSignalType != null)
                boarGo.AddComponent(attackSignalType);

            // DeathToCarcass — converts dead enemy into lootable CarcassObject
            boarGo.AddComponent<ZeldaDaughter.Combat.DeathToCarcass>();

            // Сохраняем сцену
            const string path = "Assets/Scenes/EmuStage4.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage 4 created: {path}");
        }

        // ============================================================
        // Stage 5: NPC и диалоги (TESTING_GUIDE секции 5, 13)
        // Нужно: NPCInteractable (Peasant/синяя капсула), TapInteractionManager,
        //        DialogueManager, LanguageSystem, PlayerInventory, SaveManager
        // Root objects: base(10) + TapSystem + NPC_Peasant + DialogueManager + LanguageSystem = 14. OK.
        // ============================================================
        [MenuItem("ZeldaDaughter/Scenes/Build EmuStage5 (NPC)")]
        public static void BuildStage5()
        {
            Init();
            // Minimal base: NO trees/bushes/stones (to stay under SwiftShader limit)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Ground", new Color(0.3f, 0.55f, 0.2f));
            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50, 170, 0);
            // Player
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 0.05f, 0);
            playerGo.GetComponent<Renderer>().sharedMaterial = Mat("Player", new Color(0.2f, 0.4f, 0.8f));
            Object.DestroyImmediate(playerGo.GetComponent<CapsuleCollider>());
            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
            playerGo.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            playerGo.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();
            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 8;
            cam.backgroundColor = new Color(0.53f, 0.76f, 0.96f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0, 12, -12);
            camGo.transform.rotation = Quaternion.Euler(45, 0, 0);
            var isoCam = camGo.AddComponent<ZeldaDaughter.World.IsometricCamera>();
            WireIsometricCamera(isoCam, playerGo.transform);
            // Bootstrap + Input + EventSystem
            new GameObject("GameBootstrap").AddComponent<ZeldaDaughter.World.GameBootstrap>();
            new GameObject("InputSystem").AddComponent<ZeldaDaughter.Input.GestureDispatcher>();
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            // Total: 7 root objects (Ground, Light, Player, Camera, Bootstrap, Input, EventSystem)

            var player = GameObject.FindGameObjectWithTag("Player");

            // === PlayerInventory on Player (нужен для торговли) ===
            if (player != null)
            {
                if (player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>() == null)
                    player.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            }

            // === TapInteractionManager ===
            var tapSys = new GameObject("TapSystem");
            tapSys.AddComponent<ZeldaDaughter.World.TapInteractionManager>();

            var tapMgr = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            if (tapMgr != null && player != null)
            {
                var so = new SerializedObject(tapMgr);
                var playerProp = so.FindProperty("_player");
                if (playerProp != null) playerProp.objectReferenceValue = player;
                var autoMoveProp = so.FindProperty("_autoMove");
                var autoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                if (autoMoveProp != null && autoMove != null) autoMoveProp.objectReferenceValue = autoMove;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // === SaveManager (дочерний к TapSystem — экономим root object, лимит 15) ===
            var saveSysGo = new GameObject("SaveManager");
            saveSysGo.transform.SetParent(tapSys.transform);
            saveSysGo.AddComponent<ZeldaDaughter.Save.SaveManager>();

            // === LanguageSystem ===
            var langSysGo = new GameObject("LanguageSystem");
            var langSys = langSysGo.AddComponent<ZeldaDaughter.NPC.LanguageSystem>();

            var langConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.LanguageConfig>(
                "Assets/Data/NPC/LanguageConfig.asset");
            if (langConfig == null)
                Debug.LogWarning("[EmuStageBuilder] Stage5: LanguageConfig не найден по пути Assets/Data/NPC/LanguageConfig.asset");
            else
            {
                var lso = new SerializedObject(langSys);
                var configProp = lso.FindProperty("_config");
                if (configProp != null) configProp.objectReferenceValue = langConfig;
                lso.ApplyModifiedPropertiesWithoutUndo();
            }

            // === DialogueManager ===
            var dialogueMgrGo = new GameObject("DialogueManager");
            var dialogueMgr = dialogueMgrGo.AddComponent<ZeldaDaughter.NPC.DialogueManager>();

            // Wiring DialogueManager: LanguageSystem + PlayerInventory (без UI — нет canvases)
            {
                var dso = new SerializedObject(dialogueMgr);
                var langProp = dso.FindProperty("_languageSystem");
                if (langProp != null) langProp.objectReferenceValue = langSys;
                if (player != null)
                {
                    var invRef = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();
                    var invProp = dso.FindProperty("_playerInventory");
                    if (invProp != null && invRef != null) invProp.objectReferenceValue = invRef;
                }
                dso.ApplyModifiedPropertiesWithoutUndo();
            }

            // === NPC: Peasant (синяя капсула-placeholder) ===
            var npcGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcGo.name = "NPC_Peasant";
            npcGo.tag = "Untagged"; // Нет тега NPC
            npcGo.transform.position = new Vector3(3, 0.05f, 2);
            npcGo.GetComponent<Renderer>().sharedMaterial = Mat("NPC", new Color(0.2f, 0.5f, 0.85f));

            // CapsuleCollider для raycast (уже добавлен CreatePrimitive — оставляем)
            var npcCollider = npcGo.GetComponent<CapsuleCollider>();
            if (npcCollider != null)
            {
                npcCollider.radius = 0.4f;
                npcCollider.height = 1.8f;
                npcCollider.center = new Vector3(0, 0.9f, 0);
            }

            // NPCInteractable + wiring _profile из Merchant как placeholder
            var npcInteractable = npcGo.AddComponent<ZeldaDaughter.NPC.NPCInteractable>();
            var merchantProfile = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.NPCProfile>(
                "Assets/Data/NPC/Profiles/NPC_Merchant.asset");
            if (merchantProfile == null)
                Debug.LogWarning("[EmuStageBuilder] Stage5: NPC_Merchant.asset не найден по пути Assets/Data/NPC/Profiles/NPC_Merchant.asset");
            else
            {
                var nso = new SerializedObject(npcInteractable);
                var profileProp = nso.FindProperty("_profile");
                if (profileProp != null) profileProp.objectReferenceValue = merchantProfile;
                nso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Сохраняем сцену
            const string path = "Assets/Scenes/EmuStage5.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage 5 created: {path}");
        }

        private static void WireIsometricCamera(Component isoCam, Transform player)
        {
            var so = new SerializedObject(isoCam);
            var targetProp = so.FindProperty("_target");
            if (targetProp != null)
                targetProp.objectReferenceValue = player;
            // Reduce distance for emulator (480x800 screen)
            var distProp = so.FindProperty("_cameraDistance");
            if (distProp != null) distProp.floatValue = 12f;
            var angleProp = so.FindProperty("_cameraAngle");
            if (angleProp != null) angleProp.floatValue = 45f;
            var yRotProp = so.FindProperty("_cameraYRotation");
            if (yRotProp != null) yRotProp.floatValue = 0f;
            var sizeProp = so.FindProperty("_orthographicSize");
            if (sizeProp != null) sizeProp.floatValue = 8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============================================================
        // Stage 6: Progression + Day/Night + Water (TESTING_GUIDE секции 10, 12, 18)
        // Нужно: DayNightCycle, PlayerStats, WaterZone, Combat (для прогрессии)
        // ============================================================
        [MenuItem("ZeldaDaughter/Scenes/Build EmuStage6 (Progression+DayNight+Water)")]
        public static void BuildStage6()
        {
            Init();
            SetupStage1Base();

            var player = GameObject.FindGameObjectWithTag("Player");

            // === Combat on Player (progression needs combat events) ===
            if (player != null)
            {
                if (player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>() == null)
                {
                    var phs = player.AddComponent<ZeldaDaughter.Combat.PlayerHealthState>();
                    var combatCfg0 = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.CombatConfig>(
                        "Assets/Data/Combat/CombatConfig.asset");
                    if (combatCfg0 != null)
                    {
                        var phsSo = new SerializedObject(phs);
                        phsSo.FindProperty("_config").objectReferenceValue = combatCfg0;
                        phsSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                if (player.GetComponent<ZeldaDaughter.Combat.CombatController>() == null)
                    player.AddComponent<ZeldaDaughter.Combat.CombatController>();
                if (player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>() == null)
                    player.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();

                // PlayerHitbox
                if (player.transform.Find("PlayerHitbox") == null)
                {
                    var hitboxGo = new GameObject("PlayerHitbox");
                    hitboxGo.transform.SetParent(player.transform);
                    hitboxGo.transform.localPosition = new Vector3(0, 0.9f, 0);
                    var col = hitboxGo.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = new Vector3(0.6f, 0.6f, 0.6f);
                    hitboxGo.AddComponent<ZeldaDaughter.Combat.HitboxTrigger>();
                }

                // Wire CombatController
                var combatConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.CombatConfig>(
                    "Assets/Data/Combat/CombatConfig.asset");
                var cc = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                if (cc != null && combatConfig != null)
                {
                    var so = new SerializedObject(cc);
                    so.FindProperty("_config").objectReferenceValue = combatConfig;
                    var autoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                    if (autoMove != null) so.FindProperty("_autoMove").objectReferenceValue = autoMove;
                    var hitbox = player.GetComponentInChildren<ZeldaDaughter.Combat.HitboxTrigger>();
                    if (hitbox != null) so.FindProperty("_hitbox").objectReferenceValue = hitbox;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // === PlayerStats (progression) ===
            var statsGo = new GameObject("PlayerStats");
            var stats = statsGo.AddComponent<ZeldaDaughter.Progression.PlayerStats>();
            var progConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Progression.ProgressionConfig>(
                "Assets/Data/Progression/ProgressionConfig.asset");
            if (progConfig != null)
            {
                var so = new SerializedObject(stats);
                var configProp = so.FindProperty("_config");
                if (configProp != null) configProp.objectReferenceValue = progConfig;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // === DayNightCycle ===
            var light = Object.FindObjectOfType<Light>();
            var dnGo = new GameObject("DayNightCycle");
            var dn = dnGo.AddComponent<ZeldaDaughter.World.DayNightCycle>();
            if (light != null)
            {
                var so = new SerializedObject(dn);
                var lightProp = so.FindProperty("_directionalLight");
                if (lightProp != null) lightProp.objectReferenceValue = light;
                // Fast cycle for testing (2 min = full day)
                var cycleProp = so.FindProperty("_fullCycleMinutes");
                if (cycleProp != null) cycleProp.floatValue = 2f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // === WaterZone (blue plane with trigger) ===
            var waterGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterGo.name = "WaterZone";
            waterGo.transform.position = new Vector3(-5, 0f, 0);
            waterGo.transform.localScale = new Vector3(4, 2f, 6);
            waterGo.GetComponent<Renderer>().sharedMaterial = Mat("Water", new Color(0.1f, 0.3f, 0.7f, 0.5f));
            Object.DestroyImmediate(waterGo.GetComponent<BoxCollider>());
            var waterTrigger = waterGo.AddComponent<BoxCollider>();
            waterTrigger.isTrigger = true;
            waterGo.AddComponent<ZeldaDaughter.World.WaterZone>();

            // === Enemy Boar (for progression testing) ===
            var boarGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boarGo.name = "Enemy_Boar";
            boarGo.transform.position = new Vector3(3, 0.05f, 0);
            boarGo.transform.localScale = Vector3.one * 1.5f;
            boarGo.GetComponent<Renderer>().sharedMaterial = Mat("Enemy", new Color(0.8f, 0.15f, 0.1f));
            try { boarGo.tag = "Enemy"; } catch { }
            var boarData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.EnemyData>(
                "Assets/Data/Combat/EnemyData_Boar.asset");
            var eh = boarGo.AddComponent<ZeldaDaughter.Combat.EnemyHealth>();
            if (boarData != null)
            {
                var so = new SerializedObject(eh);
                so.FindProperty("_data").objectReferenceValue = boarData;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var fsm = boarGo.AddComponent<ZeldaDaughter.Combat.EnemyFSM>();
            if (boarData != null)
            {
                var so = new SerializedObject(fsm);
                so.FindProperty("_data").objectReferenceValue = boarData;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // === TapInteractionManager ===
            var tapSys = new GameObject("TapSystem");
            tapSys.AddComponent<ZeldaDaughter.World.TapInteractionManager>();
            var tapMgr = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            if (tapMgr != null && player != null)
            {
                var so = new SerializedObject(tapMgr);
                so.FindProperty("_player").objectReferenceValue = player;
                var autoMove = player.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                if (autoMove != null) so.FindProperty("_autoMove").objectReferenceValue = autoMove;
                var combatCtrl = player.GetComponent<ZeldaDaughter.Combat.CombatController>();
                if (combatCtrl != null) so.FindProperty("_combatController").objectReferenceValue = combatCtrl;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // === SaveManager ===
            var saveSys = new GameObject("SaveManager");
            saveSys.AddComponent<ZeldaDaughter.Save.SaveManager>();

            const string path = "Assets/Scenes/EmuStage6.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage 6 created: {path}");
        }

        // ============================================================
        // Stage 5 Bisect variants — find which NPC component crashes SwiftShader
        // ============================================================

        /// <summary>
        /// Stage5_Bare: Same base as Stage5 + bare NPC capsule (NO NPCInteractable, NO LanguageSystem, NO DialogueManager).
        /// If this crashes → problem is in the base setup or rendering.
        /// </summary>
        [MenuItem("ZeldaDaughter/Scenes/Bisect/Stage5_Bare (no NPC components)")]
        public static void BuildStage5_Bare()
        {
            Init();
            SetupStage5Base();

            // NPC capsule WITHOUT any NPC components
            var npcGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcGo.name = "NPC_Peasant";
            npcGo.transform.position = new Vector3(3, 0.05f, 2);
            npcGo.GetComponent<Renderer>().sharedMaterial = Mat("NPC", new Color(0.2f, 0.5f, 0.85f));

            const string path = "Assets/Scenes/EmuStage5_Bare.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage5_Bare created: {path}");
        }

        /// <summary>
        /// Stage5_Lang: Base + LanguageSystem only (no DialogueManager, no NPCInteractable).
        /// Tests if LanguageSystem causes crash.
        /// </summary>
        [MenuItem("ZeldaDaughter/Scenes/Bisect/Stage5_Lang (LanguageSystem only)")]
        public static void BuildStage5_Lang()
        {
            Init();
            SetupStage5Base();

            // LanguageSystem
            var langSysGo = new GameObject("LanguageSystem");
            var langSys = langSysGo.AddComponent<ZeldaDaughter.NPC.LanguageSystem>();
            var langConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.LanguageConfig>(
                "Assets/Data/NPC/LanguageConfig.asset");
            if (langConfig != null)
            {
                var lso = new SerializedObject(langSys);
                var configProp = lso.FindProperty("_config");
                if (configProp != null) configProp.objectReferenceValue = langConfig;
                lso.ApplyModifiedPropertiesWithoutUndo();
            }

            const string path = "Assets/Scenes/EmuStage5_Lang.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage5_Lang created: {path}");
        }

        /// <summary>
        /// Stage5_NPC: Base + NPC capsule WITH NPCInteractable (no LanguageSystem, no DialogueManager).
        /// Tests if NPCInteractable causes crash.
        /// </summary>
        [MenuItem("ZeldaDaughter/Scenes/Bisect/Stage5_NPC (NPCInteractable only)")]
        public static void BuildStage5_NPC()
        {
            Init();
            SetupStage5Base();

            // NPC capsule WITH NPCInteractable
            var npcGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcGo.name = "NPC_Peasant";
            npcGo.transform.position = new Vector3(3, 0.05f, 2);
            npcGo.GetComponent<Renderer>().sharedMaterial = Mat("NPC", new Color(0.2f, 0.5f, 0.85f));
            var npcCollider = npcGo.GetComponent<CapsuleCollider>();
            if (npcCollider != null)
            {
                npcCollider.radius = 0.4f;
                npcCollider.height = 1.8f;
                npcCollider.center = new Vector3(0, 0.9f, 0);
            }
            var npcInteractable = npcGo.AddComponent<ZeldaDaughter.NPC.NPCInteractable>();
            var merchantProfile = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.NPCProfile>(
                "Assets/Data/NPC/Profiles/NPC_Merchant.asset");
            if (merchantProfile != null)
            {
                var nso = new SerializedObject(npcInteractable);
                var profileProp = nso.FindProperty("_profile");
                if (profileProp != null) profileProp.objectReferenceValue = merchantProfile;
                nso.ApplyModifiedPropertiesWithoutUndo();
            }

            const string path = "Assets/Scenes/EmuStage5_NPC.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage5_NPC created: {path}");
        }

        /// <summary>
        /// Stage5_Dialogue: Base + LanguageSystem + DialogueManager (no NPC capsule).
        /// Tests if DialogueManager causes crash.
        /// </summary>
        [MenuItem("ZeldaDaughter/Scenes/Bisect/Stage5_Dialogue (DialogueManager only)")]
        public static void BuildStage5_Dialogue()
        {
            Init();
            SetupStage5Base();

            var player = GameObject.FindGameObjectWithTag("Player");

            // LanguageSystem (DialogueManager depends on it)
            var langSysGo = new GameObject("LanguageSystem");
            var langSys = langSysGo.AddComponent<ZeldaDaughter.NPC.LanguageSystem>();
            var langConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.LanguageConfig>(
                "Assets/Data/NPC/LanguageConfig.asset");
            if (langConfig != null)
            {
                var lso = new SerializedObject(langSys);
                var configProp = lso.FindProperty("_config");
                if (configProp != null) configProp.objectReferenceValue = langConfig;
                lso.ApplyModifiedPropertiesWithoutUndo();
            }

            // DialogueManager
            var dialogueMgrGo = new GameObject("DialogueManager");
            var dialogueMgr = dialogueMgrGo.AddComponent<ZeldaDaughter.NPC.DialogueManager>();
            {
                var dso = new SerializedObject(dialogueMgr);
                var langProp = dso.FindProperty("_languageSystem");
                if (langProp != null) langProp.objectReferenceValue = langSys;
                if (player != null)
                {
                    var invRef = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();
                    var invProp = dso.FindProperty("_playerInventory");
                    if (invProp != null && invRef != null) invProp.objectReferenceValue = invRef;
                }
                dso.ApplyModifiedPropertiesWithoutUndo();
            }

            const string path = "Assets/Scenes/EmuStage5_Dialogue.unity";
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            AddToBuildSettings(path);
            Debug.Log($"[EmuStageBuilder] Stage5_Dialogue created: {path}");
        }

        /// <summary>
        /// Shared base for Stage5 bisect variants.
        /// Minimal scene: Ground, Light, Player (with movement+inventory), Camera, Bootstrap, Input, EventSystem, TapSystem+SaveManager.
        /// </summary>
        private static void SetupStage5Base()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Ground", new Color(0.3f, 0.55f, 0.2f));
            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50, 170, 0);
            // Player
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 0.05f, 0);
            playerGo.GetComponent<Renderer>().sharedMaterial = Mat("Player", new Color(0.2f, 0.4f, 0.8f));
            Object.DestroyImmediate(playerGo.GetComponent<CapsuleCollider>());
            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
            playerGo.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            playerGo.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();
            if (playerGo.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>() == null)
                playerGo.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 8;
            cam.backgroundColor = new Color(0.53f, 0.76f, 0.96f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0, 12, -12);
            camGo.transform.rotation = Quaternion.Euler(45, 0, 0);
            var isoCam = camGo.AddComponent<ZeldaDaughter.World.IsometricCamera>();
            WireIsometricCamera(isoCam, playerGo.transform);
            // Bootstrap + Input + EventSystem
            new GameObject("GameBootstrap").AddComponent<ZeldaDaughter.World.GameBootstrap>();
            new GameObject("InputSystem").AddComponent<ZeldaDaughter.Input.GestureDispatcher>();
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            // TapInteractionManager
            var tapSys = new GameObject("TapSystem");
            tapSys.AddComponent<ZeldaDaughter.World.TapInteractionManager>();
            var tapMgr = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            if (tapMgr != null)
            {
                var so = new SerializedObject(tapMgr);
                var playerProp = so.FindProperty("_player");
                if (playerProp != null) playerProp.objectReferenceValue = playerGo;
                var autoMoveProp = so.FindProperty("_autoMove");
                var autoMove = playerGo.GetComponent<ZeldaDaughter.Input.CharacterAutoMove>();
                if (autoMoveProp != null && autoMove != null) autoMoveProp.objectReferenceValue = autoMove;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // SaveManager (child of TapSystem)
            var saveSysGo = new GameObject("SaveManager");
            saveSysGo.transform.SetParent(tapSys.transform);
            saveSysGo.AddComponent<ZeldaDaughter.Save.SaveManager>();
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == scenePath) return;

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            // Put our scene first
            newScenes[0] = new EditorBuildSettingsScene(scenePath, true);
            for (int i = 0; i < scenes.Length; i++)
            {
                scenes[i].enabled = false; // Disable other scenes
                newScenes[i + 1] = scenes[i];
            }
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
