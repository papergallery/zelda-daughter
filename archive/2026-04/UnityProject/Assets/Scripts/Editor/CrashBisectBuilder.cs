using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Creates test scenes to binary-search what causes SIGSEGV on emulator.
    /// Each scene adds one more component from TestScene.
    /// </summary>
    public static class CrashBisectBuilder
    {
        [MenuItem("ZeldaDaughter/Debug/Build Bisect Scenes")]
        public static void BuildAll()
        {
            // Scene with just many meshes (test if mesh count is the issue)
            BuildMeshScene();
            // Scene with Cinemachine-like camera
            BuildCameraScene();
            // Scene with lights
            BuildLightScene();
        }

        [MenuItem("ZeldaDaughter/Debug/Build Mesh Test Scene")]
        public static void BuildMeshScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Add 50 cubes — test if mesh count causes crash
            var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (int i = 0; i < 50; i++)
            {
                var go = new GameObject($"Cube_{i}");
                go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                go.AddComponent<MeshRenderer>();
                go.transform.position = new Vector3(i % 10 * 2, 0, i / 10 * 2);
            }

            // Add directional light
            var light = new GameObject("Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BisectMeshScene.unity");
            Debug.Log("[CrashBisect] Created BisectMeshScene (50 cubes)");
        }

        [MenuItem("ZeldaDaughter/Debug/Build Camera Scene")]
        public static void BuildCameraScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(5, 1, 5);

            // Player-like object
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0, 1, 0);

            // Add GameBootstrap
            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<ZeldaDaughter.World.GameBootstrap>();

            // Add IsometricCamera
            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<ZeldaDaughter.World.IsometricCamera>();
                cam.transform.position = new Vector3(0, 10, -10);
                cam.transform.LookAt(player.transform);
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BisectCameraScene.unity");
            Debug.Log("[CrashBisect] Created BisectCameraScene");
        }

        [MenuItem("ZeldaDaughter/Debug/Build Light Scene")]
        public static void BuildLightScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground + cubes
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(5, 1, 5);

            for (int i = 0; i < 10; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = new Vector3(i * 2 - 10, 0.5f, 0);
            }

            // Multiple lights (like DayNightCycle)
            var dirLight = new GameObject("DirectionalLight");
            var dl = dirLight.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.shadows = LightShadows.Soft;

            var pointLight = new GameObject("PointLight");
            var pl = pointLight.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 10;
            pl.transform.position = new Vector3(0, 3, 0);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BisectLightScene.unity");
            Debug.Log("[CrashBisect] Created BisectLightScene");
        }
    }
}
