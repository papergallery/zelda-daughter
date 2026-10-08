using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// T-04: the one scene builder — scenes/&lt;name&gt;.json → Assets/Scenes/&lt;name&gt;.unity, rebuilt whole every time
    /// (docs/scene-config.md). No fixers on top: change the config or this builder. Batchmode:
    /// -executeMethod ZeldaDaughter.Editor.SceneBuilder.BuildAll. Log: "[ZD:Scene] built &lt;name&gt; objects=N hash=…".
    /// </summary>
    public static partial class SceneBuilder
    {
        public const string ConfigDir = "../scenes";
        public const string DataDir = "../data";
        const string ScenesDir = "Assets/Scenes";
        const string MaterialsDir = "Assets/Generated/Materials";
        const string LitShader = ModelLook.ShaderName; // D-08: the toon material everywhere

        [MenuItem("Zelda/Scenes/Build all from config")]
        public static void BuildAll()
        {
            GuardEditorState();
            var before = SceneManager.GetActiveScene().path;
            var configs = new List<SceneConfig>();
            foreach (var path in Directory.GetFiles(ConfigDir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                if (Build(path) != null) configs.Add(SceneConfig.Parse(File.ReadAllText(path)));
            }
            ApplyBuildSettings(configs);
            // The builder replaced whatever was open; put the author's scene back.
            if (!string.IsNullOrEmpty(before) && File.Exists(before)) EditorSceneManager.OpenScene(before, OpenSceneMode.Single);
        }

        /// <summary>
        /// EditorBuildSettings.scenes is rebuilt whole from the configs (build.include / build.order), never appended to:
        /// the first scene is the one the player build starts in.
        /// </summary>
        static void ApplyBuildSettings(List<SceneConfig> configs)
        {
            var names = SceneConfig.BuildList(configs);
            EditorBuildSettings.scenes = names.Select(n => new EditorBuildSettingsScene($"{ScenesDir}/{n}.unity", true)).ToArray();
            Debug.Log("[ZD:Scene] build order: " + string.Join(" → ", names));
        }

        /// <summary>The builder opens scenes in place of the current one — refuse rather than lose the author's edits.</summary>
        public static void GuardEditorState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("[ZD:Scene] the editor is in Play Mode — stop it first");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isDirty)
                    throw new InvalidOperationException($"[ZD:Scene] '{(string.IsNullOrEmpty(sc.name) ? "untitled" : sc.name)}' has unsaved changes — save or revert it first");
            }
        }

        /// <summary>Returns the content hash, or null when the config has problems (logged as errors).</summary>
        public static string Build(string configPath)
        {
            GuardEditorState();
            var config = SceneConfig.Parse(File.ReadAllText(configPath));
            var catalog = LoadCatalog();
            var terrains = ZeldaDaughter.Core.Data.DataSet.Load(DataDir).Movement.Terrain.Keys.ToList();
            var problems = config.Validate(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null, catalog, terrains).ToList();
            problems.AddRange(catalog.Validate(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null).Where(_ => config.Objects.Any(o => o.Model != null) || config.Scatter.Count > 0));
            if (problems.Count > 0)
            {
                foreach (var p in problems) Debug.LogError("[ZD:Scene] " + p);
                return null;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(config.Ground.SizeX / 10f, 1f, config.Ground.SizeZ / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = MaterialFor(config.Ground.Color);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(V(config.Light.Rotation));
            sun.color = ColorOf(config.Light.Color);
            sun.intensity = config.Light.Intensity;
            sun.shadows = config.Light.Shadows == "none" ? LightShadows.None : config.Light.Shadows == "soft" ? LightShadows.Soft : LightShadows.Hard;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ColorOf(config.Ambient.Color);
            RenderSettings.skybox = null;

            var hero = Spawn("Hero", config.Hero.Shape, config.Hero.Prefab, config.Hero.Color);
            hero.transform.position = V(config.Hero.Spawn) + Vector3.up * 1f; // capsule pivot is its centre, 2 m tall
            UnityEngine.Object.DestroyImmediate(hero.GetComponent<Collider>());
            var cc = hero.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = Vector3.zero;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = config.Camera.Orthographic;
            cam.orthographicSize = config.Camera.Size;
            cam.fieldOfView = config.Camera.FieldOfView;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = config.Camera.Distance * 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColorOf(config.Ambient.Color);
            camGo.AddComponent<AudioListener>();
            var iso = camGo.AddComponent<IsoCamera>();
            iso.Configure(hero.transform, config.Camera.Pitch, config.Camera.Yaw, config.Camera.Distance, config.Camera.FollowSmoothTime);
            var heroCtl = hero.AddComponent<HeroController>();
            heroCtl.Configure(iso, cam, config.Ground.Terrain);

            BuildLayout(config, catalog, heroCtl);

            var root = new GameObject("Objects").transform;
            var tagged = new List<SceneTags>();
            foreach (var o in config.Objects)
            {
                var go = o.Marker ? new GameObject(o.Id)
                    : !string.IsNullOrEmpty(o.Model) ? SpawnModel(o.Id, o.Model, catalog, o.Collide ?? true) : Spawn(o.Id, o.Shape, o.Prefab, o.Color);
                go.transform.SetParent(root, false);
                go.transform.localPosition = V(o.Position);
                go.transform.localRotation = Quaternion.Euler(V(o.Rotation));
                go.transform.localScale = V(o.Scale);
                var tags = go.AddComponent<SceneTags>();
                tags.Configure(o.Id, o.Tags.ToArray(), o.Item);
                tagged.Add(tags);
            }

            // T-10: the session that runs the core in this scene, with its placeholder UI.
            var game = new GameObject("Game");
            var sunCtl = game.AddComponent<SunController>();
            sunCtl.Configure(sun, V(config.Light.Rotation), config.Light.Intensity, ColorOf(config.Ambient.Color));
            sunCtl.SetCamera(cam);
            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(game.transform, false);
            var ui = uiGo.AddComponent<SessionUI>();
            ui.Configure(cam);
            game.AddComponent<GameSession>().Configure(hero.GetComponent<HeroController>(), sunCtl, ui, config.Name, tagged.ToArray(), config.Save.Slot);
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(game.transform, false);

            if (!AssetDatabase.IsValidFolder(ScenesDir)) AssetDatabase.CreateFolder("Assets", "Scenes");
            string scenePath = $"{ScenesDir}/{config.Name}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            string hash = ContentHash(scene, out int count);
            Debug.Log($"[ZD:Scene] built {config.Name} objects={count} hash={hash}");
            return hash;
        }

        static GameObject Spawn(string name, string shape, string prefab, string color)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(prefab))
                go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveOf(shape));
                if (!string.IsNullOrEmpty(color)) go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            }
            go.name = name;
            return go;
        }

        static PrimitiveType PrimitiveOf(string shape)
        {
            switch (shape)
            {
                case "cube": return PrimitiveType.Cube;
                case "sphere": return PrimitiveType.Sphere;
                case "capsule": return PrimitiveType.Capsule;
                case "cylinder": return PrimitiveType.Cylinder;
                case "plane": return PrimitiveType.Plane;
                case "quad": return PrimitiveType.Quad;
                default: throw new ArgumentException("shape " + shape);
            }
        }

        /// <summary>One material per colour, created once (April made a new one per run).</summary>
        static Material MaterialFor(string hex)
        {
            string path = $"{MaterialsDir}/c_{hex.TrimStart('#').ToLowerInvariant()}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets/Generated", "Materials");
            mat = new Material(ModelLook.LoadShader());
            ModelLook.Style(mat);
            mat.SetColor("_BaseColor", ColorOf(hex));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Names, transforms and component types of every object — the same config gives the same hash.</summary>
        public static string ContentHash(Scene scene, out int count)
        {
            var sb = new StringBuilder();
            int n = 0;
            void Walk(Transform t, string path)
            {
                n++;
                string p = path + "/" + t.name;
                var comps = t.GetComponents<Component>().Select(c => c.GetType().Name).OrderBy(x => x, StringComparer.Ordinal);
                sb.Append(p).Append('|').Append(F(t.localPosition)).Append('|').Append(F(t.localEulerAngles)).Append('|')
                  .Append(F(t.localScale)).Append('|').Append(string.Join(",", comps)).Append('\n');
                foreach (Transform c in t) Walk(c, p);
            }
            foreach (var r in scene.GetRootGameObjects().OrderBy(g => g.name, StringComparer.Ordinal)) Walk(r.transform, "");
            count = n;
            using (var sha = SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }

        static string F(Vector3 v) => FormattableString.Invariant($"{v.x:0.###},{v.y:0.###},{v.z:0.###}");
        static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        static Color ColorOf(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
