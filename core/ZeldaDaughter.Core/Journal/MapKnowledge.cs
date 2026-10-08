#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Journal
{
    /// <summary>data/map.json (D-04): marks that talking can open on the map (project-design.md §4).</summary>
    public sealed class MapSettings
    {
        public Dictionary<string, MarkDef> Marks { get; set; } = new Dictionary<string, MarkDef>();
    }

    public sealed class MarkDef
    {
        public string Name { get; set; } = "";
        /// <summary>Id of the scene object the mark sits on (scenes/region.json).</summary>
        public string Object { get; set; } = "";
    }

    /// <summary>
    /// What the hero has learned about places (§4): marks open from conversations, even before she owns a map — they pile up and show once she
    /// buys one (the map is the item «map» in the bag). Nothing opens by itself: no quest markers, no towers.
    /// </summary>
    public sealed class MapKnowledge
    {
        public const string MapItem = "map";

        readonly MapSettings _s;
        readonly Func<bool> _hasMap;
        readonly List<string> _known = new List<string>();

        public MapKnowledge(MapSettings settings, Func<bool> hasMap)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _hasMap = hasMap ?? throw new ArgumentNullException(nameof(hasMap));
        }

        /// <summary>A mark became known (first time only) — for a remark and the map screen.</summary>
        public event Action<string>? MarkOpened;

        public bool HasMap => _hasMap();
        /// <summary>Everything learned, in the order it was learned.</summary>
        public IReadOnlyList<string> Known => _known;
        /// <summary>What the map screen draws: nothing without a map.</summary>
        public IReadOnlyList<string> VisibleMarks => HasMap ? _known : (IReadOnlyList<string>)Array.Empty<string>();
        public bool IsOpen(string markId) => _known.Contains(markId);
        public MarkDef Def(string markId) => _s.Marks[markId];

        /// <summary>False when the mark was known already or does not exist.</summary>
        public bool Open(string markId)
        {
            if (!_s.Marks.ContainsKey(markId) || _known.Contains(markId)) return false;
            _known.Add(markId);
            MarkOpened?.Invoke(markId);
            return true;
        }

        public void Restore(IEnumerable<string>? known)
        {
            _known.Clear();
            if (known == null) return;
            foreach (var k in known) if (_s.Marks.ContainsKey(k) && !_known.Contains(k)) _known.Add(k);
        }
    }
}
