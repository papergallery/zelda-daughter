using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-22 (docs/done-criteria/D-22.md) in scenes/region.json as built: the heroine ≈ 1/9 of the frame, the ground patches walk-through, the morning mist there at the start of the game.</summary>
    public class D22RegionLookTests
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            Application.runInBackground = true;
            TestSaves.UseCleanFolder();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown] public void TearDown() => TestSaves.Clear();

        [Test]
        public void Mist_strength_follows_the_morning()
        {
            Assert.AreEqual(0f, MorningMist.Strength(0.1), 1e-4f);   // night
            Assert.AreEqual(0f, MorningMist.Strength(0.2), 1e-4f);   // dawn begins
            Assert.Greater(MorningMist.Strength(0.25), 0.3f);
            Assert.AreEqual(1f, MorningMist.Strength(0.35), 1e-4f);  // the game starts here
            Assert.Less(MorningMist.Strength(0.46), 0.7f);
            Assert.AreEqual(0f, MorningMist.Strength(0.6), 1e-4f);   // afternoon
            Assert.AreEqual(0f, MorningMist.Strength(0.9), 1e-4f);
        }

        [Test]
        public void The_heroine_is_about_a_ninth_of_the_frame()
        {
            var cam = Camera.main;
            var hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(hero);
            var foot = hero.transform.position - Vector3.up * 1f; // capsule pivot is its centre, 2 m tall
            float h = cam.WorldToScreenPoint(foot + Vector3.up * 1.7f).y - cam.WorldToScreenPoint(foot).y;
            float share = h / cam.pixelHeight;
            Assert.That(share, Is.InRange(1f / 9f * 0.85f, 1f / 9f * 1.15f), $"heroine share of the frame {share:0.000}");
        }

        [Test]
        public void Patches_do_not_block_and_the_mist_is_up_in_the_morning()
        {
            var patches = Object.FindObjectsByType<SceneTags>(FindObjectsSortMode.None).Where(t => t.Has("ground_patch")).ToList();
            Assert.GreaterOrEqual(patches.Count, 200);
            Assert.IsTrue(patches.All(p => p.GetComponentInChildren<Collider>() == null), "ground patches carry no colliders");
            var mist = Object.FindObjectsByType<MorningMist>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(mist.Length, 8);
            Assert.IsTrue(mist.All(m => m.GetComponent<Renderer>().enabled), "the game starts at 0.35 — the mist is there");
            Assert.IsTrue(mist.All(m => m.GetComponent<Collider>() == null));
        }
    }
}
