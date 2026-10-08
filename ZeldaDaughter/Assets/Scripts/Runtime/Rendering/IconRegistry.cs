using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// id → icon (ItemIcons.asset from item-icons.json, TalkIcons.asset from talk-icons.json). An id with no picture has no sprite: the UI draws a
    /// paper plate with the first letter (<c>UiKit.Icon</c>) and this registry says so once as <c>[ZD:Art] missing icon &lt;id&gt;</c>.
    /// </summary>
    public sealed class IconRegistry : ScriptableObject
    {
        [SerializeField] private string[] _ids = new string[0];
        [SerializeField] private Sprite[] _sprites = new Sprite[0];
        private Dictionary<string, Sprite> _map;
        private readonly HashSet<string> _warned = new HashSet<string>();

        public IReadOnlyList<string> Ids => _ids;

        public void Configure(string[] ids, Sprite[] sprites)
        {
            _ids = ids;
            _sprites = sprites;
            _map = null;
        }

        /// <summary>The sprite, or null (and a one-time warning) if the id has no picture yet.</summary>
        public Sprite Get(string id)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                for (int i = 0; i < _ids.Length; i++) if (_sprites[i] != null) _map[_ids[i]] = _sprites[i];
            }
            if (id != null && _map.TryGetValue(id, out var s)) return s;
            if (_warned.Add(id ?? "")) ZdLog.Warn("Art", $"missing icon {id}");
            return null;
        }

        private void OnEnable() => _warned.Clear();
    }
}
