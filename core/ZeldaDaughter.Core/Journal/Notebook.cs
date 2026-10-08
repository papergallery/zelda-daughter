#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Journal
{
    /// <summary>data/notebook.json (D-04): what the hero writes down, in her own words (§5).</summary>
    public sealed class NotebookSettings
    {
        public Dictionary<string, NotebookEntryDef> Entries { get; set; } = new Dictionary<string, NotebookEntryDef>();
    }

    public sealed class NotebookEntryDef
    {
        /// <summary>NPC id the note is about, or empty.</summary>
        public string Who { get; set; } = "";
        /// <summary>Her words: what was said or asked.</summary>
        public string Text { get; set; } = "";
        /// <summary>Roughly where (§5 «где примерно») — no marker, just words.</summary>
        public string Where { get; set; } = "";
    }

    /// <summary>One note. Deliberately no status: it is a diary, not a task list (§5).</summary>
    public readonly struct NotebookEntry
    {
        public readonly string Id;
        public readonly string Who;
        public readonly string Text;
        public readonly string Where;

        public NotebookEntry(string id, NotebookEntryDef d) { Id = id; Who = d.Who; Text = d.Text; Where = d.Where; }
    }

    /// <summary>Notes in the order they were written; added by conversations and requests, never removed, never reminding (§5).</summary>
    public sealed class Notebook
    {
        readonly NotebookSettings _s;
        readonly List<NotebookEntry> _entries = new List<NotebookEntry>();

        public Notebook(NotebookSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public event Action<NotebookEntry>? EntryAdded;
        public IReadOnlyList<NotebookEntry> Entries => _entries;

        public bool Has(string entryId) => _entries.Exists(e => e.Id == entryId);

        /// <summary>False when the note already exists or the id is unknown.</summary>
        public bool Add(string entryId)
        {
            if (Has(entryId) || !_s.Entries.TryGetValue(entryId, out var d)) return false;
            var e = new NotebookEntry(entryId, d);
            _entries.Add(e);
            EntryAdded?.Invoke(e);
            return true;
        }

        public void Restore(IEnumerable<string>? ids)
        {
            _entries.Clear();
            if (ids == null) return;
            foreach (var id in ids)
                if (!Has(id) && _s.Entries.TryGetValue(id, out var d)) _entries.Add(new NotebookEntry(id, d));
        }
    }
}
