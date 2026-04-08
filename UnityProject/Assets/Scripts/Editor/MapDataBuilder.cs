using UnityEngine;
using UnityEditor;
using ZeldaDaughter.World;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Editor
{
    public static class MapDataBuilder
    {
        private const string MapDir      = "Assets/Data/Map";
        private const string UIDir       = "Assets/Data/UI";

        [MenuItem("ZeldaDaughter/Data/Build Map Data")]
        public static void Build()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(MapDir);
            EnsureFolder(UIDir);

            BuildForestTownRegion();
            BuildNotebookConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MapDataBuilder] Map data built.");
        }

        // ─── Регион ────────────────────────────────────────────────────────────

        private static void BuildForestTownRegion()
        {
            string path = $"{MapDir}/MapRegion_ForestTown.asset";
            var region = CreateIfNotExists<MapRegionData>(path);
            if (region == null)
            {
                Debug.Log($"[MapDataBuilder] MapRegionData уже существует: {path}");
                return;
            }

            var so = new SerializedObject(region);
            so.FindProperty("_regionId").stringValue       = "forest_town";
            so.FindProperty("_regionName").stringValue     = "Лес и окрестности";
            so.FindProperty("_requiredMapItemId").stringValue = "map_forest";

            // WorldBounds: Rect(x, y, width, height)
            var bounds = so.FindProperty("_worldBounds");
            bounds.FindPropertyRelative("m_XMin").floatValue  = 0f;
            bounds.FindPropertyRelative("m_YMin").floatValue  = 0f;
            bounds.FindPropertyRelative("m_Width").floatValue = 200f;
            bounds.FindPropertyRelative("m_Height").floatValue = 200f;

            // Маркеры: (markerId, normalizedX, normalizedY, label)
            var markers = new (string id, float nx, float ny, string label)[]
            {
                ("market",         0.5f,  0.70f, "Рынок"),
                ("forge",          0.4f,  0.65f, "Кузница"),
                ("tavern",         0.6f,  0.70f, "Таверна"),
                ("herb_shop",      0.45f, 0.75f, "Лавка травницы"),
                ("gate",           0.5f,  0.55f, "Ворота города"),
                ("forest_south",   0.5f,  0.30f, "Южный лес"),
                ("river",          0.7f,  0.40f, "Река"),
            };

            var markersProp = so.FindProperty("_markers");
            markersProp.arraySize = markers.Length;

            for (int i = 0; i < markers.Length; i++)
            {
                var m = markersProp.GetArrayElementAtIndex(i);
                m.FindPropertyRelative("markerId").stringValue = markers[i].id;
                m.FindPropertyRelative("label").stringValue    = markers[i].label;
                m.FindPropertyRelative("description").stringValue = string.Empty;

                var pos = m.FindPropertyRelative("normalizedPosition");
                pos.FindPropertyRelative("x").floatValue = markers[i].nx;
                pos.FindPropertyRelative("y").floatValue = markers[i].ny;
            }

            so.ApplyModifiedProperties();
            Debug.Log("[MapDataBuilder] MapRegionData создан: forest_town");
        }

        // ─── NotebookConfig ────────────────────────────────────────────────────

        private static void BuildNotebookConfig()
        {
            string path = $"{UIDir}/NotebookConfig.asset";
            var config = CreateIfNotExists<NotebookConfig>(path);
            if (config == null)
            {
                Debug.Log($"[MapDataBuilder] NotebookConfig уже существует: {path}");
                return;
            }

            var so = new SerializedObject(config);
            so.FindProperty("_maxEntries").intValue = 100;
            // Иконки оставляем null — заполнятся через Inspector после импорта спрайтов
            so.ApplyModifiedProperties();
            Debug.Log("[MapDataBuilder] NotebookConfig создан.");
        }

        // ─── Утилиты ───────────────────────────────────────────────────────────

        private static T CreateIfNotExists<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return null;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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
