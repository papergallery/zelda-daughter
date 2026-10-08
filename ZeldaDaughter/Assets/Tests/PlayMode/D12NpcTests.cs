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
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.NPC;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-12 (docs/demo/unity-architecture.md §7, docs/done-criteria/D-12.md): the town by the clock and the talk with a resident. Most in scenes/test-demo.json, the roads and the pointing in the region.</summary>
    public class D12NpcTests
    {
        GameSession _s;
        HeroController _hero;
        NpcPresenter _npcs;
        TalkPresenter _talk;
        TalkBubbleView _bubble;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield break;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            TestSaves.Clear();
        }

        IEnumerator Load(string scene)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode($"Assets/Scenes/{scene}.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _npcs = Object.FindFirstObjectByType<NpcPresenter>();
            _talk = Object.FindFirstObjectByType<TalkPresenter>();
            _bubble = Object.FindFirstObjectByType<TalkBubbleView>();
            Assert.NotNull(_s, scene + " has a GameSession");
            Assert.NotNull(_npcs, scene + " has the residents (SceneBuilder.AddNpcs)");
            Assert.NotNull(_bubble, scene + " has the talk view");
            _hero.UseDpi(160f);
            yield return null;
        }

        /// <summary>Time of day in hours, and the residents are put at their places at once (as after a sleep).</summary>
        void SetHour(double hour)
        {
            _s.State.Clock.SetTime(1, hour / 24.0);
            _s.JumpTime(() => System.Array.Empty<ClockEvent>(), 0);
        }

        Vector3 Anchor(string id) => _s.Index.Anchors[id];
        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        NpcView View(string id) => _npcs.View(id);

        static float DistanceToPolyline(Vector3 p, IReadOnlyList<Vector3> line)
        {
            float best = float.MaxValue;
            var q = new Vector2(p.x, p.z);
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = new Vector2(line[i].x, line[i].z);
                var ab = new Vector2(line[i + 1].x, line[i + 1].z) - a;
                float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }
            return best;
        }

        /// <summary>Answers with the icon while the talk goes on (the first answer if there is no such icon); false if it did not end in <paramref name="max"/> steps.</summary>
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

        // ------------------------------------------------------------------ the town by the clock

        [UnityTest]
        public IEnumerator Residents_stand_at_the_anchors_of_the_hour()
        {
            yield return Load("test-demo");
            SetHour(12);
            yield return null;
            Assert.Less(Flat(View("peasant").transform.position, Anchor("anchor_field")), 0.05f, "the peasant works in the field at noon");
            Assert.Less(Flat(View("merchant").transform.position, Anchor("anchor_shop")), 0.05f, "the merchant is at her stall");
            Assert.IsFalse(View("peasant").Hidden);

            SetHour(18.5);
            yield return null;
            Assert.AreEqual(NpcPresenter.StrollRingMeters, Flat(View("peasant").transform.position, Anchor("anchor_hut")), 0.05f, "the peasant strolls by his hut in the evening");
            float ring = Flat(View("merchant").transform.position, Anchor("anchor_tavern_hall"));
            Assert.AreEqual(NpcPresenter.TavernRingMeters, ring, 0.05f, "the merchant is in the tavern, on its ring");
        }

        [UnityTest]
        public IEnumerator Sleepers_are_hidden_and_cannot_be_tapped_and_the_shop_is_shut()
        {
            yield return Load("test-demo");
            SetHour(23);
            yield return null;
            foreach (var id in new[] { "peasant", "merchant" })
            {
                var v = View(id);
                Assert.IsTrue(v.Hidden, id + " sleeps");
                Assert.IsFalse(v.Figure.gameObject.activeSelf, id + " is not drawn");
                Assert.IsFalse(_s.Index.FindTappable("npc_" + id).Enabled, id + " cannot be tapped");
            }
            Assert.IsFalse(_npcs.ShopOpen("merchant"));
            _s.Tap("npc_merchant");
            yield return null;
            Assert.IsFalse(_talk.Active, "no talk with a sleeper");
            Assert.IsNull(_s.UI.NpcBubbleText);
            Assert.IsFalse(_bubble.TradeVisible, "no trade icon at night");

            SetHour(12);
            yield return null;
            Assert.IsFalse(View("peasant").Hidden);
            Assert.IsTrue(_s.Index.FindTappable("npc_peasant").Enabled);
            Assert.IsTrue(View("peasant").Figure.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Trade_icon_only_while_the_trader_stands_at_her_shop()
        {
            yield return Load("test-demo");
            string traded = null;
            _s.Events.TradeRequested += (npc, item) => traded = npc;

            SetHour(12);
            yield return null;
            Assert.IsTrue(_npcs.ShopOpen("merchant"));
            _s.Tap("npc_merchant");
            yield return null;
            Assert.IsTrue(_bubble.TradeVisible, "the stall is open at noon");
            _bubble.TradeButton.onClick.Invoke();
            Assert.AreEqual("merchant", traded, "the icon asks for the trade window");

            SetHour(18);
            yield return null;
            Assert.IsFalse(_npcs.ShopOpen("merchant"), "in the tavern she does not sell");
            _s.Tap("npc_merchant");
            yield return null;
            Assert.IsTrue(_bubble.BubbleVisible);
            Assert.IsFalse(_bubble.TradeVisible, "the stall is shut in the evening");

            SetHour(23);
            yield return null;
            Assert.IsFalse(_npcs.ShopOpen("merchant"));
            Assert.IsFalse(_bubble.TradeVisible);
        }

        [UnityTest]
        public IEnumerator After_a_sleep_the_residents_are_in_place_at_once()
        {
            yield return Load("test-demo");
            SetHour(21.5);
            yield return null;
            Assert.IsTrue(View("peasant").Hidden);
            double hours = _s.State.Data.World.SleepHours;
            _s.JumpTime(() => _s.State.Sleep(), hours);
            // not a frame later: the morning slot is already filled
            Assert.IsFalse(View("peasant").Hidden, "the peasant is up and at work");
            Assert.IsFalse(View("peasant").IsWalking, "…not on his way there");
            Assert.Less(Flat(View("peasant").transform.position, Anchor("anchor_field")), 0.05f);
            Assert.IsTrue(View("merchant").Hidden, "the merchant still sleeps before eight");
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_merchant_walks_to_the_tavern_along_the_roads_not_through_houses()
        {
            yield return Load("region");
            var merchant = View("merchant");
            SetHour(16.9);
            yield return null;
            Assert.Less(Flat(merchant.transform.position, Anchor("anchor_shop")), 0.05f, "16:54 at the stall");
            var road = _npcs.RouteBetween("anchor_shop", "anchor_tavern_hall");
            Assert.IsNotNull(road, "the scene's roads join the shop and the tavern");
            Assert.GreaterOrEqual(road.Count, 2);

            Time.timeScale = 3f;
            _s.State.Clock.SetTime(1, 17.01 / 24.0); // 17:00:36 — the evening slot begins, the next sync sends her walking
            float waited = 0f;
            while (!merchant.IsWalking && waited < 3f) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(merchant.IsWalking, "she sets off after 17:00");

            var blocking = LayerMask.GetMask("Blocking");
            var tavern = Anchor("anchor_tavern_hall");
            var walkway = new List<Vector3> { merchant.transform.position };
            walkway.AddRange(merchant.Waypoints);
            // what she walks is the road of the core, bent only round things standing on it
            int bent = 0;
            for (int i = 1; i < walkway.Count - 1; i++)
            {
                if (DistanceToPolyline(walkway[i], road) <= 0.3f) continue;
                bent++;
                Assert.Greater(Physics.OverlapSphere(walkway[i], 1.6f, blocking).Length, 0, $"waypoint {walkway[i]} is off the road and not by an obstacle");
            }
            Debug.Log($"[ZD:Test] merchant walkway {walkway.Count} points, {bent} round obstacles");

            float worst = 0f, walked = 0f, simStart = Time.time;
            var last = merchant.transform.position;
            float limit = Time.realtimeSinceStartup + 150f;
            while (merchant.IsWalking && Time.realtimeSinceStartup < limit)
            {
                var p = merchant.transform.position;
                walked += Flat(p, last);
                last = p;
                var hits = Physics.OverlapSphere(p + Vector3.up * 0.6f, 0.15f, blocking);
                Assert.AreEqual(0, hits.Length, $"through a wall at {p}: {string.Join(", ", hits.Select(h => h.transform.root == h.transform ? h.name : h.transform.parent.name + "/" + h.name))}");
                worst = Mathf.Max(worst, DistanceToPolyline(p, walkway));
                yield return null;
            }
            Assert.IsFalse(merchant.IsWalking, "she got there");
            Assert.LessOrEqual(worst, 0.05f, "she walks her waypoints");
            Assert.AreEqual(NpcPresenter.TavernRingMeters, Flat(merchant.transform.position, tavern), 0.1f);
            float speed = walked / (Time.time - simStart);
            float pace = _s.State.Npcs.WalkSpeed("merchant");
            Assert.That(speed, Is.InRange(pace * 0.5f, pace * 1.25f), "at about the pace of npcs.json (long frames are clamped, so never faster)");
        }

        // ------------------------------------------------------------------ the talk

        [UnityTest]
        public IEnumerator Stage_one_is_runes_and_icons_stage_two_a_mix_stage_three_the_text()
        {
            yield return Load("test-demo");
            SetHour(12);
            string glyphs = _s.State.Data.Language.Glyphs;
            var node = _s.State.Data.Dialogues.Npcs["peasant"].Nodes["start"];
            bool HasGlyph(string t) => t.Any(c => glyphs.IndexOf(c) >= 0);
            bool HasCyrillic(string t) => t.Any(c => c >= 'А' && c <= 'я');

            _s.Tap("npc_peasant");
            yield return null;
            Assert.IsFalse(_s.UI.NpcBubbleText.Contains(node.Line), "stage 1: not the line");
            Assert.IsTrue(HasGlyph(_s.UI.NpcBubbleText), "…runes");
            CollectionAssert.AreEqual(node.Icons, _bubble.IconIds, "…and the icons of the line");
            Assert.IsTrue(_s.UI.ReplyButtons.All(b => b.name.StartsWith("Reply_")));

            yield return Load("test-demo");
            SetHour(12);
            _s.State.Language.Restore(0.35f);
            _s.Tap("npc_peasant");
            yield return null;
            string mix = _s.UI.NpcBubbleText;
            Assert.IsTrue(HasGlyph(mix) && HasCyrillic(mix), $"stage 2: short words come through, the rest stays runes: {mix}");
            Assert.AreEqual(0, _bubble.IconIds.Count, "no icons once the hero understands a little");

            yield return Load("test-demo");
            SetHour(12);
            _s.State.Language.Restore(0.8f);
            _s.Tap("npc_peasant");
            yield return null;
            Assert.AreEqual(node.Line, _s.UI.NpcBubbleText, "stage 3: the text");
            Assert.IsFalse(HasGlyph(_s.UI.NpcBubbleText));
        }

        [UnityTest]
        public IEnumerator A_reply_icon_moves_the_talk_and_an_unclear_one_shows_a_question_mark()
        {
            yield return Load("test-demo");
            SetHour(12);
            bool question = false, moved = false;
            for (int i = 0; i < 60 && !(question && moved); i++)
            {
                if (!_talk.Active) { _s.Tap("npc_peasant"); yield return null; }
                string before = _talk.NodeId;
                var b = _s.UI.ReplyButtons.First(x => x.name == "Reply_town");
                b.onClick.Invoke();
                yield return null;
                if (_bubble.QuestionVisible) { question = true; Assert.AreEqual(before, _talk.NodeId, "she did not get it: the talk stays"); Assert.IsTrue(_talk.Active); }
                else if (!_talk.Active || _talk.NodeId != before) moved = true;
            }
            Assert.IsTrue(moved, "Reply_town takes the talk on");
            Assert.IsTrue(question, "at understanding 0 about half the icons are not understood: «?» at her");
        }

        [UnityTest]
        public IEnumerator A_line_with_a_gesture_turns_her_to_the_object_and_she_points()
        {
            yield return Load("region");
            var peasant = View("peasant");
            _hero.Teleport(peasant.transform.position + new Vector3(4f, 1f, 0f), 0f);
            yield return null;
            LogAssert.Expect(LogType.Log, "[ZD:Talk] peasant point town_gate");
            _s.Tap("npc_peasant");
            yield return null;
            Assert.IsTrue(peasant.Talking);
            var toHero = (_hero.transform.position - peasant.transform.position); toHero.y = 0f;
            Assert.Greater(Vector3.Dot(peasant.Heading, toHero.normalized), 0.95f, "she turns to the hero while they talk");

            yield return Converse("town", 40);
            Assert.IsFalse(_talk.Active, "the talk ended");
            var gate = _s.Index.Find("town_gate").transform.position;
            var toGate = gate - peasant.transform.position; toGate.y = 0f;
            Assert.Greater(Vector3.Dot(peasant.Heading, toGate.normalized), 0.98f, "…and to the town gate");
            Assert.IsTrue(peasant.Pointing);
            Assert.IsFalse(peasant.Talking, "she is free again");
        }

        [UnityTest]
        public IEnumerator The_bubble_never_covers_the_hero_or_her_answers_and_takes_no_touches()
        {
            yield return Load("test-demo");
            SetHour(12);
            Assume.That(Screen.height, Is.GreaterThanOrEqualTo(600), "the game view is big enough to place a bubble");
            var peasant = View("peasant");
            // the hero a step behind the peasant on the screen: the bubble would sit right on her head
            _hero.Teleport(peasant.transform.position + new Vector3(0.6f, 1f, 0.6f), 0f);
            yield return null;
            _s.Tap("npc_peasant");
            yield return null;
            yield return null;
            Assert.IsTrue(_bubble.BubbleVisible);
            Assert.IsFalse(_bubble.BubbleRect.Overlaps(_bubble.HeroRect), $"bubble {_bubble.BubbleRect} / hero {_bubble.HeroRect}");
            Assert.IsFalse(_bubble.ReplyRowRect.Overlaps(_bubble.HeroRect), "her answers are above her head");
            Assert.IsFalse(_bubble.BubbleRect.Overlaps(_bubble.ReplyRowRect), "…and clear of the bubble");
            Assert.GreaterOrEqual(_bubble.BubbleRect.xMin, 0f);
            Assert.LessOrEqual(_bubble.BubbleRect.xMax, Screen.width);

            foreach (var g in _bubble.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.raycastTarget) continue;
                Assert.IsNotNull(g.GetComponent<Button>(), $"{g.name} takes touches but is not a button");
            }
        }

        // ------------------------------------------------------------------ what the talk does

        [UnityTest]
        public IEnumerator Lines_write_the_notebook_through_the_core()
        {
            yield return Load("test-demo");
            SetHour(12);
            _s.Tap("npc_merchant");
            yield return null;
            Assert.IsFalse(_s.State.Notebook.Has("merchant_coins"));
            yield return Converse("coin", 60);
            Assert.IsFalse(_talk.Active);
            Assert.IsTrue(_s.State.Notebook.Has("merchant_coins"), "the «coins» node writes its note");
        }

        [UnityTest]
        public IEnumerator Handing_over_a_request_starts_the_thanks()
        {
            yield return Load("test-demo");
            SetHour(12);
            _s.State.Language.Restore(0.8f);
            Assert.AreEqual(Core.Journal.OfferOutcome.Accepted, _s.State.Quests.Offer("letter"));
            Assert.AreEqual(1, _s.State.Bag.Count("letter"));
            var done = _s.State.Quests.Give("peasant", "letter");
            Assert.AreEqual(Core.Journal.QuestOutcome.Done, done.Outcome);
            yield return null;
            Assert.AreEqual(_s.State.Data.Dialogues.Npcs["peasant"].Nodes["thanks_letter"].Line, _s.UI.NpcBubbleText, "the peasant thanks her");
            Assert.IsFalse(_talk.Active, "the thanks end the talk");
            Assert.Greater(_s.State.Bag.Count("coin"), 0, "…with the reward");
        }

        // ------------------------------------------------------------------ frames for the morning notes (docs/demo/frames/D-12-*.png)

        static string FramePath(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "demo", "frames", $"D-12-{name}.png"));

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            if (t == null) yield break;
            string path = FramePath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Debug.Log($"[ZD:Frame] D-12 {name} {t.width}x{t.height} → {path}");
            Object.Destroy(t);
        }

        [UnityTest]
        public IEnumerator Frames_of_the_square_and_the_talk()
        {
            yield return Load("region");
            SetHour(11.5);
            var oldMan = View("old_man");
            var at = oldMan.transform.position;
            _hero.Teleport(at + new Vector3(-2.4f, 1f, -2.4f), 45f);
            yield return new WaitForSeconds(1.2f);
            yield return Capture("square-day");

            _s.Tap("npc_old_man");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("talk-stage1");

            yield return Load("region");
            SetHour(11.5);
            _hero.Teleport(View("old_man").transform.position + new Vector3(-2.4f, 1f, -2.4f), 45f);
            _s.State.Language.Restore(0.8f);
            yield return new WaitForSeconds(1.2f);
            _s.Tap("npc_old_man");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("talk-stage3");

            yield return Load("region");
            SetHour(12);
            var shop = Anchor("anchor_shop");
            _hero.Teleport(shop + new Vector3(-2.4f, 1f, -2.4f), 45f);
            yield return new WaitForSeconds(1.2f);
            _s.Tap("npc_merchant");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("talk-trade");
        }
    }
}
