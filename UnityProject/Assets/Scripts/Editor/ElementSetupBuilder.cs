using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    public static class ElementSetupBuilder
    {
        [MenuItem("ZeldaDaughter/Setup/Add Element Tags to Scene")]
        public static void AddElementTagsToScene()
        {
            var stats = new Stats();

            var allObjects = Object.FindObjectsOfType<GameObject>(includeInactive: true);

            foreach (var go in allObjects)
                ProcessObject(go, stats);

            Debug.Log("[ElementSetupBuilder] Done. Results:");
            Debug.Log($"  FlammableTag added (trees):  {stats.FlammableTrees}");
            Debug.Log($"  FlammableTag added (grass):  {stats.FlammableGrass}");
            Debug.Log($"  WettableTag added (rocks):   {stats.WettableRocks}");
            Debug.Log($"  WettableTag added (terrain): {stats.WettableTerrain}");
            Debug.Log($"  Skipped (already tagged):    {stats.Skipped}");
        }

        private static void ProcessObject(GameObject go, Stats stats)
        {
            string nameLower = go.name.ToLowerInvariant();

            if (IsTree(go, nameLower))
            {
                if (go.TryGetComponent<FlammableTag>(out _))
                {
                    stats.Skipped++;
                    return;
                }
                Undo.AddComponent<FlammableTag>(go);
                stats.FlammableTrees++;
                return;
            }

            if (IsGrass(go, nameLower))
            {
                if (go.TryGetComponent<FlammableTag>(out _))
                {
                    stats.Skipped++;
                    return;
                }
                Undo.AddComponent<FlammableTag>(go);
                stats.FlammableGrass++;
                return;
            }

            if (IsRock(nameLower))
            {
                if (go.TryGetComponent<WettableTag>(out _))
                {
                    stats.Skipped++;
                    return;
                }
                Undo.AddComponent<WettableTag>(go);
                stats.WettableRocks++;
                return;
            }

            if (IsTerrain(go, nameLower))
            {
                if (go.TryGetComponent<WettableTag>(out _))
                {
                    stats.Skipped++;
                    return;
                }
                Undo.AddComponent<WettableTag>(go);
                stats.WettableTerrain++;
            }
        }

        private static bool IsTree(GameObject go, string nameLower)
        {
            if (go.CompareTag("Tree")) return true;
            return nameLower.Contains("tree") || nameLower.Contains("pine") || nameLower.Contains("oak")
                   || nameLower.Contains("birch") || nameLower.Contains("palm");
        }

        private static bool IsGrass(GameObject go, string nameLower)
        {
            if (go.CompareTag("Grass")) return true;
            return nameLower.Contains("grass") || nameLower.Contains("bush") || nameLower.Contains("shrub")
                   || nameLower.Contains("fern") || nameLower.Contains("plant") || nameLower.Contains("flower");
        }

        private static bool IsRock(string nameLower)
        {
            return nameLower.Contains("rock") || nameLower.Contains("stone") || nameLower.Contains("cliff")
                   || nameLower.Contains("boulder") || nameLower.Contains("pebble");
        }

        private static bool IsTerrain(GameObject go, string nameLower)
        {
            if (go.TryGetComponent<Terrain>(out _)) return true;
            return nameLower == "terrain" || nameLower.Contains("ground") || nameLower.Contains("dirt")
                   || nameLower.Contains("soil") || nameLower.Contains("earth");
        }

        private struct Stats
        {
            public int FlammableTrees;
            public int FlammableGrass;
            public int WettableRocks;
            public int WettableTerrain;
            public int Skipped;
        }
    }
}
