using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-11 (docs/demo/unity-architecture.md §7, docs/done-criteria/D-11.md): the hero as a figure. The hero is walked by moving her
    /// character controller by hand — HeroView reads the way she really went, as it does for a swipe.
    /// </summary>
    public class D11HeroViewTests
    {
        GameSession _s;
        HeroController _hero;
        HeroView _view;
        BillboardSprite _sprite;
        ScreenFader _fader;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Grab();
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.3f);
        }

        void Grab()
        {
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_s, "the scene has a GameSession");
            _view = _hero.GetComponent<HeroView>();
            Assert.NotNull(_view, "SceneBuilder.AddHeroView put a HeroView on the hero");
            _sprite = _view.Sprite;
            _fader = Object.FindFirstObjectByType<ScreenFader>();
        }

        [TearDown] public void TearDown() => TestSaves.Clear();

        static Vector3 CamForward() { var f = Camera.main.transform.forward; f.y = 0f; return f.normalized; }
        static Vector3 CamRight() { var r = Camera.main.transform.right; r.y = 0f; return r.normalized; }

        /// <summary>Walk the hero <paramref name="meters"/> along a direction at a speed (by time, so the pace is steady).</summary>
        IEnumerator Walk(Vector3 dir, float meters, float speed = 1.6f)
        {
            var cc = _hero.GetComponent<CharacterController>();
            cc.minMoveDistance = 0f; // a slow step at a high frame rate is under the default 1 mm and would be dropped
            dir.y = 0f;
            dir.Normalize();
            float left = meters;
            while (left > 0f)
            {
                float step = Mathf.Min(left, speed * Time.deltaTime);
                cc.Move(dir * step);
                left -= step;
                yield return null;
            }
        }

        IEnumerator Settle(int frames = 2) { for (int i = 0; i < frames; i++) yield return null; }

        // ------------------------------------------------------------------ criterion 1: the way she faces

        [UnityTest]
        public IEnumerator She_faces_the_way_she_goes_the_side_view_mirrored_for_the_left()
        {
            yield return Walk(-CamForward(), 0.6f); yield return Settle();
            Assert.AreEqual(Facing.Front, _sprite.Facing, "towards the camera");
            yield return Walk(CamForward(), 0.6f); yield return Settle();
            Assert.AreEqual(Facing.Back, _sprite.Facing, "away from the camera");
            yield return Walk(CamRight(), 0.6f); yield return Settle();
            Assert.AreEqual(Facing.Side, _sprite.Facing);
            Assert.IsFalse(_sprite.Mirrored, "right: the drawn side view as it is");
            yield return Walk(-CamRight(), 0.6f); yield return Settle();
            Assert.AreEqual(Facing.Side, _sprite.Facing);
            Assert.IsTrue(_sprite.Mirrored, "left: the mirror");
        }

        [UnityTest]
        public IEnumerator A_walk_along_a_diagonal_does_not_flicker_between_the_views()
        {
            var cc = _hero.GetComponent<CharacterController>();
            // in the side view, then the direction wobbles around the diagonal
            yield return Walk(CamRight(), 0.5f); yield return Settle();
            Assert.AreEqual(Facing.Side, _sprite.Facing);
            int changes = 0;
            var last = _sprite.Facing;
            for (int i = 0; i < 40; i++)
            {
                float y = i % 2 == 0 ? 1.1f : 0.9f;
                cc.Move((CamRight() + CamForward() * y).normalized * 0.03f);
                yield return null;
                if (_sprite.Facing != last) { changes++; last = _sprite.Facing; }
            }
            Assert.AreEqual(0, changes, "a wobble of 10 % around the diagonal changes nothing (hysteresis)");

            // and from the front view the same wobble keeps the front
            yield return Walk(-CamForward(), 0.5f); yield return Settle();
            Assert.AreEqual(Facing.Front, _sprite.Facing);
            last = _sprite.Facing;
            for (int i = 0; i < 40; i++)
            {
                float y = i % 2 == 0 ? 1.1f : 0.9f;
                cc.Move((CamRight() - CamForward() * y).normalized * 0.03f);
                yield return null;
                if (_sprite.Facing != last) { changes++; last = _sprite.Facing; }
            }
            Assert.AreEqual(0, changes);
        }

        // ------------------------------------------------------------------ criterion 2: the step

        [UnityTest]
        public IEnumerator The_frame_follows_the_path_walked_not_the_clock_and_stands_still_when_she_does()
        {
            Assert.AreEqual(0, _sprite.FrameIndex);
            yield return Walk(CamRight(), 0.5f, 3.0f); yield return Settle(1);   // 0.5 m fast
            int fast = _sprite.FrameIndex;
            yield return new WaitForSeconds(0.4f);                              // stands: back to the first frame
            Assert.AreEqual(0, _sprite.FrameIndex, "standing: the first frame");
            yield return Walk(CamRight(), 0.5f, 0.7f); yield return Settle(1);  // 0.5 m slowly
            int slow = _sprite.FrameIndex;
            Assert.AreEqual(fast, slow, "the same path — the same frame, whatever the speed");
            Assert.Greater(fast, 0, "half a metre of a 0.8 m stride is past the first frame");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, _sprite.FrameIndex);
            var seen = new HashSet<int>();
            for (int i = 0; i < 30; i++) { seen.Add(_sprite.FrameIndex); yield return null; }
            Assert.AreEqual(1, seen.Count, "standing: the frame does not change");
        }

        [UnityTest]
        public IEnumerator A_step_bounces_a_little_and_a_stand_does_not()
        {
            Assert.AreEqual(0f, _sprite.Pose.Lift, 1e-5f);
            float max = 0f;
            var cc = _hero.GetComponent<CharacterController>();
            for (int i = 0; i < 60; i++)
            {
                cc.Move(CamRight() * 1.6f * Time.deltaTime);
                yield return null;
                max = Mathf.Max(max, _sprite.Pose.Lift);
            }
            Assert.Greater(max, 0.01f, "a bounce while walking");
            Assert.Less(max, 0.1f, "a light one");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, _sprite.Pose.Lift, 1e-5f);
        }

        [UnityTest]
        public IEnumerator A_fracture_makes_the_steps_uneven_and_a_healthy_walk_is_even()
        {
            var healthy = new List<float>();
            void OnStep(Vector3 p, bool limp) => healthy.Add(Time.time);
            _s.Events.HeroStep += OnStep;
            yield return Walk(CamRight(), 3.6f);
            _s.Events.HeroStep -= OnStep;
            float healthyRatio = Ratio(healthy);

            yield return new WaitForSeconds(0.3f);
            _hero.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return new WaitForSeconds(0.2f);
            _s.State.Condition.Wound(WoundType.Fracture, 1f);
            yield return null;
            var hurt = new List<float>();
            bool flag = false;
            void OnStepHurt(Vector3 p, bool limp) { hurt.Add(Time.time); flag |= limp; }
            _s.Events.HeroStep += OnStepHurt;
            yield return Walk(CamRight(), 3.6f);
            _s.Events.HeroStep -= OnStepHurt;
            float hurtRatio = Ratio(hurt);
            Debug.Log($"[ZD:Test] steps healthy={healthy.Count} ratio {healthyRatio:0.00}; fracture={hurt.Count} ratio {hurtRatio:0.00}");

            Assert.GreaterOrEqual(healthy.Count, 6);
            Assert.GreaterOrEqual(hurt.Count, 6);
            Assert.IsTrue(flag, "the step event tells she limps");
            Assert.Less(healthyRatio, 1.3f, "healthy: even steps");
            Assert.Greater(hurtRatio, 1.8f, "a limp: long and short steps (the rhythm is broken)");
        }

        /// <summary>Longest gap between steps over the shortest (the first one is skipped: it starts from the stand).</summary>
        static float Ratio(List<float> times)
        {
            float lo = float.MaxValue, hi = 0f;
            for (int i = 2; i < times.Count; i++)
            {
                float d = times[i] - times[i - 1];
                lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d);
            }
            return hi / Mathf.Max(lo, 1e-4f);
        }

        [Test]
        public void The_limp_warp_keeps_whole_steps_and_is_even_when_healthy()
        {
            Assert.AreEqual(1.7f, HeroView.Warp(1.7f, 0f), 1e-6f);
            Assert.AreEqual(0f, HeroView.Warp(0f, 0.4f), 1e-6f);
            Assert.AreEqual(1f, HeroView.Warp(1.4f, 0.4f), 1e-5f, "the long step ends at 1.4 of a half-stride");
            Assert.AreEqual(2f, HeroView.Warp(2f, 0.4f), 1e-5f);
            Assert.AreEqual(3f, HeroView.Warp(3.4f, 0.4f), 1e-5f);
        }

        // ------------------------------------------------------------------ criterion 3: the hands

        [UnityTest]
        public IEnumerator Every_action_shows_for_at_least_a_third_of_a_second()
        {
            var cases = new[] { HeroActKind.Strike, HeroActKind.Pickup, HeroActKind.Eat, HeroActKind.Treat, HeroActKind.Butcher, HeroActKind.Place, HeroActKind.Craft };
            foreach (var kind in cases)
            {
                yield return new WaitForSeconds(0.1f);
                Assert.IsFalse(_view.Acting);
                var target = _hero.transform.position + CamRight() * 2f;
                _s.Events.RaiseHeroActed(new HeroAct(kind, kind == HeroActKind.Strike ? target : default));
                yield return null;
                Assert.IsTrue(_view.Acting, kind + " begins");
                float t0 = Time.time, peak = 0f;
                while (_view.Acting && Time.time - t0 < 2f)
                {
                    var p = _sprite.Pose;
                    peak = Mathf.Max(peak, Mathf.Abs(p.TiltDegrees) + p.Crouch * 40f + p.Shake * 400f);
                    yield return null;
                }
                float shown = Time.time - t0;
                Assert.GreaterOrEqual(shown, 0.3f, kind + " is seen long enough");
                Assert.Less(shown, 1.5f);
                Assert.Greater(peak, 4f, kind + " has a pose");
            }
        }

        [UnityTest]
        public IEnumerator A_blow_leans_towards_the_target_a_pickup_crouches()
        {
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, _hero.transform.position + CamRight() * 2f));
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(_sprite.Pose.TiltDegrees, 5f, "to the right of the screen");
            yield return new WaitForSeconds(0.5f);
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, _hero.transform.position - CamRight() * 2f));
            yield return new WaitForSeconds(0.2f);
            Assert.Less(_sprite.Pose.TiltDegrees, -5f, "to the left");
            yield return new WaitForSeconds(0.6f);

            _s.Tap("pickup_stick_1");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(HeroActKind.Pickup, _view.CurrentAct);
            Assert.Greater(_sprite.Pose.Crouch, 0.3f, "she crouches to the thing");
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(_view.Acting);
            Assert.AreEqual(0f, _sprite.Pose.Crouch, 0.02f);
        }

        // ------------------------------------------------------------------ criterion 6: the drawn poses of D-09

        string Shown() => _sprite.CurrentSprite != null ? _sprite.CurrentSprite.name : "";

        [UnityTest]
        public IEnumerator The_hands_show_the_drawn_poses_a_blow_in_two_frames_and_mirrored_to_the_left()
        {
            Assume.That(_sprite.HasPose("attack"), "the registry has the D-09 poses");
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, _hero.transform.position + CamRight() * 2f));
            var seen = new List<string>();
            for (int i = 0; i < 40 && _view.Acting; i++) { yield return null; if (Shown() != (seen.Count > 0 ? seen[seen.Count - 1] : null)) seen.Add(Shown()); }
            Assert.AreEqual(new[] { "heroine_side_attack_0", "heroine_side_attack_1" }, seen.GetRange(0, Mathf.Min(2, seen.Count)).ToArray(), "wind-up, then the thrust");
            yield return new WaitForSeconds(0.7f);

            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, _hero.transform.position - CamRight() * 2f));
            yield return new WaitForSeconds(0.1f);
            StringAssert.StartsWith("heroine_side_attack", Shown());
            Assert.Less(_sprite.Card.localScale.x, 0f, "to the left of the screen: the drawing is mirrored");
            Assert.Less(Quaternion.Angle(_sprite.Card.rotation, Camera.main.transform.rotation), 0.5f, "a drawn pose is not leaned on top");
            yield return new WaitForSeconds(0.7f);

            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Pickup));
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual("heroine_front_pickup_0", Shown());
            Assert.Greater(_sprite.Card.localScale.x, 0f, "the front view is never mirrored");
            yield return new WaitForSeconds(0.7f);
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Eat));
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual("heroine_front_eat_0", Shown());
            yield return new WaitForSeconds(1.0f);
            StringAssert.DoesNotContain("eat", Shown());
        }

        [UnityTest]
        public IEnumerator A_wounded_hero_holds_her_side_standing_and_walks_on_the_step_frames_a_knockout_lies_on_the_drawn_frame()
        {
            Assume.That(_sprite.HasPose("hurt"), "the registry has the D-09 poses");
            _s.State.Condition.Wound(WoundType.Cut, 0.8f);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual("heroine_front_hurt_0", Shown(), "standing with a cut: the hand at the side");
            yield return Walk(CamRight(), 0.6f);
            StringAssert.StartsWith("heroine_side_", Shown());
            StringAssert.DoesNotContain("hurt", Shown(), "walking: the step");
            _s.State.Condition.Treat("bandage");

            _s.State.Condition.Damage(10000f);
            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(_view.IsDown);
            Assert.AreEqual("heroine_side_down_0", Shown(), "the knockout: the drawn lying frame");
            Assert.Less(Quaternion.Angle(_sprite.Card.rotation, Camera.main.transform.rotation), 0.5f, "not turned: it is drawn lying");
        }

        // ------------------------------------------------------------------ criterion 4: wounds without numbers

        [UnityTest]
        public IEnumerator Wounds_tint_the_clothes_a_cut_reddish_a_burn_dark_poison_greenish_hunger_nothing()
        {
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(Color.white, _sprite.Tint, "healthy: untouched");
            var okPose = _sprite.Pose;
            Assert.AreEqual(0f, okPose.TiltDegrees, 1e-4f);

            _s.State.Condition.Wound(WoundType.Cut, 0.8f);
            yield return new WaitForSeconds(0.9f);
            var cut = _sprite.Tint;
            Assert.Greater(cut.r, cut.g + 0.2f, "reddish");
            Assert.Greater(cut.r, cut.b + 0.2f);
            Assert.Less(_sprite.Pose.TiltDegrees, -1f, "a hand at the side: she leans on it");
            Assert.Greater(_sprite.Pose.Crouch, 0.01f);

            _s.State.Condition.Treat("bandage");
            _s.State.Condition.Wound(WoundType.Burn, 0.8f);
            yield return new WaitForSeconds(0.9f);
            var burn = _sprite.Tint;
            Assert.Less(burn.r + burn.g + burn.b, 2.0f, "dark");
            Assert.Less(Mathf.Abs(burn.r - burn.g), 0.15f, "not coloured, dark");

            _s.State.Condition.Treat("burn_salve");
            _s.State.Condition.Wound(WoundType.Poison, 0.8f);
            yield return new WaitForSeconds(0.9f);
            var poison = _sprite.Tint;
            Assert.Greater(poison.g, poison.r + 0.15f, "greenish");
            Assert.Greater(poison.g, poison.b + 0.15f);

            _s.State.Condition.Treat("antidote");
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(1f, _sprite.Tint.r, 0.03f, "healed: the colour comes back");
            Assert.AreEqual(1f, _sprite.Tint.g, 0.03f);
        }

        [UnityTest]
        public IEnumerator A_blow_to_the_hero_makes_her_tremble()
        {
            _s.Events.RaiseEnemy(new ZeldaDaughter.Core.Combat.EnemyNotice("boar_1",
                new ZeldaDaughter.Core.Combat.EnemyEvent(ZeldaDaughter.Core.Combat.EnemyEventKind.Struck, 10f, null)));
            yield return null;
            yield return null;
            Assert.Greater(_sprite.Pose.Shake, 0.01f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, _sprite.Pose.Shake, 1e-4f);
        }

        // ------------------------------------------------------------------ criterion 5: the knockout

        [UnityTest]
        public IEnumerator A_knockout_lays_her_down_the_screen_goes_dark_with_a_glimpse_and_she_gets_up()
        {
            Assert.AreEqual(0f, _fader.Alpha, 1e-4f);
            var downs = new List<bool>();
            _s.Events.HeroDown += downs.Add;
            _s.State.Condition.Damage(10000f); // the core's knockout; the view reads it from the state
            yield return null;
            yield return null;
            float t0 = Time.time;
            bool wentDark = false, lay = false, locked = false;
            int dips = 0;
            bool wasDark = false;
            while (_view.IsDown && Time.time - t0 < 9f)
            {
                float a = _fader.Alpha;
                if (a > 0.8f) { wentDark = true; wasDark = true; }
                else if (wasDark && a < 0.8f) { dips++; wasDark = false; } // a glimpse
                lay |= _sprite.Pose.Lying;
                locked |= _hero.Locked;
                yield return null;
            }
            Debug.Log($"[ZD:Test] knockout: dark={wentDark} lay={lay} glimpses={dips} for {Time.time - t0:0.0} s");
            Assert.IsTrue(wentDark, "the screen goes darker than 0.8");
            Assert.IsTrue(lay, "she lies");
            Assert.IsTrue(locked, "and does not walk");
            Assert.GreaterOrEqual(dips, 1, "at least one glimpse");
            Assert.LessOrEqual(dips, 3, "at most three");
            Assert.Greater(downs.Count, 0);

            yield return new WaitForSeconds(1.0f);
            Assert.IsFalse(_view.IsDown, "she got up");
            Assert.IsFalse(_sprite.Pose.Lying);
            Assert.AreEqual(0f, _fader.Alpha, 0.01f, "the eyes are open: the dark is gone");
            Assert.IsFalse(_hero.Locked, "she can walk");
            Assert.IsFalse(_s.State.Condition.IsKnockedOut);
        }

        [UnityTest]
        public IEnumerator A_knockout_by_a_hit_of_an_enemy_is_seen_too()
        {
            _s.Events.RaiseEnemy(new ZeldaDaughter.Core.Combat.EnemyNotice("boar_1",
                new ZeldaDaughter.Core.Combat.EnemyEvent(ZeldaDaughter.Core.Combat.EnemyEventKind.HeroKnockedOut, 0f, null)));
            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(_view.IsDown);
            Assert.IsTrue(_sprite.Pose.Lying);
            Assert.Greater(_fader.Alpha, 0.8f);
            _s.Events.RaiseCondition(new ConditionEvent(ConditionEventKind.Revived));
            yield return new WaitForSeconds(1.0f);
            Assert.IsFalse(_view.IsDown);
            Assert.AreEqual(0f, _fader.Alpha, 0.01f);
        }

        // ------------------------------------------------------------------ criterion 6: depth, shadow

        [UnityTest]
        public IEnumerator The_figure_is_a_cut_out_card_that_writes_depth_with_a_blob_of_shadow_and_no_capsule()
        {
            yield return null;
            var card = _sprite.Card.GetComponent<MeshRenderer>();
            Assert.IsTrue(card.enabled);
            Assert.AreEqual(1f, card.sharedMaterial.GetFloat("_ZWrite"), "writes depth: the pen outline of D-08 sees her");
            Assert.IsTrue(card.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON"), "the edge is a cut");
            var shadow = _sprite.transform.Find("Shadow").GetComponent<MeshRenderer>();
            Assert.AreEqual(0f, shadow.sharedMaterial.GetFloat("_ZWrite"), "the shadow is a transparent blob");
            Assert.Less(Mathf.Abs(shadow.transform.eulerAngles.x - 90f), 0.1f, "lying flat on the ground");
            Assert.NotNull(_sprite.CurrentSprite);
            foreach (var r in _hero.GetComponentsInChildren<MeshRenderer>(true))
                if (r.transform != card.transform && r.transform != shadow.transform && r.transform.GetComponentInParent<Light>() == null) Assert.IsFalse(r.enabled, "the capsule is not drawn: " + r.name + " (" + r.GetType().Name + ", under " + (r.transform.parent != null ? r.transform.parent.name : "-") + ")");
            // the picture is not stretched: the card has the proportions of the PNG and shows the whole of it (a Tight sprite's textureRect is the trimmed box)
            var cur = _sprite.CurrentSprite;
            var sc = _sprite.Card.localScale;
            Assert.AreEqual(cur.rect.width / cur.rect.height, Mathf.Abs(sc.x) / sc.y, 0.03f * cur.rect.width / cur.rect.height, "card proportions = PNG proportions");
            var pb = new MaterialPropertyBlock();
            card.GetPropertyBlock(pb);
            var st = pb.GetVector("_BaseMap_ST");
            Assert.AreEqual(1f, st.x, 1e-3f, "the whole width of the picture");
            Assert.AreEqual(1f, st.y, 1e-3f, "the whole height of the picture");
            var feet = _sprite.transform.position.y;
            Assert.AreEqual(_hero.transform.position.y - 1f, feet, 0.01f, "the figure stands on the ground under the capsule's centre");
        }

        // ------------------------------------------------------------------ frames for the critic (criterion 7)

        static string FramePath(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "demo", "frames", "D-11-" + name + ".png"));

        Texture2D Crop(Texture2D full, Vector3 world)
        {
            var s = Camera.main.WorldToScreenPoint(world + Vector3.up * 0.9f);
            int h = Mathf.Min(full.height, Mathf.RoundToInt(full.height * 0.2f)), w = Mathf.RoundToInt(h * 0.9f);
            int x = Mathf.Clamp(Mathf.RoundToInt(s.x) - w / 2, 0, full.width - w), y = Mathf.Clamp(Mathf.RoundToInt(s.y) - h / 2, 0, full.height - h);
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.SetPixels(full.GetPixels(x, y, w, h));
            t.Apply();
            return t;
        }

        static Texture2D Strip(List<Texture2D> parts)
        {
            int w = 0, h = parts[0].height;
            foreach (var p in parts) w += p.width;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            int x = 0;
            foreach (var p in parts) { t.SetPixels(x, 0, p.width, h, p.GetPixels()); x += p.width; }
            t.Apply();
            return t;
        }

        static void Save(Texture2D t, string name)
        {
            string path = FramePath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Debug.Log($"[ZD:Frame] D-11 {name} → {path}");
        }

        IEnumerator Shot(System.Action<Texture2D> got)
        {
            yield return new WaitForEndOfFrame();
            got(ScreenCapture.CaptureScreenshotAsTexture());
        }

        [UnityTest]
        public IEnumerator Frames_of_the_hero_in_the_region()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Grab();
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.6f);
            Application.targetFrameRate = -1;

            var dirs = new[] { ("front", -CamForward()), ("back", CamForward()), ("side_right", CamRight()), ("side_left", -CamRight()) };
            var parts = new List<Texture2D>();
            foreach (var (name, dir) in dirs)
            {
                yield return Walk(dir, 0.5f);
                yield return new WaitForSeconds(0.25f);
                Texture2D shot = null;
                yield return Shot(t => shot = t);
                if (shot == null) { Assert.Inconclusive("no screen capture in this run"); yield break; }
                parts.Add(Crop(shot, _hero.transform.position - Vector3.up));
                Object.Destroy(shot);
            }
            Save(Strip(parts), "directions");

            // mid-step, walking right
            var cc = _hero.GetComponent<CharacterController>();
            Texture2D walk = null;
            for (int i = 0; i < 24; i++) { cc.Move(CamRight() * 1.6f * Time.deltaTime); yield return null; }
            yield return Shot(t => walk = t);
            Save(Crop(walk, _hero.transform.position - Vector3.up), "walk");
            Object.Destroy(walk);
            yield return new WaitForSeconds(0.25f);

            // the hands
            parts.Clear();
            foreach (var (kind, wait) in new[] { (HeroActKind.Strike, 0.06f), (HeroActKind.Strike, 0.30f), (HeroActKind.Pickup, 0.15f), (HeroActKind.Eat, 0.15f), (HeroActKind.Butcher, 0.35f) })
            {
                _s.Events.RaiseHeroActed(new HeroAct(kind, kind == HeroActKind.Strike ? _hero.transform.position + CamRight() * 2f : default));
                yield return new WaitForSeconds(wait);
                Texture2D shot = null;
                yield return Shot(t => shot = t);
                parts.Add(Crop(shot, _hero.transform.position - Vector3.up));
                Object.Destroy(shot);
                yield return new WaitForSeconds(1.0f);
            }
            Save(Strip(parts), "actions");

            // the wounds: healthy, cut, burn, poison
            parts.Clear();
            {
                Texture2D shot = null;
                yield return Shot(t => shot = t);
                parts.Add(Crop(shot, _hero.transform.position - Vector3.up));
                Object.Destroy(shot);
            }
            foreach (var wound in new[] { WoundType.Cut, WoundType.Burn, WoundType.Poison })
            {
                _s.State.Condition.Wound(wound, 0.8f);
                yield return new WaitForSeconds(1.0f);
                Texture2D shot = null;
                yield return Shot(t => shot = t);
                parts.Add(Crop(shot, _hero.transform.position - Vector3.up));
                Object.Destroy(shot);
                _s.State.Condition.Treat(wound == WoundType.Cut ? "bandage" : wound == WoundType.Burn ? "burn_salve" : "antidote");
            }
            Save(Strip(parts), "wounds");
            yield return new WaitForSeconds(1.0f);

            // the knockout: lying (a crop), the dark (the whole screen), a glimpse (the whole screen)
            _s.State.Condition.Damage(10000f);
            yield return new WaitForSeconds(0.45f);
            Texture2D lying = null;
            yield return Shot(t => lying = t);
            Save(Crop(lying, _hero.transform.position - Vector3.up + CamRight() * 0.8f), "knockout_lying");
            Object.Destroy(lying);
            float t0 = Time.time;
            bool darkSaved = false, glimpseSaved = false;
            while (_view.IsDown && Time.time - t0 < 9f && !(darkSaved && glimpseSaved))
            {
                float a = _fader.Alpha;
                if (!darkSaved && a > 0.9f && Time.time - t0 > 0.9f)
                {
                    Texture2D full = null;
                    yield return Shot(t => full = t);
                    Save(full, "knockout_dark");
                    Object.Destroy(full);
                    darkSaved = true;
                }
                else if (!glimpseSaved && a < 0.7f && a > 0.4f && Time.time - t0 > 0.9f)
                {
                    Texture2D full = null;
                    yield return Shot(t => full = t);
                    Save(full, "knockout_glimpse");
                    Object.Destroy(full);
                    glimpseSaved = true;
                }
                yield return null;
            }
            Assert.IsTrue(darkSaved, "the dark frame");
        }

        /// <summary>D-11 criterion 6 / task point 3: at night a campfire lights the figure (the sprite takes light) — a frame of the hero at the fire, day for comparison.</summary>
        [UnityTest]
        public IEnumerator Frame_of_the_hero_at_night_by_the_campfire()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Grab();
            _hero.UseDpi(160f);
            var g = _s.State;
            var at = _hero.transform.position;
            g.Clock.SetTime(1, 0.5);
            yield return new WaitForSeconds(0.8f);
            Texture2D day = null;
            yield return Shot(t => day = t);
            Save(Crop(day, _hero.transform.position - Vector3.up), "day_for_light");
            Object.Destroy(day);

            g.Clock.SetTime(1, 0.0);
            g.Bag.Add("firewood");
            g.Bag.Add("flint");
            var fireAt = at + CamRight() * 1.6f - CamForward() * 0.4f;
            var placed = g.Camp.Place("firewood", new Vec2(fireAt.x, fireAt.z));
            var used = g.Camp.Use(placed.Object.Id, "flint");
            _s.Events.RaisePlaced(placed.Object);
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            _s.BagChanged("frame");
            yield return new WaitForSeconds(2.5f);
            Texture2D night = null;
            yield return Shot(t => night = t);
            Save(night, "night_campfire");
            Object.Destroy(night);
        }
    }
}
