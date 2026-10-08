#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Remarks;

namespace ZeldaDaughter.Core.World
{
    /// <summary>data/camp.json (D-06).</summary>
    public sealed class CampSettings
    {
        public float BurnSeconds { get; set; }
        public float MaxBurnSeconds { get; set; }
        /// <summary>The light flickers and dies in the last seconds.</summary>
        public float FadeSeconds { get; set; }
        public float RestRadius { get; set; }
        public float LightRadius { get; set; }
        public float RainBurnFactor { get; set; } = 1f;
        public float PlaceMinSpacing { get; set; }
        /// <summary>A lit torch in the bag burns this long (real seconds) and then becomes <see cref="BurntItem"/> (D-23).</summary>
        public float TorchBurnSeconds { get; set; }
        /// <summary>Its light weakens over the last seconds.</summary>
        public float TorchFadeSeconds { get; set; }
        /// <summary>In the rain a torch burns down this many times faster.</summary>
        public float TorchRainBurnFactor { get; set; } = 1f;
        /// <summary>What is left of a burnt-down torch (an item id), or empty for nothing.</summary>
        public string BurntItem { get; set; } = "";
        /// <summary>Item id → world object kind the placed item becomes (the target of world recipes); other items are just «item».</summary>
        public Dictionary<string, string> PlacedKinds { get; set; } = new Dictionary<string, string>();
        /// <summary>Item id → seconds it adds to a burning campfire.</summary>
        public Dictionary<string, float> Fuel { get; set; } = new Dictionary<string, float>();
    }

    /// <summary>A thing the hero put on the ground (§7 «Размещение предметов в мире»).</summary>
    public sealed class PlacedObject
    {
        public PlacedObject(string id, string kind, string item, Vec2 position) { Id = id; Kind = kind; Item = item; Position = position; }
        public string Id { get; }
        /// <summary>firewood_placed | planks_placed | item.</summary>
        public string Kind { get; }
        public string Item { get; }
        public Vec2 Position { get; }
    }

    /// <summary>A burning campfire: a rest zone, a light, a fire to light torches from. When the time runs out it is gone.</summary>
    public sealed class Campfire
    {
        readonly float _fade;

        public Campfire(string id, Vec2 position, float burnLeft, float fadeSeconds) { Id = id; Position = position; BurnLeft = burnLeft; _fade = fadeSeconds; }
        public string Id { get; }
        public Vec2 Position { get; }
        public float BurnLeft { get; internal set; }
        public bool IsLit => BurnLeft > 0f;
        /// <summary>0..1 for the view's light: full until the last seconds, then fading.</summary>
        public float Light => BurnLeft <= 0f ? 0f : _fade <= 0f ? 1f : Math.Min(1f, BurnLeft / _fade);
    }

    public enum PlaceOutcome { Placed, NoItem, NotPlaceable, InvalidSurface, TooClose }

    public readonly struct PlaceResult
    {
        public readonly PlaceOutcome Outcome;
        public readonly PlacedObject? Object;
        /// <summary>The hero's remark topic (remarks.json), or null.</summary>
        public readonly string? Topic;

        public PlaceResult(PlaceOutcome outcome, PlacedObject? obj = null, string? topic = null) { Outcome = outcome; Object = obj; Topic = topic; }
    }

    public enum UseOutcome { Done, Refueled, NoTarget, MissingItem, NoRecipe, NeedFire, NoRoom }

    public readonly struct UseResult
    {
        public readonly UseOutcome Outcome;
        /// <summary>Item that came into the bag (cooked meat, a lit torch), or null.</summary>
        public readonly string? Item;
        /// <summary>World object that appeared (campfire), or null.</summary>
        public readonly string? WorldResult;
        public readonly string? Topic;

        public UseResult(UseOutcome outcome, string? item = null, string? worldResult = null, string? topic = null)
        {
            Outcome = outcome; Item = item; WorldResult = worldResult; Topic = topic;
        }
        public override string ToString() => $"{Outcome} {Item} {WorldResult}";
    }

    /// <summary>
    /// The camp (project-design.md §6–7): things placed on the ground, campfires made from them and the rules around a fire. Placing takes the
    /// item from the bag; bringing an item to a placed thing or a fire goes through the world recipes (recipes.json); the surface is the view's call.
    /// </summary>
    public sealed class Camp
    {
        readonly CampSettings _s;
        readonly DataSet _d;
        readonly Bag _bag;
        readonly Crafting.Crafting _crafting;
        readonly List<PlacedObject> _objects = new List<PlacedObject>();
        readonly List<Campfire> _fires = new List<Campfire>();
        int _counter;

        public Camp(DataSet data, Bag bag, Crafting.Crafting crafting)
        {
            _d = data ?? throw new ArgumentNullException(nameof(data));
            _s = data.Camp;
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
            _crafting = crafting ?? throw new ArgumentNullException(nameof(crafting));
        }

        public IReadOnlyList<PlacedObject> Objects => _objects;
        public IReadOnlyList<Campfire> Campfires => _fires;
        static readonly WorldEvent[] NoEvents = new WorldEvent[0];
        internal int Counter => _counter;

