using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Combat;

namespace ZeldaDaughter.Editor
{
    public static class EnemyPrefabBuilder
    {
        [MenuItem("ZeldaDaughter/Prefabs/Build Enemy Prefabs")]
        public static void Build()
        {
            const string path = "Assets/Prefabs/Enemies";
            EnsureFolder(path);

            BuildEnemy("Boar", path, typeof(BoarBehavior));
            BuildEnemy("Wolf", path, typeof(WolfBehavior));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyPrefabBuilder] Enemy prefabs built.");
        }

        private static void BuildEnemy(string name, string folder, System.Type behaviorType)
        {
            var go = new GameObject(name);
            go.tag = "Enemy";
            go.layer = LayerMask.NameToLayer("Default");

            // Визуальный placeholder
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;

            // Убрать auto-collider с visual
            var meshCol = visual.GetComponent<Collider>();
            if (meshCol != null) Object.DestroyImmediate(meshCol);

            // Основной collider
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.5f;
            col.center = new Vector3(0f, 1f, 0f);

            // Interaction point
            var interPoint = new GameObject("InteractionPoint");
            interPoint.transform.SetParent(go.transform);
            interPoint.transform.localPosition = new Vector3(0f, 0f, 1.5f);

            // Компоненты
            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyFSM>();
            go.AddComponent<EnemyAttackSignal>();
            go.AddComponent<StunEffect>();
            if (behaviorType != null)
                go.AddComponent(behaviorType);

            // Hitbox (дочерний объект)
            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(go.transform);
            hitboxGo.transform.localPosition = new Vector3(0f, 0.5f, 0.8f);
            var hitboxCol = hitboxGo.AddComponent<BoxCollider>();
            hitboxCol.isTrigger = true;
            hitboxCol.size = new Vector3(0.8f, 0.8f, 0.8f);
            hitboxGo.AddComponent<HitboxTrigger>();

            string prefabPath = $"{folder}/{name}.prefab";
            // Удалить старый если есть
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            Debug.Log($"[EnemyPrefabBuilder] Created {prefabPath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
