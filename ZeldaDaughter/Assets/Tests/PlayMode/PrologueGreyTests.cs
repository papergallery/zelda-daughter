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
    /// <summary>T-09: the grey prologue keeps the design distances when actually walked (project-design.md §2).</summary>
    public class PrologueGreyTests
    {
        [UnityTest]
        public IEnumerator Walking_the_road_reaches_the_peasant_in_about_fifteen_seconds()
        {
            Application.runInBackground = true;
            TestSaves.UseCleanFolder();
            yield return SceneManager.LoadSceneAsync("prologue-grey");
            yield return null;
            var hero = Object.FindFirstObjectByType<HeroController>();
            hero.UseDpi(160f);
            var peasant = GameObject.Find("Objects/npc_peasant").transform;
            yield return new WaitForSeconds(0.5f);

            // World +x (east along the road) is screen up-right with the camera turned 45°; 40 px → walking strength.
            var h = Camera.main.WorldToScreenPoint(hero.transform.position);
            var o = new Vector2(h.x - 150f, h.y - 250f);
            var d = new Vector2(28.28f, 28.28f);
            double Now() => Time.realtimeSinceStartupAsDouble;
            hero.Feed(new TouchSample(0, TouchPhase.Began, Now(), new Vec2(o.x, o.y), hero.HitAt(new Vec2(o.x, o.y))));
            hero.Feed(new TouchSample(0, TouchPhase.Moved, Now(), new Vec2(o.x + d.x, o.y + d.y), default));
            float start = Time.time;
            // Reached the field: along the road up to the peasant (he stands beside it), not a fixed radius around him.
            while (hero.transform.position.x < peasant.position.x - 3f && Time.time - start < 25f)
            {
                hero.Feed(new TouchSample(0, TouchPhase.Stationary, Now(), new Vec2(o.x + d.x, o.y + d.y), default));
                yield return null;
            }
            float took = Time.time - start;
            hero.Feed(new TouchSample(0, TouchPhase.Ended, Now(), new Vec2(o.x + d.x, o.y + d.y), default));
            Debug.Log($"[ZD:Test] prologue spawn→peasant {took:0.0}s");
            Assert.That(took, Is.InRange(11f, 17f), "§2: ~15 s of walking to the field");
        }

        [TearDown] public void TearDown() => TestSaves.Clear();
    }
}
