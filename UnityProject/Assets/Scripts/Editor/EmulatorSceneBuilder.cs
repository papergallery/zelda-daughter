using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Builds an emulator-compatible scene using only Unity primitives and Unlit shaders.
    /// SwiftShader (Android emulator) crashes on URP/Lit, so we use Unlit/Color fallback.
    /// Called via -executeMethod ZeldaDaughter.Editor.EmulatorSceneBuilder.BuildEmulatorScene
    /// </summary>
    public static class EmulatorSceneBuilder
    {
        private static Shader _unlitShader;
        private static bool _useBaseColor;

        [MenuItem("ZeldaDaughter/Scenes/Build Emulator Scene")]
        public static void BuildEmulatorScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemies"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");

            ResolveUnlitShader();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = SetupScene();
            var player = SetupPlayer();
            SetupCamera(player);
            SetupCoreSystems(sun);
            SetupNPCSystems(player);
            SetupQuestSystems(player);
            SetupUI(player);

            PlaceMeadow();
            PlaceRoad();
            PlaceCity();

            WireReferences();

            const string scenePath = "Assets/Scenes/EmulatorScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            Debug.Log($"[EmulatorSceneBuilder] EmulatorScene created at {scenePath}");
        }

        // ─────────────────────────────────────────────────────────────────
        // Shader resolution
        // ─────────────────────────────────────────────────────────────────

        private static void ResolveUnlitShader()
        {
            _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_unlitShader != null)
            {
                _useBaseColor = true;
                Debug.Log("[EmulatorSceneBuilder] Using URP/Unlit shader.");
                return;
            }

            _unlitShader = Shader.Find("Unlit/Color");
            if (_unlitShader != null)
            {
                _useBaseColor = false;
                Debug.Log("[EmulatorSceneBuilder] Using Unlit/Color shader.");
                return;
            }

            _unlitShader = Shader.Find("Standard");
            _useBaseColor = false;
            Debug.LogWarning("[EmulatorSceneBuilder] Falling back to Standard shader.");
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            var mat = new Material(_unlitShader);
            if (_useBaseColor)
                mat.SetColor("_BaseColor", color);
            else
                mat.SetColor("_Color", color);
            mat.color = color;

            string matPath = $"Assets/Materials/Emu_{name}_Mat.mat";
            if (File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), matPath)))
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static Material CreateUnlitMaterialTransparent(string name, Color color)
        {
            var mat = new Material(_unlitShader);

            // Enable transparency
            if (_useBaseColor)
            {
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = 3000;
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                mat.SetColor("_Color", color);
                mat.renderQueue = 3000;
            }
            mat.color = color;

            string matPath = $"Assets/Materials/Emu_{name}_Mat.mat";
            if (File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), matPath)))
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // ─────────────────────────────────────────────────────────────────
        // Primitive helpers
        // ─────────────────────────────────────────────────────────────────

        private static GameObject CreateTree(string name, Vector3 position, Transform parent)
        {
            var treeRoot = new GameObject(name);
            treeRoot.transform.position = position;
            treeRoot.transform.SetParent(parent);

            // Trunk: Cylinder
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(treeRoot.transform);
            trunk.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            trunk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            trunk.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial($"{name}_Trunk", new Color(0.45f, 0.28f, 0.1f));

            // Canopy: Sphere
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(treeRoot.transform);
            canopy.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            canopy.transform.localScale = Vector3.one * 1.5f;
            canopy.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial($"{name}_Canopy", new Color(0.2f, 0.55f, 0.1f));

            treeRoot.AddComponent<BoxCollider>();
            return treeRoot;
        }

        private static GameObject CreateStone(string name, Vector3 position, Transform parent, float scale)
        {
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = name;
            stone.transform.position = position;
            stone.transform.SetParent(parent);
            stone.transform.localScale = Vector3.one * scale;
            stone.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial(name, new Color(0.55f, 0.53f, 0.5f));
            return stone;
        }

        private static GameObject CreateBuilding(string name, Vector3 position, Transform parent, Vector3 scale)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = name;
            building.transform.position = position;
            building.transform.SetParent(parent);
            building.transform.localScale = scale;
            building.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial(name, new Color(0.55f, 0.38f, 0.22f));
            // BoxCollider comes from CreatePrimitive
            return building;
        }

        private static GameObject CreateNPCCapsule(string name, Vector3 position, Transform parent, Color color)
        {
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = name;
            npc.transform.position = position;
            npc.transform.SetParent(parent);
            npc.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial($"NPC_{name}", color);
            return npc;
        }

        // ─────────────────────────────────────────────────────────────────
        // Enemy prefab creation (inline primitives, no FBX)
        // ─────────────────────────────────────────────────────────────────

        private static GameObject CreateEnemyPrefab(string prefabName, Color color, string enemyDataPath)
        {
            string prefabPath = $"Assets/Prefabs/Enemies/{prefabName}.prefab";

            // Delete existing prefab if any
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);

            var go = new GameObject(prefabName);

            // Visual child (capsule)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial(prefabName, color);
            // Remove auto-collider from visual
            var meshCol = visual.GetComponent<Collider>();
            if (meshCol != null) Object.DestroyImmediate(meshCol);

            // Main collider on root
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.5f;
            col.center = new Vector3(0f, 1f, 0f);

            // InteractionPoint
            var interPoint = new GameObject("InteractionPoint");
            interPoint.transform.SetParent(go.transform);
            interPoint.transform.localPosition = new Vector3(0f, 0f, 1.5f);

            // Combat components (matching EnemyPrefabBuilder)
            go.AddComponent<ZeldaDaughter.Combat.EnemyHealth>();
            go.AddComponent<ZeldaDaughter.Combat.EnemyFSM>();
            go.AddComponent<ZeldaDaughter.Combat.EnemyAttackSignal>();
            go.AddComponent<ZeldaDaughter.Combat.StunEffect>();

            // Behavior-specific component
            bool isBoar = prefabName.Contains("Boar");
            if (isBoar)
                go.AddComponent<ZeldaDaughter.Combat.BoarBehavior>();
            else
                go.AddComponent<ZeldaDaughter.Combat.WolfBehavior>();

            // Hitbox child
            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(go.transform);
            hitboxGo.transform.localPosition = new Vector3(0f, 0.5f, 0.8f);
            var hitboxCol = hitboxGo.AddComponent<BoxCollider>();
            hitboxCol.isTrigger = true;
            hitboxCol.size = new Vector3(0.8f, 0.8f, 0.8f);
            hitboxGo.AddComponent<ZeldaDaughter.Combat.HitboxTrigger>();

            // Wire EnemyData
            var enemyData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.EnemyData>(enemyDataPath);
            if (enemyData != null)
            {
                var health = go.GetComponent<ZeldaDaughter.Combat.EnemyHealth>();
                if (health != null)
                {
                    var so = new SerializedObject(health);
                    var dataProp = so.FindProperty("_data");
                    if (dataProp != null)
                    {
                        dataProp.objectReferenceValue = enemyData;
                        so.ApplyModifiedProperties();
                    }
                }
                var fsm = go.GetComponent<ZeldaDaughter.Combat.EnemyFSM>();
                if (fsm != null)
                {
                    var so = new SerializedObject(fsm);
                    var dataProp = so.FindProperty("_data");
                    if (dataProp != null)
                    {
                        dataProp.objectReferenceValue = enemyData;
                        so.ApplyModifiedProperties();
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[EmulatorSceneBuilder] EnemyData not found: {enemyDataPath}");
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[EmulatorSceneBuilder] Enemy prefab saved: {prefabPath}");
            return prefab;
        }

        // ─────────────────────────────────────────────────────────────────
        // 1. SetupScene - ground + light
        // ─────────────────────────────────────────────────────────────────

        private static Light SetupScene()
        {
            // Ground (50x50 via Plane scale: 5x1x5 = 50x50)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, 0f, 0f);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            ground.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial("Ground", new Color(0.25f, 0.52f, 0.18f));

            // Directional Light
            var lightGO = new GameObject("Directional Light");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.None; // No shadows for emulator perf
            lightGO.transform.rotation = Quaternion.Euler(50f, 170f, 0f);

            return sun;
        }

        // ─────────────────────────────────────────────────────────────────
        // 2. SetupPlayer - all components identical to DemoScene
        // ─────────────────────────────────────────────────────────────────

        private static GameObject SetupPlayer()
        {
            EnsurePlayerTag();

            var spawnPos = new Vector3(-20f, 0f, 0f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = spawnPos;
            player.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial("Player", new Color(0.2f, 0.4f, 0.8f));
            // Remove default CapsuleCollider (CharacterController will handle collision)
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            player.tag = "Player";

            // CharacterController
            var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;

            // Core movement
            AddComponentIfMissing<ZeldaDaughter.Input.CharacterMovement>(player);
            AddComponentIfMissing<ZeldaDaughter.Input.CharacterAutoMove>(player);
            AddComponentIfMissing<ZeldaDaughter.World.SurfaceDetector>(player);

            // Inventory
            AddComponentIfMissing<ZeldaDaughter.Inventory.PlayerInventory>(player);
            AddComponentIfMissing<ZeldaDaughter.Inventory.WeightSystem>(player);

            var inventory = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            var charMovement = player.GetComponent<ZeldaDaughter.Input.CharacterMovement>();

            var invConfig = AssetDatabase.LoadAssetAtPath<InventoryConfig>("Assets/Data/InventoryConfig.asset");
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
                var db = AssetDatabase.LoadAssetAtPath<CraftRecipeDatabase>(
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
            WireCombatConfig(player, combatConfig);

            // WeaponBone
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

            // FoodConsumption wiring
            var foodConsumption = player.GetComponent<ZeldaDaughter.Combat.FoodConsumption>();
            if (foodConsumption != null)
            {
                var healthState = player.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
                var hungerSystem = player.GetComponent<ZeldaDaughter.Combat.HungerSystem>();
                var so = new SerializedObject(foodConsumption);
                if (healthState != null) so.FindProperty("_healthState").objectReferenceValue = healthState;
                if (hungerSystem != null) so.FindProperty("_hungerSystem").objectReferenceValue = hungerSystem;
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

            Debug.Log("[EmulatorSceneBuilder] Player setup complete.");
            return player;
        }

        private static void WireCombatConfig(GameObject player, ZeldaDaughter.Combat.CombatConfig combatConfig)
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

        // ─────────────────────────────────────────────────────────────────
        // 3. SetupCamera
        // ─────────────────────────────────────────────────────────────────

        private static void SetupCamera(GameObject player)
        {
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.75f, 0.9f);
            // No UniversalAdditionalCameraData — avoid URP dependency for emulator
            var isoCam = cameraGO.AddComponent<ZeldaDaughter.World.IsometricCamera>();
            isoCam.SetTarget(player.transform);
        }

        // ─────────────────────────────────────────────────────────────────
        // 4. SetupCoreSystems
        // ─────────────────────────────────────────────────────────────────

        private static void SetupCoreSystems(Light sun)
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
                else Debug.LogWarning("[EmulatorSceneBuilder] WeatherConfig.asset not found.");
            }

            // TapInteractionManager
            var tapSystemGO = new GameObject("TapSystem");
            tapSystemGO.transform.SetParent(systemsParent.transform);
            tapSystemGO.AddComponent<ZeldaDaughter.World.TapInteractionManager>();

            // SaveManager
            var saveGO = new GameObject("SaveManager");
            saveGO.transform.SetParent(systemsParent.transform);
            saveGO.AddComponent<ZeldaDaughter.Save.SaveManager>();

            Debug.Log("[EmulatorSceneBuilder] CoreSystems setup complete.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 5. SetupNPCSystems
        // ─────────────────────────────────────────────────────────────────

        private static void SetupNPCSystems(GameObject player)
        {
            var npcSystemsGO = new GameObject("NPCSystems");

            var playerInventory = player.GetComponent<ZeldaDaughter.Inventory.PlayerInventory>();
            var playerStats = player.GetComponent<PlayerStats>();

            // LanguageSystem
            var langGO = new GameObject("LanguageSystem");
            langGO.transform.SetParent(npcSystemsGO.transform);
            var langSys = langGO.AddComponent<ZeldaDaughter.NPC.LanguageSystem>();
            {
                var langConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.LanguageConfig>(
                    "Assets/Data/NPC/LanguageConfig.asset");
                var so = new SerializedObject(langSys);
                if (langConfig != null) so.FindProperty("_config").objectReferenceValue = langConfig;
                else Debug.LogWarning("[EmulatorSceneBuilder] LanguageConfig.asset not found.");
                if (playerStats != null) so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                so.ApplyModifiedProperties();
            }

            // DialogueManager
            var dialogueGO = new GameObject("DialogueManager");
            dialogueGO.transform.SetParent(npcSystemsGO.transform);
            var dialogueMgr = dialogueGO.AddComponent<ZeldaDaughter.NPC.DialogueManager>();
            {
                var so = new SerializedObject(dialogueMgr);
                so.FindProperty("_languageSystem").objectReferenceValue = langSys;
                if (playerInventory != null) so.FindProperty("_playerInventory").objectReferenceValue = playerInventory;
                if (playerStats != null) so.FindProperty("_playerStats").objectReferenceValue = playerStats;
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

            Debug.Log("[EmulatorSceneBuilder] NPCSystems setup complete.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 6. SetupQuestSystems
        // ─────────────────────────────────────────────────────────────────

        private static void SetupQuestSystems(GameObject player)
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
                if (questDb != null) so.FindProperty("_database").objectReferenceValue = questDb;
                else Debug.LogWarning("[EmulatorSceneBuilder] QuestDatabase.asset not found.");
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
                        if (cityRegion != null) regionsProp.GetArrayElementAtIndex(idx).objectReferenceValue = cityRegion;
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

            Debug.Log("[EmulatorSceneBuilder] QuestSystems setup complete.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 7. SetupUI - all canvases identical to DemoScene
        // ─────────────────────────────────────────────────────────────────

        private static void SetupUI(GameObject player)
        {
            var uiParent = new GameObject("UI");

            var recipeDb = AssetDatabase.LoadAssetAtPath<CraftRecipeDatabase>(
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

            var slotsGridGO = new GameObject("SlotsGrid");
            slotsGridGO.transform.SetParent(inventoryPanelGO.transform, false);
            var slotsGridRect = slotsGridGO.AddComponent<RectTransform>();
            slotsGridRect.anchorMin = Vector2.zero;
            slotsGridRect.anchorMax = Vector2.one;
            slotsGridRect.offsetMin = Vector2.zero;
            slotsGridRect.offsetMax = Vector2.zero;

            var slotPrefabGO = new GameObject("SlotPrefab");
            slotPrefabGO.transform.SetParent(inventoryPanelGO.transform, false);
            var slotPrefabRect = slotPrefabGO.AddComponent<RectTransform>();
            slotPrefabRect.sizeDelta = new Vector2(64f, 64f);
            slotPrefabGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            slotPrefabGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();
            slotPrefabGO.SetActive(false);

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

                var weaponEquipForDrag = player.GetComponent<ZeldaDaughter.Combat.WeaponEquipSystem>();

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

                var slotAGO = new GameObject("SlotA");
                slotAGO.transform.SetParent(stationCanvas.transform, false);
                var slotARect = slotAGO.AddComponent<RectTransform>();
                slotARect.anchorMin = new Vector2(0.1f, 0.5f);
                slotARect.anchorMax = new Vector2(0.35f, 0.8f);
                slotARect.offsetMin = Vector2.zero;
                slotARect.offsetMax = Vector2.zero;
                slotAGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                var slotAUI = slotAGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();

                var slotBGO = new GameObject("SlotB");
                slotBGO.transform.SetParent(stationCanvas.transform, false);
                var slotBRect = slotBGO.AddComponent<RectTransform>();
                slotBRect.anchorMin = new Vector2(0.4f, 0.5f);
                slotBRect.anchorMax = new Vector2(0.65f, 0.8f);
                slotBRect.offsetMin = Vector2.zero;
                slotBRect.offsetMax = Vector2.zero;
                slotBGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                var slotBUI = slotBGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();

                var resultGO = new GameObject("ResultSlot");
                resultGO.transform.SetParent(stationCanvas.transform, false);
                var resultRect = resultGO.AddComponent<RectTransform>();
                resultRect.anchorMin = new Vector2(0.65f, 0.5f);
                resultRect.anchorMax = new Vector2(0.9f, 0.8f);
                resultRect.offsetMin = Vector2.zero;
                resultRect.offsetMax = Vector2.zero;
                resultGO.AddComponent<Image>().color = new Color(0.15f, 0.3f, 0.15f, 0.8f);
                var resultSlotUI = resultGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();

                var craftBtnGO = new GameObject("CraftButton");
                craftBtnGO.transform.SetParent(stationCanvas.transform, false);
                var craftBtnRect = craftBtnGO.AddComponent<RectTransform>();
                craftBtnRect.anchorMin = new Vector2(0.35f, 0.15f);
                craftBtnRect.anchorMax = new Vector2(0.65f, 0.35f);
                craftBtnRect.offsetMin = Vector2.zero;
                craftBtnRect.offsetMax = Vector2.zero;
                craftBtnGO.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 1f);
                var craftBtn = craftBtnGO.AddComponent<Button>();

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
                optionBtnGO.SetActive(false);

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
                var merchantPanelGO = new GameObject("MerchantPanel");
                merchantPanelGO.transform.SetParent(tradeCanvas.transform, false);
                var merchantRect = merchantPanelGO.AddComponent<RectTransform>();
                merchantRect.anchorMin = new Vector2(0f, 0.1f);
                merchantRect.anchorMax = new Vector2(0.45f, 0.9f);
                merchantRect.offsetMin = Vector2.zero;
                merchantRect.offsetMax = Vector2.zero;
                merchantPanelGO.AddComponent<Image>().color = new Color(0.15f, 0.1f, 0.05f, 0.85f);

                var playerPanelGO = new GameObject("PlayerPanel");
                playerPanelGO.transform.SetParent(tradeCanvas.transform, false);
                var playerRect = playerPanelGO.AddComponent<RectTransform>();
                playerRect.anchorMin = new Vector2(0.55f, 0.1f);
                playerRect.anchorMax = new Vector2(1f, 0.9f);
                playerRect.offsetMin = Vector2.zero;
                playerRect.offsetMax = Vector2.zero;
                playerPanelGO.AddComponent<Image>().color = new Color(0.05f, 0.1f, 0.15f, 0.85f);

                var tradeZoneGO = new GameObject("TradeZone");
                tradeZoneGO.transform.SetParent(tradeCanvas.transform, false);
                var tradeZoneRect = tradeZoneGO.AddComponent<RectTransform>();
                tradeZoneRect.anchorMin = new Vector2(0.35f, 0.1f);
                tradeZoneRect.anchorMax = new Vector2(0.65f, 0.9f);
                tradeZoneRect.offsetMin = Vector2.zero;
                tradeZoneRect.offsetMax = Vector2.zero;
                tradeZoneGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

                var balanceGO = new GameObject("BalanceIndicator");
                balanceGO.transform.SetParent(tradeCanvas.transform, false);
                var balanceRect = balanceGO.AddComponent<RectTransform>();
                balanceRect.anchorMin = new Vector2(0.3f, 0.02f);
                balanceRect.anchorMax = new Vector2(0.7f, 0.08f);
                balanceRect.offsetMin = Vector2.zero;
                balanceRect.offsetMax = Vector2.zero;
                var balanceImage = balanceGO.AddComponent<Image>();
                balanceImage.color = new Color(0.6f, 0.8f, 0.2f, 0.9f);

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

                var tradeSlotGO = new GameObject("TradeSlotPrefab");
                tradeSlotGO.transform.SetParent(tradeCanvas.transform, false);
                var tradeSlotRect = tradeSlotGO.AddComponent<RectTransform>();
                tradeSlotRect.sizeDelta = new Vector2(56f, 56f);
                tradeSlotGO.AddComponent<Image>().color = new Color(0.2f, 0.18f, 0.14f, 0.9f);
                tradeSlotGO.AddComponent<ZeldaDaughter.UI.InventorySlotUI>();
                tradeSlotGO.SetActive(false);

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

            // Hide all UI panels by default
            void HideCanvasGroup(CanvasGroup cg) { if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; } }
            HideCanvasGroup(radialCG);
            HideCanvasGroup(inventoryPanelCG);
            HideCanvasGroup(itemInfoCG);
            HideCanvasGroup(stationCG);
            HideCanvasGroup(dialogueCG);
            HideCanvasGroup(tradeCG);
            HideCanvasGroup(longPressCG);
            var mapPanelCG = mapPanelGO.AddComponent<CanvasGroup>();
            HideCanvasGroup(mapPanelCG);
            var notebookPanelCG = notebookPanelGO.AddComponent<CanvasGroup>();
            HideCanvasGroup(notebookPanelCG);

            Debug.Log("[EmulatorSceneBuilder] UI setup complete (all panels hidden).");
        }

        // ─────────────────────────────────────────────────────────────────
        // 8. PlaceMeadow - starting area with primitives
        // ─────────────────────────────────────────────────────────────────

        private static void PlaceMeadow()
        {
            var meadowParent = new GameObject("Meadow");
            meadowParent.transform.position = new Vector3(-20f, 0f, 0f);

            // Trees
            var treeParent = new GameObject("Trees");
            treeParent.transform.SetParent(meadowParent.transform);
            Vector3[] treePositions = {
                new Vector3(-25f, 0f, 5f),  new Vector3(-22f, 0f, -8f),
                new Vector3(-18f, 0f, 10f), new Vector3(-28f, 0f, -3f),
                new Vector3(-15f, 0f, -6f), new Vector3(-24f, 0f, 12f),
                new Vector3(-20f, 0f, -12f), new Vector3(-27f, 0f, 8f),
                new Vector3(-13f, 0f, 3f),  new Vector3(-25f, 0f, -10f),
            };
            for (int i = 0; i < treePositions.Length; i++)
                CreateTree($"Tree_{i}", treePositions[i], treeParent.transform);

            // Stones
            var stoneParent = new GameObject("Stones");
            stoneParent.transform.SetParent(meadowParent.transform);
            Vector3[] stonePositions = {
                new Vector3(-22f, 0f, 3f),  new Vector3(-17f, 0f, -5f),
                new Vector3(-26f, 0f, -7f), new Vector3(-19f, 0f, 8f),
                new Vector3(-24f, 0f, 4f),  new Vector3(-16f, 0f, -2f),
            };
            float[] stoneSizes = { 0.6f, 0.8f, 0.5f, 0.7f, 0.6f, 0.5f };
            for (int i = 0; i < stonePositions.Length; i++)
                CreateStone($"Stone_{i}", stonePositions[i], stoneParent.transform, stoneSizes[i]);

            // Bushes with EnvironmentReactor
            var bushParent = new GameObject("Bushes");
            bushParent.transform.SetParent(meadowParent.transform);
            for (int i = 0; i < 8; i++)
            {
                var pos = new Vector3(-20f + (i % 4) * 3f - 6f, 0f, (i / 4) * 6f - 3f);
                var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bush.name = $"Bush_{i}";
                bush.transform.position = pos;
                bush.transform.localScale = Vector3.one * 0.8f;
                bush.transform.SetParent(bushParent.transform);
                bush.GetComponent<Renderer>().sharedMaterial =
                    CreateUnlitMaterial($"Bush_{i}", new Color(0.2f, 0.5f, 0.15f));
                bush.AddComponent<ZeldaDaughter.World.EnvironmentReactor>();
                var bushCol = bush.AddComponent<SphereCollider>();
                bushCol.isTrigger = true;
                bushCol.radius = 0.6f;
            }

            // ResourceNodes
            var resourceParent = new GameObject("ResourceNodes");
            resourceParent.transform.SetParent(meadowParent.transform);
            PlaceResourceNode(resourceParent, "ResourceNode_Tree0",
                new Vector3(-23f, 0f, 8f), "Assets/Data/World/ResourceNode_Tree.asset");
            PlaceResourceNode(resourceParent, "ResourceNode_Stone0",
                new Vector3(-18f, 0f, -10f), "Assets/Data/World/ResourceNode_Stone.asset");
            PlaceResourceNode(resourceParent, "ResourceNode_Ore",
                new Vector3(-27f, 0f, -8f), "Assets/Data/World/ResourceNode_Ore.asset");

            // Pickupables
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
                new Vector3(-22f, 0.15f, 4f),  new Vector3(-18f, 0.15f, -3f),
                new Vector3(-25f, 0.15f, -7f), new Vector3(-16f, 0.15f, 9f),
                new Vector3(-21f, 0.15f, 2f),  new Vector3(-19f, 0.15f, -5f),
                new Vector3(-24f, 0.15f, 11f), new Vector3(-17f, 0.15f, -12f),
            };
            for (int i = 0; i < 8; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Pickup_{i}";
                go.transform.position = pickupPositions[i];
                go.transform.localScale = Vector3.one * 0.25f;
                go.transform.SetParent(pickupParent.transform);
                go.GetComponent<Renderer>().sharedMaterial =
                    CreateUnlitMaterial($"Pickup_{i}", new Color(0.8f, 0.7f, 0.2f));
                var pickupable = go.AddComponent<ZeldaDaughter.World.Pickupable>();
                var itemData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Inventory.ItemData>(
                    pickupDataPaths[i]);
                var so = new SerializedObject(pickupable);
                if (itemData != null) so.FindProperty("_itemData").objectReferenceValue = itemData;
                so.FindProperty("_amount").intValue = 1;
                so.FindProperty("_saveId").stringValue = $"pickup_meadow_{i}";
                so.ApplyModifiedProperties();
            }

            // Campfire
            var campfireParent = new GameObject("Campfire");
            campfireParent.transform.SetParent(meadowParent.transform);
            campfireParent.transform.position = new Vector3(-20f, 0f, -3f);

            var campfireVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            campfireVisual.name = "CampfireVisual";
            campfireVisual.transform.SetParent(campfireParent.transform);
            campfireVisual.transform.localPosition = Vector3.zero;
            campfireVisual.transform.localScale = Vector3.one * 0.5f;
            campfireVisual.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial("Campfire", new Color(0.8f, 0.35f, 0.05f));

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

                var interPointGO = new GameObject("InteractionPoint");
                interPointGO.transform.SetParent(campfireParent.transform);
                interPointGO.transform.localPosition = new Vector3(0f, 0.5f, 0.8f);
                so.FindProperty("_interactionPoint").objectReferenceValue = interPointGO.transform;
                so.ApplyModifiedProperties();

                AddComponentIfMissing<ZeldaDaughter.Combat.RestZoneDetector>(campfireParent);
            }

            // Enemy spawn zones using emulator prefabs
            var boarPrefab = CreateEnemyPrefab("Emu_Boar", new Color(0.7f, 0.25f, 0.2f),
                "Assets/Data/Combat/EnemyData_Boar.asset");
            var wolfPrefab = CreateEnemyPrefab("Emu_Wolf", new Color(0.5f, 0.5f, 0.55f),
                "Assets/Data/Combat/EnemyData_Wolf.asset");

            PlaceEmuSpawnZone("SpawnZone_Boars_Meadow",
                new Vector3(-25f, 0f, -10f), boarPrefab,
                "Assets/Data/Combat/EnemyData_Boar.asset", 2, 10f);

            PlaceEmuSpawnZone("SpawnZone_Wolves_Meadow",
                new Vector3(-12f, 0f, -12f), wolfPrefab,
                "Assets/Data/Combat/EnemyData_Wolf.asset", 2, 12f);

            Debug.Log("[EmulatorSceneBuilder] Meadow placed.");
        }

        private static void PlaceResourceNode(
            GameObject parent, string goName, Vector3 pos, string dataPath)
        {
            var go = GameObject.CreatePrimitive(
                goName.Contains("Tree") ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
            go.name = goName;
            go.transform.position = pos;
            go.transform.SetParent(parent.transform);

            Color nodeColor = goName.Contains("Tree")
                ? new Color(0.35f, 0.65f, 0.2f)
                : goName.Contains("Ore")
                    ? new Color(0.7f, 0.55f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f);
            go.GetComponent<Renderer>().sharedMaterial = CreateUnlitMaterial(goName, nodeColor);

            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(go);
            var node = go.AddComponent<ZeldaDaughter.World.ResourceNode>();
            var data = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.ResourceNodeData>(dataPath);
            var so = new SerializedObject(node);
            if (data != null) so.FindProperty("_data").objectReferenceValue = data;
            else Debug.LogWarning($"[EmulatorSceneBuilder] ResourceNodeData not found: {dataPath}");
            so.FindProperty("_saveId").stringValue = $"resourcenode_{goName.ToLower()}";
            so.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────────
        // 9. PlaceRoad
        // ─────────────────────────────────────────────────────────────────

        private static void PlaceRoad()
        {
            var roadParent = new GameObject("Road");

            // Road tiles from X=-15 to X=5, Z=3..5
            for (int i = 0; i < 10; i++)
            {
                float x = -15f + i * 2f;
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Road_{i}";
                tile.transform.position = new Vector3(x, 0.01f, 4f);
                tile.transform.localScale = new Vector3(2f, 0.05f, 2f);
                tile.transform.SetParent(roadParent.transform);
                tile.GetComponent<Renderer>().sharedMaterial =
                    CreateUnlitMaterial($"Road_{i}", new Color(0.55f, 0.52f, 0.45f));
            }

            Debug.Log("[EmulatorSceneBuilder] Road placed.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 10. PlaceCity
        // ─────────────────────────────────────────────────────────────────

        private static void PlaceCity()
        {
            var cityParent = new GameObject("City");
            cityParent.transform.position = new Vector3(13f, 0f, 9f);

            // Buildings (Cubes)
            var buildingsParent = new GameObject("Buildings");
            buildingsParent.transform.SetParent(cityParent.transform);
            CreateBuilding("Building_Tavern",    new Vector3(13f, 1.5f, 14f), buildingsParent.transform, new Vector3(6f, 3f, 6f));
            CreateBuilding("Building_Smithy",    new Vector3(7f, 1.5f, 5f),   buildingsParent.transform, new Vector3(4f, 3f, 4f));
            CreateBuilding("Building_Shop",      new Vector3(20f, 1.5f, 8f),  buildingsParent.transform, new Vector3(4f, 3f, 4f));
            CreateBuilding("Building_Herbalist", new Vector3(16f, 1.5f, 0f),  buildingsParent.transform, new Vector3(4f, 3f, 4f));
            CreateBuilding("Building_House1",    new Vector3(8f, 1.5f, 15f),  buildingsParent.transform, new Vector3(4f, 3f, 4f));

            // Training dummy
            var dummyGO = new GameObject("TrainingDummy");
            dummyGO.transform.SetParent(cityParent.transform);
            dummyGO.transform.position = new Vector3(10f, 0f, 8f);
            var dummyCollider = dummyGO.AddComponent<CapsuleCollider>();
            dummyCollider.height = 1.8f;
            dummyCollider.radius = 0.35f;
            dummyCollider.center = new Vector3(0f, 0.9f, 0f);
            var dummyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyVisual.name = "DummyVisual";
            dummyVisual.transform.SetParent(dummyGO.transform);
            dummyVisual.transform.localPosition = Vector3.zero;
            dummyVisual.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial("TrainingDummy", new Color(0.6f, 0.45f, 0.2f));
            var dummyComp = dummyGO.AddComponent<ZeldaDaughter.Combat.TrainingDummy>();
            {
                var dummyConfig = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.TrainingDummyConfig>(
                    "Assets/Data/Combat/TrainingDummyConfig.asset");
                var so = new SerializedObject(dummyComp);
                if (dummyConfig != null) so.FindProperty("_config").objectReferenceValue = dummyConfig;
                so.ApplyModifiedProperties();
            }
            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(dummyGO);

            // Stations (primitives)
            PlaceEmuStation("Smelter", new Vector3(8f, 0f, 4f), cityParent.transform,
                new Color(0.8f, 0.4f, 0.1f), StationType.Smelter);
            PlaceEmuStation("Anvil", new Vector3(10f, 0f, 4f), cityParent.transform,
                new Color(0.4f, 0.4f, 0.5f), StationType.Anvil);

            // Bed
            var bedGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bedGo.name = "Bed";
            bedGo.transform.position = new Vector3(15f, 0.2f, 14f);
            bedGo.transform.localScale = new Vector3(1f, 0.4f, 2f);
            bedGo.transform.SetParent(cityParent.transform);
            bedGo.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial("Bed", new Color(0.6f, 0.3f, 0.1f));
            var bedInterPoint = new GameObject("InteractionPoint");
            bedInterPoint.transform.SetParent(bedGo.transform);
            bedInterPoint.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var sleepInteraction = bedGo.AddComponent<ZeldaDaughter.Combat.SleepInteraction>();
            {
                var so = new SerializedObject(sleepInteraction);
                var interPointProp = so.FindProperty("_interactionPoint");
                if (interPointProp != null)
                {
                    interPointProp.objectReferenceValue = bedInterPoint.transform;
                    so.ApplyModifiedProperties();
                }
            }
            AddComponentIfMissing<ZeldaDaughter.World.InteractableHighlight>(bedGo);

            // NPCs (Capsules with distinct colors)
            var npcParent = new GameObject("NPCs");
            npcParent.transform.SetParent(cityParent.transform);
            var npcData = new (string name, Vector3 pos, Color color)[] {
                ("Merchant",   new Vector3(20f, 0f, 9f),   new Color(0.8f, 0.6f, 0.1f)),
                ("Blacksmith", new Vector3(7f, 0f, 6f),    new Color(0.4f, 0.4f, 0.45f)),
                ("Bartender",  new Vector3(13f, 0f, 13f),  new Color(0.6f, 0.3f, 0.5f)),
                ("Herbalist",  new Vector3(16f, 0f, 2f),   new Color(0.2f, 0.7f, 0.3f)),
                ("Guard",      new Vector3(12f, 0f, 3f),   new Color(0.5f, 0.5f, 0.6f)),
                ("Villager1",  new Vector3(17f, 0f, 12f),  new Color(0.7f, 0.5f, 0.3f)),
                ("Villager2",  new Vector3(9f, 0f, 11f),   new Color(0.65f, 0.45f, 0.35f)),
            };
            foreach (var (npcName, npcPos, npcColor) in npcData)
            {
                var npcGO = CreateNPCCapsule(npcName, npcPos, npcParent.transform, npcColor);
                var npcInteractable = npcGO.AddComponent<ZeldaDaughter.NPC.NPCInteractable>();
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
            }

            // Peasant on the road
            var peasantGO = CreateNPCCapsule("Peasant", new Vector3(0f, 0f, 3f), cityParent.transform,
                new Color(0.6f, 0.5f, 0.3f));
            var peasantInteractable = peasantGO.AddComponent<ZeldaDaughter.NPC.NPCInteractable>();
            var peasantProfile = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.NPC.NPCProfile>(
                "Assets/Data/NPC/Profiles/NPC_Peasant.asset");
            if (peasantProfile != null)
            {
                var so = new SerializedObject(peasantInteractable);
                var profileProp = so.FindProperty("_profile");
                if (profileProp != null)
                {
                    profileProp.objectReferenceValue = peasantProfile;
                    so.ApplyModifiedProperties();
                }
            }

            // Waypoints
            var waypointsParent = new GameObject("Waypoints");
            waypointsParent.transform.SetParent(cityParent.transform);
            PlaceWaypoint(waypointsParent, "WP_Fountain",   new Vector3(13f, 0f, 7f),   "fountain");
            PlaceWaypoint(waypointsParent, "WP_Market",     new Vector3(20f, 0f, 8f),   "market");
            PlaceWaypoint(waypointsParent, "WP_Smithy",     new Vector3(7f, 0f, 5f),    "smithy");
            PlaceWaypoint(waypointsParent, "WP_Gate",       new Vector3(5f, 0f, 7f),    "gate");
            PlaceWaypoint(waypointsParent, "WP_TavernDoor", new Vector3(13f, 0f, 11f),  "tavern");
            PlaceWaypoint(waypointsParent, "WP_Patrol1",    new Vector3(9f, 0f, 3f),    "patrol");
            PlaceWaypoint(waypointsParent, "WP_Patrol2",    new Vector3(18f, 0f, 12f),  "patrol");

            // Water zone
            var waterZoneGO = new GameObject("WaterZone");
            waterZoneGO.transform.position = new Vector3(25f, 0f, 0f);
            var waterVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterVisual.name = "WaterVisual";
            waterVisual.transform.SetParent(waterZoneGO.transform);
            waterVisual.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            waterVisual.transform.localScale = new Vector3(4f, 0.3f, 30f);
            waterVisual.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterialTransparent("Water", new Color(0.1f, 0.35f, 0.75f, 0.7f));
            Object.DestroyImmediate(waterVisual.GetComponent<BoxCollider>());

            var waterBoxCol = waterZoneGO.AddComponent<BoxCollider>();
            waterBoxCol.isTrigger = true;
            waterBoxCol.size = new Vector3(4f, 2f, 30f);
            waterBoxCol.center = Vector3.zero;
            waterZoneGO.AddComponent<ZeldaDaughter.World.WaterZone>();

            Debug.Log("[EmulatorSceneBuilder] City placed.");
        }

        private static void PlaceEmuStation(
            string stationName, Vector3 position, Transform parent,
            Color color, StationType type)
        {
            var stationGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stationGO.name = stationName;
            stationGO.transform.position = position;
            stationGO.transform.SetParent(parent);
            stationGO.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
            stationGO.GetComponent<Renderer>().sharedMaterial =
                CreateUnlitMaterial(stationName, color);

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
        }

        private static void PlaceEmuSpawnZone(
            string goName, Vector3 position, GameObject enemyPrefab,
            string enemyDataPath, int maxEnemies, float spawnRadius)
        {
            if (GameObject.Find(goName) != null) return;

            var zoneGo = new GameObject(goName);
            zoneGo.transform.position = position;
            var spawnZone = zoneGo.AddComponent<ZeldaDaughter.Combat.EnemySpawnZone>();

            var enemyData = AssetDatabase.LoadAssetAtPath<ZeldaDaughter.Combat.EnemyData>(enemyDataPath);

            var so = new SerializedObject(spawnZone);
            if (enemyPrefab != null)
                so.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab;
            else
                Debug.LogWarning($"[EmulatorSceneBuilder] Enemy prefab is null for {goName}.");

            if (enemyData != null)
                so.FindProperty("_enemyData").objectReferenceValue = enemyData;
            else
                Debug.LogWarning($"[EmulatorSceneBuilder] EnemyData not found: {enemyDataPath}");

            so.FindProperty("_maxEnemies").intValue = maxEnemies;
            so.FindProperty("_spawnRadius").floatValue = spawnRadius;
            so.ApplyModifiedProperties();
        }

        private static void PlaceWaypoint(GameObject parent, string goName, Vector3 position, string waypointId)
        {
            var wpGO = new GameObject(goName);
            wpGO.transform.SetParent(parent.transform);
            wpGO.transform.position = position;
            var wp = wpGO.AddComponent<ZeldaDaughter.NPC.NPCWaypoint>();
            var so = new SerializedObject(wp);
            so.FindProperty("_waypointId").stringValue = waypointId;
            so.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────────
        // 11. WireReferences - final cross-linking
        // ─────────────────────────────────────────────────────────────────

        private static void WireReferences()
        {
            var player      = GameObject.Find("Player");
            var dialogueMgr = Object.FindObjectOfType<ZeldaDaughter.NPC.DialogueManager>();
            var tradeMgr    = Object.FindObjectOfType<ZeldaDaughter.NPC.TradeManager>();
            var questMgr    = Object.FindObjectOfType<ZeldaDaughter.Quest.QuestManager>();
            var gestureDisp = Object.FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            var tapManager  = Object.FindObjectOfType<ZeldaDaughter.World.TapInteractionManager>();
            var dialogueUI  = Object.FindObjectOfType<ZeldaDaughter.UI.DialoguePanelUI>();
            var tradeUI     = Object.FindObjectOfType<ZeldaDaughter.UI.TradeUI>();
            var mapPanelUI  = Object.FindObjectOfType<ZeldaDaughter.UI.MapPanelUI>();
            var notebookUI  = Object.FindObjectOfType<ZeldaDaughter.UI.NotebookPanelUI>();
            var mapMgr      = Object.FindObjectOfType<ZeldaDaughter.World.MapManager>();
            var notebookMgr = Object.FindObjectOfType<ZeldaDaughter.UI.NotebookManager>();

            // DialogueManager -> DialoguePanelUI
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

            // TradeManager -> TradeUI
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

            // QuestManager -> DialogueManager
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

            // KnockoutSystem -> GestureDispatcher
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

            // TapInteractionManager -> Player, AutoMove, CombatController
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

            // NPCInteractable -> DialogueManager
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

            Debug.Log("[EmulatorSceneBuilder] WireReferences complete.");
        }

        // ─────────────────────────────────────────────────────────────────
        // Utility methods
        // ─────────────────────────────────────────────────────────────────

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

        private static void AddComponentIfMissing<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();
        }

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
                    return;
                }
            }
            tagsProp.arraySize++;
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = playerTag;
            tagManager.ApplyModifiedProperties();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == scenePath) return;

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(newScenes, 0);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"[EmulatorSceneBuilder] Added '{scenePath}' to Build Settings.");
        }
    }
}
