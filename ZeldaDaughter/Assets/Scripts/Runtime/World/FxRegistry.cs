using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// id → effect prefab (Fx.asset from fx.json): <c>campfire</c>, <c>torch_flame</c>, <c>grass_fire</c>, <c>burnt_patch</c>, <c>rain</c>, <c>placed_&lt;item&gt;</c>.
    /// No prefab: <see cref="Get"/> is null and the presenter draws a primitive (one <c>[ZD:Art]</c> warning).
    /// </summary>
    public sealed class FxRegistry : ScriptableObject
    {
        [SerializeField] private string[] _ids = new string[0];
        [SerializeField] private GameObject[] _prefabs = new GameObject[0];
        private Dictionary<string, GameObject> _map;
        private readonly HashSet<string> _warned = new HashSet<string>();

        public IReadOnlyList<string> Ids => _ids;

        public void Configure(string[] ids, GameObject[] prefabs)
        {
            _ids = ids;
            _prefabs = prefabs;
            _map = null;
        }

        public GameObject Get(string id)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                for (int i = 0; i < _ids.Length; i++) if (_prefabs[i] != null) _map[_ids[i]] = _prefabs[i];
            }
            if (id != null && _map.TryGetValue(id, out var p)) return p;
            if (_warned.Add(id ?? "")) ZdLog.Warn("Art", $"missing fx {id}");
            return null;
        }

        private void OnEnable() => _warned.Clear();
    }
}
