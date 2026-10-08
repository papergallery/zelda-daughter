#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Inventory;

namespace ZeldaDaughter.Core.Journal
{
    /// <summary>data/quests.json (D-04): small requests from residents (§2 «мелкие квесты», §5).</summary>
    public sealed class QuestSettings
    {
        public Dictionary<string, QuestDef> Quests { get; set; } = new Dictionary<string, QuestDef>();
    }

    public sealed class QuestDef
    {
        /// <summary>Who asks (dialogue effect «offer»).</summary>
        public string Giver { get; set; } = "";
        /// <summary>Whom the things are handed to.</summary>
        public string Receiver { get; set; } = "";
        /// <summary>Notebook entry written when the request is accepted.</summary>
        public string Note { get; set; } = "";
        /// <summary>false — the thing may be handed over before being asked for (a lost locket found by chance).</summary>
        public bool RequiresOffer { get; set; } = true;
        /// <summary>Things the giver puts in the hero's hands when asking (a letter to carry).</summary>
        public Dictionary<string, int> HandOut { get; set; } = new Dictionary<string, int>();
        /// <summary>What must be handed to the receiver.</summary>
        public Dictionary<string, int> Need { get; set; } = new Dictionary<string, int>();
        public RewardDef Reward { get; set; } = new RewardDef();
    }

    public sealed class RewardDef
    {
        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
        /// <summary>Map marks the receiver tells about.</summary>
        public List<string> Marks { get; set; } = new List<string>();
    }

    public enum QuestOutcome { Done, NoQuest, NotEnough, NoRoom }

    public readonly struct QuestResult
    {
        public readonly QuestOutcome Outcome;
        public readonly string? QuestId;

        public QuestResult(QuestOutcome outcome, string? questId = null) { Outcome = outcome; QuestId = questId; }
        public override string ToString() => $"{Outcome} {QuestId}";
    }

    public enum OfferOutcome { Accepted, AlreadyAsked, AlreadyDone, NoRoom, Unknown }

    /// <summary>
    /// Requests of residents. State per request: not asked → asked → done. Handing over is checked in the core (bag), a reward is all-or-nothing.
    /// The notebook never shows state — this class is for the rules and for the view to know what to do on a drag onto an NPC.
    /// </summary>
    public sealed class Quests
    {
        readonly QuestSettings _s;
        readonly Bag _bag;
        readonly Notebook _notebook;
        readonly MapKnowledge _map;
        readonly List<string> _offered = new List<string>();
        readonly List<string> _done = new List<string>();

        public Quests(QuestSettings settings, Bag bag, Notebook notebook, MapKnowledge map)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
            _notebook = notebook ?? throw new ArgumentNullException(nameof(notebook));
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public event Action<string>? Done;

        public bool IsOffered(string questId) => _offered.Contains(questId) || _done.Contains(questId);
        public bool IsDone(string questId) => _done.Contains(questId);
        public IReadOnlyList<string> OfferedIds => _offered;
        public IReadOnlyList<string> DoneIds => _done;

        /// <summary>The giver asks (dialogue effect). Writes the note and hands out what she gives; with no room for it nothing happens and she can ask again.</summary>
        public OfferOutcome Offer(string questId)
        {
            if (!_s.Quests.TryGetValue(questId, out var q)) return OfferOutcome.Unknown;
            if (_done.Contains(questId)) return OfferOutcome.AlreadyDone;
            if (_offered.Contains(questId)) return OfferOutcome.AlreadyAsked;
            if (!AddAll(q.HandOut)) return OfferOutcome.NoRoom;
            _offered.Add(questId);
            _notebook.Add(q.Note);
            return OfferOutcome.Accepted;
        }

        /// <summary>Is any open request waiting for this item from this NPC (the view may highlight a drop target; no hint is shown otherwise).</summary>
        public bool Wants(string npcId, string itemId) => Find(npcId, itemId) != null;

        /// <summary>The hero hands an item to an NPC (drag onto her). The whole set the request needs must be in the bag.</summary>
        public QuestResult Give(string npcId, string itemId)
        {
            var found = Find(npcId, itemId);
            if (found == null) return new QuestResult(QuestOutcome.NoQuest);
            string id = found.Value.Key;
            var q = found.Value.Value;
            foreach (var n in q.Need) if (_bag.Count(n.Key) < n.Value) return new QuestResult(QuestOutcome.NotEnough, id);

            var snapshot = _bag.Stacks.Select(s => new Stack(s.ItemId, s.Count)).ToList();
            foreach (var n in q.Need) _bag.Remove(n.Key, n.Value);
            if (!AddAll(q.Reward.Items))
            {
                _bag.Restore(snapshot);
                return new QuestResult(QuestOutcome.NoRoom, id);
            }
            _offered.Remove(id);
            _done.Add(id);
            foreach (var m in q.Reward.Marks) _map.Open(m);
            Done?.Invoke(id);
            return new QuestResult(QuestOutcome.Done, id);
        }

        KeyValuePair<string, QuestDef>? Find(string npcId, string itemId)
        {
            foreach (var kv in _s.Quests)
            {
                var q = kv.Value;
                if (q.Receiver != npcId || _done.Contains(kv.Key) || !q.Need.ContainsKey(itemId)) continue;
                if (q.RequiresOffer && !_offered.Contains(kv.Key)) continue;
                return kv;
            }
            return null;
        }

        bool AddAll(Dictionary<string, int> items)
        {
            if (items.Count == 0) return true;
            var snapshot = _bag.Stacks.Select(s => new Stack(s.ItemId, s.Count)).ToList();
            foreach (var kv in items)
                if (!_bag.Add(kv.Key, kv.Value)) { _bag.Restore(snapshot); return false; }
            return true;
        }

        public void Restore(IEnumerable<string>? offered, IEnumerable<string>? done)
        {
            _offered.Clear(); _done.Clear();
            if (done != null) foreach (var d in done) if (_s.Quests.ContainsKey(d) && !_done.Contains(d)) _done.Add(d);
            if (offered != null) foreach (var o in offered) if (_s.Quests.ContainsKey(o) && !_done.Contains(o) && !_offered.Contains(o)) _offered.Add(o);
        }
    }
}
