using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    public static class StationPrefabBuilder
    {
        [MenuItem("ZeldaDaughter/Prefabs/Build Station Prefabs")]
        public static void Build()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Stations");

            BuildStation("Smelter", StationType.Smelter, "Assets/Prefabs/Stations");
            BuildStation("Anvil",   StationType.Anvil,   "Assets/Prefabs/Stations");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StationPrefabBuilder] Station prefabs built.");
        }

        private static void BuildStation(string stationName, StationType type, string folder)
        {
            string prefabPath = $"{folder}/{stationName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
                Debug.Log($"[StationPrefabBuilder] Перезаписываем: {prefabPath}");
            }

            var root = new GameObject(stationName);

            // Визуальный placeholder
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(1f, 0.8f, 0.6f);
            // Убираем коллайдер с визуала — он нужен только на корне
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            // Interaction point — перед станком
            var interPoint = new GameObject("InteractionPoint");
            interPoint.transform.SetParent(root.transform);
            interPoint.transform.localPosition = new Vector3(0f, 0f, 1f);

            // Коллайдер на корне
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 0.8f, 0.6f);

            // StationInteractable
            var station = root.AddComponent<StationInteractable>();
            var so = new SerializedObject(station);
            so.FindProperty("_stationType").enumValueIndex = (int)type;
            so.FindProperty("_interactionPoint").objectReferenceValue = interPoint.transform;
            so.FindProperty("_interactionRange").floatValue = 2f;
            so.ApplyModifiedProperties();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (saved != null)
                Debug.Log($"[StationPrefabBuilder] Prefab сохранён: {prefabPath}");
            else
                Debug.LogError($"[StationPrefabBuilder] Не удалось сохранить prefab: {prefabPath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int lastSlash = path.LastIndexOf('/');
            string parent = path[..lastSlash];
            string folderName = path[(lastSlash + 1)..];
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
