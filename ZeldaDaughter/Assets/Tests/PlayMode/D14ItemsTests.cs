using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-14 (docs/demo/unity-architecture.md §7): the radial menu, the bag, crafting by dragging, food, putting things in the world, the campfire. Scene: test-demo.</summary>
    public class D14ItemsTests
    {
        GameSession _s;
        HeroController _hero;
        RadialMenu _radial;
        InventoryWindow _bag;
        ItemDrag _drag;
        CampPresenter _camp;
        WindowStack _windows;
        float _gap;
        readonly List<string> _said = new List<string>();

        static double Now => Time.realtimeSinceStartupAsDouble;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            Assert.NotNull(_s, "test-demo has a GameSession");
            _hero = Object.FindFirstObjectByType<HeroController>();
            _hero.UseDpi(160f);
            _radial = Object.FindFirstObjectByType<RadialMenu>();
            _bag = Object.FindFirstObjectByType<InventoryWindow>();
            _drag = Object.FindFirstObjectByType<ItemDrag>();
            _camp = Object.FindFirstObjectByType<CampPresenter>();
            _windows = Object.FindFirstObjectByType<WindowStack>();
            Assert.NotNull(_radial, "SceneBuilder.AddItemsUi built the radial menu");
            Assert.NotNull(_camp);
            _gap = _s.State.Data.Remarks.GlobalGapSeconds;
            _s.State.Data.Remarks.GlobalGapSeconds = 0f; // the data set is shared: restored in TearDown
            _said.Clear();
            _s.Events.HeroSaid += (topic, line) => _said.Add(topic);
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_s != null) _s.State.Data.Remarks.GlobalGapSeconds = _gap;
            TestSaves.Clear();
        }

        // ------------------------------------------------------------------ helpers

        static Vec2 V(Vector2 p) => new Vec2(p.x, p.y);

        Vector2 ScreenOf(Vector3 world) => Camera.main.WorldToScreenPoint(world);
        Vector2 HeroScreen => ScreenOf(_hero.transform.position);

        /// <summary>A point beside the open bag window: moving the finger there hides the window.</summary>
        Vector2 BesideWindow()
        {
            var c = new Vector3[4];
            _bag.Root.GetWorldCorners(c);
            return new Vector2(c[0].x - 80f, c[0].y - 80f);
        }

        IEnumerator OpenBag()
        {
            _bag.Open();
            yield return null;
            Assert.IsTrue(_windows.IsOpen("bag"));
        }

        /// <summary>Drag cell <paramref name="from"/> out of the window and let go at the screen point.</summary>
        DragResult DragOut(int from, Vector2 to)
        {
            Assert.IsTrue(_drag.Begin(from, _bag.CellScreenPosition(from)), "the drag began");
            _drag.MoveTo(BesideWindow());
            Assert.IsTrue(_drag.IsOutsideWindow, "past the edge of the window");
            return _drag.Release(to);
        }

        // ------------------------------------------------------------------ the radial menu and the bag

        [UnityTest]
        public IEnumerator Long_press_on_the_hero_opens_the_fan_and_lifting_on_the_bag_opens_the_window()
        {
            var h = HeroScreen;
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Now, V(h), TouchHit.Hero));
            yield return new WaitForSeconds(0.75f); // the recogniser turns a still finger into a long press
            Assert.IsTrue(_radial.IsHolding, "the fan is up while the finger is down");
            CollectionAssert.AreEqual(new[] { "bag", "notebook" }, _radial.SectorIds.ToArray(), "no map is bought, so no map plate");

            var onBag = _radial.SectorScreenPosition("bag");
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Now, V(onBag), default));
            yield return null;
            Assert.AreEqual("bag", _radial.LitSector, "the plate under the finger lights up");

            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Now, V(onBag), default));
            yield return null;
            Assert.IsFalse(_radial.IsOpen, "the fan goes");
            Assert.IsTrue(_windows.IsOpen("bag"), "the bag opened");
            Assert.AreEqual(_s.State.Data.Inventory.Slots, _bag.CellCount, "a cell for every slot of inventory.json");
            Assert.IsTrue(_hero.Locked, "she does not walk while the window is open");
        }

        [UnityTest]
        public IEnumerator The_map_plate_is_there_only_once_a_map_is_bought()
        {
            _s.Events.RaiseLongPressHero();
            CollectionAssert.AreEqual(new[] { "bag", "notebook" }, _radial.SectorIds.ToArray());
            _radial.Close();
            _s.State.Bag.Add("map");
            _s.Events.RaiseLongPressHero();
            CollectionAssert.AreEqual(new[] { "bag", "map", "notebook" }, _radial.SectorIds.ToArray());
            string chosen = null;
            _s.Events.RadialChosen += id => chosen = id;
            _radial.Choose("map");
            Assert.AreEqual("map", chosen, "the map window (D-15) is told");
            Assert.IsFalse(_windows.AnyOpen, "the bag did not open");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lifting_in_the_middle_keeps_the_plates_as_buttons_and_a_tap_beside_closes()
        {
            var h = HeroScreen;
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Now, V(h), TouchHit.Hero));
            yield return new WaitForSeconds(0.75f);
            Assert.IsTrue(_radial.IsHolding);
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Now, V(h), default));
            yield return null;
            Assert.IsTrue(_radial.IsOpen && !_radial.IsHolding, "the plates stay");
            var plate = _s.UI.Windows.Find("RadialMenu/Sector_bag").GetComponent<Button>();
            _s.UI.Windows.Find("RadialMenu/Backdrop").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(_radial.IsOpen, "a tap beside the plates closes the menu");
            Assert.IsFalse(_windows.AnyOpen);
            yield return null;
            Assert.IsNotNull(plate);
        }

        [UnityTest]
        public IEnumerator A_tap_beside_the_bag_closes_it_and_frees_the_hero()
        {
            yield return OpenBag();
            Assert.IsTrue(_hero.Locked);
            _s.UI.Windows.Find("Backdrop").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(_windows.AnyOpen);
            Assert.IsFalse(_hero.Locked);
        }

        [UnityTest]
        public IEnumerator The_bag_shows_stacks_with_their_count()
        {
            var g = _s.State;
            g.Bag.Add("stick", 3);
            g.Bag.Add("cloth");
            _s.BagChanged("test");
            yield return OpenBag();
            Assert.AreEqual("stick", _bag.ItemIn(0));
            Assert.AreEqual("cloth", _bag.ItemIn(1));
            Assert.IsNull(_bag.ItemIn(2));
            var count = _bag.Root.Find("Cell_0/Count").GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsTrue(count.gameObject.activeSelf);
            Assert.AreEqual("3", count.text);
            Assert.IsFalse(_bag.Root.Find("Cell_1/Count").gameObject.activeSelf, "a single thing has no digit");
        }

        // ------------------------------------------------------------------ crafting

        [UnityTest]
        public IEnumerator A_stick_dragged_onto_cloth_makes_a_torch()
        {
            var g = _s.State;
            g.Bag.Add("stick");
            g.Bag.Add("cloth");
            _s.BagChanged("test");
            yield return OpenBag();

            Assert.IsTrue(_drag.Begin(0, _bag.CellScreenPosition(0)));
            Assert.IsFalse(_drag.IsOutsideWindow);
            Assert.AreEqual(DragResult.Crafted, _drag.Release(_bag.CellScreenPosition(1)));
            Assert.AreEqual(1, g.Bag.Count("torch_unlit"));
            Assert.AreEqual(0, g.Bag.Count("stick"));
            Assert.AreEqual(0, g.Bag.Count("cloth"));
            Assert.Contains("craft_ok", _said);
            Assert.IsTrue(_windows.IsOpen("bag"), "crafting happens inside the window: it stays");
            Assert.AreEqual("torch_unlit", _bag.ItemIn(0), "the cell shows the new thing");
        }

        [UnityTest]
        public IEnumerator Things_that_do_not_fit_say_so_and_stay()
        {
            var g = _s.State;
            g.Bag.Add("stick");
            g.Bag.Add("berries");
            g.Bag.Add("metal");
            _s.BagChanged("test");
            yield return OpenBag();

            _drag.Begin(0, _bag.CellScreenPosition(0));
            Assert.AreEqual(DragResult.CraftFailed, _drag.Release(_bag.CellScreenPosition(1)));
            Assert.Contains("craft_fail", _said);
            Assert.AreEqual(1, g.Bag.Count("stick"));
            Assert.AreEqual(1, g.Bag.Count("berries"));

            _said.Clear();
            _drag.Begin(2, _bag.CellScreenPosition(2));
            _drag.Release(_bag.CellScreenPosition(0)); // metal + stick is a sword — but only on the anvil
            Assert.Contains("craft_station", _said);
            Assert.AreEqual(1, g.Bag.Count("metal"));
        }

        // ------------------------------------------------------------------ food

        [UnityTest]
        public IEnumerator Berries_dragged_onto_the_hero_are_eaten()
        {
            var g = _s.State;
            g.Hunger.Restore(0.6f);
            g.Bag.Add("berries", 2);
            _s.BagChanged("test");
            HeroAct act = default;
            _s.Events.HeroActed += a => act = a;
            yield return OpenBag();

            Assert.AreEqual(DragResult.Ate, DragOut(0, HeroScreen));
            Assert.Less(g.Hunger.Value, 0.6f, "less hungry");
            Assert.AreEqual(1, g.Bag.Count("berries"));
            Assert.AreEqual(HeroActKind.Eat, act.Kind);
            Assert.IsFalse(_windows.AnyOpen, "something was done: the window goes, the hands are free");
        }

        [UnityTest]
        public IEnumerator Something_she_cannot_use_on_herself_stays_and_the_bag_returns()
        {
            _s.State.Bag.Add("stone");
            _s.BagChanged("test");
            yield return OpenBag();
            Assert.AreEqual(DragResult.Rejected, DragOut(0, HeroScreen));
            Assert.Contains("use_nothing", _said);
            Assert.AreEqual(1, _s.State.Bag.Count("stone"));
            Assert.IsTrue(_windows.IsOpen("bag") && _bag.Root.gameObject.activeSelf, "nothing came of it: the bag comes back");
        }

        // ------------------------------------------------------------------ the world

        [UnityTest]
        public IEnumerator Firewood_dragged_to_the_ground_lies_there()
        {
            var g = _s.State;
            g.Bag.Add("firewood");
            _s.BagChanged("test");
            yield return OpenBag();

            var spot = new Vector3(-3f, 0f, -5f);
            Assert.AreEqual(DragResult.Placed, DragOut(0, ScreenOf(spot)));
            Assert.AreEqual(0, g.Bag.Count("firewood"));
            Assert.AreEqual(1, g.Camp.Objects.Count);
            var o = g.Camp.Objects[0];
            Assert.AreEqual("firewood_placed", o.Kind);
            Assert.AreEqual(spot.x, o.Position.X, 0.6f);
            Assert.AreEqual(spot.z, o.Position.Y, 0.6f);
            Assert.IsTrue(_camp.HasView(o.Id), "it lies in the scene");
            Assert.IsNotNull(_s.Index.FindTappable(o.Id), "…and can be tapped");
        }

        [UnityTest]
        public IEnumerator Dragged_onto_a_wall_it_stays_in_the_bag_with_a_remark()
        {
            var g = _s.State;
            _hero.Teleport(new Vector3(-19f, 0.1f, 0f), 0f);
            yield return null;
            yield return null;
            g.Bag.Add("firewood");
            _s.BagChanged("test");
            yield return OpenBag();

            Assert.AreEqual(DragResult.Rejected, DragOut(0, ScreenOf(new Vector3(-22f, 1f, 0f))));
            Assert.Contains("place_invalid", _said);
            Assert.AreEqual(1, g.Bag.Count("firewood"), "the item stays in the bag");
            Assert.AreEqual(0, g.Camp.Objects.Count);
            Assert.IsTrue(_windows.IsOpen("bag") && _bag.Root.gameObject.activeSelf, "the window comes back");
        }

        [UnityTest]
        public IEnumerator A_placed_thing_goes_back_into_the_bag_on_a_tap()
        {
            var g = _s.State;
            g.Bag.Add("firewood");
            _s.BagChanged("test");
            yield return OpenBag();
            DragOut(0, ScreenOf(new Vector3(-3f, 0f, -5f)));
            string id = g.Camp.Objects[0].Id;
            _s.Tap(id);
            yield return null;
            Assert.AreEqual(1, g.Bag.Count("firewood"));
            Assert.AreEqual(0, g.Camp.Objects.Count);
            Assert.IsFalse(_camp.HasView(id), "the view is gone with the object");
        }

        [UnityTest]
        public IEnumerator An_item_that_is_not_asked_for_offered_to_a_resident_asks_for_trade()
        {
            var g = _s.State;
            _hero.Teleport(new Vector3(5f, 0.1f, 2f), 0f);
            yield return null;
            yield return null;
            g.Bag.Add("stick");
            _s.BagChanged("test");
            string npc = null, item = null;
            _s.Events.TradeRequested += (n, i) => { npc = n; item = i; };
            yield return OpenBag();

            var peasant = _s.Index.FindTappable("npc_peasant");
            Assert.AreEqual(DragResult.TradeAsked, DragOut(0, ScreenOf(peasant.AimPoint)));
            Assert.AreEqual("peasant", npc);
            Assert.AreEqual("stick", item);
            Assert.AreEqual(1, g.Bag.Count("stick"), "an offer takes nothing");
        }

        [UnityTest]
        public IEnumerator Flint_on_firewood_lights_a_fire_and_an_unlit_torch_is_lit_from_it()
        {
            var g = _s.State;
            g.Bag.Add("firewood");
            g.Bag.Add("flint");
            _s.BagChanged("test");
            yield return OpenBag();
            DragOut(0, ScreenOf(new Vector3(-3f, 0f, -5f)));
            string id = g.Camp.Objects[0].Id;
            var placed = _s.Index.FindTappable(id);

            // holding the flint over the firewood, the hero hints that it is for it
            yield return OpenBag();
            _drag.Begin(0, _bag.CellScreenPosition(0));
            _drag.MoveTo(BesideWindow());
            _said.Clear();
            _drag.MoveTo(ScreenOf(placed.AimPoint));
            Assert.Contains("hint_world_use", _said);
            Assert.AreEqual(DragResult.Used, _drag.Release(ScreenOf(placed.AimPoint)));

            Assert.AreEqual(1, g.Camp.Campfires.Count, "a fire is burning");
            Assert.AreEqual(0, g.Camp.Objects.Count);
            Assert.AreEqual(1, g.Bag.Count("flint"), "the flint is a tool: it stays");
            yield return null;
            Assert.IsTrue(_camp.IsFire(id), "the view became a campfire");
            Assert.Greater(_camp.LightOf(id), 0f, "it shines");
            Assert.AreEqual(TapKind.Campfire, _s.Index.FindTappable(id).Kind);

            // an unlit torch to the fire becomes a torch
            g.Bag.Add("torch_unlit");
            _s.BagChanged("test");
            yield return OpenBag();
            int cell = Enumerable.Range(0, _bag.CellCount).First(i => _bag.ItemIn(i) == "torch_unlit");
            var fire = _s.Index.FindTappable(id);
            Assert.AreEqual(DragResult.Used, DragOut(cell, ScreenOf(fire.AimPoint)));
            Assert.AreEqual(1, g.Bag.Count("torch"));
            Assert.AreEqual(0, g.Bag.Count("torch_unlit"));
        }

        [UnityTest]
        public IEnumerator A_fire_that_burns_out_leaves_no_view()
        {
            var g = _s.State;
            g.Bag.Add("firewood");
            g.Bag.Add("flint");
            _s.BagChanged("test");
            var p = g.Camp.Place("firewood", new Vec2(-3f, -5f));
            _s.Events.RaisePlaced(p.Object);
            Assert.IsTrue(_camp.HasView(p.Object.Id));
            g.Camp.Use(p.Object.Id, "flint");
            _s.Events.RaiseUsedOnWorld(p.Object.Id, new Core.World.UseResult(Core.World.UseOutcome.Done, null, "campfire"));
            Assert.IsTrue(_camp.IsFire(p.Object.Id));
            foreach (var e in g.Camp.Tick(1000f, false)) _s.Events.RaiseWorld(e); // it burns down at once; the world event tells the view
            yield return null;
            Assert.AreEqual(0, g.Camp.Campfires.Count);
            Assert.IsFalse(_camp.HasView(p.Object.Id));
        }
    }

    /// <summary>D-14 frames for the author (docs/demo/frames/D-14-*.png): the radial menu, the bag, a campfire at night — in the real region scene, at the phone's 1080×2340.
    /// Written next to the repository (on the PC); where there is no such folder nothing is written.</summary>
    public class D14FrameTests
    {
        static string FramesDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/demo/frames"));
        static double Now => Time.realtimeSinceStartupAsDouble;

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            int w = tex.width, h = tex.height;
            if (Directory.Exists(FramesDir)) File.WriteAllBytes(Path.Combine(FramesDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log($"[ZD:Frame] {name} {w}x{h}");
        }

        [UnityTest]
        public IEnumerator Frames_menu_bag_and_a_campfire_at_night()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1080, 2340, "ZD phone");
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return null;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(1f);
            var s = Object.FindFirstObjectByType<GameSession>();
            var hero = Object.FindFirstObjectByType<HeroController>();
            var radial = Object.FindFirstObjectByType<RadialMenu>();
            var bag = Object.FindFirstObjectByType<InventoryWindow>();
            hero.UseDpi(160f);
            var g = s.State;
            g.Bag.Add("stick", 3); g.Bag.Add("cloth", 2); g.Bag.Add("berries", 4); g.Bag.Add("firewood", 2); g.Bag.Add("flint");
            g.Bag.Add("knife"); g.Bag.Add("meat", 2); g.Bag.Add("coin", 7); g.Bag.Add("healing_herbs");
            s.BagChanged("frame");

            // 1. the fan, the finger on the bag plate
            var h = Camera.main.WorldToScreenPoint(hero.transform.position);
            hero.Feed(new TouchSample(0, TouchPhase.Began, Now, new Vec2(h.x, h.y), TouchHit.Hero));
            yield return new WaitForSeconds(0.8f);
            var onBag = radial.SectorScreenPosition("bag");
            hero.Feed(new TouchSample(0, TouchPhase.Moved, Now, new Vec2(onBag.x, onBag.y), default));
            yield return null;
            yield return Shot("D-14-menu");
            hero.Feed(new TouchSample(0, TouchPhase.Ended, Now, new Vec2(onBag.x, onBag.y), default));
            yield return new WaitForSeconds(0.3f);

            // 2. the bag
            Assert.IsTrue(Object.FindFirstObjectByType<WindowStack>().IsOpen("bag"));
            yield return Shot("D-14-bag");
            Object.FindFirstObjectByType<WindowStack>().Close();
            yield return null;

            // 3. a fire by the hero, at night
            var at = hero.transform.position + new Vector3(-1.6f, 0f, -0.4f);
            var placed = g.Camp.Place("firewood", new Vec2(at.x, at.z));
            s.Events.RaisePlaced(placed.Object);
            var used = g.Camp.Use(placed.Object.Id, "flint");
            s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            s.BagChanged("frame");
            for (int i = 0; i < 24 && g.Clock.Daylight > 0.1; i++) s.JumpTime(() => g.SkipHours(1), 1);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[ZD:Frame] night daylight={g.Clock.Daylight:0.00} fires={g.Camp.Campfires.Count}");
            yield return Shot("D-14-campfire-night");

        }
    }
}