        /// <param name="validSurface">The view's verdict on the spot (a wall is not ground): false — the hero remarks and the item stays.</param>
        public PlaceResult Place(string itemId, Vec2 position, bool validSurface = true)
        {
            if (!_d.Items.TryGetValue(itemId, out var def) || _bag.Count(itemId) < 1) return new PlaceResult(PlaceOutcome.NoItem);
            if (!def.Placeable) return new PlaceResult(PlaceOutcome.NotPlaceable, topic: Topics.PlaceInvalid);
            if (!validSurface) return new PlaceResult(PlaceOutcome.InvalidSurface, topic: Topics.PlaceInvalid);
            if (_objects.Any(o => (o.Position - position).Length < _s.PlaceMinSpacing) || _fires.Any(f => (f.Position - position).Length < _s.PlaceMinSpacing))
                return new PlaceResult(PlaceOutcome.TooClose, topic: Topics.PlaceInvalid);
            _bag.Remove(itemId);
            var o = new PlacedObject($"placed_{++_counter}", _s.PlacedKinds.TryGetValue(itemId, out var kind) ? kind : "item", itemId, position);
            _objects.Add(o);
            return new PlaceResult(PlaceOutcome.Placed, o);
        }

        /// <summary>Takes a placed thing back into the bag; false if there is no such thing or no room.</summary>
        public bool PickUp(string objectId)
        {
            var o = _objects.Find(x => x.Id == objectId);
            if (o == null || !_bag.Add(o.Item)) return false;
            _objects.Remove(o);
            return true;
        }

        /// <summary>Bring an item to a placed thing or to a burning campfire (drag the item onto the object).</summary>
        public UseResult Use(string objectId, string itemId)
        {
            var placed = _objects.Find(x => x.Id == objectId);
            var fire = _fires.Find(x => x.Id == objectId && x.IsLit);
            if (placed == null && fire == null) return new UseResult(UseOutcome.NoTarget);
            if (_bag.Count(itemId) < 1) return new UseResult(UseOutcome.MissingItem);

            if (fire != null && _s.Fuel.TryGetValue(itemId, out float seconds))
            {
                _bag.Remove(itemId);
                fire.BurnLeft = Math.Min(_s.MaxBurnSeconds, fire.BurnLeft + seconds);
                return new UseResult(UseOutcome.Refueled);
            }
            var kinds = placed != null ? new[] { placed.Kind } : new[] { "campfire", "fire" };
            var r = UseOnKinds(kinds, itemId);
            if (r.Outcome == UseOutcome.Done && r.WorldResult == "campfire" && placed != null)
            {
                _objects.Remove(placed);
                _fires.Add(new Campfire(placed.Id, placed.Position, _s.BurnSeconds, _s.FadeSeconds));
            }
            return r;
        }

        /// <summary>The same through the world recipes for a target the camp does not own (a burning grass cell is the kind «fire»).</summary>
        public UseResult UseOnKinds(IReadOnlyList<string> kinds, string itemId)
        {
            if (_bag.Count(itemId) < 1) return new UseResult(UseOutcome.MissingItem);
            foreach (var kind in kinds)
            {
                var c = _crafting.InWorld(kind, itemId, _bag);
                if (c.Outcome == Crafting.CraftOutcome.Done) return new UseResult(UseOutcome.Done, c.Item, c.WorldResult);
                if (c.Outcome == Crafting.CraftOutcome.NoRoom) return new UseResult(UseOutcome.NoRoom, topic: Topics.CraftNoRoom);
            }
            // an item that needs a fire (torch) brought to something that is not one: the hero hints
            if (_crafting.HasWorldUse("fire", itemId) && !kinds.Contains("fire")) return new UseResult(UseOutcome.NeedFire, topic: Topics.NeedFire);
            return new UseResult(UseOutcome.NoRecipe, topic: Topics.CraftFail);
        }

        public bool IsLitNear(Vec2 position, float radius)
        {
            for (int i = 0; i < _fires.Count; i++)
                if (_fires[i].IsLit && (_fires[i].Position - position).Length <= radius) return true;
            return false;
        }

        /// <summary>Campfires burn down (faster in the rain); the ones that went out are removed and reported.</summary>
        public IReadOnlyList<WorldEvent> Tick(float dt, bool raining)
        {
            List<WorldEvent>? events = null;
            if (dt <= 0f) return NoEvents;
            float rate = raining ? _s.RainBurnFactor : 1f;
            for (int i = _fires.Count - 1; i >= 0; i--)
            {
                var f = _fires[i];
                f.BurnLeft -= dt * rate;
                if (f.BurnLeft <= 0f)
                {
                    _fires.RemoveAt(i);
                    (events ??= new List<WorldEvent>()).Insert(0, new WorldEvent(WorldEventKind.CampfireBurntOut, f.Id, f.Position));
                }
            }
            return events ?? (IReadOnlyList<WorldEvent>)NoEvents;   // nothing burnt out: a shared empty list (C8)
        }

        public void Restore(int counter, IEnumerable<PlacedObject>? objects, IEnumerable<Campfire>? fires)
        {
            _counter = Math.Max(0, counter);
            _objects.Clear(); _fires.Clear();
            if (objects != null) foreach (var o in objects) if (_d.Items.ContainsKey(o.Item)) _objects.Add(o);
            if (fires != null) foreach (var f in fires) if (f.BurnLeft > 0f) _fires.Add(f);
        }
    }
}
