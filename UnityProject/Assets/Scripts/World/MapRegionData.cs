using UnityEngine;

namespace ZeldaDaughter.World
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/World/Map Region", fileName = "NewMapRegion")]
    public class MapRegionData : ScriptableObject
    {
        [SerializeField] private string _regionId;
        [SerializeField] private string _regionName;
        [SerializeField] private Sprite _outlineSprite;
        [SerializeField] private Rect _worldBounds;
        [SerializeField] private MapMarkerData[] _markers;
        [SerializeField] private string _requiredMapItemId;

        public string RegionId => _regionId;
        public string RegionName => _regionName;
        public Sprite OutlineSprite => _outlineSprite;
        public Rect WorldBounds => _worldBounds;
        public MapMarkerData[] Markers => _markers;
        public string RequiredMapItemId => _requiredMapItemId;

        /// <summary>Ищет маркер по id. Возвращает null если не найден.</summary>
        public MapMarkerData? FindMarker(string markerId)
        {
            if (_markers == null) return null;
            foreach (var marker in _markers)
            {
                if (marker.markerId == markerId)
                    return marker;
            }
            return null;
        }

        public bool ContainsPoint(Vector3 worldPos)
        {
            return _worldBounds.Contains(new Vector2(worldPos.x, worldPos.z));
        }
    }
}
