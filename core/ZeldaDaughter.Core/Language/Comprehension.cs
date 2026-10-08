#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace ZeldaDaughter.Core.Language
{
    /// <summary>data/language.json (C-11).</summary>
    public sealed class LanguageSettings
    {
        public float PerNewLine { get; set; }
        public float PerNewNpc { get; set; }
        public float Decay { get; set; }
        public float IconsBelow { get; set; }
        public float Stage2At { get; set; }
        public float Stage3At { get; set; }
        public float MoneyAt { get; set; }
        /// <summary>Chance the NPC gets the hero's icon at zero understanding; grows to 1 at full.</summary>
        public float HeroUnderstoodAtZero { get; set; }
        public string Glyphs { get; set; } = "";
    }

    /// <summary>
    /// How much of the common tongue the hero understands (project-design.md §3). Grows by talking — new lines and new
    /// people, not repeats. Rendering is deterministic: the same word always looks the same, whoever says it.
    /// </summary>
    public sealed class Comprehension
    {
        readonly LanguageSettings _s;
        readonly HashSet<string> _npcs = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _lines = new HashSet<string>(StringComparer.Ordinal);

        public Comprehension(LanguageSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(settings.Glyphs)) throw new ArgumentException("language.json: glyphs", nameof(settings));
        }

        public float Understanding { get; private set; }
        public int Stage => Understanding >= _s.Stage3At ? 3 : Understanding >= _s.Stage2At ? 2 : 1;
        public bool ShowIcons => Understanding < _s.IconsBelow;
        public bool UnderstandsMoney => Understanding >= _s.MoneyAt;

        /// <summary>The hero heard a line from an NPC. Returns true if this changed understanding.</summary>
        public bool Heard(string npcId, string lineId)
        {
            float raw = 0;
            if (_npcs.Add(npcId)) raw += _s.PerNewNpc;
            if (_lines.Add(npcId + "|" + lineId)) raw += _s.PerNewLine;
            if (raw <= 0) return false;
            Understanding = Math.Min(1f, Understanding + raw * (float)Math.Pow(1.0 - Understanding, _s.Decay));
            return true;
        }

        /// <summary>Did the NPC get the hero's icon? <paramref name="roll"/> is a uniform 0..1 from the caller's seeded random.</summary>
        public bool HeroUnderstood(double roll) => roll < _s.HeroUnderstoodAtZero + (1 - _s.HeroUnderstoodAtZero) * Understanding;

        /// <summary>What the hero sees in the NPC's speech bubble.</summary>
        public string Render(string text, string npcId)
        {
            if (Understanding >= _s.Stage3At) return text;
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (!char.IsLetter(text[i])) { sb.Append(text[i++]); continue; }
                int start = i;
                while (i < text.Length && char.IsLetter(text[i])) i++;
                string word = text.Substring(start, i - start);
                sb.Append(Revealed(word, npcId) ? word : Glyphs(word, npcId));
            }
            return sb.ToString();
        }

        /// <summary>Stage 2: short words first (April), the rest in a fixed per-word order.</summary>
        bool Revealed(string word, string npcId)
        {
            if (Understanding < _s.Stage2At) return false;
            float progress = (Understanding - _s.Stage2At) / (_s.Stage3At - _s.Stage2At);
            float lengthShare = Math.Min(word.Length - 1, 9) / 10f;            // 1 letter → 0, 10+ → 0.9
            float jitter = Hash(word.ToLowerInvariant()) % 1000 / 10000f;        // 0..0.1, stable and the same for every speaker (D-26)
            return progress > lengthShare + jitter;
        }

        string Glyphs(string word, string npcId)
        {
            var sb = new StringBuilder(word.Length);
            uint h = Hash(word.ToLowerInvariant());
            for (int k = 0; k < word.Length; k++)
            {
                h = unchecked(h * 1103515245u + 12345u);
                sb.Append(_s.Glyphs[(int)(h >> 16) % _s.Glyphs.Length]);
            }
            return sb.ToString();
        }

        /// <summary>FNV-1a of the word alone — stable across runs and platforms (string.GetHashCode is not), and the same for every NPC: one word, one runes (D-26).</summary>
        static uint Hash(string word)
        {
            uint h = 2166136261;
            unchecked { foreach (char c in word) { h ^= c; h *= 16777619; } }
            return h;
        }

        /// <summary>For save/load (C-13) and tests.</summary>
        public void Restore(float understanding, IEnumerable<string>? npcs = null, IEnumerable<string>? lines = null)
        {
            Understanding = Math.Max(0f, Math.Min(1f, understanding));
            if (npcs != null) { _npcs.Clear(); foreach (var n in npcs) _npcs.Add(n); }
            if (lines != null) { _lines.Clear(); foreach (var l in lines) _lines.Add(l); }
        }

        public IReadOnlyCollection<string> MetNpcs => _npcs;
        public IReadOnlyCollection<string> HeardLines => _lines;
    }
}
