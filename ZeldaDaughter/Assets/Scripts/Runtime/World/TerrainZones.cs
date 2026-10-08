using UnityEngine;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// D-10: which terrain id (ground, road, water, mud…) lies under a ground point — the scene's roads, rivers and zone
    /// overrides, serialised by SceneBuilder from the config. The hero asks it every frame; the speed rule itself is the
    /// core's <c>SpeedModel</c> (water ×0.4).
    /// </summary>
    public sealed class TerrainZones : MonoBehaviour
    {
        [SerializeField] private string _json;
        private TerrainMap _map;

        public void Configure(string json)
        {
            _json = json;
            _map = null;
        }

        public string At(Vector3 position)
        {
            if (_map == null) _map = string.IsNullOrEmpty(_json) ? new TerrainMap() : TerrainMap.FromJson(_json);
            return _map.At(position.x, position.z);
        }
    }
}
