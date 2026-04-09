using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class CrashBisect3Builder
    {
        [MenuItem("ZeldaDaughter/Debug/Build Component Test Scenes")]
        public static void BuildAll()
        {
            // Start from BisectFullScene and add components progressively

            // Test 1: Add GestureDispatcher + CharacterMovement
            BuildWithInput();

            // Test 2: Add DayNightCycle
            BuildWithDayNight();

            // Test 3: Add all gameplay systems
            BuildWithAllSystems();
        }

        private static GameObject SetupBase(out UnityEngine.SceneManagement.Scene scene)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(10, 1, 10);

            var light = new GameObject("DirectionalLight");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;

            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<ZeldaDaughter.World.GameBootstrap>();

            // Character with controller
            var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx");
            GameObject player;
            if (charPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
            }
            else
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = Vector3.zero;
            player.AddComponent<CharacterController>();

            // Camera
            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<ZeldaDaughter.World.IsometricCamera>();
                cam.transform.position = new Vector3(0, 10, -10);
                cam.transform.rotation = Quaternion.Euler(45, 0, 0);
            }

            // Trees
            string[] models = {
                "Assets/Models/Kenney/NatureKit/Models/FBX format/tree_pineTallC.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/stone_tallA.fbx"
            };
            for (int i = 0; i < models.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(models[i]);
                if (prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.position = new Vector3(i * 4 - 2, 0, 5);
                }
            }

            return player;
        }

        public static void BuildWithInput()
        {
            var player = SetupBase(out var scene);

            // Add input system
            var inputGO = new GameObject("InputSystem");
            inputGO.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();

            // Add movement to player
            player.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            player.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Bisect3_Input.unity");
            Debug.Log("[CrashBisect3] Created Bisect3_Input");
        }

        public static void BuildWithDayNight()
        {
            var player = SetupBase(out var scene);

            // Add DayNightCycle
            var dirLight = GameObject.Find("DirectionalLight");
            if (dirLight != null)
                dirLight.AddComponent<ZeldaDaughter.World.DayNightCycle>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Bisect3_DayNight.unity");
            Debug.Log("[CrashBisect3] Created Bisect3_DayNight");
        }

        public static void BuildWithAllSystems()
        {
            var player = SetupBase(out var scene);

            // Input
            var inputGO = new GameObject("InputSystem");
            inputGO.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();
            player.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            player.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();

            // DayNight
            var dirLight = GameObject.Find("DirectionalLight");
            if (dirLight != null)
                dirLight.AddComponent<ZeldaDaughter.World.DayNightCycle>();

            // Surface detector
            player.AddComponent<ZeldaDaughter.World.SurfaceDetector>();

            // Progression
            var progressionGO = new GameObject("ProgressionSystem");
            progressionGO.AddComponent<ZeldaDaughter.Progression.PlayerStats>();
            progressionGO.AddComponent<ZeldaDaughter.Progression.ActionTracker>();

            // Inventory
            var inventoryGO = new GameObject("InventorySystem");
            inventoryGO.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Bisect3_AllSystems.unity");
            Debug.Log("[CrashBisect3] Created Bisect3_AllSystems");
        }
    }
}
