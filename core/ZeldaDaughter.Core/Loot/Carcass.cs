#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Inventory;

namespace ZeldaDaughter.Core.Loot
{
    /// <summary>What an enemy leaves (enemies.json «loot»): the minimum for bare hands, the full set for a knife (project-design.md §6).</summary>
    public sealed class LootTable
    {
        public Dictionary<string, int> Minimal { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Full { get; set; } = new Dictionary<string, int>();
    }

    public enum CarcassState { Fresh, Looted }

    public enum LootOutcome { Minimal, Butchered, Nothing, NoRoom, Gone }

    public readonly struct LootLine
    {
        public readonly string Item;
        public readonly int Count;
        public LootLine(string item, int count) { Item = item; Count = count; }
        public override string ToString() => $"{Item}×{Count}";
    }

    public readonly struct LootResult
    {
        public readonly LootOutcome Outcome;
        public readonly IReadOnlyList<LootLine> Items;

        public LootResult(LootOutcome outcome, IReadOnlyList<LootLine>? items = null) { Outcome = outcome; Items = items ?? Array.Empty<LootLine>(); }
        public override string ToString() => $"{Outcome} [{string.Join(", ", Items)}]";
    }

    /// <summary>A dead enemy lying in the world.</summary>
    public sealed class Carcass
    {
        public Carcass(string id, string defId, Vec2 position, CarcassState state = CarcassState.Fresh)
        {
            Id = id; DefId = defId; Position = position; State = state;
        }

        /// <summary>The id of the enemy it came from (the same id sits in <c>GameState.Killed</c>).</summary>
        public string Id { get; }
        public string DefId { get; }
        public Vec2 Position { get; }
        public CarcassState State { get; internal set; }
    }

    /// <summary>
    /// Carcasses in the world (§6 «Лут с врагов»). A tap without the knife in the bag gives the minimum once; a tap with the knife gives the full set
    /// (minus what the minimum already gave) and the carcass is gone. All-or-nothing on the bag: with no room nothing is lost and the carcass waits.
    /// </summary>
    public sealed class Carcasses
    {
        readonly EnemySettings _s;
        readonly Bag _bag;
        readonly List<Carcass> _list = new List<Carcass>();

        public Carcasses(EnemySettings settings, Bag bag)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        }

        /// <summary>A carcass was butchered and left the world: the view removes it.</summary>
        public event Action<string>? Disappeared;

        public IReadOnlyList<Carcass> Active => _list;
        public Carcass? Get(string id) => _list.Find(c => c.Id == id);

        /// <summary>The hero has the butcher's tool — a tap will butcher, not just pick at it (the view may show another hint).</summary>
        public bool CanButcher => _bag.Count(_s.ButcherTool) > 0;

        public Carcass? Spawn(string enemyId, string defId, Vec2 position)
        {
            if (!_s.Enemies.ContainsKey(defId) || Get(enemyId) != null) return null;
            var c = new Carcass(enemyId, defId, position);
            _list.Add(c);
            return c;
        }

        public LootResult Tap(string id)
        {
            var c = Get(id);
            if (c == null) return new LootResult(LootOutcome.Gone);
            var table = _s.Enemies[c.DefId].Loot;
            if (CanButcher)
            {
                var items = new List<LootLine>();
                foreach (var kv in table.Full)
                {
                    int already = c.State == CarcassState.Looted && table.Minimal.TryGetValue(kv.Key, out var m) ? m : 0;
                    if (kv.Value - already > 0) items.Add(new LootLine(kv.Key, kv.Value - already));
                }
                if (!AddAll(items)) return new LootResult(LootOutcome.NoRoom);
                _list.Remove(c);
                Disappeared?.Invoke(id);
                return new LootResult(LootOutcome.Butchered, items);
            }
            if (c.State == CarcassState.Looted) return new LootResult(LootOutcome.Nothing);
            var min = table.Minimal.Select(kv => new LootLine(kv.Key, kv.Value)).ToList();
            if (!AddAll(min)) return new LootResult(LootOutcome.NoRoom);
            c.State = CarcassState.Looted;
            return new LootResult(LootOutcome.Minimal, min);
        }

        bool AddAll(IReadOnlyList<LootLine> items)
        {
            var snapshot = _bag.Stacks.Select(s => new Stack(s.ItemId, s.Count)).ToList();
            foreach (var l in items)
                if (!_bag.Add(l.Item, l.Count)) { _bag.Restore(snapshot); return false; }
            return true;
        }

        public void Restore(IEnumerable<Carcass>? carcasses)
        {
            _list.Clear();
            if (carcasses == null) return;
            foreach (var c in carcasses) if (_s.Enemies.ContainsKey(c.DefId) && Get(c.Id) == null) _list.Add(c);
        }
    }
}
