using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.NPC;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-23 (docs/done-criteria/D-23.md) in the region scene: the night wolf comes from 20–35 m and swings at one without a fire, stops at the edge of a
    /// campfire's light, the torch weakens and burns down to a burnt stick, a long press in the bag shows the description, the request ends with thanks and
    /// the reward, the heroine's new remarks fire. The rules are tested in the core (D23Tests); here is what the scene shows. Frames — docs/demo/frames/D-23-*.png.
    /// </summary>
    public class D23LoopTests
    {
        GameSession _s;
        HeroController _hero;
        CombatPresenter _combat;
        readonly List<string> _said = new List<string>();
        readonly List<EnemyNotice> _notices = new List<EnemyNotice>();
        readonly List<WorldEvent> _world = new List<WorldEvent>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _said.Clear(); _notices.Clear(); _world.Clear();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _combat = Object.FindFirstObjectByType<CombatPresenter>();
            Assert.NotNull(_s);
            Assert.NotNull(_combat);
            _hero.UseDpi(160f);
            _s.Events.HeroSaid += (topic, line) => _said.Add(topic);
            _s.Events.Enemy += n => _notices.Add(n);
            _s.Events.World += e => _world.Add(e);
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            GameData.Reset();
            TestSaves.Clear();
        }

        static IEnumerator Until(System.Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        void SetHour(double hour)
        {
            _s.State.Clock.SetTime(1, hour / 24.0);
            _s.JumpTime(() => System.Array.Empty<ZeldaDaughter.Core.World.ClockEvent>(), 0);
        }

        IEnumerator StandAt(float x, float z)
        {
            _hero.Teleport(new Vector3(x, 1f, z), 0f);
            yield return null;
            yield return null;
        }

        static string FramePath(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "demo", "frames", $"D-23-{name}.png"));

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            if (t == null) yield break;
            string path = FramePath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            ZdLog.Info("Frame", $"D-23 {name} {t.width}x{t.height} → {path}");
            Object.Destroy(t);
        }

        // ------------------------------------------------------------------ 4. the night wolf

        [UnityTest]
        public IEnumerator The_night_wolf_is_called_20_to_35_m_away_comes_and_swings_at_one_without_fire()
        {
            var g = _s.State;
            g.Data.Night.SpawnIntervalSeconds = 0.3f;
            yield return StandAt(-168f, 0f);
            SetHour(0);
            Assume.That(g.Clock.Daylight, Is.EqualTo(0f).Within(1e-3f), "midnight is dark");
            Assert.AreEqual(0, g.Bag.Count("torch"), "no fire, no torch");

            yield return Until(() => _world.Any(e => e.Kind == WorldEventKind.PredatorSpawned), 10f);
            var spawn = _world.FirstOrDefault(e => e.Kind == WorldEventKind.PredatorSpawned);
            Assert.AreEqual(WorldEventKind.PredatorSpawned, spawn.Kind, "the dark calls a wolf");
            float d = (spawn.Position - new Vec2(-168f, 0f)).Length;
            Assert.That(d, Is.InRange(g.Data.Night.MinHeroDistance - 0.5f, g.Data.Night.MaxHeroDistance + 0.5f), "called 20–35 m from her, not 130");
            yield return null;
            yield return null;
            Assert.NotNull(_combat.FindEnemy(spawn.Id), "…and it has a view");

            float hp = g.Condition.Hp;
            yield return Until(() => _notices.Any(n => n.Event.Kind == EnemyEventKind.WindupStarted), 25f);
            Assert.IsTrue(_notices.Any(n => n.EnemyId == spawn.Id && n.Event.Kind == EnemyEventKind.WindupStarted), "the wolf came up and wound up its blow");
            yield return Until(() => _notices.Any(n => n.EnemyId == spawn.Id && (n.Event.Kind == EnemyEventKind.Struck || n.Event.Kind == EnemyEventKind.Dodged)), 3f);
            Assert.IsTrue(_notices.Any(n => n.EnemyId == spawn.Id && n.Event.Kind == EnemyEventKind.Struck), "…and bit: no fire to stop it");
            Assert.Less(g.Condition.Hp, hp);

            SetHour(12);
            yield return Until(() => _world.Any(e => e.Kind == WorldEventKind.PredatorDespawned), 25f);
            Assert.IsTrue(_world.Any(e => e.Kind == WorldEventKind.PredatorDismissed || e.Kind == WorldEventKind.PredatorDespawned), "by day it goes");
        }

        // ------------------------------------------------------------------ 2. fire against wolves

        [UnityTest]
        public IEnumerator A_wolf_stops_at_the_edge_of_the_campfires_light_and_does_not_strike()
        {
            var g = _s.State;
            yield return StandAt(-168f, 0f);
            SetHour(0);
            var fireAt = new Vec2(-163f, 6f);
            g.Bag.Add("firewood");
            g.Bag.Add("flint");
            var placed = g.Camp.Place("firewood", fireAt);
            Assert.AreEqual(PlaceOutcome.Placed, placed.Outcome);
            var used = g.Camp.Use(placed.Object.Id, "flint");
            _s.Events.RaisePlaced(placed.Object);
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            _s.BagChanged("test");
            Assert.AreEqual(1, g.Camp.Campfires.Count);
            yield return StandAt(fireAt.X - 1.5f, fireAt.Y - 1.5f);

            // a wolf comes at her from the north-east, along the screen's vertical
            float r = g.Data.Enemies.Fire.CampfireRadius;
            var wolf = g.Enemies.Spawn("night_wolf_frame", "wolf", fireAt + new Vec2(r + 12f, r + 12f) * 0.7071f);
            Assert.NotNull(wolf);
            wolf.Hunt();
            float nearest = float.MaxValue;
            float until = Time.realtimeSinceStartup + 14f;
            while (Time.realtimeSinceStartup < until)
            {
                nearest = Mathf.Min(nearest, (wolf.Position - fireAt).Length);
                yield return null;
            }
            Assert.GreaterOrEqual(nearest, r - 0.4f, "the wolf never came into the light's reach");
            Assert.IsFalse(_notices.Any(n => n.Event.Kind == EnemyEventKind.WindupStarted || n.Event.Kind == EnemyEventKind.Struck), "…and never swung at her by the fire");
            Assert.LessOrEqual((wolf.Position - fireAt).Length, r + 2f, "…it waits at the edge");
            Assert.NotNull(_combat.FindEnemy("night_wolf_frame"));
            yield return Capture("wolf-at-the-light");
            Assert.Contains("wolf_close", _said, "she is afraid aloud");
        }

        [UnityTest]
        public IEnumerator A_wolf_in_a_torchs_reach_runs_and_she_says_so()
        {
            var g = _s.State;
            yield return StandAt(-168f, 0f);
            SetHour(0);
            g.Bag.Add("torch");
            _s.BagChanged("test");
            g.Data.Remarks.GlobalGapSeconds = 0f;
            var wolf = g.Enemies.Spawn("night_wolf_torch", "wolf", new Vec2(-168f + 3f, 0f));
            wolf.Provoke();
            yield return Until(() => _notices.Any(n => n.Event.Kind == EnemyEventKind.Frightened), 3f);
            Assert.IsTrue(_notices.Any(n => n.EnemyId == "night_wolf_torch" && n.Event.Kind == EnemyEventKind.Frightened), "the torch frightens the wolf");
            yield return Until(() => _said.Contains("wolf_flees"), 3f);
            Assert.Contains("wolf_flees", _said);
            Assert.IsFalse(_notices.Any(n => n.Event.Kind == EnemyEventKind.Struck));
        }

        // ------------------------------------------------------------------ 3. the torch burns down

        [UnityTest]
        public IEnumerator The_torch_weakens_then_burns_down_to_a_burnt_stick()
        {
            var g = _s.State;
            var light = Object.FindFirstObjectByType<HeroTorchLight>();
            g.Data.Remarks.GlobalGapSeconds = 0f;
            g.Bag.Add("torch");
            _s.BagChanged("test");
            yield return null;
            Assert.IsTrue(light.IsOn);
            float full = light.Range;
            yield return new WaitForSeconds(0.5f);
            float strong = Mathf.Max(light.Intensity, 0.01f);

            g.Torch.Restore(g.Data.Camp.TorchFadeSeconds * 0.3f);
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(light.IsOn, "still burning");
            Assert.Less(light.Fade, 0.5f, "the fade is the core's");
            Assert.Less(light.Range, full, "the light shrinks");
            Assert.Less(light.Intensity, strong * 0.8f, "…and weakens");
            yield return Until(() => _said.Contains("torch_dying"), 3f);
            Assert.Contains("torch_dying", _said, "she notices");

            yield return Until(() => g.Bag.Count("torch") == 0, g.Data.Camp.TorchFadeSeconds);
            yield return null;
            yield return null;
            Assert.AreEqual(0, g.Bag.Count("torch"), "burnt down");
            Assert.AreEqual(1, g.Bag.Count("burnt_stick"), "…a burnt stick is left");
            Assert.IsFalse(light.IsOn, "the light is out");
            Assert.IsTrue(_world.Any(e => e.Kind == WorldEventKind.TorchBurntOut));
        }

        // ------------------------------------------------------------------ 5. descriptions

        [UnityTest]
        public IEnumerator A_long_press_on_an_item_shows_its_description_in_a_cloud_and_it_goes()
        {
            var g = _s.State;
            var bag = Object.FindFirstObjectByType<InventoryWindow>();
            var drag = Object.FindFirstObjectByType<ItemDrag>();
            Assert.NotNull(bag);
            g.Bag.Add("fang");
            g.Bag.Add("berries", 3);
            _s.BagChanged("test");
            bag.Open();
            yield return null;
            yield return null;
            Assert.IsNull(bag.InfoText, "nothing is described until asked");
            Assert.IsFalse(drag.Describe(7), "an empty cell tells nothing");

            // the long press itself: a finger down on cell 0, held still for longPressSeconds
            drag.PointerDown(0, bag.CellScreenPosition(0));
            var held = bag.CellScreenPosition(0);
            float heldUntil = Time.unscaledTime + (float)g.Data.Input.LongPressSeconds + 0.3f;
            while (Time.unscaledTime < heldUntil) { drag.Poll(held, true); yield return null; }
            drag.Poll(held, true);
            string expected = g.Data.Items[g.Bag.Stacks[0].ItemId].Description;
            Assert.IsNotNull(bag.InfoText, "a held finger asks «what is this?»");
            StringAssert.Contains(expected, bag.InfoText);
            Assert.AreEqual(0, bag.InfoCell);
            Assert.IsFalse(drag.IsDragging, "…and it is not a drag");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(bag.InfoText, @"\d"), "no numbers in the cloud");
            yield return Capture("item-description");

            Assert.IsTrue(drag.Describe(1));
            StringAssert.Contains(g.Data.Items["berries"].Description, bag.InfoText);
            yield return new WaitForSeconds(g.Data.Session.ItemInfoSeconds + 0.5f);
            Assert.IsNull(bag.InfoText, "the cloud goes by itself");
        }

        // ------------------------------------------------------------------ 6. thanks

        [UnityTest]
        public IEnumerator Handing_over_the_locket_ends_with_thanks_and_six_coins_in_the_bag()
        {
            var g = _s.State;
            var talk = Object.FindFirstObjectByType<TalkPresenter>();
            SetHour(12);
            yield return null;
            var old = _s.Index.Find("npc_old_man").transform.position;
            _hero.Teleport(new Vector3(old.x + 2f, 1f, old.z), 0f);
            yield return new WaitForSeconds(0.6f);
            g.Bag.Add("locket");
            _s.BagChanged("test");
            int coins = g.Bag.Count("coin");
            var r = g.Quests.Give("old_man", "locket");
            Assert.AreEqual(ZeldaDaughter.Core.Journal.QuestOutcome.Done, r.Outcome);
            yield return new WaitForSeconds(0.3f);
            string thanks = g.Data.Dialogues.Npcs["old_man"].Nodes[r.Thanks].Line;
            AssertThanksByLanguageStage(g, thanks);
            Assert.AreEqual(coins + g.Data.Quests.Quests["locket"].Reward.Items["coin"], g.Bag.Count("coin"), "the reward is in her hands");
            yield return Capture("thanks");
        }

        /// <summary>The thanks show by the stage of her language: words once she understands them, runes before (the same rule as every talk).</summary>
        public static void AssertThanksByLanguageStage(ZeldaDaughter.Core.Save.GameState g, string line)
        {
            var shown = Object.FindFirstObjectByType<GameSession>().UI.NpcBubbleText;
            Assert.IsFalse(string.IsNullOrEmpty(shown), "the receiver says something");
            bool runes = shown.Any(c => c >= 0x16A0 && c <= 0x16FF);
            if (!runes)   // before she understands the words the thanks are runes (partly, by the stage); once she does, they read
            {
                Assert.AreEqual(line, shown, "…in words she understands");
                StringAssert.Contains("спасибо", shown.ToLowerInvariant());
            }
        }

        // ------------------------------------------------------------------ 7. the heroine's remarks

        [UnityTest]
        public IEnumerator Rain_a_first_purchase_and_a_full_belly_make_her_speak()
        {
            var g = _s.State;
            g.Data.Remarks.GlobalGapSeconds = 0f;
            SetHour(12);
            yield return null;

            // rain
            g.Nature.Weather.StartRain(60f);
            yield return Until(() => _said.Contains("rain"), 4f);
            Assert.Contains("rain", _said, "the first drops");
            g.Nature.Weather.StopRain();

            // the first purchase: three stones for a stick at the merchant's counter (a barter; the very first thing off her shelf)
            var trade = Object.FindFirstObjectByType<TradeWindow>();
            Assert.NotNull(trade);
            g.Bag.Add("stone", 3);
            _s.BagChanged("test");
            _s.Events.RaiseTradeRequested("merchant", null);
            Assert.IsTrue(trade.IsOpen);
            Assert.IsTrue(trade.AddTake("stick"));
            for (int i = 0; i < 3; i++) trade.AddGive("stone");
            var res = trade.Deal();
            Assert.AreEqual(TradeOutcome.Done, res.Outcome);
            Assert.IsTrue(res.FirstPurchase);
            Assert.Contains("first_purchase", _said);
            Object.FindFirstObjectByType<WindowStack>().CloseAll();

            // a meal that fills her
            g.Hunger.Restore(g.Data.Hunger.HungryAt + 0.02f);
            g.Bag.Add("cooked_meat", 4);
            _s.BagChanged("test");
            var bag = Object.FindFirstObjectByType<InventoryWindow>();
            var drag = Object.FindFirstObjectByType<ItemDrag>();
            bag.Open();
            yield return null;
            int cell = g.Bag.Stacks.ToList().FindIndex(x => x.ItemId == "cooked_meat");
            var heroScreen = (Vector2)Camera.main.WorldToScreenPoint(_hero.transform.position);
            for (int i = 0; i < 4 && !_said.Contains("sated"); i++)
            {
                if (!bag.IsOpen) { bag.Open(); yield return null; }
                cell = g.Bag.Stacks.ToList().FindIndex(x => x.ItemId == "cooked_meat");
                Assert.GreaterOrEqual(cell, 0);
                Assert.IsTrue(drag.Begin(cell, bag.CellScreenPosition(cell)));
                var c = new Vector3[4];
                bag.Root.GetWorldCorners(c);
                drag.MoveTo(new Vector2(c[0].x - 80f, c[0].y - 80f));
                drag.Release(heroScreen);
                yield return null;
            }
            Assert.Contains("sated", _said, "she says she is full");
        }
    }
}
