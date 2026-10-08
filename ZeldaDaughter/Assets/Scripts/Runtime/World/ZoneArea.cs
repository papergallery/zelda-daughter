using UnityEngine;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.World
{
    /// <summary>A named region of the scene (predator spawn, wet grass…): geometry plus tags, built from the config's zones.</summary>
    public sealed class ZoneArea : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private string[] _tags = new string[0];
        [SerializeField] private string _areaJson;
        private Area _area;

        public string Id => _id;
        public bool Has(string tag) => System.Array.IndexOf(_tags, tag) >= 0;
        public bool Contains(Vector3 position) => (_area ?? (_area = Area.FromJson(_areaJson))).Contains(position.x, position.z);

        public void Configure(string id, string[] tags, string areaJson)
        {
            _id = id;
            _tags = tags ?? new string[0];
            _areaJson = areaJson;
            _area = null;
        }
    }
}
