using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Hero;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-10, done-criteria item 4: in scenes/models-test.json the hero does not pass through a house, crosses by the bridge, and is slower in water.</summary>
    public class ModelsTestSceneTests
    {
        HeroController _hero;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Application.runInBackground = true;
            TestSaves.UseCleanFolder();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/models-test.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_hero);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.3f);
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        void PlaceAt(float x, float z)
        {
            _hero.Teleport(new Vector3(x, 1f, z), 0f);
        }

        /// <summary>Walk along a world direction (xz) for a time, calling onFrame each frame.</summary>
        IEnumerator Walk(Vector2 worldDir, float seconds, System.Action onFrame = null)
        {
            var a = Camera.main.WorldToScreenPoint(_hero.transform.position);
            // The game turns a swipe into a ground direction by the camera yaw alone (not by the projection): invert that.
            float yaw = Camera.main.GetComponent<ZeldaDaughter.World.IsoCamera>().Yaw * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            var d = new Vector2(Vector2.Dot(worldDir, right), Vector2.Dot(worldDir, fwd)).normalized * 40f;
            var o = new Vector2(a.x - 150f, a.y - 250f);
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Now, new Vec2(o.x, o.y), _hero.HitAt(new Vec2(o.x, o.y))));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Now, new Vec2(o.x + d.x, o.y + d.y), default));
            float start = Time.time;
            while (Time.time - start < seconds)
            {
                _hero.Feed(new TouchSample(0, TouchPhase.Stationary, Now, new Vec2(o.x + d.x, o.y + d.y), default));
                onFrame?.Invoke();
                yield return null;
            }
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Now, new Vec2(o.x + d.x, o.y + d.y), default));
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_hero_does_not_walk_through_a_house_wall()
        {
            // house_a stands at (-12, -2): 2×2 tiles, south wall on z = -5; x = -10.5 is a solid tile (the door is at x = -13.5).
            PlaceAt(-10.5f, -9f);
            yield return new WaitForSeconds(0.2f);
            var start = _hero.transform.position;
            yield return Walk(Vector2.up, 6f); // world +z, 6 s ≈ 15 m if nothing stopped her
            float z = _hero.transform.position.z;
            Debug.Log($"[ZD:Test] house wall: from {start} to {_hero.transform.position} (wall at z=-5)");
            Assert.Less(z, -4.9f, $"the south wall holds (from {start} to {_hero.transform.position})");
            Assert.Greater(z, -6.5f, "she did walk up to it");
        }

        [UnityTest]
        public IEnumerator The_hero_crosses_the_river_by_the_bridge_without_wading()
        {
            PlaceAt(6f, 2.5f);
            yield return new WaitForSeconds(0.2f);
            bool waded = false;
            yield return Walk(Vector2.up, 9f, () => waded |= _hero.CurrentTerrain == "water");
            float z = _hero.transform.position.z;
            Debug.Log($"[ZD:Test] bridge: z={z:0.00} waded={waded}");
            Assert.Greater(z, 13.5f, "across the river (water z 5…12)");
            Assert.IsFalse(waded, "the bridge deck is not water");
        }

        [UnityTest]
        public IEnumerator Water_is_slower_than_grass()
        {
            PlaceAt(-20f, 0f);
            yield return new WaitForSeconds(0.2f);
            var grass = new List<float>();
            var water = new List<float>();
            Vector3 last = _hero.transform.position;
            yield return Walk(Vector2.up, 6f, () =>
            {
                var now = _hero.transform.position;
                float v = Mathf.Abs(now.z - last.z) / Mathf.Max(Time.deltaTime, 1e-4f);
                (_hero.CurrentTerrain == "water" ? water : grass).Add(v);
                last = now;
            });
            Assert.Greater(grass.Count, 20);
            Assert.Greater(water.Count, 20, "she was in the river for a while");
            float Avg(List<float> l) { float s = 0; foreach (var x in l) s += x; return s / l.Count; }
            // The first frames after Teleport may be zero; compare medians-ish averages.
            Debug.Log($"[ZD:Test] speed grass={Avg(grass):0.00} water={Avg(water):0.00}");
            Assert.Less(Avg(water), Avg(grass) * 0.6f);
        }
    }
}
