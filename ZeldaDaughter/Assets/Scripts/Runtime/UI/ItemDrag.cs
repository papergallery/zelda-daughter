using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Crafting;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.World;

namespace ZeldaDaughter.UI
{
    /// <summary>What a drag of an item came to (tests and logs read it).</summary>
    public enum DragResult
    {
        None, Cancelled,
        Crafted, CraftFailed,          // onto another cell
        Ate, Treated,                  // onto the hero
        Used,                          // onto a placed thing, a campfire, a burning cell
        Given, TradeAsked,             // onto a resident
        Placed, Rejected,              // onto the ground
    }

    /// <summary>A cell of the bag takes the finger: the drag itself runs in <see cref="ItemDrag"/>, which reads the pointer — the cell goes inactive when the window hides, and a disabled object never gets its end-of-drag.</summary>
    public sealed class CellHandler : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private ItemDrag _drag;
        [SerializeField] private int _index;

        public void Configure(ItemDrag drag, int index)
        {
            _drag = drag;
            _index = index;
        }

        public void OnPointerDown(PointerEventData eventData) => _drag.PointerDown(_index, eventData.position);
    }

    /// <summary>
    /// The one drag of an item (docs/demo/unity-architecture.md §3). It starts on a cell of the bag after the move threshold (data/input.json), with no long
    /// press first. Inside the window: onto another cell is crafting (<c>g.Crafting.Combine</c>). Past the edge of the window the window hides (WindowStack.Suspend)
    /// and the icon stays on the finger; released over the hero it is eaten or used (<c>g.UseOnHero</c>), over a resident it is given (<c>g.Quests.Give</c>) or offered
    /// for trade, over a placed thing, a campfire or a burning grass cell it is used on it (<c>g.UseOnWorld</c>), over the ground it is put there
    /// (<c>g.Camp.Place</c>; a wall, deep water, a blocked spot — «not here», the item stays in the bag). A drop that did nothing brings the window back; one that did
    /// something closes it. Over an object that has a use for the item the hero says <c>hint_world_use</c> once per drag.
    /// </summary>
    public sealed class ItemDrag : MonoBehaviour
    {
        private const float GhostSize = 150f;
        private const float GhostLift = 120f;       // the icon rides above the finger so the finger does not hide it
        private const float GrassReach = 1.0f;      // metres: a drop this close to a burning grass cell is a drop on it
        private const float BlockRadius = 0.3f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private HeroController _hero;
        [SerializeField] private WorldPicker _picker;
        [SerializeField] private InventoryWindow _inventory;
        [SerializeField] private TerrainZones _zones;
        [SerializeField] private IconRegistry _icons;

        private enum Phase { Idle, Pending, Dragging }

        private GameState _g;
        private Phase _phase;
        private bool _polled;             // a real finger drives it (Update); tests drive it by calls
        private int _slot;
        private string _item;
        private Vector2 _down;
        private float _downAt;
        private bool _inWorld;
        private bool _hinted;
        private RectTransform _ghost;
        private int _solidMask, _blockingMask, _groundLayer;

        public void Configure(GameSession session, SessionUI ui, WindowStack windows, HeroController hero, WorldPicker picker, InventoryWindow inventory, TerrainZones zones, IconRegistry icons)
        {
            _session = session;
            _ui = ui;
            _windows = windows;
            _hero = hero;
            _picker = picker;
            _inventory = inventory;
            _zones = zones;
            _icons = icons;
        }

        public bool IsDragging => _phase == Phase.Dragging;
        public bool IsOutsideWindow => _phase == Phase.Dragging && _inWorld;
        public string DraggedItem => _phase == Phase.Dragging ? _item : null;
        /// <summary>The last drag's outcome.</summary>
        public DragResult Last { get; private set; }

        private void OnEnable() => _session.Events.StateReady += OnReady;

        private void OnDisable()
        {
            if (_session != null) _session.Events.StateReady -= OnReady;
            _g = null;
        }

        private void OnReady(GameState g)
        {
            _g = g;
            _groundLayer = LayerMask.NameToLayer("Ground");
            _blockingMask = LayerMask.GetMask("Blocking");
            _solidMask = LayerMask.GetMask("Ground", "Blocking");
        }

        // ------------------------------------------------------------------ the finger

        /// <summary>A finger went down on cell <paramref name="slot"/> of the bag; it becomes a drag when it moves past the threshold.</summary>
        public void PointerDown(int slot, Vector2 screen)
        {
            if (_g == null || _phase != Phase.Idle || slot < 0 || slot >= _g.Bag.UsedSlots) return;
            _phase = Phase.Pending;
            _polled = true;
            _slot = slot;
            _item = _g.Bag.Stacks[slot].ItemId;
            _down = screen;
            _downAt = Time.unscaledTime;
            _inventory.HideInfo();
        }

        /// <summary>A long press on cell <paramref name="slot"/> (the finger stayed down and still, data/input.json longPressSeconds): the cell's description (D-23). Also how tests do it.</summary>
        public bool Describe(int slot)
        {
            if (_g == null || _phase == Phase.Dragging || slot < 0 || slot >= _g.Bag.UsedSlots) return false;
            return _inventory.ShowInfo(slot);
        }

        private void Update()
        {
            if (_phase == Phase.Idle || !_polled) return;
            if (!ReadPointer(out var pos, out bool pressed)) { Cancel(); return; }
            Look(pos, pressed);
        }

        /// <summary>One look at the finger given from outside (tests): the device is not read for this touch any more.</summary>
        public void Poll(Vector2 pos, bool pressed)
        {
            _polled = false;
            Look(pos, pressed);
        }

        private void Look(Vector2 pos, bool pressed)
        {
            if (_phase == Phase.Idle) return;
            if (_phase == Phase.Pending)
            {
                if (!pressed) { _phase = Phase.Idle; return; }
                float threshold = _g.Data.Input.MoveThresholdPx * _hero.DpiScale;
                if ((pos - _down).magnitude > threshold) Begin(_slot, pos);
                else if (Time.unscaledTime - _downAt >= _g.Data.Input.LongPressSeconds)
                {
                    // held still: not a drag but a question «what is this?» — the finger may go up or stay, nothing follows
                    _phase = Phase.Idle;
                    _polled = false;
                    Describe(_slot);
                }
                return;
            }
            MoveTo(pos);
            if (!pressed) Release(pos);
        }

        private static bool ReadPointer(out Vector2 pos, out bool pressed)
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                pos = ts.primaryTouch.position.ReadValue();
                pressed = true;
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pos = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                return true;
            }
            pos = default;
            pressed = false;
            return false;
        }

        // ------------------------------------------------------------------ the drag

        /// <summary>Starts dragging the item of cell <paramref name="slot"/> at once (the finger moved past the threshold; also how tests start it).</summary>
        public bool Begin(int slot, Vector2 screen)
        {
            if (_g == null || _phase == Phase.Dragging || slot < 0 || slot >= _g.Bag.UsedSlots) return false;
            _slot = slot;
            _item = _g.Bag.Stacks[slot].ItemId;
            _phase = Phase.Dragging;
            _inWorld = false;
            _hinted = false;
            Last = DragResult.None;
            _ghost = UiKit.MakeIcon(_ui.World, "DragGhost", _icons, _item, _ui.Look, GhostSize);
            _inventory.SetDimmed(slot, true);
            MoveTo(screen);
            ZdLog.Info("Items", $"drag {_item}");
            return true;
        }

        /// <summary>The finger moved: the icon follows; past the edge of the window the window hides.</summary>
        public void MoveTo(Vector2 screen)
        {
            if (_phase != Phase.Dragging) return;
            float scale = _ui.Canvas.scaleFactor;
            _ghost.position = screen + new Vector2(0f, GhostLift * scale);
            if (!_inWorld && !_inventory.ContainsScreenPoint(screen))
            {
                _inWorld = true;
                _windows.Suspend();
                ZdLog.Info("Items", $"{_item} leaves the window");
            }
            if (_inWorld && !_hinted && HasUseAt(screen))
            {
                _hinted = true;
                _session.Say(Topics.HintWorldUse);
            }
        }

        /// <summary>The finger lifted at <paramref name="screen"/>: whatever is under it takes the item.</summary>
        public DragResult Release(Vector2 screen)
        {
            if (_phase != Phase.Dragging) return DragResult.None;
            MoveTo(screen);
            var result = _inWorld ? DropInWorld(screen) : DropInWindow(screen);
            Finish(result);
            return result;
        }

        /// <summary>Ends the drag with nothing done (the window closed, the pointer is gone).</summary>
        public void Cancel()
        {
            if (_phase == Phase.Idle) return;
            if (_phase == Phase.Pending) { _phase = Phase.Idle; return; }
            Finish(DragResult.Cancelled);
        }

        private void Finish(DragResult result)
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            _ghost = null;
            _inventory.SetDimmed(_slot, false);
            bool wasWorld = _inWorld;
            _phase = Phase.Idle;
            _polled = false;
            _inWorld = false;
            Last = result;
            if (wasWorld && _windows.IsOpen(_inventory.Id))
            {
                // done something — the hands are free, the window goes; nothing came of it — the bag comes back
                if (result == DragResult.Cancelled || result == DragResult.Rejected) _windows.Resume();
                else _windows.Close(_inventory.Id);
            }
            _inventory.Refresh();
        }

        // ------------------------------------------------------------------ onto another cell: crafting

        private DragResult DropInWindow(Vector2 screen)
        {
            int target = _inventory.CellAt(screen);
            if (target < 0 || target >= _g.Bag.UsedSlots) return DragResult.Cancelled;
            string other = _g.Bag.Stacks[target].ItemId;
            bool same = target == _slot;
            if (same && _g.Bag.Count(_item) < 2) return DragResult.Cancelled;

            var r = _g.Crafting.Combine(_item, other, _g.Bag);
            switch (r.Outcome)
            {
                case CraftOutcome.Done:
                    _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Craft, default, r.Item));
                    _session.BagChanged("craft");
                    _session.Say(Topics.CraftOk);
                    ZdLog.Info("Items", $"craft {_item}+{other} -> {r.Item}x{r.Count}");
                    return DragResult.Crafted;
                case CraftOutcome.NeedsStation:
                    _session.Say(Topics.CraftStation);
                    ZdLog.Info("Items", $"craft {_item}+{other} needs a station");
                    return DragResult.CraftFailed;
                case CraftOutcome.NoRoom:
                    _session.Say(Topics.CraftNoRoom);
                    ZdLog.Info("Items", $"craft {_item}+{other} no room");
                    return DragResult.CraftFailed;
                default:
                    if (!same) _session.Say(Topics.CraftFail);
                    ZdLog.Info("Items", $"craft {_item}+{other} no recipe");
                    return same ? DragResult.Cancelled : DragResult.CraftFailed;
            }
        }

        // ------------------------------------------------------------------ out of the window: into the world

        private DragResult DropInWorld(Vector2 screen)
        {
            if (_g.Bag.Count(_item) < 1) return DragResult.Cancelled;
            var at = new Vec2(screen.x, screen.y);
            if (_hero.IsOnHero(at)) return OnHero();

            var target = _picker.NearestTarget(at);
            if (target != null)
            {
                switch (target.Kind)
                {
                    case TapKind.Npc: return OnNpc(target);
                    case TapKind.Placed:
                    case TapKind.Campfire: return OnObject(target.Id, target.transform.position);
                }
            }

            bool hit = GroundAt(screen, out var point, out bool valid);
            if (hit)
            {
                string cell = BurningCellNear(point);
                if (cell != null) return OnObject(cell, point);
            }
            return Place(hit, point, valid);
        }

        private DragResult OnHero()
        {
            var r = _g.UseOnHero(_item);
            switch (r.Outcome)
            {
                case HeroUseOutcome.Ate:
                    _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Eat, default, _item));
                    _session.BagChanged("eat");
                    ZdLog.Info("Items", $"eat {_item} heal={r.Heal:0.#}");
                    if (r.Sated) _session.Say(Topics.Sated);
                    return DragResult.Ate;
                case HeroUseOutcome.Treated:
                    _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Treat, default, _item));
                    _session.BagChanged("treat");
                    ZdLog.Info("Items", $"treat {_item}");
                    return DragResult.Treated;
                default:
                    if (r.Topic != null) _session.Say(r.Topic);
                    ZdLog.Info("Items", $"use on hero {_item}: nothing");
                    return DragResult.Rejected;
            }
        }

        private DragResult OnNpc(Tappable npc)
        {
            string key = npc.Id.StartsWith("npc_") ? npc.Id.Substring(4) : npc.Id;
            if (!_g.Quests.Wants(key, _item))
            {
                _session.Events.RaiseTradeRequested(key, _item);
                ZdLog.Info("Items", $"{_item} offered to {key}");
                return DragResult.TradeAsked;
            }
            var q = _g.Quests.Give(key, _item);
            switch (q.Outcome)
            {
                case QuestOutcome.Done:
                    _session.Events.RaiseQuestGiven(key, q);
                    _session.BagChanged("quest");
                    ZdLog.Info("Items", $"{_item} given to {key}: quest {q.QuestId} done");
                    return DragResult.Given;
                case QuestOutcome.NoRoom:
                    _session.Say(Topics.CraftNoRoom);
                    break;
            }
            ZdLog.Info("Items", $"{_item} to {key}: {q.Outcome}");
            return DragResult.Rejected;
        }

        private DragResult OnObject(string objectId, Vector3 where)
        {
            var r = _g.UseOnWorld(objectId, _item);
            if (r.Outcome == UseOutcome.Done || r.Outcome == UseOutcome.Refueled)
            {
                _session.Events.RaiseUsedOnWorld(objectId, r);
                _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Place, where, _item));
                _session.BagChanged("use_world");
                ZdLog.Info("Items", $"{_item} on {objectId}: {r}");
                return DragResult.Used;
            }
            if (r.Topic != null) _session.Say(r.Topic);
            ZdLog.Info("Items", $"{_item} on {objectId}: {r.Outcome}");
            return DragResult.Rejected;
        }

        private DragResult Place(bool hit, Vector3 point, bool valid)
        {
            var at = hit ? new Vec2(point.x, point.z) : _g.HeroPosition;
            var r = _g.Camp.Place(_item, at, hit && valid);
            if (r.Outcome == PlaceOutcome.Placed)
            {
                _session.Events.RaisePlaced(r.Object);
                _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Place, point, _item));
                _session.BagChanged("place");
                ZdLog.Info("Items", $"placed {_item} {r.Object.Id} at {at.X:0.0},{at.Y:0.0}");
                return DragResult.Placed;
            }
            if (r.Topic != null) _session.Say(r.Topic);
            ZdLog.Info("Items", $"place {_item}: {r.Outcome}");
            return DragResult.Rejected;
        }

        // ------------------------------------------------------------------ what is under the finger

        /// <summary>The ground point under the screen point and whether a thing may be put there: ground (not a wall), not deep water, nothing solid within a hand.</summary>
        private bool GroundAt(Vector2 screen, out Vector3 point, out bool valid)
        {
            point = default;
            valid = false;
            var ray = _ui.Camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 500f, _solidMask, QueryTriggerInteraction.Ignore)) return false;
            point = hit.point;
            bool onGround = hit.collider.gameObject.layer == _groundLayer;
            bool water = _zones != null && _zones.At(point) == "water";
            valid = onGround && !water && !Physics.CheckSphere(point + Vector3.up * 0.45f, BlockRadius, _blockingMask, QueryTriggerInteraction.Ignore);
            return true;
        }

        private string BurningCellNear(Vector3 point)
        {
            var cells = _session.Index.GrassCells;
            var grass = _g.Nature.Grass;
            string best = null;
            float bestD = GrassReach;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!grass.Has(cells[i].Id) || grass.StateOf(cells[i].Id) != GrassState.Burning) continue;
                var p = cells[i].Position;
                float d = new Vector2(p.x - point.x, p.z - point.z).magnitude;
                if (d <= bestD) { bestD = d; best = cells[i].Id; }
            }
            return best;
        }

        /// <summary>Is there, under the finger, a placed thing or a fire the dragged item does something to (§7: the hero hints).</summary>
        private bool HasUseAt(Vector2 screen)
        {
            var t = _picker.NearestTarget(new Vec2(screen.x, screen.y));
            if (t == null) return false;
            if (t.Kind == TapKind.Campfire)
                return _g.Crafting.HasWorldUse("campfire", _item) || _g.Crafting.HasWorldUse("fire", _item) || _g.Data.Camp.Fuel.ContainsKey(_item);
            if (t.Kind != TapKind.Placed) return false;
            var objects = _g.Camp.Objects;
            for (int i = 0; i < objects.Count; i++)
                if (objects[i].Id == t.Id) return _g.Crafting.HasWorldUse(objects[i].Kind, _item);
            return false;
        }
    }
}
