using ZeldaDaughter.World;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ZeldaDaughter.Editor.MapGen
{
    public static class RegionPlacer
    {
        /// <summary>
        /// Menu item: pick a JSON config and place the region.
        /// </summary>
        [MenuItem("ZeldaDaughter/MapGen/Place Region from JSON...")]
        public static void PlaceFromMenu()
        {
            var path = EditorUtility.OpenFilePanel("Select Region Config", "Assets/Content/Configs", "json");
            if (string.IsNullOrEmpty(path)) return;
            PlaceRegion(path);
        }

        /// <summary>
        /// Batch mode entry point:
        /// Unity -executeMethod ZeldaDaughter.Editor.MapGen.RegionPlacer.PlaceFromCommandLine -regionConfig path/to/config.json
        /// </summary>
        public static void PlaceFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string configPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-regionConfig" && i + 1 < args.Length)
                {
                    configPath = args[i + 1];
                    break;
                }
            }

            if (string.IsNullOrEmpty(configPath))
            {
                Debug.LogError("[MapGen] Missing -regionConfig argument");
                return;
            }

            PlaceRegion(configPath);
            EditorSceneManager.SaveOpenScenes();
        }

        public static void PlaceRegion(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[MapGen] Config not found: {jsonPath}");
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var config = JsonUtility.FromJson<RegionConfigData>(json);

            if (config == null || string.IsNullOrEmpty(config.regionId))
            {
                Debug.LogError("[MapGen] Invalid config or missing regionId");
                return;
            }

            Debug.Log($"[MapGen] Placing region '{config.regionId}' ({config.regionName})...");

            // Idempotent: remove previous generation
            var existing = GameObject.Find($"Region_{config.regionId}");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log($"[MapGen] Removed previous Region_{config.regionId}");
            }

            // Root
            var root = new GameObject($"Region_{config.regionId}");
            Undo.RegisterCreatedObjectUndo(root, "MapGen Place Region");

            // Groups
            var objectsGroup = CreateChild(root, "Objects");
            var decorGroup = CreateChild(root, "Decoration");
            var spawnGroup = CreateChild(root, "SpawnZones");
            var pathsGroup = CreateChild(root, "Paths");
            var waterGroup = CreateChild(root, "Water");

            Random.InitState(config.seed);

            // Place individual objects
            foreach (var obj in config.objects)
                PlaceObject(obj, objectsGroup.transform);

            // Place decoration zones
            foreach (var zone in config.decorationZones)
                PlaceDecorationZone(zone, decorGroup.transform, config.seed);

            // Place spawn zones
            foreach (var sz in config.spawnZones)
                PlaceSpawnZone(sz, spawnGroup.transform);

            // Place paths
            foreach (var path in config.paths)
                PlacePath(path, pathsGroup.transform);

            // Place water
            foreach (var water in config.waterAreas)
                PlaceWaterArea(water, waterGroup.transform);

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            float sizeX = (config.bounds.maxX - config.bounds.minX) / 10f; // Plane default = 10 units
            float sizeZ = (config.bounds.maxZ - config.bounds.minZ) / 10f;
            ground.transform.localScale = new Vector3(sizeX, 1f, sizeZ);
            ground.transform.position = new Vector3(
                (config.bounds.minX + config.bounds.maxX) / 2f, 0f,
                (config.bounds.minZ + config.bounds.maxZ) / 2f);
            EnsureTagExists("Grass");
            ground.tag = "Grass";
            var groundRenderer = ground.GetComponent<Renderer>();
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.35f, 0.55f, 0.25f);
            groundRenderer.material = groundMat;
            Undo.RegisterCreatedObjectUndo(ground, "MapGen Ground");

            // Colliders и вода
            ZeldaDaughter.Editor.ColliderSetup.SetupCollidersOnRegion(root);
            ZeldaDaughter.Editor.WaterSetup.SetupWaterInRegion(root);

            Debug.Log($"[MapGen] Region '{config.regionId}' placed successfully. " +
                      $"Objects: {config.objects.Count}, " +
                      $"DecorZones: {config.decorationZones.Count}, " +
                      $"SpawnZones: {config.spawnZones.Count}, " +
                      $"Paths: {config.paths.Count}, " +
                      $"Water: {config.waterAreas.Count}");
        }

        private static void PlaceObject(PlacedObjectData data, Transform parent)
        {
            GameObject go;

            if (!string.IsNullOrEmpty(data.prefab))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.prefab);
                if (prefab == null)
                {
                    // Try adding common extensions
                    foreach (var ext in new[] { ".prefab", ".fbx", ".obj" })
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.prefab + ext);
                        if (prefab != null) break;
                    }
                }

                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                }
                else
                {
                    Debug.LogWarning($"[MapGen] Prefab not found: {data.prefab}, creating empty marker");
                    go = new GameObject(data.id ?? "UnknownObject");
                    go.transform.SetParent(parent);
                }
            }
            else
            {
                go = new GameObject(data.id ?? "Marker");
                go.transform.SetParent(parent);
            }

            go.name = data.id ?? go.name;
            if (data.position != null) go.transform.localPosition = data.position.ToVector3();
            if (data.rotation != null) go.transform.localRotation = data.rotation.ToQuaternion();
            if (data.scale != null) go.transform.localScale = data.scale.ToVector3();

            if (data.tags != null && data.tags.Count > 0)
            {
                var tag = go.AddComponent<MapGenTag>();
                tag.SetTags(data.tags.ToArray());
            }
        }

        private static void PlaceDecorationZone(DecorationZoneData zone, Transform parent, int baseSeed)
        {
            if (zone.prefabs == null || zone.prefabs.Count == 0) return;

            var zoneGO = new GameObject($"Zone_{zone.zoneType}");
            zoneGO.transform.SetParent(parent);
            zoneGO.transform.position = zone.center.ToVector3();

            float area = Mathf.PI * zone.radius * zone.radius;
            int count = Mathf.RoundToInt(area * zone.density);

            // Load prefabs
            var loadedPrefabs = new GameObject[zone.prefabs.Count];
            for (int i = 0; i < zone.prefabs.Count; i++)
            {
                loadedPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(zone.prefabs[i]);
                if (loadedPrefabs[i] == null)
                {
                    foreach (var ext in new[] { ".prefab", ".fbx", ".obj", ".dae" })
                    {
                        loadedPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(zone.prefabs[i] + ext);
                        if (loadedPrefabs[i] != null) break;
                    }
                }

                if (loadedPrefabs[i] == null)
                    Debug.LogWarning($"[MapGen] Decoration prefab not found: {zone.prefabs[i]}");
            }

            // Build weight array for weighted random
            float totalWeight = 0f;
            var weights = new float[zone.prefabs.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = (zone.prefabWeights != null && i < zone.prefabWeights.Count)
                    ? zone.prefabWeights[i]
                    : 1f;
                totalWeight += weights[i];
            }

            Random.InitState(baseSeed + zone.zoneType.GetHashCode());

            for (int i = 0; i < count; i++)
            {
                // Random point in circle
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.Range(0f, 1f)) * zone.radius;
                var pos = zone.center.ToVector3() + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                // Weighted random prefab selection
                int prefabIndex = SelectWeighted(weights, totalWeight);
                if (loadedPrefabs[prefabIndex] == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(loadedPrefabs[prefabIndex], zoneGO.transform);
                go.transform.position = pos;

                float scale = Random.Range(zone.minScale, zone.maxScale);
                go.transform.localScale = Vector3.one * scale;

                if (zone.randomRotationY)
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }

        private static int SelectWeighted(float[] weights, float totalWeight)
        {
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) return i;
            }
            return weights.Length - 1;
        }

        private static void PlaceSpawnZone(SpawnZoneData data, Transform parent)
        {
            var go = new GameObject($"Spawn_{data.id ?? data.enemyType}");
            go.transform.SetParent(parent);
            go.transform.position = data.center.ToVector3();

            var marker = go.AddComponent<SpawnZoneMarker>();
            marker.Setup(data.enemyType, data.radius, data.maxCount, data.respawnTimeSec);
        }

        private static void PlacePath(PathData data, Transform parent)
        {
            var go = new GameObject($"Path_{data.id ?? data.pathType}");
            go.transform.SetParent(parent);

            var marker = go.AddComponent<PathMarker>();
            marker.Setup(data.pathType, data.width);

            for (int i = 0; i < data.waypoints.Count; i++)
            {
                var wp = new GameObject($"WP_{i}");
                wp.transform.SetParent(go.transform);
                wp.transform.position = data.waypoints[i].ToVector3();
            }
        }

        private static void PlaceWaterArea(WaterAreaData data, Transform parent)
        {
            var go = new GameObject($"Water_{data.id ?? data.waterType}");
            go.transform.SetParent(parent);
            go.transform.position = data.center.ToVector3();

            var marker = go.AddComponent<WaterAreaMarker>();
            marker.Setup(data.waterType, data.radius, data.depth);
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        private static void EnsureTagExists(string tag)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProperty = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProperty.arraySize; i++)
            {
                if (tagsProperty.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }
            tagsProperty.InsertArrayElementAtIndex(tagsProperty.arraySize);
            tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
