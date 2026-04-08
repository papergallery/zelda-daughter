using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor.MapGen
{
    // ─────────────────────────────────────────────
    //  ScriptableObject-обёртка для RegionConfigData.
    //  Альтернатива JSON: можно редактировать конфиг
    //  прямо в Inspector, а также хранить ссылки
    //  на префабы напрямую (без строковых путей).
    //
    //  Создание: Assets → Create → ZeldaDaughter → Region Config
    //  Путь хранения: Assets/ScriptableObjects/Regions/
    //
    //  Файл: Assets/Scripts/Editor/MapGen/RegionConfigAsset.cs
    // ─────────────────────────────────────────────
    [CreateAssetMenu(
        fileName = "NewRegionConfig",
        menuName = "ZeldaDaughter/Region Config",
        order = 100)]
    public class RegionConfigAsset : ScriptableObject
    {
        [Header("Регион")]
        public string regionId;
        public string displayName;
        public int seed = 42;

        [Header("Границы")]
        public Vector2 boundsMin = new(-50, -50);
        public Vector2 boundsMax = new(50, 50);

        [Header("Данные (JSON)")]
        [Tooltip("Текстовый JSON-конфиг. Заполняется автоматически при импорте или вручную.")]
        [TextArea(5, 20)]
        public string rawJson;

        /// <summary>
        /// Парсит rawJson в RegionConfigData.
        /// </summary>
        public RegionConfigData Parse()
        {
            if (string.IsNullOrEmpty(rawJson))
            {
                Debug.LogError($"[RegionConfigAsset] rawJson пуст в {name}");
                return null;
            }
            return JsonUtility.FromJson<RegionConfigData>(rawJson);
        }

        /// <summary>
        /// Заполняет rawJson из переданных данных.
        /// </summary>
        public void Serialize(RegionConfigData data)
        {
            rawJson = JsonUtility.ToJson(data, true);
            regionId = data.regionId;
            displayName = data.regionName;
            seed = data.seed;
        }
    }
}
