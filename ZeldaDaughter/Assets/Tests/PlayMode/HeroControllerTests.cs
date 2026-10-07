using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Hero;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>T-05, docs/done-criteria/T-05.md items 1–8: touches fed straight into the hero controller.</summary>
    public class HeroControllerTests
    {
        const float Dpi = 160f; // reference density: pixel numbers below are as in data/input.json
        HeroController _hero;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Application.runInBackground = true; // the editor is driven through the bridge, often without focus
            yield return SceneManager.LoadSceneAsync("g1-capsule");
            yield return null;
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_hero, "g1-capsule has a HeroController (SceneBuilder)");
            _hero.UseDpi(Dpi);
            yield return new WaitForSeconds(0.5f); // settle on the ground
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        static TouchSample S(TouchPhase phase, Vector2 p, TouchHit hit = default) =>
            new TouchSample(0, phase, Now, new Vec2(p.x, p.y), hit);

        Vector2 AwayFromHero()
        {
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            return new Vector2(h.x, h.y - 200f);
        }

        IEnumerator Swipe(Vector2 delta, float seconds)
        {
            var o = AwayFromHero();
            _hero.Feed(S(TouchPhase.Began, o, _hero.HitAt(new Vec2(o.x, o.y))));
            _hero.Feed(S(TouchPhase.Moved, o + delta));
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                _hero.Feed(S(TouchPhase.Stationary, o + delta));
                yield return null;
            }
            _hero.Feed(S(TouchPhase.Ended, o + delta));
            yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [UnityTest]
        public IEnumerator Swipe_up_walks_forward_of_the_camera_and_stops_on_release()
        {
            var start = _hero.transform.position;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[ZD:Move\] start dir="));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[ZD:Move\] stop dist="));
            yield return Swipe(new Vector2(0, 40), 1f);
            var d = Flat(_hero.transform.position - start);
            Assert.GreaterOrEqual(d.magnitude, 2f, "walk ≥ 2 m in 1 s");
            Assert.Greater(Vector3.Dot(d.normalized, new Vector3(0.7071f, 0, 0.7071f)), 0.98f, "camera yaw 45°: screen up = +x +z");
            var after = _hero.transform.position;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Flat(_hero.transform.position - after).magnitude, 0.01f, "stands after release");
        }

        [UnityTest]
        public IEnumerator Strong_swipe_runs_faster_than_a_gentle_one()
        {
            var a = _hero.transform.position;
            yield return Swipe(new Vector2(24, 0), 1f);
            float gentle = Flat(_hero.transform.position - a).magnitude;
            var b = _hero.transform.position;
            yield return Swipe(new Vector2(-80, 0), 1f);
            float strong = Flat(_hero.transform.position - b).magnitude;
            Assert.Greater(strong, gentle * 1.4f, $"run {strong:0.00} m vs walk {gentle:0.00} m");
        }

        [UnityTest]
        public IEnumerator Long_press_on_hero_does_not_move_and_is_logged()
        {
            var start = _hero.transform.position;
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            var p = new Vector2(h.x, h.y);
            Assert.AreEqual(TouchHitKind.Hero, _hero.HitAt(new Vec2(p.x, p.y)).Kind, "screen projection hits the hero");
            LogAssert.Expect(LogType.Log, "[ZD:Gesture] long_press_hero");
            _hero.Feed(S(TouchPhase.Began, p, TouchHit.Hero));
            yield return new WaitForSeconds(0.7f);
            _hero.Feed(S(TouchPhase.Ended, p));
            yield return null;
            Assert.Less(Flat(_hero.transform.position - start).magnitude, 0.01f);
        }

        [UnityTest]
        public IEnumerator Tap_on_an_object_is_logged_and_does_not_move()
        {
            Transform target = null;
            foreach (Transform t in GameObject.Find("Objects").transform)
            {
                var sp = Camera.main.WorldToScreenPoint(t.position);
                if (sp.z > 0 && sp.x > 0 && sp.y > 0 && sp.x < Screen.width && sp.y < Screen.height) { target = t; break; }
            }
            Assert.NotNull(target, "some object of g1-capsule is on screen");
            var s = Camera.main.WorldToScreenPoint(target.position);
            var p = new Vector2(s.x, s.y);
            var hit = _hero.HitAt(new Vec2(p.x, p.y));
            Assert.AreEqual(TouchHitKind.Object, hit.Kind);
            var start = _hero.transform.position;
            LogAssert.Expect(LogType.Log, "[ZD:Gesture] tap " + target.name);
            _hero.Feed(S(TouchPhase.Began, p, hit));
            yield return new WaitForSeconds(0.1f);
            _hero.Feed(S(TouchPhase.Ended, p));
            yield return null;
            Assert.Less(Flat(_hero.transform.position - start).magnitude, 0.01f);
        }

        [UnityTest]
        public IEnumerator Hero_never_falls_through_the_ground()
        {
            // April: Y ≈ −6000 after 10+ swipes.
            float y0 = _hero.transform.position.y;
            var dirs = new[] { new Vector2(0, 60), new Vector2(60, 0), new Vector2(0, -60), new Vector2(-60, 0), new Vector2(45, 45), new Vector2(-45, -45) };
            foreach (var d in dirs)
            {
                yield return Swipe(d, 5f);
                Assert.That(_hero.transform.position.y, Is.EqualTo(y0).Within(0.1f));
            }
        }
    }
}
