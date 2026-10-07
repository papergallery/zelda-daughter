using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Builds progressively complex scenes to find what crashes SwiftShader.
    /// Each level adds more content from EmulatorScene.
    /// </summary>
    public static class EmulatorSceneMinBuilder
    {
        private static Shader _shader;
        private static bool _useBaseColor;

        private static void InitShader()
        {
            _shader = Shader.Find("Unlit/Color");
            _useBaseColor = false;
            if (_shader == null)
            {
                _shader = Shader.Find("Standard");
                _useBaseColor = false;
            }
        }

        private static Material MakeMat(string name, Color c)
        {
            var mat = new Material(_shader);
            mat.name = name;
            if (_useBaseColor)
                mat.SetColor("_BaseColor", c);
            else
                mat.SetColor("_Color", c);
            return mat;
        }

        // Level 1: Ground + Player(Capsule) + Camera + Light
        // This MUST work — same as Bisect3 that works
        [MenuItem("ZeldaDaughter/Debug/EmuMin Level 1 (Base)")]
        public static void BuildLevel1()
        {
            InitShader();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5, 1, 5);
            ground.GetComponent<Renderer>().sharedMaterial = MakeMat("Emu_Ground", new Color(0.3f, 0.55f, 0.2f));

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50, 170, 0);

            // Player
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(-20, 1, 0);
            player.GetComponent<Renderer>().sharedMaterial = MakeMat("Emu_Player", new Color(0.2f, 0.4f, 0.8f));
            player.AddComponent<CharacterController>();

            // Camera
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.backgroundColor = new Color(0.5f, 0.7f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(-20, 15, -15);
            cam.transform.rotation = Quaternion.Euler(45, 0, 0);

            // Some trees (composites)
            for (int i = 0; i < 5; i++)
            {
                var tree = new GameObject($"Tree_{i}");
                tree.transform.position = new Vector3(-20 + i * 3 - 6, 0, 5);

                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Trunk";
                trunk.transform.SetParent(tree.transform);
                trunk.transform.localPosition = new Vector3(0, 0.5f, 0);
                trunk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
                trunk.GetComponent<Renderer>().sharedMaterial = MakeMat("Emu_Trunk", new Color(0.45f, 0.3f, 0.15f));

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Crown";
                crown.transform.SetParent(tree.transform);
                crown.transform.localPosition = new Vector3(0, 1.5f, 0);
                crown.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                crown.GetComponent<Renderer>().sharedMaterial = MakeMat("Emu_Foliage", new Color(0.2f, 0.6f, 0.15f));
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/EmuMin1.unity");
            Debug.Log("[EmuMin] Level 1 created");
        }

        // Level 2: + GameBootstrap + GestureDispatcher + CharacterMovement + IsometricCamera
        [MenuItem("ZeldaDaughter/Debug/EmuMin Level 2 (Input)")]
        public static void BuildLevel2()
        {
            BuildLevel1(); // Start from Level 1 but don't save yet

            var player = GameObject.FindGameObjectWithTag("Player");
            player.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            player.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();

            var cam = Camera.main;
            if (cam != null)
                cam.gameObject.AddComponent<ZeldaDaughter.World.IsometricCamera>();

            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<ZeldaDaughter.World.GameBootstrap>();

            var inputSys = new GameObject("InputSystem");
            inputSys.AddComponent<ZeldaDaughter.Input.GestureDispatcher>();

            var eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<EventSystem>();
            eventSys.AddComponent<StandaloneInputModule>();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/EmuMin2.unity");
            Debug.Log("[EmuMin] Level 2 created (+ Input)");
        }

        // Level 3: + Canvas UI (simplified — just one canvas)
        [MenuItem("ZeldaDaughter/Debug/EmuMin Level 3 (Canvas)")]
        public static void BuildLevel3()
        {
            BuildLevel2();

            // Add a simple Canvas
            var canvasGo = new GameObject("TestCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Add a text
            var textGo = new GameObject("TestText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = "Emulator Test Scene";
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, 400);
            rt.sizeDelta = new Vector2(400, 50);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/EmuMin3.unity");
            Debug.Log("[EmuMin] Level 3 created (+ Canvas)");
        }

        // Build all and test sequentially
        [MenuItem("ZeldaDaughter/Debug/Build All EmuMin Levels")]
        public static void BuildAllLevels()
        {
            BuildLevel1();
            BuildLevel2();
            BuildLevel3();
        }
    }
}
