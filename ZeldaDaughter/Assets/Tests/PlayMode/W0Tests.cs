using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>W0 (docs/demo/unity-architecture.md §7): the frame of the Unity layer, in the test scene scenes/test-demo.json.</summary>
    public class W0Tests
    {
        GameSession _s;
        HeroController _hero;
        float _remarkCheck;

        static double Now => Time.realtimeSinceStartupAsDouble;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_s, "test-demo has a GameSession (SceneBuilder)");
            _hero.UseDpi(160f);
            _remarkCheck = _s.State.Data.Session.RemarkCheckSeconds;
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_s != null) _s.State.Data.Session.RemarkCheckSeconds = _remarkCheck; // the data set is shared by all tests
            TestSaves.Clear();
        }

        [UnityTest]
        public IEnumerator StateReady_comes_once_with_the_loaded_state()
        {
            int calls = 0;
            Core.Save.GameState got = null;
            _s.Events.StateReady += g => { calls++; got = g; }; // replayed at once: the order of Start between components does not matter
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, calls);
            Assert.AreSame(_s.State, got);
        }

        [UnityTest]
        public IEnumerator Tap_goes_to_the_handler_of_the_targets_kind()
        {
            string bed = null, station = null, pickup = null;
            _s.OnTap(TapKind.Bed, t => bed = t.Id);
            _s.OnTap(TapKind.Station, t => station = t.Id);
            _s.Tap("bed_test");
            Assert.AreEqual("bed_test", bed);
            Assert.IsNull(station);
            _s.Tap("anvil_test");
            Assert.AreEqual("anvil_test", station);
            _s.Tap("wall_test");  // scenery: no handler, nothing happens
            _s.Tap("no_such_id"); // unknown: nothing happens
            Assert.AreEqual("bed_test", bed);
            Assert.AreEqual("anvil_test", station);

            _s.OnTap(TapKind.Pickup, t => pickup = t.Id);
            _s.Tap("pickup_stick_1"); // the PickupPresenter's handler ran too
            Assert.AreEqual("pickup_stick_1", pickup);
            Assert.AreEqual(1, _s.State.Bag.Count("stick"));
            Assert.IsFalse(_s.Index.Find("pickup_stick_1").gameObject.activeSelf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Picker_prefers_the_enemy_to_the_npc_and_finds_the_hero()
        {
            var picker = Object.FindFirstObjectByType<WorldPicker>();
            var npc = _s.Index.FindTappable("npc_peasant");
            _hero.Teleport(new Vector3(7f, 1f, -26f), 0f); // far from the peasant on the screen too
            yield return null;
            yield return null;

            var enemyGo = new GameObject("enemy_probe");
            enemyGo.transform.position = npc.transform.position;
            var enemy = enemyGo.AddComponent<Tappable>();
            enemy.Configure("enemy_probe", TapKind.Enemy);
            _s.Index.RegisterDynamic(enemy);

            var p = Camera.main.WorldToScreenPoint(npc.AimPoint);
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            Assume.That(Vector2.Distance(p, h), Is.GreaterThan(250f), "the screen is big enough to keep the peasant away from the hero's circle");
            var at = new Vec2(p.x, p.y);

            var hit = picker.Pick(at);
            Assert.AreEqual(TouchHitKind.Object, hit.Kind);
            Assert.AreEqual("enemy_probe", hit.TargetId, "enemy before NPC");
            enemy.Enabled = false;
            Assert.AreEqual("npc_peasant", picker.Pick(at).TargetId, "the NPC when the enemy is gone");
            Assert.AreEqual(TouchHitKind.Hero, picker.Pick(new Vec2(h.x, h.y)).Kind, "the hero's own circle");
            Assert.AreEqual(TouchHitKind.Ground, picker.Pick(new Vec2(p.x + 900f, p.y - 900f)).Kind, "open ground");
            _s.Index.UnregisterDynamic(enemy);
            Object.Destroy(enemyGo);
        }

        [UnityTest]
        public IEnumerator Telling_the_peasant_about_the_town_opens_the_mark_on_the_map_and_a_note()
        {
            Assert.IsFalse(_s.State.Map.IsOpen("town"));
            _s.Tap("npc_peasant");
            yield return null;
            Assert.IsTrue(_s.UI.ReplyButtons.Count > 0);
            for (int i = 0; i < 20 && _s.UI.ReplyButtons.Count > 0; i++)
            {
                var town = _s.UI.ReplyButtons.FirstOrDefault(b => b.name == "Reply_town") ?? _s.UI.ReplyButtons[0];
                town.onClick.Invoke();
                yield return null;
            }
            Assert.IsTrue(_s.State.Map.IsOpen("town"), "g.Talk applies the effects of the lines (a mark)");
            Assert.IsTrue(_s.State.Notebook.Has("peasant_town"), "…and the notes");
        }

        /// <summary>One swipe up-right on the screen (towards +x on the ground) for the seconds; reports the ground distance covered.</summary>
        IEnumerator Swipe(float seconds, System.Action<float> distance)
        {
            var from = _hero.transform.position;
            var h = Camera.main.WorldToScreenPoint(from);
            var o = new Vec2(h.x, h.y - 250f);
            var at = new Vec2(o.X + 28, o.Y + 28);
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Now, o, TouchHit.Ground));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Now, at, default));
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                _hero.Feed(new TouchSample(0, TouchPhase.Stationary, Now, at, default));
                yield return null;
            }
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Now, at, default));
            yield return null;
            var d = _hero.transform.position - from;
            distance(new Vector2(d.x, d.z).magnitude);
        }

        [UnityTest]
        public IEnumerator A_broken_leg_shortens_the_same_swipe()
        {
            float whole = 0, hurt = 0;
            yield return Swipe(1.5f, d => whole = d);
            _hero.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return new WaitForSeconds(0.2f);
            _s.State.Condition.Wound(WoundType.Fracture, 1.0f);
            yield return Swipe(1.5f, d => hurt = d);
            Debug.Log($"[ZD:Test] swipe 1.5 s: whole {whole:0.00} m, fracture 1.0 {hurt:0.00} m");
            Assert.Greater(whole, 2.0f, "she walked");
            Assert.Less(hurt, whole * 0.75f, "the limp slows her (fracture ×0.5)");
        }

        [UnityTest]
        public IEnumerator A_campfire_with_little_left_burns_out_and_the_world_says_so()
        {
            var seen = new List<WorldEvent>();
            _s.Events.World += seen.Add;
            var pos = new Vec2(30f, 30f);
            _s.State.Camp.Restore(1, null, new[] { new Campfire("fire_probe", pos, 0.3f, _s.State.Data.Camp.FadeSeconds) });
            Assert.IsTrue(_s.State.Camp.Campfires.Count == 1 && _s.State.Camp.Campfires[0].IsLit);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(seen.Any(e => e.Kind == WorldEventKind.CampfireBurntOut && e.Id == "fire_probe"), "events: " + string.Join(", ", seen));
        }

        sealed class ProbeWindow : IWindow
        {
            public string Id => "probe";
            public RectTransform Root { get; }
            public int Opened, Closed;
            public ProbeWindow(Transform parent) { Root = new GameObject("ProbeWindow", typeof(RectTransform)).GetComponent<RectTransform>(); Root.SetParent(parent, false); Root.sizeDelta = new Vector2(400, 300); }
            public void OnOpened() => Opened++;
            public void OnClosed() => Closed++;
        }

        [UnityTest]
        public IEnumerator An_open_window_stops_the_hero_and_closing_it_lets_her_go()
        {
            var stack = Object.FindFirstObjectByType<WindowStack>();
            var seen = new List<string>();
            _s.Events.WindowOpened += id => seen.Add("open " + id);
            _s.Events.WindowClosed += id => seen.Add("close " + id);
            var w = new ProbeWindow(_s.UI.Windows);
            stack.Open(w);
            yield return null;
            Assert.IsTrue(_hero.Locked);
            Assert.IsTrue(stack.IsOpen("probe"));
            var before = _hero.transform.position;
            yield return Swipe(0.6f, d => { });
            Assert.Less(Vector3.Distance(_hero.transform.position, before), 0.05f, "no walking under a window");
            stack.Close();
            Assert.IsFalse(_hero.Locked);
            CollectionAssert.AreEqual(new[] { "open probe", "close probe" }, seen);
            Assert.AreEqual(1, w.Opened);
            Assert.AreEqual(1, w.Closed);
        }

        [UnityTest]
        public IEnumerator The_fader_goes_dark_and_clear()
        {
            var fader = Object.FindFirstObjectByType<ScreenFader>();
            Assert.AreEqual(0f, fader.Alpha, 1e-3f);
            fader.FadeTo(1f, 0.2f);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1f, fader.Alpha, 1e-3f);
            bool dark = false, done = false;
            fader.Sleep(() => dark = fader.Alpha > 0.99f, () => done = true, 0.1f, 0.05f, 0.1f);
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(dark, "the time jump happens behind the black");
            Assert.IsTrue(done);
            Assert.AreEqual(0f, fader.Alpha, 1e-3f);
        }

        /// <summary>Managed bytes allocated per frame over <paramref name="frames"/> frames of standing still (the profiler's «GC Allocated In Frame»).</summary>
        static IEnumerator Allocated(int frames, System.Action<long, long> result)
        {
            long total = 0, worst = 0;
            using (var rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    total += rec.LastValue;
                    if (rec.LastValue > worst) worst = rec.LastValue;
                }
            }
            result(total, worst);
        }

        sealed class Allocator : MonoBehaviour
        {
            public static object Sink;
            private void Update() { Sink = new byte[100]; }
        }

        [UnityTest]
        public IEnumerator The_script_phase_counter_sees_an_allocating_update()
        {
            // the idle test below is only worth something if the counter it relies on would notice a leak
            for (int i = 0; i < 10; i++) yield return null;
            var go = new GameObject("Allocator", typeof(Allocator));
            ScriptPhaseAlloc.Start();
            long s0 = ScriptPhaseAlloc.Total;
            for (int i = 0; i < 20; i++) yield return null;
            long got = ScriptPhaseAlloc.Total - s0;
            Object.Destroy(go);
            ScriptPhaseAlloc.Stop();
            Debug.Log($"[ZD:Test] script phases wrapped {ScriptPhaseAlloc.Wrapped}, begin calls {ScriptPhaseAlloc.Calls}, bytes {got}, recorder bytes {ScriptPhaseAlloc.RecTotal}");
            Assert.GreaterOrEqual(got, 20 * 100, "20 frames x a 100-byte array in Update");
        }

        [UnityTest]
        public IEnumerator Idle_frames_do_not_allocate()
        {
            // Remarks.ConditionTopics (core) builds a list per call, once a second: reported to package C, kept out of this measurement.
            _s.State.Data.Session.RemarkCheckSeconds = 1000f;
            yield return new WaitForSeconds(1.3f);
            for (int i = 0; i < 60; i++) yield return null; // warm-up: first-use allocations (UI text, caches) are not the steady state

            // The profiler's per-frame counter carries the editor's own allocations (~300 KB a frame on 2026-10-08: the bridge, the Game view, the test runner), which swing by
            // hundreds of KB between two windows - against a limit of 512 B. The assertion is on the bytes allocated inside the scripts' frame phases
            // (Update / LateUpdate / FixedUpdate), counted on the main thread (ScriptPhaseAlloc); the profiler counter is only logged. Hold the game to the difference
            // with every ZeldaDaughter component switched off.
            long withGame = 0, worstWith = 0, bare = 0, worstBare = 0, scriptsWith = 0, scriptsBare = 0;
            ScriptPhaseAlloc.Start();
            try
            {
                long s0 = ScriptPhaseAlloc.Total;
                yield return Allocated(120, (t, w) => { withGame = t; worstWith = w; });
                scriptsWith = ScriptPhaseAlloc.Total - s0;
                var off = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .Where(m => m.enabled && m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("ZeldaDaughter")).ToList();
                foreach (var m in off) m.enabled = false;
                for (int i = 0; i < 10; i++) yield return null;
                s0 = ScriptPhaseAlloc.Total;
                yield return Allocated(120, (t, w) => { bare = t; worstBare = w; });
                scriptsBare = ScriptPhaseAlloc.Total - s0;
                foreach (var m in off) m.enabled = true;
                Debug.Log($"[ZD:Test] GC allocated in 120 idle frames: scripts' phases game on {scriptsWith} B, game off {scriptsBare} B; whole frame (with the editor) game on {withGame} B (worst frame {worstWith}), game off {bare} B (worst frame {worstBare}), {off.Count} components");
            }
            finally { ScriptPhaseAlloc.Stop(); }
            Assert.LessOrEqual(scriptsWith - scriptsBare, 512, "GC bytes the game's scripts allocated over 120 idle frames (over the engine's own)");
        }
    }
}
