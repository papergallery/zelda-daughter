#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Inventory;

namespace ZeldaDaughter.Core.Crafting
{
    public enum CraftOutcome { Done, NoRecipe, MissingIngredients, NeedsStation, NoRoom }

    public readonly struct CraftResult
    {
        public readonly CraftOutcome Outcome;
        /// <summary>Item put into the bag (or null).</summary>
        public readonly string? Item;
        public readonly int Count;
        /// <summary>World object that appears (campfire) — the view places it.</summary>
        public readonly string? WorldResult;
        /// <summary>First time this recipe worked — for the notebook (§5) and a remark.</summary>
        public readonly bool Discovered;

        public CraftResult(CraftOutcome outcome, string? item = null, int count = 0, string? worldResult = null, bool discovered = false)
        {
            Outcome = outcome; Item = item; Count = count; WorldResult = worldResult; Discovered = discovered;
        }

        public override string ToString() => $"{Outcome} {Item}×{Count} {WorldResult}";
    }

    /// <summary>
    /// Drag-and-drop crafting of project-design.md §7: item onto item in the field, stations for weapons, items brought to
    /// world objects. Every operation is all-or-nothing on the bag; tools listed in "keep" stay.
    /// </summary>
    public sealed class Crafting
    {
        readonly DataSet _d;
        readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);

        public Crafting(DataSet data) { _d = data ?? throw new ArgumentNullException(nameof(data)); }

        public IReadOnlyCollection<string> Known => _known;

        public CraftResult Combine(string a, string b, Bag bag)
        {
            var r = _d.FieldRecipes.FirstOrDefault(x => (x.A == a && x.B == b) || (x.A == b && x.B == a));
            if (r == null)
                return _d.StationRecipes.Any(s => SameSet(s.In, new[] { a, b })) ? new CraftResult(CraftOutcome.NeedsStation) : new CraftResult(CraftOutcome.NoRecipe);
            var consume = new List<string>();
            if (!r.Keep.Contains(r.A)) consume.Add(r.A);
            if (!r.Keep.Contains(r.B)) consume.Add(r.B);
            var need = new List<string> { r.A, r.B };
            return Apply(bag, need, consume, r.Out, r.Count, null, $"field:{r.A}+{r.B}");
        }

        public CraftResult AtStation(string station, IReadOnlyList<string> inputs, Bag bag)
        {
            var r = _d.StationRecipes.FirstOrDefault(x => x.Station == station && SameSet(x.In, inputs));
            if (r == null) return new CraftResult(CraftOutcome.NoRecipe);
            return Apply(bag, r.In, r.In, r.Out, r.Count, null, $"{station}:{string.Join("+", r.In)}");
        }

        /// <summary>Bring an item to a world object (placed firewood, fire).</summary>
        public CraftResult InWorld(string target, string withItem, Bag bag)
        {
            var r = _d.WorldRecipes.FirstOrDefault(x => x.Target == target && x.With == withItem);
            if (r == null) return new CraftResult(CraftOutcome.NoRecipe);
            var consume = r.Keep.Contains(r.With) ? new List<string>() : new List<string> { r.With };
            bool resultIsItem = _d.Items.ContainsKey(r.Result);
            return Apply(bag, new[] { r.With }, consume, resultIsItem ? r.Result : null, 1, resultIsItem ? null : r.Result, $"world:{target}+{withItem}");
        }

        /// <summary>§7: the hero hints when holding an item next to an object it could be used on.</summary>
        public bool HasWorldUse(string target, string item) => _d.WorldRecipes.Any(x => x.Target == target && x.With == item);

        CraftResult Apply(Bag bag, IEnumerable<string> need, IEnumerable<string> consume, string? outItem, int outCount, string? world, string key)
        {
            foreach (var g in need.GroupBy(x => x))
                if (bag.Count(g.Key) < g.Count()) return new CraftResult(CraftOutcome.MissingIngredients);
            var taken = new List<string>();
            foreach (var id in consume) { bag.Remove(id); taken.Add(id); }
            if (outItem != null && !bag.Add(outItem, outCount))
            {
                foreach (var id in taken) bag.Add(id);
                return new CraftResult(CraftOutcome.NoRoom);
            }
            bool discovered = _known.Add(key);
            return new CraftResult(CraftOutcome.Done, outItem, outItem != null ? outCount : 0, world, discovered);
        }

        static bool SameSet(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
            a.Count == b.Count && a.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(b.OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>For save/load (C-13).</summary>
        public void Restore(IEnumerable<string> known)
        {
            _known.Clear();
            foreach (var k in known) _known.Add(k);
        }
    }
}
