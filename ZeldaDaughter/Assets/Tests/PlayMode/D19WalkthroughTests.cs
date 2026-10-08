using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.NPC;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-19, docs/done-criteria/D-19.md item 3: the whole demo in the region scene from spawn to a reloaded save, through the session's own API
    /// (taps, replies, the trade and station windows, the bed) — the finger is only used where it is cheap (the swipe of the first hint).
    /// Every milestone is a line <c>[ZD:Demo] NN name</c> in the log, next to the lines the game writes itself ([ZD:Pickup], [ZD:Talk], [ZD:Combat],
    /// [ZD:Trade], [ZD:Station], [ZD:Rest], [ZD:Nature], [ZD:Save]); the test fails on any error or exception in the log (Unity's own rule) and
    /// checks that the game's own lines of the milestone were written.
    /// </summary>
    public class D19WalkthroughTests
    {
        const string Boar = "spawn_boar";

        GameSession _s;
        HeroController _hero;
        CombatPresenter _combat;
        WindowStack _windows;
        StationWindow _station;
        TradeWindow _trade;
        MapWindow _map;
        RestPresenter _rest;
        TalkPresenter _talk;
        readonly List<string> _log = new List<string>();
        readonly List<string> _miles = new List<string>();

        void OnLog(string message, string stack, LogType type) { if (type == LogType.Log || type == LogType.Warning) _log.Add(message); }

        void Mile(string name)
        {
            string line = $"{_miles.Count + 1:00} {name}";
            _miles.Add(line);
            ZdLog.Info("Demo", line);
        }

        bool Logged(string prefix) => _log.Any(l => l.StartsWith(prefix));

        void Said(string prefix) => Assert.IsTrue(Logged(prefix), $"the game wrote «{prefix}…» (log has {_log.Count} lines)");

        IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _combat = Object.FindFirstObjectByType<CombatPresenter>();
            _windows = Object.FindFirstObjectByType<WindowStack>();
            _station = Object.FindFirstObjectByType<StationWindow>();
            _trade = Object.FindFirstObjectByType<TradeWindow>();
            _map = Object.FindFirstObjectByType<MapWindow>();
            _rest = Object.FindFirstObjectByType<RestPresenter>();
            _talk = Object.FindFirstObjectByType<TalkPresenter>();
            Assert.NotNull(_s, "the region has a GameSession");
            Assert.NotNull(_combat);
            Assert.NotNull(_station);
            Assert.NotNull(_trade);
            Assert.NotNull(_map);
            Assert.NotNull(_rest);
            Assert.NotNull(_talk);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.3f);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _log.Clear();
            _miles.Clear();
            Application.logMessageReceived += OnLog;
            yield return Load();
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            TestSaves.Clear();
        }

        // ------------------------------------------------------------------ helpers

        void SetHour(double hour) => _s.State.Clock.SetTime(_s.State.Clock.Day, hour / 24.0);

        Vector3 Where(string objectId)
        {
            var t = _s.Index.Find(objectId);
            Assert.NotNull(t, objectId + " is in the region");
            return t.transform.position;
        }

        /// <summary>Stand <paramref name="meters"/> east of the thing (the hero is teleported; the walk itself is the bridge milestone and RegionTests).</summary>
        IEnumerator StandBy(string objectId, float meters = 1.2f)
        {
            var p = Where(objectId);
            _hero.Teleport(new Vector3(p.x + meters, 1f, p.z), 0f);
            yield return null;
            yield return null;
        }

        IEnumerator Converse(string prefer, int max)
        {
            for (int i = 0; i < max && _talk.Active; i++)
            {
                var buttons = _s.UI.ReplyButtons;
                if (buttons.Count == 0) break;
                var b = buttons.FirstOrDefault(x => x.name == "Reply_" + prefer) ?? buttons[0];
                b.onClick.Invoke();
                yield return null;
            }
        }

        IEnumerator Pick(string id, string item)
        {
            yield return StandBy(id, 1.0f);
            int before = _s.State.Bag.Count(item);
            _s.Tap(id);
            yield return null;
            Assert.AreEqual(before + 1, _s.State.Bag.Count(item), $"{id} → {item}");
        }

        // ------------------------------------------------------------------ the walkthrough

        [UnityTest]
        public IEnumerator From_spawn_to_a_reloaded_save()
        {
            var g = _s.State;

            // 1. spawn: the heroine stands at the west end of the road in the morning, no window open, the swipe hint shows
            SetHour(7);
            yield return null;
            var spawn = _hero.transform.position;
            Assert.Less(spawn.x, -150f, "spawn at the west end of the road");
            Assert.IsFalse(_windows.AnyOpen, "no window at the start");
            Mile($"spawn at {spawn.x:0},{spawn.z:0} day={g.Clock.Day}");
            Said("[ZD:Session] ready");

            // 2. the first hint (the swipe) — and the swipe takes it away
            yield return null;
            Assert.AreEqual(g.Hints.TextOf("swipe"), _s.UI.CurrentHint, "the first hint is the swipe");
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            var o = new Vec2(h.x, h.y - 250f);
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Time.realtimeSinceStartupAsDouble, o, TouchHit.Ground));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 40, o.Y), default));
            yield return new WaitForSeconds(0.3f);
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 40, o.Y), default));
            yield return null;
            Assert.AreNotEqual(g.Hints.TextOf("swipe"), _s.UI.CurrentHint, "the swipe took the hint away");
            Mile("swipe hint done");
            Said("[ZD:Hint] show swipe");
            Said("[ZD:Move] start");

            // 3. the stick (and what the way will need: a short stick for the knife, cloth for a torch, firewood and flint for the night)
            yield return Pick("pickup_stick_1", "stick");
            Mile($"stick in the bag ({g.Bag.Count("stick")})");
            Said("[ZD:Pickup] pickup_stick_1");
            yield return Pick("pickup_short_stick_1", "short_stick");
            yield return Pick("pickup_cloth_1", "cloth");
            yield return Pick("pickup_firewood_3", "firewood");
            yield return Pick("pickup_flint_1", "flint");

            // 4. the peasant: a talk by icons that ends pointing at the town; the town gets its mark and the notebook its note
            SetHour(12);
            yield return null;
            yield return StandBy("npc_peasant", 1.5f);
            _s.Tap("npc_peasant");
            yield return null;
            Assert.IsTrue(_talk.Active, "the peasant talks");
            Assert.Greater(_s.UI.ReplyButtons.Count, 0, "the heroine answers with icons");
            yield return Converse("town", 60);
            Assert.IsFalse(_talk.Active, "the talk ended");
            Assert.IsTrue(g.Notebook.Has("peasant_town"), "the note of the way to town");
            Mile("peasant talked, pointed at town_gate");
            Said("[ZD:Talk] peasant point town_gate");

            // 5. the glade: berries
            foreach (var id in new[] { "bush_berries_1", "bush_berries_2" })
            {
                yield return StandBy(id, 1.0f);
                _s.Tap(id);
                yield return null;
            }
            Assert.Greater(g.Bag.Count("berries"), 0, "berries from the glade");
            Mile($"berries {g.Bag.Count("berries")}");
            // D-23: the honest map — what an ordinary hour brings (data/traders.json typicalRound): stones, herbs and the locket are picked on the way, no coins and no fangs are added to the bag
            foreach (var (id, item) in new[] { ("pickup_stone_1", "stone"), ("pickup_stone_2", "stone"), ("pickup_stone_3", "stone"), ("pickup_herbs_1", "herbs"), ("pickup_herbs_2", "herbs"),
                                               ("pickup_herbs_3", "herbs"), ("pickup_healing_herbs_1", "healing_herbs"), ("pickup_healing_herbs_2", "healing_herbs"), ("pickup_locket", "locket") })
                yield return Pick(id, item);
            Mile($"on the way: stones {g.Bag.Count("stone")}, herbs {g.Bag.Count("herbs")}, healing herbs {g.Bag.Count("healing_herbs")}, the locket");

            // 6. the boar: a blow, the readable windup, the kill, the carcass (bare hands: a fang), the ore of the den
            var boar = g.Enemies.Get(Boar);
            Assert.NotNull(boar, "the den has its boar");
            _hero.Teleport(new Vector3(boar.Position.X + 0.7f, 1f, boar.Position.Y), 0f);
            yield return null;
            yield return null;
            float hp = boar.Hp;
            _s.Tap(Boar);
            Assert.IsTrue(Logged("[ZD:Combat] hit " + Boar) || Logged("[ZD:Combat] miss " + Boar), "a blow: hit or miss");
            Assert.Less(boar.Hp, hp, "the blow was dealt");
            Mile($"boar struck hp {hp:0.#} -> {boar.Hp:0.#}");

            bool windup = false, resolved = false;
            _s.Events.Enemy += n =>
            {
                if (n.EnemyId != Boar) return;
                windup |= n.Event.Kind == EnemyEventKind.WindupStarted;
                resolved |= n.Event.Kind == EnemyEventKind.Struck || n.Event.Kind == EnemyEventKind.Dodged;
            };
            boar.Provoke();
            float until = Time.time + 6f;
            while (!resolved && Time.time < until) yield return null;
            Assert.IsTrue(windup, "the boar wound up before it struck (readable)");
            Assert.IsTrue(resolved, "…and the blow came or was dodged");
            Mile("boar windup seen and resolved");
            Said("[ZD:Combat] " + Boar + " WindupStarted");

            for (float wake = Time.time + 25f; g.Condition.IsKnockedOut && Time.time < wake;) yield return null; // a hard blow knocks her out; she comes round
            boar = g.Enemies.Get(Boar);
            var fell = boar.Position;
            boar.Receive(1000f, null, 0f, 0f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(g.Killed.Contains(Boar), "the boar is dead");
            Assert.NotNull(_combat.FindCarcass(Boar), "a carcass lies where it fell");
            Said("[ZD:Combat] carcass " + Boar);
            _hero.Teleport(new Vector3(fell.X + 1f, 1f, fell.Y), 0f);
            yield return null;
            yield return null;
            _s.Tap(Boar);
            Assert.AreEqual(1, g.Bag.Count("fang"), "bare hands take the fang");
            Mile("carcass: fang");
            yield return Pick("pickup_ore_1", "ore");
            yield return Pick("pickup_special_herb_1", "special_herb");

            // 7. the bridge: she walks over the river on the planks (the character controller, not a teleport)
            var bridge = Where("anchor_bridge");
            _hero.Teleport(new Vector3(bridge.x - 9f, 1f, bridge.z), 0f);
            yield return null;
            yield return null;
            var cc = _hero.GetComponent<CharacterController>();
            cc.minMoveDistance = 0f;
            float walkEnd = Time.time + 12f;
            while (_hero.transform.position.x < bridge.x + 9f && Time.time < walkEnd)
            {
                cc.Move(new Vector3(Mathf.Min(3f * Time.deltaTime, 0.5f), 0f, 0f));
                yield return null;
            }
            Assert.GreaterOrEqual(_hero.transform.position.x, bridge.x + 9f - 0.2f, "the bridge lets her over the river");
            Mile($"bridge crossed x={_hero.transform.position.x:0.0}");

            // 8. the guard: points at the square
            SetHour(12);
            yield return null;
            yield return StandBy("npc_guard", 1.5f);
            _s.Tap("npc_guard");
            yield return null;
            Assert.IsTrue(_talk.Active, "the guard talks");
            yield return Converse("town", 60);
            Assert.IsFalse(_talk.Active);
            Mile("guard talked, pointed at town_square");
            Said("[ZD:Talk] guard point town_square");

            // 9. the square: the old man by the fountain is there and can be tapped
            yield return StandBy("npc_old_man", 2f);
            var old = _s.Index.FindTappable("npc_old_man");
            Assert.NotNull(old);
            Assert.IsTrue(old.Enabled && old.gameObject.activeInHierarchy, "the old man stands in the square by day");
            Mile("square: the old man by the fountain");

            // 10. the old man's locket: a request handed over ends with his thanks, and the reward is in her hands (D-23)
            int coinsBefore = g.Bag.Count("coin");
            var handed = g.Quests.Give("old_man", "locket");
            Assert.AreEqual(ZeldaDaughter.Core.Journal.QuestOutcome.Done, handed.Outcome, "the locket goes to the old man");
            yield return null;
            yield return null;
            D23LoopTests.AssertThanksByLanguageStage(g, g.Data.Dialogues.Npcs["old_man"].Nodes[handed.Thanks].Line);   // runes or words, by the stage of her language
            Assert.AreEqual(coinsBefore + g.Data.Quests.Quests["locket"].Reward.Items["coin"], g.Bag.Count("coin"), "…with coins");
            Mile($"old man: locket handed over, thanks, coins {g.Bag.Count("coin")}");
            Said("[ZD:Talk] old_man → thanks_locket");

            // 11. the forge: ore → metal at the smelter, metal + short stick → a knife on the anvil; the knife takes the whole carcass
            SetHour(12);
            yield return null;
            yield return StandBy("station_smelter", 1.5f);
            _s.Tap("station_smelter");
            Assert.AreEqual("smelter", _station.Kind);
            Assert.IsTrue(_station.Put("ore"));
            for (int i = 0; i < _station.StrikesNeeded; i++) _station.Strike();
            Assert.AreEqual(1, g.Bag.Count("metal"), "ore → metal");
            _windows.CloseAll();
            yield return StandBy("station_anvil", 1.5f);
            _s.Tap("station_anvil");
            Assert.AreEqual("anvil", _station.Kind);
            Assert.IsTrue(_station.Put("metal"));
            Assert.IsTrue(_station.Put("short_stick"));
            for (int i = 0; i < _station.StrikesNeeded; i++) _station.Strike();
            Assert.AreEqual(1, g.Bag.Count("knife"), "a knife");
            _windows.CloseAll();
            Mile("forge: ore → metal → knife");
            Said("[ZD:Station] open smelter");
            Said("[ZD:Station] open anvil");

            _hero.Teleport(new Vector3(fell.X + 1f, 1f, fell.Y), 0f);
            yield return null;
            yield return null;
            _s.Tap(Boar);
            yield return null;
            Assert.Greater(g.Bag.Count("meat"), 0, "the knife takes the meat");
            Mile($"carcass with the knife: meat {g.Bag.Count("meat")}");

            // 12. the merchant: her words about the map; the first barter (three stones for a stick), the lesson of coins, then the map for the coins and the goods of the hunt
            //     (D-23: nothing is added to the bag — the bag holds what the way gave; traders.json typicalRound)
            SetHour(12);
            yield return null;
            yield return StandBy("npc_merchant", 1.5f);
            _s.Tap("npc_merchant");
            yield return null;
            yield return Converse("map", 60);
            Assert.IsTrue(g.Notebook.Has("merchant_map"), "she told about the map");
            _s.Events.RaiseTradeRequested("merchant", null);
            Assert.IsTrue(_trade.IsOpen, "the merchant opens her shop by day");
            Assert.IsTrue(_trade.AddTake("stick"));
            for (int i = 0; i < 3; i++) _trade.AddGive("stone");
            Assert.AreEqual(TradeOutcome.Done, _trade.Deal().Outcome, "the first deal is a barter");
            _windows.CloseAll();
            _s.Tap("npc_merchant");
            yield return null;
            yield return Converse("coin", 60);
            Assert.IsTrue(g.Trade.KnowsCoins, "after the first barter someone teaches her coins");
            _s.Events.RaiseTradeRequested("merchant", null);
            Assert.IsTrue(_trade.IsOpen);
            Assert.IsTrue(_trade.AddTake("map"), "the map is on her shelf");
            float worth = 0f;
            foreach (var item in new[] { "coin", "fang", "hide", "bone", "meat", "fat", "herbs", "healing_herbs", "special_herb", "berries" })
            {
                int have = g.Bag.Count(item);
                for (int i = 0; i < have && _trade.Evaluate().Outcome != TradeOutcome.Done; i++) _trade.AddGive(item);
            }
            var offer = _trade.Evaluate();
            worth = offer.GiveValue;
            Assert.AreEqual(TradeOutcome.Done, offer.Outcome, $"what the way gave pays for the map: {offer.GiveValue:0.#} of {offer.TakeValue:0.#}");
            Assert.AreEqual(TradeOutcome.Done, _trade.Deal().Outcome);
            Assert.AreEqual(1, g.Bag.Count("map"), "the map is hers now");
            _windows.CloseAll();
            _s.Events.RaiseRadialChosen("map");
            Assert.IsTrue(_map.IsOpen, "with a map the map opens");
            Assert.Greater(_map.ShownMarks.Count, 0, "…and it shows what she was told");
            _windows.CloseAll();
            Mile($"barter done, map in the bag for {worth:0.#}, marks {g.Map.VisibleMarks.Count}");
            Said("[ZD:Trade] merchant Done");
            Said("[ZD:Map] open");

            // 13. the tavern: the bed, the sleep jumps the clock by the night's hours and the dark returns the light
            // D-23: before the first night the bed says «not yet»; once she has seen the dark (a frame at 23:00) it lets her sleep
            SetHour(12);
            yield return StandBy("bed_tavern", 1.2f);
            _s.Tap("bed_tavern");
            Assert.IsFalse(_rest.Sleeping, "by day, before the first night, the bed does not put her to sleep");
            Said("[ZD:Rest] bed_tavern not_sleepy");
            SetHour(23);
            yield return null;
            yield return null;
            Assert.IsTrue(g.NightSeen, "the dark came while she was awake");
            yield return null;
            yield return StandBy("bed_tavern", 1.2f);
            _rest.SetTimes(0.3f, 0.1f, 0.3f);
            int sleeps = _rest.Sleeps;
            double hoursBefore = g.Clock.TotalHours;
            _s.Tap("bed_tavern");
            Assert.IsTrue(_rest.Sleeping, "a tap on the bed puts her to sleep");
            until = Time.time + 10f;
            while (_rest.Sleeping && Time.time < until) yield return null;
            Assert.IsFalse(_rest.Sleeping, "she wakes");
            Assert.AreEqual(sleeps + 1, _rest.Sleeps);
            Assert.AreEqual(hoursBefore + g.Data.World.SleepHours, g.Clock.TotalHours, 0.6, "the clock jumped by the sleep hours");
            Mile($"tavern: slept {g.Data.World.SleepHours:0.#} h");
            Said("[ZD:Rest] sleep begin");
            Said("[ZD:Rest] wake");

            // 13. the night: a torch from stick + cloth, a campfire (firewood + flint) that lights the torch, the dark calls a wolf
            SetHour(0);
            yield return null;
            Assert.AreEqual(0f, g.Clock.Daylight, 1e-3f, "midnight is dark");
            var camp = new Vector3(-20f, 1f, -8f);
            _hero.Teleport(camp, 0f);
            yield return null;
            yield return null;
            var combined = g.Crafting.Combine("stick", "cloth", g.Bag);
            Assert.AreEqual(ZeldaDaughter.Core.Crafting.CraftOutcome.Done, combined.Outcome, "stick + cloth");
            Assert.AreEqual(1, g.Bag.Count("torch_unlit"));
            var placed = g.Camp.Place("firewood", new Vec2(camp.x + 1.5f, camp.z));
            Assert.AreEqual(ZeldaDaughter.Core.World.PlaceOutcome.Placed, placed.Outcome, "firewood on the ground");
            _s.Events.RaisePlaced(placed.Object);
            var lit = g.UseOnWorld(placed.Object.Id, "flint");
            Assert.AreEqual(ZeldaDaughter.Core.World.UseOutcome.Done, lit.Outcome, "flint on firewood");
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, lit);
            _s.BagChanged("walkthrough");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, g.Camp.Campfires.Count, "the fire burns");
            var torch = g.UseOnWorld(placed.Object.Id, "torch_unlit");
            Assert.AreEqual(ZeldaDaughter.Core.World.UseOutcome.Done, torch.Outcome, "the torch is lit at the fire");
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, torch);
            _s.BagChanged("walkthrough");
            yield return null;
            Assert.AreEqual(1, g.Bag.Count("torch"));
            Assert.IsTrue(Object.FindFirstObjectByType<HeroTorchLight>().IsOn, "the torch lights the heroine");
            Mile("night: campfire and a torch");
            Said("[ZD:Nature] torch_on");

            // forty seconds of the night in a flash — the world's steps as GameSession.StepWorld takes them (the events reach the views)
            for (int i = 0; i < 160 && !g.Enemies.Active.Any(e => e.DefId == "wolf"); i++)
            {
                var events = g.TickWorld(0.25f, _s.Rolls.World.Next());
                for (int k = 0; k < events.Count; k++) _s.Events.RaiseWorld(events[k]);
            }
            yield return null;
            yield return null;
            var wolf = g.Enemies.Active.FirstOrDefault(e => e.DefId == "wolf");
            Assert.NotNull(wolf, "the dark called a wolf");
            Assert.GreaterOrEqual((wolf.Position - g.HeroPosition).Length, g.Data.Night.MinHeroDistance - 0.01f, "…not at her feet");
            Assert.NotNull(_combat.FindEnemy(wolf.Id), "…and it has a view");
            Mile($"night: wolf {wolf.Id} at {(wolf.Position - g.HeroPosition).Length:0} m");
            Said("[ZD:Nature] wolf_called");

            // 14. the save: she is saved and loaded again — the bag, the kill, the fire, the notes, the time
            int stickCount = g.Bag.Count("stick"), meat = g.Bag.Count("meat");
            double total = g.Clock.TotalHours;
            _s.Save("walkthrough");
            Said("[ZD:Save] saved walkthrough");
            yield return Load();
            g = _s.State;
            Said("[ZD:Save] loaded");
            Assert.AreEqual(1, g.Bag.Count("map"), "the map is saved");
            Assert.AreEqual(1, g.Bag.Count("knife"), "the knife is saved");
            Assert.AreEqual(1, g.Bag.Count("torch"), "the torch is saved");
            Assert.AreEqual(stickCount, g.Bag.Count("stick"));
            Assert.AreEqual(meat, g.Bag.Count("meat"));
            Assert.IsTrue(g.Killed.Contains(Boar), "the kill is saved");
            Assert.AreEqual(1, g.Camp.Campfires.Count, "the fire is saved");
            Assert.IsTrue(g.Notebook.Has("peasant_town") && g.Notebook.Has("merchant_map"), "the notes are saved");
            Assert.AreEqual(total, g.Clock.TotalHours, 0.1, "the time is saved");
            Mile("saved and loaded: bag, kill, fire, notes, time");

            Assert.GreaterOrEqual(_miles.Count, 14, "the 14 stages of the demo, each a [ZD:Demo] line");
        }
    }
}
