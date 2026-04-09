using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class CrashBisect2Builder
    {
        [MenuItem("ZeldaDaughter/Debug/Build Model Test Scenes")]
        public static void BuildAll()
        {
            // Test with KayKit character model
            BuildSceneWithModel("BisectCharScene",
                "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx");

            // Test with Kenney nature models
            BuildSceneWithModel("BisectNatureScene",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/tree_pineTallC.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/stone_tallA.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/tree_fat.fbx");

            // Test with all models from TestScene
            BuildFullTestScene();
        }

        private static void BuildSceneWithModel(string sceneName, params string[] modelPaths)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(5, 1, 5);

            foreach (var path in modelPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                    Debug.Log($"[CrashBisect2] Added {path}");
                }
                else
                {
                    Debug.LogWarning($"[CrashBisect2] Model not found: {path}");
                }
            }

            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneName}.unity");
            Debug.Log($"[CrashBisect2] Created {sceneName}");
        }

        private static void BuildFullTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(10, 1, 10);

            // Light with shadows
            var light = new GameObject("DirectionalLight");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.shadows = LightShadows.Soft;

            // GameBootstrap
            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<ZeldaDaughter.World.GameBootstrap>();

            // Character
            var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx");
            if (charPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
                player.name = "Player";
                player.tag = "Player";
                player.transform.position = new Vector3(0, 0, 0);
                // Add character controller
                player.AddComponent<CharacterController>();
            }

            // Nature models
            string[] natureModels = {
                "Assets/Models/Kenney/NatureKit/Models/FBX format/tree_pineTallC.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/stone_tallA.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/stone_largeE.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/tree_fat.fbx",
                "Assets/Models/Kenney/NatureKit/Models/FBX format/tree_simple_fall.fbx"
            };

            for (int i = 0; i < natureModels.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(natureModels[i]);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = new Vector3(i * 3 - 6, 0, 5);
                }
            }

            // IsometricCamera
            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<ZeldaDaughter.World.IsometricCamera>();
                cam.transform.position = new Vector3(0, 10, -10);
                cam.transform.rotation = Quaternion.Euler(45, 0, 0);
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BisectFullScene.unity");
            Debug.Log("[CrashBisect2] Created BisectFullScene (character + nature + camera + bootstrap)");
        }
    }
}
