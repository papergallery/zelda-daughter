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
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-13 (docs/demo/unity-architecture.md §7): the fight in the scene — the boar of scenes/test-demo.json, night wolves, carcasses, the kill that stays.</summary>
    public class D13CombatTests
    {
        const string Boar = "spawn_boar";

        GameSession _s;
        HeroController _hero;
        CombatPresenter _combat;
        readonly List<string> _log = new List<string>();

        void OnLog(string message, string stack, LogType type) => _log.Add(message);

        IEnumerator LoadScene(string path)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _combat = Object.FindFirstObjectByType<CombatPresenter>();
            Assert.NotNull(_s, "a GameSession (SceneBuilder)");
            Assert.NotNull(_combat, "a CombatPresenter (SceneBuilder.Combat)");
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.3f);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _log.Clear();
            Application.logMessageReceived += OnLog;
            yield return LoadScene("Assets/Scenes/test-demo.unity");
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            TestSaves.Clear();
        }

        /// <summary>The hero stands <paramref name="meters"/> from the enemy (on the +x side) and the session has seen it.</summary>
        IEnumerator HeroBeside(Enemy e, float meters)
        {
            _hero.Teleport(new Vector3(e.Position.X + meters, 1f, e.Position.Y), 0f);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_boar_is_in_the_scene_with_a_view_and_a_tap_target()
        {
            var e = _s.State.Enemies.Get(Boar);
            Assert.NotNull(e, "the spawn marker made a boar");
            Assert.AreEqual("boar", e.DefId);
            var view = _combat.FindEnemy(Boar);
            Assert.NotNull(view, "…and a view");
            var t = _s.Index.FindTappable(Boar);
            Assert.NotNull(t);
            Assert.AreEqual(ZeldaDaughter.Input.TapKind.Enemy, t.Kind);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tap_on_the_boar_in_reach_strikes_and_logs_hit_or_miss()
        {
            var e = _s.State.Enemies.Get(Boar);
            yield return HeroBeside(e, 0.7f);
            float hp = e.Hp;
            var struck = new List<StrikeOutcome>();
            _s.Events.HeroStruck += (id, r) => struck.Add(r.Outcome);
            var acts = new List<HeroActKind>();
            _s.Events.HeroActed += a => acts.Add(a.Kind);

            _s.Tap(Boar);

            Assert.IsTrue(_log.Any(l => l.StartsWith("[ZD:Combat] hit " + Boar) || l.StartsWith("[ZD:Combat] miss " + Boar)), "[ZD:Combat] hit|miss");
            Assert.AreEqual(1, struck.Count);
            Assert.Contains(HeroActKind.Strike, acts);
            Assert.Less(e.Hp, hp, "even a miss scratches; the blow was dealt");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tap_on_a_far_boar_turns_the_hero_and_does_not_strike()
        {
            var e = _s.State.Enemies.Get(Boar);
            _hero.Teleport(new Vector3(e.Position.X, 1f, e.Position.Y + 6f), 0f); // 6 m north: the boar is to the south
            yield return null;
            yield return null;
            float hp = e.Hp;
            var from = _hero.transform.position;
            bool outOfRange = false;
            _s.Events.EnemyOutOfRange += id => outOfRange = id == Boar;

            _s.Tap(Boar);
            yield return null;

            Assert.IsTrue(outOfRange);
            Assert.IsTrue(_log.Any(l => l.Contains("[ZD:Combat] out_of_range " + Boar)));
            Assert.AreEqual(hp, e.Hp, 1e-4f, "no blow");
            Assert.Greater(Vector3.Dot(_hero.transform.forward, new Vector3(e.Position.X - from.x, 0f, e.Position.Y - from.z).normalized), 0.9f, "she faces the boar");
            Assert.AreEqual(from.x, _hero.transform.position.x, 0.05f, "…and does not walk");
        }

        [UnityTest]
        public IEnumerator The_windup_shows_trembling_a_crouch_and_a_growing_ring_and_lasts_long_enough()
        {
            var e = _s.State.Enemies.Get(Boar);
            var view = _combat.FindEnemy(Boar);
            yield return HeroBeside(e, 0.7f);
            double started = -1, ended = -1;
            _s.Events.Enemy += n =>
            {
                if (n.EnemyId != Boar) return;
                if (n.Event.Kind == EnemyEventKind.WindupStarted) started = Time.timeAsDouble;
                if (n.Event.Kind == EnemyEventKind.Struck || n.Event.Kind == EnemyEventKind.Dodged) ended = Time.timeAsDouble;
            };
            e.Provoke();

            float limit = Time.time + 4f;
            while (e.State != EnemyState.Windup && Time.time < limit) yield return null;
            Assert.AreEqual(EnemyState.Windup, e.State, "the provoked boar winds up");
            while (e.WindupProgress < 0.4f && e.State == EnemyState.Windup) yield return null;

            var pose = view.Sprite.Pose;
            Assert.Greater(pose.Shake, 0f, "trembling");
            Assert.Greater(pose.Crouch, 0f, "crouching");
            Assert.AreNotEqual(0f, pose.TiltDegrees, "leaning back");
            Assert.IsTrue(view.RingVisible);
            Assert.Greater(view.RingScale, 1.05f, "the shadow grows");

            while (ended < 0 && Time.time < limit + 2f) yield return null;
            Assert.GreaterOrEqual(ended - started, 0.6 - 0.02, "readable for at least 0.6 s");
        }

        [UnityTest]
        public IEnumerator Running_away_during_the_windup_is_a_dodge_and_agility_grows()
        {
            var e = _s.State.Enemies.Get(Boar);
            yield return HeroBeside(e, 0.7f);
            bool dodged = false, struck = false;
            _s.Events.Enemy += n =>
            {
                if (n.EnemyId != Boar) return;
                dodged |= n.Event.Kind == EnemyEventKind.Dodged;
                struck |= n.Event.Kind == EnemyEventKind.Struck;
            };
            float agility = _s.State.Skills.Get(Stat.Agility);
            e.Provoke();
            float limit = Time.time + 4f;
            while (e.State != EnemyState.Windup && Time.time < limit) yield return null;
            Assert.AreEqual(EnemyState.Windup, e.State);
            _hero.Teleport(new Vector3(e.Position.X + 5f, 1f, e.Position.Y), 0f); // out of the blow's reach
            while (e.State == EnemyState.Windup && Time.time < limit + 2f) yield return null;
            yield return null;

            Assert.IsTrue(dodged, "Dodged");
            Assert.IsFalse(struck);
            Assert.Greater(_s.State.Skills.Get(Stat.Agility), agility, "agility grows");
        }

        [UnityTest]
        public IEnumerator A_killed_boar_leaves_a_carcass_bare_hands_take_the_minimum_the_knife_takes_all()
        {
            var g = _s.State;
            var e = g.Enemies.Get(Boar);
            var where = e.Position;
            e.Receive(1000f, null, 0f, 0f);
            yield return new WaitForSeconds(0.3f); // the next enemy step removes it and leaves a carcass
            Assert.IsNull(g.Enemies.Get(Boar));
            Assert.IsNull(_combat.FindEnemy(Boar), "the enemy view is gone");
            var carcass = _combat.FindCarcass(Boar);
            Assert.NotNull(carcass, "the carcass lies where it fell");
            Assert.IsTrue(g.Killed.Contains(Boar));
            _hero.Teleport(new Vector3(where.X + 1f, 1f, where.Y), 0f);
            yield return null;
            yield return null;

            _s.Tap(Boar);
            Assert.AreEqual(1, g.Bag.Count("fang"), "bare hands: the fang");
            Assert.AreEqual(0, g.Bag.Count("meat"));
            Assert.NotNull(_combat.FindCarcass(Boar), "still lying");
            _s.Tap(Boar);
            Assert.AreEqual(1, g.Bag.Count("fang"), "the minimum once");

            Assert.IsTrue(g.Bag.Add("knife"));
            _s.Tap(Boar);
            yield return null;
            Assert.AreEqual(3, g.Bag.Count("meat"), "the knife: the full set");
            Assert.AreEqual(2, g.Bag.Count("fang"), "…the fang is not given twice");
            Assert.IsNull(_combat.FindCarcass(Boar), "…and the carcass is gone");
            Assert.AreEqual(0, g.Carcasses.Active.Count);
        }

        [UnityTest]
        public IEnumerator Night_wolves_come_not_nearer_than_the_minimum_distance_and_have_views()
        {
            var g = _s.State;
            g.Clock.SetTime(g.Clock.Day, 0.0); // midnight
            float min = g.Data.Night.MinHeroDistance;
            for (int i = 0; i < 160; i++) g.TickWorld(0.25f, _s.Rolls.World.Next()); // 40 s: the first wolf is due after 20
            var wolves = g.Enemies.Active.Where(e => e.DefId == "wolf").ToList();
            Assert.GreaterOrEqual(wolves.Count, 1, "the dark called a wolf");
            foreach (var w in wolves)
                Assert.GreaterOrEqual((w.Position - g.HeroPosition).Length, min - 0.01f, w.Id + " is not nearer than minHeroDistance");
            yield return null;
            foreach (var w in wolves) Assert.NotNull(_combat.FindEnemy(w.Id), w.Id + " has a view");
        }

        [UnityTest]
        public IEnumerator Enemies_do_not_stand_in_walls()
        {
            var blocked = _s.State.Enemies.Blocked;
            Assert.NotNull(blocked);
            Assert.IsTrue(blocked(new Vec2(-22f, 0f)), "inside the wall");
            Assert.IsFalse(blocked(new Vec2(-10f, -30f)), "open ground");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_boar_killed_in_the_region_does_not_come_back_after_a_reload()
        {
            yield return LoadScene("Assets/Scenes/region.unity");
            var g = _s.State;
            var e = g.Enemies.Get(Boar);
            Assert.NotNull(e, "the region has the boar's den");
            e.Receive(1000f, null, 0f, 0f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(g.Killed.Contains(Boar));
            _s.Save("test");

            yield return LoadScene("Assets/Scenes/region.unity");
            Assert.IsTrue(_s.State.Killed.Contains(Boar), "the kill is in the save");
            Assert.IsNull(_s.State.Enemies.Get(Boar), "the boar is not spawned again");
            Assert.IsNull(_combat.FindEnemy(Boar));
            Assert.NotNull(_combat.FindCarcass(Boar), "…its carcass lies where it fell");
        }

        /// <summary>A phone-shaped frame (1080×2340) from the game camera, close in, into docs/demo/frames/ (the proof of the done-criteria).</summary>
        IEnumerator Frame(string name, float ortho)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var iso = cam.GetComponent<ZeldaDaughter.World.IsoCamera>();
            if (iso != null) iso.SnapToTarget();
            float size = cam.orthographicSize, aspect = cam.aspect;
            var prev = cam.targetTexture;
            var rt = new RenderTexture(1080, 2340, 24);
            cam.orthographicSize = ortho;
            cam.targetTexture = rt;
            cam.aspect = 1080f / 2340f;
            cam.Render(); // the first render after a load can come out empty
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            tex.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "demo", "frames"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.orthographicSize = size;
            cam.aspect = aspect;
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            ZdLog.Info("Frame", "D-13 " + name);
        }

        [UnityTest]
        public IEnumerator Frames_of_the_boars_windup_and_of_its_carcass()
        {
            yield return LoadScene("Assets/Scenes/region.unity");
            var g = _s.State;
            var e = g.Enemies.Get(Boar);
            Assert.NotNull(e);
            _hero.Teleport(new Vector3(e.Position.X + 1.4f, 1f, e.Position.Y), 0f);
            yield return null;
            yield return null;
            e.Provoke();
            float limit = Time.time + 5f;
            while (e.State != EnemyState.Windup && Time.time < limit) yield return null;
            while (e.WindupProgress < 0.75f && e.State == EnemyState.Windup && Time.time < limit) yield return null;
            Assume.That(e.State, Is.EqualTo(EnemyState.Windup), "caught the windup");
            yield return Frame("D-13-windup.png", 3.2f);

            e.Receive(1000f, null, 0f, 0f);
            yield return new WaitForSeconds(0.3f);
            Assert.NotNull(_combat.FindCarcass(Boar));
            yield return Frame("D-13-carcass.png", 3.2f);
        }
    }
}
