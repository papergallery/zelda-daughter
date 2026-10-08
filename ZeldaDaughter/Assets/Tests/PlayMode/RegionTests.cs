using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Hero;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-10 criteria 2 and 4 in scenes/region.json: the road is walked end to end in design time (as T-09 for the grey prologue), the bridge is not water, walls hold.</summary>
    public class RegionTests
    {
        HeroController _hero;
        SceneConfig _config;

        static double Now => Time.realtimeSinceStartupAsDouble;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Application.runInBackground = true;
            _config = SceneConfig.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scenes", "region.json"))));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_hero);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>One continuous swipe whose direction is re-aimed every frame at the next point; the loop ends when <paramref name="done"/> says so.</summary>
        IEnumerator Steer(System.Func<Vector2> target, System.Func<bool> done, float timeout, System.Action onFrame = null)
        {
            var a = Camera.main.WorldToScreenPoint(_hero.transform.position);
            float yaw = Camera.main.GetComponent<ZeldaDaughter.World.IsoCamera>().Yaw * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            var o = new Vector2(a.x - 150f, a.y - 250f);
            Vector2 D()
            {
                var p = _hero.transform.position;
                var dir = (target() - new Vector2(p.x, p.z)).normalized;
                return new Vector2(Vector2.Dot(dir, right), Vector2.Dot(dir, fwd)) * 40f;
            }
            var d = D();
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Now, new Vec2(o.x, o.y), _hero.HitAt(new Vec2(o.x, o.y))));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Now, new Vec2(o.x + d.x, o.y + d.y), default));
            float start = Time.time;
            while (!done() && Time.time - start < timeout)
            {
                d = D();
                _hero.Feed(new TouchSample(0, TouchPhase.Moved, Now, new Vec2(o.x + d.x, o.y + d.y), default));
                onFrame?.Invoke();
                yield return null;
            }
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Now, new Vec2(o.x + d.x, o.y + d.y), default));
            yield return null;
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator Walking_the_road_end_to_end_takes_two_to_three_minutes()
        {
            var road = _config.Paths.Single(p => p.Id == "road_main").Points;
            float start = Time.time;
            float tPeasant = -1, tGate = -1, tEnd = -1;
            bool waded = false;
            var peasant = GameObject.Find("Objects/npc_peasant").transform.position;
            var gate = GameObject.Find("Objects/town_gate").transform.position;
            int next = 1;
            Vector2 Target() => new Vector2(road[next].X, road[next].Z);
            bool Done()
            {
                var p = _hero.transform.position;
                if (tPeasant < 0 && p.x >= peasant.x - 3f) tPeasant = Time.time - start;
                if (tGate < 0 && p.x >= gate.x) tGate = Time.time - start;
                if (Mathf.Abs(p.x) < 5f && _hero.CurrentTerrain == "water") waded = true;
                while (next < road.Count - 1 && (Target() - new Vector2(p.x, p.z)).magnitude < 2.5f) next++;
                if (next == road.Count - 1 && (Target() - new Vector2(p.x, p.z)).magnitude < 2.5f) { tEnd = Time.time - start; return true; }
                return false;
            }
            yield return Steer(Target, Done, 220f);
            Debug.Log($"[ZD:Test] region walk: peasant {tPeasant:0.0}s gate {tGate:0.0}s east end {tEnd:0.0}s at {_hero.transform.position} waded={waded}");
            Assert.That(tPeasant, Is.InRange(11f, 17f), "§2: ~15 s of walking to the field");
            Assert.Greater(tGate, tPeasant);
            Assert.LessOrEqual(tGate - tPeasant, 75f, "from the peasant to the gate within ~75 s");
            Assert.That(tEnd, Is.InRange(120f, 180f), "§2: 2–3 minutes from the spawn to the far edge of the town");
            Assert.IsFalse(waded, "the bridge deck is not water");
        }

        [UnityTest]
        public IEnumerator The_tavern_wall_holds()
        {
            // house_big: door at (60; 18.3) facing south; the wall at x = 64 is solid.
            _hero.Teleport(new Vector3(64f, 1f, 12.5f), 0f);
            yield return new WaitForSeconds(0.2f);
            yield return Steer(() => new Vector2(64f, 40f), () => false, 5f);
            float z = _hero.transform.position.z;
            Debug.Log($"[ZD:Test] tavern wall: z={z:0.00}");
            Assert.Less(z, 18.4f);
            Assert.Greater(z, 16.5f, "she did walk up to it");
        }

        [UnityTest]
        public IEnumerator The_tavern_door_lets_her_in()
        {
            _hero.Teleport(new Vector3(60f, 1f, 12.5f), 0f);
            yield return new WaitForSeconds(0.2f);
            yield return Steer(() => new Vector2(60f, 40f), () => _hero.transform.position.z > 21f, 8f);
            Debug.Log($"[ZD:Test] tavern door: z={_hero.transform.position.z:0.00}");
            Assert.Greater(_hero.transform.position.z, 20.5f, "through the doorway");
        }

        [UnityTest]
        public IEnumerator The_fountain_holds()
        {
            _hero.Teleport(new Vector3(72f, 1f, 8f), 0f);
            yield return new WaitForSeconds(0.2f);
            yield return Steer(() => new Vector2(72f, -40f), () => false, 5f);
            float z = _hero.transform.position.z;
            Debug.Log($"[ZD:Test] fountain: z={z:0.00}");
            Assert.Greater(z, 2.5f, "the fountain (6×6) stops her");
            Assert.Less(z, 4.5f);
        }
    }
}
