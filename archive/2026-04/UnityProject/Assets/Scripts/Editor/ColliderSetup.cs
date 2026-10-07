using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class ColliderSetup
    {
        [MenuItem("ZeldaDaughter/MapGen/Setup Colliders")]
        public static void SetupColliders()
        {
            var roots = FindRegionRoots();
            if (roots.Count == 0)
            {
                Debug.LogWarning("[ColliderSetup] No GameObject with name starting 'Region_' found in scene.");
                return;
            }

            foreach (var root in roots)
                SetupCollidersOnRegion(root);
        }

        /// <summary>
        /// Настраивает коллайдеры на конкретном регионе. Вызывается из RegionPlacer после размещения.
        /// </summary>
        public static void SetupCollidersOnRegion(GameObject regionRoot)
        {
            var stats = new Dictionary<string, int>
            {
                { "CapsuleCollider", 0 },
                { "MeshCollider", 0 },
                { "BoxCollider", 0 },
                { "SphereCollider (trigger)", 0 },
                { "Skipped (already has collider)", 0 }
            };

            Debug.Log($"[ColliderSetup] Processing '{regionRoot.name}'...");
            ProcessRecursive(regionRoot.transform, stats);

            Debug.Log("[ColliderSetup] Done. Results:");
            foreach (var kv in stats)
                Debug.Log($"  {kv.Key}: {kv.Value}");
        }

        private static List<GameObject> FindRegionRoots()
        {
            var result = new List<GameObject>();
            var allObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var go in allObjects)
            {
                if (go.name.StartsWith("Region_") && go.transform.parent == null)
                    result.Add(go);
            }
            return result;
        }

        private static void ProcessRecursive(Transform t, Dictionary<string, int> stats)
        {
            AddColliderIfNeeded(t.gameObject, stats);

            for (int i = 0; i < t.childCount; i++)
                ProcessRecursive(t.GetChild(i), stats);
        }

        private static void AddColliderIfNeeded(GameObject go, Dictionary<string, int> stats)
        {
            if (go.TryGetComponent<Collider>(out _))
            {
                stats["Skipped (already has collider)"]++;
                return;
            }

            string nameLower = go.name.ToLowerInvariant();
            var renderer = go.GetComponentInChildren<Renderer>();

            if (nameLower.Contains("tree"))
            {
                var col = Undo.AddComponent<CapsuleCollider>(go);
                col.radius = 0.3f;

                float height = 2f;
                if (renderer != null)
                    height = renderer.bounds.size.y;

                col.height = height;
                col.center = new Vector3(0f, height * 0.5f, 0f);
                stats["CapsuleCollider"]++;
            }
            else if (nameLower.Contains("rock_large") || nameLower.Contains("cliff"))
            {
                var col = Undo.AddComponent<MeshCollider>(go);
                col.convex = true;
                stats["MeshCollider"]++;
            }
            else if (nameLower.Contains("wall") || nameLower.Contains("fence") || nameLower.Contains("building"))
            {
                var col = Undo.AddComponent<BoxCollider>(go);
                FitBoxToRenderer(col, renderer);
                stats["BoxCollider"]++;
            }
            else if (nameLower.Contains("fountain") || nameLower.Contains("stall") || nameLower.Contains("cart"))
            {
                var col = Undo.AddComponent<BoxCollider>(go);
                FitBoxToRenderer(col, renderer);
                stats["BoxCollider"]++;
            }
            else if (nameLower.Contains("bush") || nameLower.Contains("grass") || nameLower.Contains("flower")
                     || nameLower.Contains("mushroom") || nameLower.Contains("plant"))
            {
                var col = Undo.AddComponent<SphereCollider>(go);
                col.isTrigger = true;
                col.radius = 0.5f;
                stats["SphereCollider (trigger)"]++;
            }
            else if (nameLower.Contains("bridge"))
            {
                var col = Undo.AddComponent<BoxCollider>(go);
                FitBoxToRenderer(col, renderer);
                stats["BoxCollider"]++;
            }
        }

        private static void FitBoxToRenderer(BoxCollider col, Renderer renderer)
        {
            if (renderer == null) return;

            // Convert world-space bounds to local-space size
            var bounds = renderer.bounds;
            var t = col.transform;

            col.center = t.InverseTransformPoint(bounds.center);
            col.size = new Vector3(
                bounds.size.x / t.lossyScale.x,
                bounds.size.y / t.lossyScale.y,
                bounds.size.z / t.lossyScale.z
            );
        }
    }
}
