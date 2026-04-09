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
            // Remove default CapsuleCollider (CharacterController provides its own)
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

        private static void WireIsometricCamera(Component isoCam, Transform player)
        {
            var so = new SerializedObject(isoCam);
            var targetProp = so.FindProperty("_target");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = player;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[EmuStageBuilder] IsometricCamera._target not found, camera won't follow player");
            }
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
