using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>What the scene config said about an object (id, tags, item to pick up) — set by SceneBuilder.</summary>
    public sealed class SceneTags : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private string[] _tags = new string[0];
        [SerializeField] private string _item;

        public string Id => _id;
        public string Item => string.IsNullOrEmpty(_item) ? null : _item;
        public bool Has(string tag) => System.Array.IndexOf(_tags, tag) >= 0;

        public void Configure(string id, string[] tags, string item)
        {
            _id = id;
            _tags = tags ?? new string[0];
            _item = item;
        }
    }
}
