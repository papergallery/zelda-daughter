using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// id → <see cref="CharacterSpriteSet"/> (Assets/Art/Registries/Characters.asset, built from characters.json by RegistryBuilder). Keys:
    /// <c>heroine</c> and the ids of data/npcs.json and data/enemies.json. An id with no set, or a set with no pictures, is drawn as a silhouette.
    /// </summary>
    public sealed class CharacterRegistry : ScriptableObject
    {
        [SerializeField] private string[] _ids = new string[0];
        [SerializeField] private CharacterSpriteSet[] _sets = new CharacterSpriteSet[0];
        private Dictionary<string, CharacterSpriteSet> _map;
        private static readonly HashSet<string> Warned = new HashSet<string>();

        public int Count => _ids.Length;
        public IReadOnlyList<string> Ids => _ids;

        public void Configure(string[] ids, CharacterSpriteSet[] sets)
        {
            _ids = ids;
            _sets = sets;
            _map = null;
        }

        public bool TryGet(string id, out CharacterSpriteSet set)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, CharacterSpriteSet>(StringComparer.Ordinal);
                for (int i = 0; i < _ids.Length; i++) _map[_ids[i]] = _sets[i];
            }
            return _map.TryGetValue(id ?? "", out set) && set != null;
        }

        /// <summary>The set for the id; never null (an unknown id gets a grey silhouette and one <c>[ZD:Art]</c> warning).</summary>
        public CharacterSpriteSet Get(string id)
        {
            if (TryGet(id, out var set)) return set;
            if (Warned.Add("char:" + id)) ZdLog.Warn("Art", $"missing character {id}");
            return PlaceholderSprites.SetFor(id, Color.gray);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Warned.Clear();
    }
}
