using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Editor.MapGen;

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

            // TouchInputManager
            var inputGO = new GameObject("InputSystem");
            inputGO.AddComponent<ZeldaDaughter.Input.TouchInputManager>();

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
