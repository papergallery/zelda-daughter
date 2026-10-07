using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Generates building prefabs from Kenney FantasyTownKit modular parts.
    /// Run via: ZeldaDaughter/Buildings/Build All Prefabs
    /// </summary>
    public static class BuildingPrefabBuilder
    {
        private const string FbxRoot = "Assets/Models/Kenney/FantasyTownKit/Models/FBX format";
        private const string PrefabOutput = "Assets/Prefabs/Buildings";

        // --- FBX filenames mapped to logical roles ---

        // Stone walls
        private const string WallPlain        = "wall.fbx";
        private const string WallDoor         = "wall-door.fbx";
        private const string WallWindow       = "wall-window-shutters.fbx";
        private const string WallWindowSmall  = "wall-window-small.fbx";
        private const string WallCorner       = "wall-corner.fbx";
        private const string WallHalf         = "wall-half.fbx";

        // Wood walls
        private const string WallWoodPlain    = "wall-wood.fbx";
        private const string WallWoodDoor     = "wall-wood-door.fbx";
        private const string WallWoodWindow   = "wall-wood-window-shutters.fbx";
        private const string WallWoodCorner   = "wall-wood-corner.fbx";
        private const string WallWoodHalf     = "wall-wood-half.fbx";

        // Roofs
        private const string RoofCenter       = "roof.fbx";
        private const string RoofCorner       = "roof-corner.fbx";
        private const string RoofLeft         = "roof-left.fbx";
        private const string RoofRight        = "roof-right.fbx";
        private const string RoofGable        = "roof-gable.fbx";
        private const string RoofGableEnd     = "roof-gable-end.fbx";
        private const string RoofGableTop     = "roof-gable-top.fbx";
        private const string RoofFlat         = "roof-flat.fbx";

        // One tile is 1 Unity unit wide; walls are placed on a grid.
        // All buildings sit at world-space Y = 0.

        [MenuItem("ZeldaDaughter/Buildings/Build All Prefabs")]
        public static void BuildAllPrefabs()
        {
            EnsureOutputFolder();

            int built = 0;
            built += BuildTavern()    ? 1 : 0;
            built += BuildShop()      ? 1 : 0;
            built += BuildBlacksmith() ? 1 : 0;
            built += BuildHut()       ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuildingPrefabBuilder] Done. Built {built}/4 prefabs → {PrefabOutput}/");
        }

        // -----------------------------------------------------------------------
        //  1. TAVERN  (4×3 footprint, stone, high roof)
        // -----------------------------------------------------------------------
        private static bool BuildTavern()
        {
            var root = new GameObject("Tavern");

            // Layout: Z axis = depth (0..3), X axis = width (0..4)
            // Front wall (Z=0): door at X=1, windows at X=0 and X=2, plain at X=3
            // Back  wall (Z=3): all plain
            // Left  wall (X=0): plain × 3
            // Right wall (X=4): plain × 3

            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform);

            // Front row (facing -Z, rotated 0°)
            AddPart(walls, WallDoor,       new Vector3(1f, 0f, 0f), 0f);
            AddPart(walls, WallWindow,     new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallWindow,     new Vector3(2f, 0f, 0f), 0f);
            AddPart(walls, WallPlain,      new Vector3(3f, 0f, 0f), 0f);

            // Back row (facing +Z, rotated 180°)
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(1f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(2f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(3f, 0f, 3f), 180f);

            // Left column (facing -X, rotated 90°)
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 1f), 90f);
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 2f), 90f);

            // Right column (facing +X, rotated 270°)
            AddPart(walls, WallPlain,      new Vector3(3f, 0f, 1f), 270f);
            AddPart(walls, WallPlain,      new Vector3(3f, 0f, 2f), 270f);

            // Corners
            AddPart(walls, WallCorner,     new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallCorner,     new Vector3(3f, 0f, 0f), 90f);
            AddPart(walls, WallCorner,     new Vector3(3f, 0f, 3f), 180f);
            AddPart(walls, WallCorner,     new Vector3(0f, 0f, 3f), 270f);

            // Roof: high gable across 4×3
            var roof = new GameObject("Roof");
            roof.transform.SetParent(root.transform);

            // Ridge runs along X; gable ends at Z=0 and Z=3
            AddPart(roof, RoofGable,       new Vector3(0f, 1f, 0f), 0f);
            AddPart(roof, RoofGable,       new Vector3(1f, 1f, 0f), 0f);
            AddPart(roof, RoofGable,       new Vector3(2f, 1f, 0f), 0f);
            AddPart(roof, RoofGable,       new Vector3(3f, 1f, 0f), 0f);

            AddPart(roof, RoofGable,       new Vector3(0f, 1f, 3f), 180f);
            AddPart(roof, RoofGable,       new Vector3(1f, 1f, 3f), 180f);
            AddPart(roof, RoofGable,       new Vector3(2f, 1f, 3f), 180f);
            AddPart(roof, RoofGable,       new Vector3(3f, 1f, 3f), 180f);

            AddPart(roof, RoofGableEnd,    new Vector3(0f, 1f, 1f), 90f);
            AddPart(roof, RoofGableEnd,    new Vector3(0f, 1f, 2f), 90f);
            AddPart(roof, RoofGableEnd,    new Vector3(3f, 1f, 1f), 270f);
            AddPart(roof, RoofGableEnd,    new Vector3(3f, 1f, 2f), 270f);

            AddPart(roof, RoofGableTop,    new Vector3(0f, 2f, 1f), 0f);
            AddPart(roof, RoofGableTop,    new Vector3(1f, 2f, 1f), 0f);
            AddPart(roof, RoofGableTop,    new Vector3(2f, 2f, 1f), 0f);
            AddPart(roof, RoofGableTop,    new Vector3(3f, 2f, 1f), 0f);

            AddBoxCollider(root, new Vector3(2f, 1f, 1.5f), new Vector3(4f, 2f, 3f));
            return SavePrefab(root, "Tavern");
        }

        // -----------------------------------------------------------------------
        //  2. SHOP  (3×3 footprint, stone)
        // -----------------------------------------------------------------------
        private static bool BuildShop()
        {
            var root = new GameObject("Shop");

            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform);

            // Front: door + window + window
            AddPart(walls, WallDoor,       new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallWindow,     new Vector3(1f, 0f, 0f), 0f);
            AddPart(walls, WallWindow,     new Vector3(2f, 0f, 0f), 0f);

            // Back
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(1f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(2f, 0f, 3f), 180f);

            // Left
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 1f), 90f);
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 2f), 90f);

            // Right
            AddPart(walls, WallPlain,      new Vector3(2f, 0f, 1f), 270f);
            AddPart(walls, WallPlain,      new Vector3(2f, 0f, 2f), 270f);

            // Corners
            AddPart(walls, WallCorner,     new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallCorner,     new Vector3(2f, 0f, 0f), 90f);
            AddPart(walls, WallCorner,     new Vector3(2f, 0f, 3f), 180f);
            AddPart(walls, WallCorner,     new Vector3(0f, 0f, 3f), 270f);

            // Roof: standard gable
            var roof = new GameObject("Roof");
            roof.transform.SetParent(root.transform);

            AddPart(roof, RoofGable,       new Vector3(0f, 1f, 0f), 0f);
            AddPart(roof, RoofGable,       new Vector3(1f, 1f, 0f), 0f);
            AddPart(roof, RoofGable,       new Vector3(2f, 1f, 0f), 0f);

            AddPart(roof, RoofGable,       new Vector3(0f, 1f, 3f), 180f);
            AddPart(roof, RoofGable,       new Vector3(1f, 1f, 3f), 180f);
            AddPart(roof, RoofGable,       new Vector3(2f, 1f, 3f), 180f);

            AddPart(roof, RoofGableEnd,    new Vector3(0f, 1f, 1f), 90f);
            AddPart(roof, RoofGableEnd,    new Vector3(0f, 1f, 2f), 90f);
            AddPart(roof, RoofGableEnd,    new Vector3(2f, 1f, 1f), 270f);
            AddPart(roof, RoofGableEnd,    new Vector3(2f, 1f, 2f), 270f);

            AddPart(roof, RoofGableTop,    new Vector3(0f, 2f, 1f), 0f);
            AddPart(roof, RoofGableTop,    new Vector3(1f, 2f, 1f), 0f);
            AddPart(roof, RoofGableTop,    new Vector3(2f, 2f, 1f), 0f);

            AddBoxCollider(root, new Vector3(1.5f, 1f, 1.5f), new Vector3(3f, 2f, 3f));
            return SavePrefab(root, "Shop");
        }

        // -----------------------------------------------------------------------
        //  3. BLACKSMITH  (3×3, одна сторона открыта — нет правой стены)
        // -----------------------------------------------------------------------
        private static bool BuildBlacksmith()
        {
            var root = new GameObject("Blacksmith");

            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform);

            // Front: door + 2 plain
            AddPart(walls, WallDoor,       new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallPlain,      new Vector3(1f, 0f, 0f), 0f);
            AddPart(walls, WallPlain,      new Vector3(2f, 0f, 0f), 0f);

            // Back
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(1f, 0f, 3f), 180f);
            AddPart(walls, WallPlain,      new Vector3(2f, 0f, 3f), 180f);

            // Left (closed)
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 1f), 90f);
            AddPart(walls, WallPlain,      new Vector3(0f, 0f, 2f), 90f);

            // Right side intentionally open (no wall pieces)

            // Corners only where walls exist
            AddPart(walls, WallCorner,     new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallCorner,     new Vector3(0f, 0f, 3f), 270f);

            // Half-walls to frame the open side visually
            AddPart(walls, WallHalf,       new Vector3(2f, 0f, 1f), 270f);
            AddPart(walls, WallHalf,       new Vector3(2f, 0f, 2f), 270f);

            // Roof: flat (workshop feel)
            var roof = new GameObject("Roof");
            roof.transform.SetParent(root.transform);

            AddPart(roof, RoofFlat,        new Vector3(0f, 1f, 0f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(1f, 1f, 0f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(2f, 1f, 0f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(0f, 1f, 1f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(1f, 1f, 1f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(2f, 1f, 1f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(0f, 1f, 2f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(1f, 1f, 2f), 0f);
            AddPart(roof, RoofFlat,        new Vector3(2f, 1f, 2f), 0f);

            AddBoxCollider(root, new Vector3(1.5f, 1f, 1.5f), new Vector3(3f, 2f, 3f));
            return SavePrefab(root, "Blacksmith");
        }

        // -----------------------------------------------------------------------
        //  4. HUT  (2×2, wood walls, small)
        // -----------------------------------------------------------------------
        private static bool BuildHut()
        {
            var root = new GameObject("Hut");

            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform);

            // Front: door + window
            AddPart(walls, WallWoodDoor,   new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallWoodWindow, new Vector3(1f, 0f, 0f), 0f);

            // Back
            AddPart(walls, WallWoodPlain,  new Vector3(0f, 0f, 2f), 180f);
            AddPart(walls, WallWoodPlain,  new Vector3(1f, 0f, 2f), 180f);

            // Left
            AddPart(walls, WallWoodPlain,  new Vector3(0f, 0f, 1f), 90f);

            // Right
            AddPart(walls, WallWoodPlain,  new Vector3(1f, 0f, 1f), 270f);

            // Corners
            AddPart(walls, WallWoodCorner, new Vector3(0f, 0f, 0f), 0f);
            AddPart(walls, WallWoodCorner, new Vector3(1f, 0f, 0f), 90f);
            AddPart(walls, WallWoodCorner, new Vector3(1f, 0f, 2f), 180f);
            AddPart(walls, WallWoodCorner, new Vector3(0f, 0f, 2f), 270f);

            // Roof: pointed (cute hut look)
            var roof = new GameObject("Roof");
            roof.transform.SetParent(root.transform);

            AddPart(roof, RoofLeft,        new Vector3(0f, 1f, 0f), 0f);
            AddPart(roof, RoofRight,       new Vector3(1f, 1f, 0f), 0f);
            AddPart(roof, RoofLeft,        new Vector3(0f, 1f, 1f), 0f);
            AddPart(roof, RoofRight,       new Vector3(1f, 1f, 1f), 0f);

            AddPart(roof, RoofCorner,      new Vector3(0f, 1f, 0f), 0f);
            AddPart(roof, RoofCorner,      new Vector3(1f, 1f, 0f), 90f);
            AddPart(roof, RoofCorner,      new Vector3(1f, 1f, 1f), 180f);
            AddPart(roof, RoofCorner,      new Vector3(0f, 1f, 1f), 270f);

            AddBoxCollider(root, new Vector3(1f, 0.75f, 1f), new Vector3(2f, 1.5f, 2f));
            return SavePrefab(root, "Hut");
        }

        // -----------------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------------

        /// <summary>Loads an FBX, instantiates it as child of parent at given local position/yRotation.</summary>
        private static void AddPart(GameObject parent, string fbxName, Vector3 localPos, float yRotDeg)
        {
            string path = $"{FbxRoot}/{fbxName}";
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefabAsset == null)
            {
                Debug.LogWarning($"[BuildingPrefabBuilder] FBX not found: {path}");
                // Place a primitive placeholder so the layout is still visible
                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.name = $"MISSING_{fbxName}";
                placeholder.transform.SetParent(parent.transform);
                placeholder.transform.localPosition = localPos;
                placeholder.transform.localRotation = Quaternion.Euler(0f, yRotDeg, 0f);
                placeholder.transform.localScale = Vector3.one * 0.9f;
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(fbxName);
            instance.transform.SetParent(parent.transform);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = Quaternion.Euler(0f, yRotDeg, 0f);
            instance.transform.localScale = Vector3.one;
        }

        /// <summary>Adds a BoxCollider to root covering the full building footprint.</summary>
        private static void AddBoxCollider(GameObject root, Vector3 center, Vector3 size)
        {
            var col = root.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
        }

        /// <summary>Saves root as a prefab asset, then destroys the scene instance.</summary>
        private static bool SavePrefab(GameObject root, string buildingName)
        {
            string prefabPath = $"{PrefabOutput}/{buildingName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (saved != null)
            {
                Debug.Log($"[BuildingPrefabBuilder] Saved: {prefabPath}");
                return true;
            }

            Debug.LogError($"[BuildingPrefabBuilder] Failed to save prefab: {prefabPath}");
            return false;
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (!AssetDatabase.IsValidFolder(PrefabOutput))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Buildings");
        }
    }
}
