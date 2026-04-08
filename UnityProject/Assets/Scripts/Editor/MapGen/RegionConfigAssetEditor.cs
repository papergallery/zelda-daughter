using UnityEditor;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor.MapGen
{
    // ─────────────────────────────────────────────
    //  Кастомный Inspector для RegionConfigAsset:
    //  добавляет кнопку «Разместить в сцене».
    //
    //  Файл: Assets/Scripts/Editor/MapGen/RegionConfigAssetEditor.cs
    // ─────────────────────────────────────────────
    [CustomEditor(typeof(RegionConfigAsset))]
    public class RegionConfigAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            var asset = (RegionConfigAsset)target;

            if (GUILayout.Button("Разместить в сцене", GUILayout.Height(30)))
            {
                if (!string.IsNullOrEmpty(asset.rawJson))
                {
                    // Write temp JSON file and place from it
                    var tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "temp_region.json");
                    System.IO.File.WriteAllText(tempPath, asset.rawJson);
                    RegionPlacer.PlaceRegion(tempPath);
                    Debug.Log($"[MapGen] Регион «{asset.displayName}» размещён из ScriptableObject");
                }
            }

            if (GUILayout.Button("Импорт JSON из файла..."))
            {
                string path = EditorUtility.OpenFilePanel("JSON-конфиг", "Assets/Content/Configs", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    Undo.RecordObject(asset, "Import JSON");
                    asset.rawJson = json;
                    var data = JsonUtility.FromJson<RegionConfigData>(json);
                    if (data != null)
                    {
                        asset.regionId = data.regionId;
                        asset.displayName = data.regionName;
                        asset.seed = data.seed;
                    }
                    EditorUtility.SetDirty(asset);
                }
            }
        }
    }
}
