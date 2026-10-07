using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    public static class InteractablePrefabBuilder
    {
        [MenuItem("ZeldaDaughter/Prefabs/Build Interactable Prefabs")]
        public static void BuildInteractablePrefabs()
        {
            EnsureFolder("Assets/Content");
            EnsureFolder("Assets/Content/Items");
            EnsureFolder("Assets/Content/Resources");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Interactables");

            var stick  = CreateItemData("Item_Stick",  "Палка",  0.5f,  true, 10, "Палка... сгодится");
            var stone  = CreateItemData("Item_Stone",  "Камень", 1.0f,  true,  5, "Тяжёлый камень");
            var berry  = CreateItemData("Item_Berry",  "Ягоды",  0.1f,  true, 20, "Ягоды. Съедобные?");
            var wood   = CreateItemData("Item_Wood",   "Дрова",  2.0f,  true,  5, "Дрова. Пригодятся");
            var branch = CreateItemData("Item_Branch", "Ветка",  0.3f,  true, 10, "Тонкая ветка");

            CreateResourceNodeData("Resource_Tree", maxHp: 5, respawn: 300f, new[]
            {
                new ResourceNodeData.ItemDrop { item = stick, minAmount = 1, maxAmount = 2 },
                new ResourceNodeData.ItemDrop { item = wood,  minAmount = 1, maxAmount = 1 },
            });

            CreateResourceNodeData("Resource_Rock", maxHp: 8, respawn: 600f, new[]
            {
                new ResourceNodeData.ItemDrop { item = stone, minAmount = 1, maxAmount = 2 },
            });

            CreateResourceNodeData("Resource_Bush", maxHp: 2, respawn: 120f, new[]
            {
                new ResourceNodeData.ItemDrop { item = berry,  minAmount = 2, maxAmount = 3 },
                new ResourceNodeData.ItemDrop { item = branch, minAmount = 0, maxAmount = 1 },
            });

            CreatePickupablePrefab(stick);
            CreatePickupablePrefab(stone);
            CreatePickupablePrefab(berry);
            CreatePickupablePrefab(wood);
            CreatePickupablePrefab(branch);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[InteractablePrefabBuilder] Все Interactable prefabs и данные созданы.");
        }

        private static ItemData CreateItemData(
            string id, string displayName, float weight,
            bool stackable, int maxStack, string pickupLine)
        {
            string assetPath = $"Assets/Content/Items/{id}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (existing != null)
            {
                Debug.Log($"[InteractablePrefabBuilder] ItemData уже существует, перезаписываем: {assetPath}");
                AssetDatabase.DeleteAsset(assetPath);
            }

            var item = ScriptableObject.CreateInstance<ItemData>();
            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_weight").floatValue = weight;
            so.FindProperty("_stackable").boolValue = stackable;
            so.FindProperty("_maxStack").intValue = maxStack;
            so.FindProperty("_pickupLine").stringValue = pickupLine;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(item, assetPath);
            Debug.Log($"[InteractablePrefabBuilder] ItemData создан: {assetPath}");
            return item;
        }

        private static void CreateResourceNodeData(
            string assetName, int maxHp, float respawn,
            ResourceNodeData.ItemDrop[] drops)
        {
            string assetPath = $"Assets/Content/Resources/{assetName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(assetPath);
            if (existing != null)
            {
                Debug.Log($"[InteractablePrefabBuilder] ResourceNodeData уже существует, перезаписываем: {assetPath}");
                AssetDatabase.DeleteAsset(assetPath);
            }

            var data = ScriptableObject.CreateInstance<ResourceNodeData>();
            var so = new SerializedObject(data);
            so.FindProperty("_maxHitPoints").intValue = maxHp;
            so.FindProperty("_respawnTime").floatValue = respawn;

            var dropsProp = so.FindProperty("_drops");
            dropsProp.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++)
            {
                var el = dropsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("item").objectReferenceValue = drops[i].item;
                el.FindPropertyRelative("minAmount").intValue = drops[i].minAmount;
                el.FindPropertyRelative("maxAmount").intValue = drops[i].maxAmount;
            }

            so.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(data, assetPath);
            Debug.Log($"[InteractablePrefabBuilder] ResourceNodeData создан: {assetPath}");
        }

        private static void CreatePickupablePrefab(ItemData itemData)
        {
            string prefabPath = $"Assets/Prefabs/Interactables/{itemData.Id}.prefab";

            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = itemData.Id;
            root.transform.localScale = Vector3.one * 0.3f;

            // Коллайдер уже добавлен CreatePrimitive — оставляем как есть.
            // Настраиваем слой Interactable (не блокирует CharacterController)
            if (root.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                capsule.isTrigger = true;
            }

            var pickupable = root.AddComponent<Pickupable>();
            var pickSo = new SerializedObject(pickupable);
            pickSo.FindProperty("_itemData").objectReferenceValue = itemData;
            pickSo.FindProperty("_amount").intValue = 1;
            pickSo.FindProperty("_saveId").stringValue = itemData.Id + "_world";
            pickSo.ApplyModifiedProperties();

            root.AddComponent<InteractableHighlight>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (saved != null)
                Debug.Log($"[InteractablePrefabBuilder] Prefab сохранён: {prefabPath}");
            else
                Debug.LogError($"[InteractablePrefabBuilder] Не удалось сохранить prefab: {prefabPath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int lastSlash = path.LastIndexOf('/');
            string parent = path[..lastSlash];
            string folderName = path[(lastSlash + 1)..];
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
