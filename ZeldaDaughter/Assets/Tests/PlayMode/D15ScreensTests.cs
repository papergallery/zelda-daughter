using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-15 (docs/demo/unity-architecture.md §7): the stations, sleep, trade, map and notebook, in the test scene scenes/test-demo.json (the frames: region).</summary>
    public class D15ScreensTests
    {
        GameSession _s;
        HeroController _hero;
        WindowStack _windows;
        StationWindow _station;
        TradeWindow _trade;
        MapWindow _map;
        NotebookWindow _notebook;
        RestPresenter _rest;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _windows = Object.FindFirstObjectByType<WindowStack>();
            _station = Object.FindFirstObjectByType<StationWindow>();
            _trade = Object.FindFirstObjectByType<TradeWindow>();
            _map = Object.FindFirstObjectByType<MapWindow>();
            _notebook = Object.FindFirstObjectByType<NotebookWindow>();
            _rest = Object.FindFirstObjectByType<RestPresenter>();
            Assert.NotNull(_s, "test-demo has a GameSession");
            Assert.NotNull(_station, "AddScreens put the station window on the Game object");
            Assert.NotNull(_trade);
            Assert.NotNull(_map);
            Assert.NotNull(_notebook);
            Assert.NotNull(_rest);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown() => TestSaves.Clear();

        IEnumerator Stand(float x, float z)
        {
            _hero.Teleport(new Vector3(x, 1f, z), 0f);
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ stations

        [UnityTest]
        public IEnumerator Anvil_forges_a_sword_from_metal_and_a_stick_after_the_strikes()
        {
            var g = _s.State;
            g.Bag.Add("metal");
            g.Bag.Add("stick");
            yield return Stand(-9f, -1.5f);

            _s.Tap("anvil_test");
            Assert.IsTrue(_windows.IsOpen("station"), "a tap on the anvil opens the station window");
            Assert.AreEqual("anvil", _station.Kind);
            Assert.IsTrue(_hero.Locked, "the hero stands while the window is open");

            Assert.IsTrue(_station.Put("metal"));
            Assert.IsTrue(_station.Put("stick"));
            Assert.IsFalse(_station.Put("metal"), "there is only one piece of metal in the bag");
            Assert.IsFalse(_station.Strike());
            Assert.IsFalse(_station.Strike());
            Assert.AreEqual(0, g.Bag.Count("sword"), "not before the last stroke");
            Assert.IsTrue(_station.Strike());

            Assert.AreEqual(1, g.Bag.Count("sword"));
            Assert.AreEqual(0, g.Bag.Count("metal"));
            Assert.AreEqual(0, g.Bag.Count("stick"));
            Assert.AreEqual("sword", _station.LastItem);
            Assert.AreEqual(0, _station.Inputs.Count);
            _windows.CloseAll();
            Assert.IsFalse(_hero.Locked);
        }

        [UnityTest]
        public IEnumerator Anvil_with_a_short_stick_makes_a_knife()
        {
            var g = _s.State;
            g.Bag.Add("metal");
            g.Bag.Add("short_stick");
            yield return Stand(-9f, -1.5f);
            _s.Tap("anvil_test");
            _station.Put("metal");
            _station.Put("short_stick");
            for (int i = 0; i < _station.StrikesNeeded; i++) _station.Strike();
            Assert.AreEqual(1, g.Bag.Count("knife"));
            _windows.CloseAll();
        }

        [UnityTest]
        public IEnumerator Smelter_melts_ore_but_forges_no_weapon()
        {
            var g = _s.State;
            g.Bag.Add("ore");
            yield return Stand(-12f, -1.5f);
            _s.Tap("smelter_test");
            Assert.AreEqual("smelter", _station.Kind);
            _station.Put("ore");
            for (int i = 0; i < _station.StrikesNeeded; i++) _station.Strike();
            Assert.AreEqual(1, g.Bag.Count("metal"), "ore → metal");
            Assert.AreEqual(0, g.Bag.Count("ore"));

            g.Bag.Add("stick");
            _station.Put("metal");
            _station.Put("stick");
            for (int i = 0; i < _station.StrikesNeeded; i++) _station.Strike();
            Assert.AreEqual(0, g.Bag.Count("sword"), "weapons only on the anvil");
            Assert.AreEqual(1, g.Bag.Count("metal"), "nothing was consumed");
            Assert.AreEqual(1, g.Bag.Count("stick"));
            _windows.CloseAll();
        }

        [UnityTest]
        public IEnumerator A_station_out_of_reach_does_not_open()
        {
            yield return Stand(0f, 0f); // the anvil is ~9.5 m away
            _s.Tap("anvil_test");
            Assert.IsFalse(_windows.AnyOpen);
        }

        // ------------------------------------------------------------------ sleep

        [UnityTest]
        public IEnumerator Bed_sleeps_through_the_hours_heals_wounds_to_the_threshold_and_hunger_grows()
        {
            var g = _s.State;
            yield return Stand(-9f, 6f);
            g.Condition.Wound(WoundType.Cut, 0.9f);
            g.Hunger.Restore(0.1f);
            double hoursBefore = g.Clock.TotalHours;
            float hungerBefore = g.Hunger.Value;
            int timeJumps = 0;
            double jumped = 0;
            _s.Events.TimeJumped += h => { timeJumps++; jumped = h; };
            _rest.SetTimes(0.3f, 0.1f, 0.3f);

            g.NightSeen = true;   // D-23: the bed lets her sleep only after the first night has come
            _s.Tap("bed_test");
            Assert.IsTrue(_rest.Sleeping, "a tap on the bed puts her to sleep");
            Assert.IsTrue(_hero.Locked);
            float darkest = 0f;
            float until = Time.time + 10f;
            while (_rest.Sleeping && Time.time < until)
            {
                darkest = Mathf.Max(darkest, _rest.LidsClosed);
                yield return null;
            }
            Assert.IsFalse(_rest.Sleeping, "she wakes");
            Assert.GreaterOrEqual(darkest, 0.99f, "the eyes closed fully");
            Assert.IsFalse(_hero.Locked);
            Assert.AreEqual(0f, _rest.LidsClosed, 0.001f, "…and opened");
            Assert.AreEqual(1, timeJumps);
            Assert.AreEqual(g.Data.World.SleepHours, jumped, 0.001);
            Assert.AreEqual(hoursBefore + g.Data.World.SleepHours, g.Clock.TotalHours, 0.6, "the clock jumped by the sleep hours (plus the seconds of the fade)");
            Assert.LessOrEqual(g.Condition.Severity(WoundType.Cut), g.Data.Wounds.SleepWoundSeverity + 0.001f, "wounds healed down to the threshold");
            Assert.Greater(g.Hunger.Value, hungerBefore, "she wakes hungrier");
        }

        [UnityTest]
        public IEnumerator A_bed_out_of_reach_does_not_put_her_to_sleep()
        {
            yield return Stand(0f, 0f);
            _s.Tap("bed_test");
            Assert.IsFalse(_rest.Sleeping);
        }

        // ------------------------------------------------------------------ trade

        void SetHour(double hour) => _s.State.Clock.SetTime(1, hour / 24.0);

        [UnityTest]
        public IEnumerator Barter_with_the_herbalist_the_reaction_comes_before_the_deal()
        {
            var g = _s.State;
            SetHour(12);
            g.Bag.Add("berries", 5);
            TradeResult? seen = null;
            _s.Events.Traded += r => seen = r;

            _s.Events.RaiseTradeRequested("herbalist", "berries");
            Assert.IsTrue(_trade.IsOpen);
            Assert.AreEqual("herbalist", _trade.Trader);
            Assert.AreEqual(1, _trade.Give.Single(l => l.Item == "berries").Count, "the item dropped on her lies on the table");
            Assert.IsFalse(_trade.AddTake("coin"), "the hero does not know coins yet: no coins on the table");

            Assert.IsTrue(_trade.AddTake("bandage"));
            Assert.AreEqual(TradeOutcome.NotEnough, _trade.Evaluate().Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(_trade.ReactionText), "she reacts to the offer before any deal");
            Assert.IsFalse(_trade.DealEnabled);
            Assert.AreEqual(5, g.Bag.Count("berries"), "Evaluate changes nothing");

            for (int i = 0; i < 4; i++) Assert.IsTrue(_trade.AddGive("berries"));
            Assert.IsFalse(_trade.AddGive("berries"), "no more berries than in the bag");
            Assert.AreEqual(TradeOutcome.Done, _trade.Evaluate().Outcome);
            Assert.IsTrue(_trade.DealEnabled);

            var r = _trade.Deal();
            Assert.AreEqual(TradeOutcome.Done, r.Outcome);
            Assert.IsTrue(seen.HasValue && seen.Value.Outcome == TradeOutcome.Done, "Traded was raised");
            Assert.AreEqual(0, g.Bag.Count("berries"));
            Assert.AreEqual(1, g.Bag.Count("bandage"));
            Assert.AreEqual(0, _trade.Give.Count + _trade.Take.Count, "the table is clear");
            Assert.AreEqual(1, g.Trade.BarterDeals);
            _windows.CloseAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Coins_come_after_the_lesson_and_what_she_sold_can_be_bought_back()
        {
            var g = _s.State;
            SetHour(12);
            g.Bag.Add("berries", 5);
            _s.Events.RaiseTradeRequested("herbalist", null);
            for (int i = 0; i < 5; i++) _trade.AddGive("berries");
            _trade.AddTake("bandage");
            Assert.AreEqual(TradeOutcome.Done, _trade.Deal().Outcome);
            Assert.IsFalse(g.Trade.KnowsCoins);
            Assert.IsTrue(g.Trade.TeachCoins(), "after a first barter someone may teach coins");
            _windows.CloseAll();

            g.Bag.Add("coin", 10);
            _s.Events.RaiseTradeRequested("herbalist", null);
            Assert.IsTrue(_trade.AddGive("coin"), "coins are on the table now");
            for (int i = 0; i < 7; i++) _trade.AddGive("coin");
            for (int i = 0; i < 5; i++) Assert.IsTrue(_trade.AddTake("berries", true), "the berries she bought are on the buyback shelf");
            Assert.IsFalse(_trade.AddTake("berries", true), "five were sold, five can come back");
            var r = _trade.Deal();
            Assert.AreEqual(TradeOutcome.Done, r.Outcome, "8 coins buy back what she paid 8 for");
            Assert.AreEqual(5, g.Bag.Count("berries"));
            Assert.AreEqual(2, g.Bag.Count("coin"));
            _windows.CloseAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_shop_is_closed_at_22_hours()
        {
            var g = _s.State;
            SetHour(22.5);
            g.Bag.Add("berries", 5);
            var offer = new TradeOffer(new[] { new TradeLine("berries", 5) }, new[] { new TradeLine("bandage", 1) });
            Assert.AreEqual(TradeOutcome.Closed, g.Trade.Evaluate("herbalist", offer).Outcome);
            _s.Events.RaiseTradeRequested("herbalist", "berries");
            Assert.IsFalse(_trade.IsOpen, "the window does not open at a closed shop");

            SetHour(12);
            _s.Events.RaiseTradeRequested("herbalist", null);
            Assert.IsTrue(_trade.IsOpen);
            _trade.AddGive("berries");
            SetHour(22.5); // evening comes while the window is open
            float until = Time.time + 2f;
            while (Time.time < until && _trade.Evaluate().Outcome != TradeOutcome.Closed) yield return null;
            Assert.AreEqual(TradeOutcome.Closed, _trade.Evaluate().Outcome);
            _trade.AddTake("bandage");
            Assert.IsFalse(_trade.DealEnabled, "no deal at a closed counter");
            _windows.CloseAll();
        }

        [UnityTest]
        public IEnumerator A_non_trader_has_no_shop()
        {
            SetHour(12);
            _s.Events.RaiseTradeRequested("peasant", null);
            Assert.IsFalse(_trade.IsOpen);
            yield return null;
        }

        // ------------------------------------------------------------------ map

        [UnityTest]
        public IEnumerator Without_a_map_there_is_no_screen_with_a_map_the_marks_show()
        {
            var g = _s.State;
            Assert.IsTrue(g.Map.Open("smithy"));
            Assert.AreEqual(0, g.Map.VisibleMarks.Count, "marks pile up unseen without a map");
            Assert.IsFalse(_map.Open());
            _s.Events.RaiseRadialChosen("map");
            Assert.IsFalse(_map.IsOpen);

            g.Bag.Add("map");
            _s.Events.RaiseRadialChosen("map");
            Assert.IsTrue(_map.IsOpen);
            Assert.AreEqual(1, _map.ShownMarks.Count);
            Assert.AreEqual("smithy", _map.ShownMarks[0]);
            Assert.IsTrue(_map.HasSheet, "the sheet is baked from the scene config");

            g.Map.Open("merchant_shop"); // talk opens a mark while the map is open
            yield return null;
            Assert.AreEqual(2, _map.ShownMarks.Count);

            var n = _map.ToSheet(0f, 0f);
            Assert.That(n.x, Is.InRange(0f, 1f));
            Assert.That(n.y, Is.InRange(0f, 1f));
            Assert.IsNull(_map.Root.Find("Marks").Find("Mark_you"), "no «you are here»");
            _windows.CloseAll();
        }

        // ------------------------------------------------------------------ notebook

        [UnityTest]
        public IEnumerator Notebook_shows_the_notes_newest_first_without_ticks()
        {
            var g = _s.State;
            g.Notebook.Add("peasant_town");
            g.Notebook.Add("peasant_beasts");
            _s.Events.RaiseRadialChosen("notebook");
            Assert.IsTrue(_notebook.IsOpen);
            Assert.AreEqual("peasant_beasts", _notebook.ShownIds[0], "the newest on top");
            Assert.AreEqual("peasant_town", _notebook.ShownIds[1]);

            g.Notebook.Add(g.Data.Notebook.Entries.Keys.First(k => !g.Notebook.Has(k)));
            yield return null;
            Assert.AreEqual(3, _notebook.ShownIds.Count, "a note written while the page is open appears on it");
            string page = _notebook.PageText;
            foreach (var mark in new[] { "✓", "✔", "☑", "[x]", "[ ]", "выполнено" })
                Assert.IsFalse(page.Contains(mark), "no ticks or statuses: " + mark);
            _windows.CloseAll();
        }
    }
}
