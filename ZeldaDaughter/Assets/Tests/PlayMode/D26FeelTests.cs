using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.World;
using CoreTouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-26 (docs/done-criteria/D-26.md): the feel of the fight and of the one-finger control in the scene — the freeze of a blow, the shake, the hand's
    /// pulse, the tap on touch and its buffer, the camera that backs off, the eyes and the growl at night, the ring of the windup, the colour that leaves,
    /// the hint that is a hand first. The rules themselves are tested in the core (FeelTests); here — that the views follow them.
    /// </summary>
    public class D26FeelTests
    {
        const string Boar = "spawn_boar";

        GameSession _s;
        HeroController _hero;
        CombatPresenter _combat;
        CombatFeel _feel;
        readonly List<string> _log = new List<string>();

        void OnLog(string message, string stack, LogType type) => _log.Add(message);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _log.Clear();
            Application.logMessageReceived += OnLog;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _combat = Object.FindFirstObjectByType<CombatPresenter>();
            _feel = Object.FindFirstObjectByType<CombatFeel>();
            Assert.NotNull(_s); Assert.NotNull(_combat);
            Assert.NotNull(_feel, "SceneBuilder.Combat adds CombatFeel");
            _hero.UseDpi(160f);
            _s.State.Clock.SetTime(1, 0.5);                       // noon: no night camera unless a test asks for it
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            CombatFeel.ShakeEnabled = true;
            Haptics.Enabled = true;
            TestSaves.Clear();
        }

        IEnumerator HeroBeside(Enemy e, float meters)
        {
            _hero.Teleport(new Vector3(e.Position.X + meters, 1f, e.Position.Y), 0f);
            yield return null;
            yield return null;
        }

        static StrikeResult Hit(bool killed = false) => new StrikeResult(StrikeOutcome.Hit, 8f, null, 0f, 0f, killed);

        // ------------------------------------------------------------------ 1. frame rate
        [UnityTest]
        public IEnumerator The_game_asks_for_sixty_frames_a_second()
        {
            yield return null;
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Assert.IsTrue(_log.Any(l => l.StartsWith("[ZD:Perf] target=60")), "[ZD:Perf] target=60");
        }

        // ------------------------------------------------------------------ 2. hit-stop
        [UnityTest]
        public IEnumerator A_hit_freezes_the_hero_and_the_boar_for_about_70_ms_without_touching_timeScale()
        {
            var e = _s.State.Enemies.Get(Boar);
            var view = _combat.FindEnemy(Boar);
            yield return HeroBeside(e, 0.8f);
            var heroView = _hero.GetComponent<HeroView>();
            _s.Events.RaiseHeroStruck(Boar, Hit());
            var feel = _s.State.Data.Feel;
            Assert.IsFalse(_feel.HitStopActive, "the freeze comes at the impact, a moment into the swing");
            float limit = Time.unscaledTime + 1f;
            while (!_feel.HitStopActive && Time.unscaledTime < limit) yield return null;
            Assert.IsTrue(_feel.HitStopActive);
            Assert.AreEqual(feel.HitStop.Hit, _feel.LastStopSeconds, 0.005f);
            Assert.IsTrue(_hero.Frozen && heroView.Frozen && view.Frozen && view.Sprite.Frozen && heroView.Sprite.Frozen, "both figures are held");
            Assert.AreEqual(1f, Time.timeScale, "not Time.timeScale");
            var p = _hero.transform.position;
            yield return null;
            Assert.AreEqual(p, _hero.transform.position, "her place is held");
            limit = Time.unscaledTime + 1f;
            while (_feel.HitStopActive && Time.unscaledTime < limit) yield return null;
            Assert.IsFalse(_feel.HitStopActive);
            yield return null;
            Assert.IsFalse(_hero.Frozen || heroView.Frozen || view.Frozen, "…and let go");
            Assert.IsTrue(_log.Any(l => l.StartsWith("[ZD:Feel] hitstop ")), "[ZD:Feel] hitstop");
        }

        [UnityTest]
        public IEnumerator The_freeze_by_kind_a_blow_to_the_hero_a_kill_and_nothing_for_a_miss()
        {
            var e = _s.State.Enemies.Get(Boar);
            yield return HeroBeside(e, 0.8f);
            var feel = _s.State.Data.Feel;

            int before = _feel.StopsStarted;
            _s.Events.RaiseHeroStruck(Boar, new StrikeResult(StrikeOutcome.Miss, 1f));
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(before, _feel.StopsStarted, "a miss freezes nothing");

            _s.Events.RaiseEnemy(new EnemyNotice(Boar, new EnemyEvent(EnemyEventKind.Struck, 20f)));
            float until = Time.unscaledTime + 0.5f;
            while (!_feel.HitStopActive && Time.unscaledTime < until) yield return null;
            Assert.IsTrue(_feel.HitStopActive, "a blow to her freezes (after the frame or two of her shudder)");
            Assert.AreEqual(feel.HitStop.HeroStruck, _feel.LastStopSeconds, 0.005f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(_feel.HitStopActive);

            _s.Events.RaiseHeroStruck(Boar, Hit(killed: true));
            float limit = Time.unscaledTime + 1f;
            while (!_feel.HitStopActive && Time.unscaledTime < limit) yield return null;
            Assert.AreEqual(feel.HitStop.Kill, _feel.LastStopSeconds, 0.005f);
        }

        // ------------------------------------------------------------------ 3. shake
        [UnityTest]
        public IEnumerator A_blow_shakes_the_camera_by_trauma_and_it_dies_down_in_a_third_of_a_second()
        {
            var feel = _s.State.Data.Feel;
            _s.Events.RaiseEnemy(new EnemyNotice(Boar, new EnemyEvent(EnemyKind(), 20f)));
            Assert.AreEqual(feel.Shake.HeroStruck, _feel.TraumaValue, 0.05f);
            Assert.LessOrEqual(_feel.ShakeAmplitude, feel.Shake.MaxMeters + 1e-4f);
            Assert.AreEqual(feel.Shake.HeroStruck * feel.Shake.HeroStruck * feel.Shake.MaxMeters, _feel.ShakeAmplitude, 0.005f, "trauma² × max");
            var cam = Camera.main.transform;
            float far = 0f;
            for (int i = 0; i < 12; i++)
            {
                yield return null;
                far = Mathf.Max(far, _feel.ShakeAmplitude);
            }
            Assert.Greater(far, 0f);
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(0f, _feel.TraumaValue, 1e-4f, "gone after the decay time");
            Assert.IsNotNull(cam);
        }

        static EnemyEventKind EnemyKind() => EnemyEventKind.Struck;

        [UnityTest]
        public IEnumerator The_no_shake_setting_keeps_the_camera_still()
        {
            CombatFeel.ShakeEnabled = false;
            yield return null;
            _s.Events.RaiseHeroStruck(Boar, Hit(killed: true));
            yield return null;
            Assert.AreEqual(0f, _feel.ShakeAmplitude, 1e-6f);
            Assert.AreEqual(0f, _feel.TraumaValue, 1e-6f);
        }

        // ------------------------------------------------------------------ 4. haptics
        [UnityTest]
        public IEnumerator The_hand_feels_a_hit_a_blow_to_her_and_nothing_on_a_miss()
        {
            int n0 = Haptics.Count;
            _s.Events.RaiseHeroStruck(Boar, new StrikeResult(StrikeOutcome.Miss, 1f));
            Assert.AreEqual(n0, Haptics.Count, "nothing on a miss");
            _s.Events.RaiseHeroStruck(Boar, Hit());
            Assert.AreEqual(n0 + 1, Haptics.Count);
            Assert.AreEqual(HapticKind.Hit, Haptics.Last);
            _s.Events.RaiseEnemy(new EnemyNotice(Boar, new EnemyEvent(EnemyEventKind.Struck, 20f)));
            Assert.AreEqual(n0 + 2, Haptics.Count);
            Assert.AreEqual(HapticKind.HeroStruck, Haptics.Last);
            Haptics.Enabled = false;
            _s.Events.RaiseHeroStruck(Boar, Hit());
            Assert.AreEqual(n0 + 2, Haptics.Count, "the player's switch");
            Assert.IsTrue(_log.Any(l => l.Contains("[ZD:Feel] hero_struck") && l.Contains("haptic=hero_struck")));
            yield return null;
        }

        // ------------------------------------------------------------------ 5. attack on touch + buffer
        [UnityTest]
        public IEnumerator A_touch_that_begins_on_the_boar_strikes_before_the_finger_is_lifted()
        {
            var e = _s.State.Enemies.Get(Boar);
            yield return HeroBeside(e, 0.9f);
            var tappable = _s.Index.FindTappable(Boar);
            var p = Camera.main.WorldToScreenPoint(tappable.AimPoint);
            double t = Time.realtimeSinceStartupAsDouble;
            var hit = _hero.HitAt(new Vec2(p.x, p.y));
            Assert.AreEqual(TouchHitKind.Object, hit.Kind);
            Assert.AreEqual(Boar, hit.TargetId);
            Assert.IsTrue(hit.OnPress, "an enemy acts on the touch");
            _log.Clear();
            _hero.Feed(new TouchSample(0, CoreTouchPhase.Began, t, new Vec2(p.x, p.y), hit));
            Assert.IsTrue(_log.Any(l => l.StartsWith("[ZD:Combat] hit " + Boar) || l.StartsWith("[ZD:Combat] miss " + Boar)), "the blow went at the touch");
            int strikes = _log.Count(l => l.StartsWith("[ZD:Combat] hit ") || l.StartsWith("[ZD:Combat] miss "));
            _hero.Feed(new TouchSample(0, CoreTouchPhase.Ended, t + 0.1, new Vec2(p.x, p.y), default));
            Assert.AreEqual(strikes, _log.Count(l => l.StartsWith("[ZD:Combat] hit ") || l.StartsWith("[ZD:Combat] miss ")), "the release adds no second blow");
        }

        [UnityTest]
        public IEnumerator A_tap_in_the_last_150_ms_of_the_cooldown_is_kept_and_strikes_when_it_ends()
        {
            var g = _s.State;
            var e = g.Enemies.Get(Boar);
            yield return HeroBeside(e, 0.7f);
            _s.Tap(Boar);                                         // the first blow: the cooldown begins
            Assert.Greater(g.Combat.CooldownLeft, 0.3f);
            _log.Clear();
            _s.Tap(Boar);                                         // too early: dropped
            Assert.IsTrue(_log.Any(l => l.Contains("[ZD:Combat] cooldown " + Boar) && l.Contains("buffered=False")), "far from the end of the cooldown");
            float limit = Time.time + 2f;
            while (g.Combat.CooldownLeft > 0.1f && Time.time < limit)
            {
                _hero.Teleport(new Vector3(e.Position.X + 0.7f, 1f, e.Position.Y), 0f);   // the boar wanders; she stays at its side
                yield return null;
            }
            _hero.Teleport(new Vector3(e.Position.X + 0.7f, 1f, e.Position.Y), 0f);
            _log.Clear();
            _s.Tap(Boar);                                         // inside the last 150 ms: kept
            Assert.IsTrue(_log.Any(l => l.Contains("buffered=True")), "kept");
            limit = Time.time + 1f;
            while (!_log.Any(l => l.StartsWith("[ZD:Combat] buffered_strike " + Boar)) && Time.time < limit)
            {
                _hero.Teleport(new Vector3(e.Position.X + 0.7f, 1f, e.Position.Y), 0f);
                yield return null;
            }
            Assert.IsTrue(_log.Any(l => l.StartsWith("[ZD:Combat] buffered_strike " + Boar)), "…and the blow goes by itself when the cooldown is over");
        }

        // ------------------------------------------------------------------ 6/7. dodge and the ring
        [UnityTest]
        public IEnumerator The_windup_ring_is_dark_red_big_and_nearly_opaque_at_the_end_and_the_boar_does_not_turn_its_back()
        {
            var e = _s.State.Enemies.Get(Boar);
            var view = _combat.FindEnemy(Boar);
            yield return HeroBeside(e, 0.7f);
            Assert.AreEqual(0.9f, e.WindupSeconds, 0.001f);
            e.Provoke();
            float limit = Time.time + 4f;
            while (e.State != EnemyState.Windup && Time.time < limit) yield return null;
            float maxAlpha = 0f, maxRatio = 0f; bool backShown = false;
            while (e.State == EnemyState.Windup)
            {
                yield return null;
                if (e.State != EnemyState.Windup) break;
                maxAlpha = Mathf.Max(maxAlpha, view.RingAlpha);
                if (view.BodyWidth > 0f) maxRatio = Mathf.Max(maxRatio, view.RingWidth / view.BodyWidth);
                backShown |= view.Sprite.Facing == Facing.Back;
            }
            Assert.GreaterOrEqual(maxAlpha, 0.6f, "opacity at the end of the windup");
            Assert.GreaterOrEqual(maxRatio, 1.5f, "the ring is at least 1.5 × the figure");
            Assert.IsFalse(backShown, "turned to the hero, not its back to the camera");
        }

        // ------------------------------------------------------------------ 8. the threat from the side
        [UnityTest]
        public IEnumerator At_night_the_camera_backs_off_by_a_fifth_over_half_a_second_and_the_hero_stays_in_a_eleventh()
        {
            var cam = Camera.main;
            var iso = cam.GetComponent<ZeldaDaughter.World.IsoCamera>();
            yield return new WaitForSeconds(0.2f);
            float baseOrtho = iso.BaseOrtho;
            Assert.AreEqual(1f, _feel.CameraWiden, 0.001f);
            _s.State.Clock.SetTime(1, 0.05);                      // night
            yield return new WaitForSeconds(0.25f);
            Assert.Greater(_feel.CameraWiden, 1.05f, "moving");
            Assert.Less(_feel.CameraWiden, 1.15f, "…not yet there at 0.25 s");
            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual(1.2f, _feel.CameraWiden, 0.01f);
            Assert.AreEqual(baseOrtho * 1.2f, cam.orthographicSize, 0.05f);
            float heroH = _hero.GetComponent<HeroView>().Sprite.Card.localScale.y;
            Assert.GreaterOrEqual(heroH / (2f * cam.orthographicSize), 1f / 11f - 1e-3f, $"the hero is {heroH:0.00} m in a frame {2f * cam.orthographicSize:0.0} m high");
            _s.State.Clock.SetTime(1, 0.5);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1f, _feel.CameraWiden, 0.01f, "back to the day's camera");
        }

        [UnityTest]
        public IEnumerator A_wolf_at_the_edge_of_the_light_shows_eyes_at_night_and_not_far_in_the_dark()
        {
            var g = _s.State;
            g.Clock.SetTime(1, 0.05);
            _hero.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return null;
            var right = Camera.main.transform.right; right.y = 0f; right.Normalize();
            var near = g.Enemies.Spawn("wolf_eyes", "wolf", new Vec2(right.x * 4f, right.z * 4f));
            Assert.NotNull(near);
            near.Provoke();
            yield return new WaitForSeconds(0.9f);
            var view = _combat.FindEnemy("wolf_eyes");
            Assert.NotNull(view);
            var eyes = view.GetComponent<WolfEyes>();
            Assert.NotNull(eyes, "a wolf gets its eyes");
            Assert.Greater(eyes.Alpha, 0.5f, "4 m from her in the dark: the eyes are seen");
            g.Clock.SetTime(1, 0.5);
            yield return new WaitForSeconds(0.9f);
            Assert.Less(eyes.Alpha, 0.1f, "by day there are none");
        }

        [UnityTest]
        public IEnumerator A_wolf_closer_than_fifteen_metres_growls_from_its_side()
        {
            var g = _s.State;
            var voices = Object.FindFirstObjectByType<FeelVoices>();
            Assert.NotNull(voices, "SceneBuilder.Audio adds FeelVoices");
            _hero.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return null;
            var right = Camera.main.transform.right; right.y = 0f; right.Normalize();
            var w = g.Enemies.Spawn("wolf_growl", "wolf", new Vec2(right.x * 9f, right.z * 9f));
            Assert.NotNull(w);
            w.Provoke();
            float limit = Time.unscaledTime + 3f;
            while (voices.GrowlCount == 0 && Time.unscaledTime < limit) yield return null;
            Assert.Greater(voices.GrowlCount, 0, "it growls");
            Assert.Greater(voices.LastGrowlPan, 0.3f, "…from the right of the screen");
            Assert.IsTrue(_log.Any(l => l.StartsWith("[ZD:Audio] wolf_growl wolf_growl")));
        }

        // ------------------------------------------------------------------ 9. hints
        [UnityTest]
        public IEnumerator The_hint_text_comes_only_after_twenty_seconds_and_never_over_a_window()
        {
            var g = _s.State;
            _s.HintIdleSeconds = 0f;
            yield return null;
            Assert.IsNotNull(g.Hints.Visible);
            Assert.IsTrue(string.IsNullOrEmpty(_s.UI.CurrentHint), "a hand, no words");
            _s.HintIdleSeconds = 20.5f;
            yield return null;
            Assert.AreEqual(g.Hints.TextOf(g.Hints.Visible), _s.UI.CurrentHint, "after 20 s of inaction");
            _s.Events.RaiseWindowOpened("bag");
            yield return null;
            Assert.IsTrue(string.IsNullOrEmpty(_s.UI.CurrentHint), "not over an open window");
            _s.Events.RaiseWindowClosed("bag");
        }

        // ------------------------------------------------------------------ 11. the state in two channels
        [UnityTest]
        public IEnumerator A_badly_hurt_hero_breathes_and_the_world_loses_a_quarter_of_its_colour()
        {
            var g = _s.State;
            var voices = Object.FindFirstObjectByType<FeelVoices>();
            Assert.AreEqual(1f, _feel.WorldSaturation, 0.001f);
            g.Condition.Damage(g.Data.Wounds.MaxHp * 0.8f);
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(0.75f, _feel.WorldSaturation, 0.01f, "−25 %");
            Assert.AreEqual(0.75f, WatercolorFeature.StateSaturation, 0.01f);
            float limit = Time.unscaledTime + 8f;
            while (voices.BreathCount == 0 && Time.unscaledTime < limit) yield return null;
            Assert.Greater(voices.BreathCount, 0, "…and she breathes hard");
            g.Condition.Heal(1000f);
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(1f, _feel.WorldSaturation, 0.01f);
        }

        [UnityTest]
        public IEnumerator A_hungry_hero_stomach_growls()
        {
            var g = _s.State;
            var voices = Object.FindFirstObjectByType<FeelVoices>();
            g.Hunger.Restore(0.95f);
            float limit = Time.unscaledTime + 12f;
            while (voices.StomachCount == 0 && Time.unscaledTime < limit) yield return null;
            Assert.Greater(voices.StomachCount, 0);
        }

        // ------------------------------------------------------------------ 13. the sounds of a blow
        [UnityTest]
        public IEnumerator Swing_hit_miss_and_kill_are_four_sounds_and_the_hit_is_louder_than_the_swing()
        {
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            Assert.NotNull(audio);
            var e = _s.State.Enemies.Get(Boar);
            yield return HeroBeside(e, 0.7f);
            int swing = audio.PlayedCount("swing"), miss = audio.PlayedCount("hit_miss"), kill = audio.PlayedCount("kill");
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, new Vector3(e.Position.X, 0f, e.Position.Y), "fists"));
            Assert.LessOrEqual(audio.PlayedCount("swing"), swing + 1);
            _s.Events.RaiseHeroStruck(Boar, new StrikeResult(StrikeOutcome.Miss, 1f));
            _s.Events.RaiseHeroStruck(Boar, Hit(killed: true));
            // with the purchased clips on the PC these count up; without them a sound is silence and is not counted — then the registry is the proof
            var sounds = Object.FindFirstObjectByType<GameSession>() != null ? ArtAssetsOf().Sounds : null;
            Assert.NotNull(sounds);
            float V(string id) => sounds.Sounds.First(d => d.Id == id).Volume;
            Assert.Greater(V("hit_blade"), V("swing"));
            Assert.Greater(V("hit_fists"), V("swing"));
            Assert.Greater(V("kill"), V("hit_fists"));
            Assert.AreNotEqual(V("hit_miss"), V("swing"));
            yield return null;
        }

        static ArtAssets ArtAssetsOf() => Object.FindFirstObjectByType<ArtAssets>();
    }
}
