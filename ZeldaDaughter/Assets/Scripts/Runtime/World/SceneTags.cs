using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>What the scene config said about an object (id, tags, item to pick up) — set by SceneBuilder.</summary>
    public sealed class SceneTags : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private string[] _tags = new string[0];
        [SerializeField] private string _item;
        [SerializeField] private string _enemy;
        [SerializeField] private string _station;

        public string Id => _id;
        public string Item => string.IsNullOrEmpty(_item) ? null : _item;
        /// <summary>The enemy (data/enemies.json) that appears at this point, or null (config field <c>enemy</c>).</summary>
        public string Enemy => string.IsNullOrEmpty(_enemy) ? null : _enemy;
        /// <summary>The crafting station (data/recipes.json) this object is, or null (config field <c>station</c>).</summary>
        public string Station => string.IsNullOrEmpty(_station) ? null : _station;
        public bool HasTag(string tag) => Has(tag);
        public bool Has(string tag) => System.Array.IndexOf(_tags, tag) >= 0;

        /// <summary>The first tag that is none of <paramref name="skip"/> (a station's kind next to the tag "station"); empty if there is none.</summary>
        public string FirstTagExcept(params string[] skip)
        {
            foreach (var t in _tags) if (System.Array.IndexOf(skip, t) < 0) return t;
            return "";
        }

        /// <summary>The rest of the first tag starting with <paramref name="prefix"/> (<c>enemy_boar</c> → <c>boar</c>), ignoring the tag <paramref name="except"/>; empty if none.</summary>
        public string TagWithPrefix(string prefix, string except)
        {
            foreach (var t in _tags) if (t != except && t.StartsWith(prefix, System.StringComparison.Ordinal)) return t.Substring(prefix.Length);
            return "";
        }

        public void Configure(string id, string[] tags, string item, string enemy = null, string station = null)
        {
            _id = id;
            _tags = tags ?? new string[0];
            _item = item;
            _enemy = enemy;
            _station = station;
        }
    }
}
