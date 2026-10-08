using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Audio
{
    /// <summary>One sound: a few clips to pick from, loudness, how far the pitch may wander, and whether it comes from a place in the world.</summary>
    [Serializable]
    public sealed class SoundDef
    {
        public string Id;
        public AudioClip[] Clips = new AudioClip[0];
        [Range(0f, 1f)] public float Volume = 1f;
        [Range(0f, 0.5f)] public float PitchJitter;
        public bool Spatial;
    }

    /// <summary>
    /// id → <see cref="SoundDef"/> (Sounds.asset from sounds.json): <c>step_&lt;terrain&gt;</c>, <c>amb_*</c>, <c>hit_*</c>, <c>fire_*</c>, <c>bard_tavern</c>.
    /// An id with no clips is silent and says so once as <c>[ZD:Art] missing sound &lt;id&gt;</c>.
    /// </summary>
    public sealed class SoundRegistry : ScriptableObject
    {
        [SerializeField] private SoundDef[] _sounds = new SoundDef[0];
        private Dictionary<string, SoundDef> _map;
        private readonly HashSet<string> _warned = new HashSet<string>();

        public IReadOnlyList<SoundDef> Sounds => _sounds;

        public void Configure(SoundDef[] sounds)
        {
            _sounds = sounds;
            _map = null;
        }

        /// <summary>The sound with at least one clip, or null (one warning per id).</summary>
        public SoundDef Get(string id)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, SoundDef>(StringComparer.Ordinal);
                foreach (var s in _sounds) _map[s.Id] = s;
            }
            if (id != null && _map.TryGetValue(id, out var def) && def.Clips != null && def.Clips.Length > 0) return def;
            if (_warned.Add(id ?? "")) ZdLog.Warn("Art", $"missing sound {id}");
            return null;
        }

        private void OnEnable() => _warned.Clear();
    }
}
