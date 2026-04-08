using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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
