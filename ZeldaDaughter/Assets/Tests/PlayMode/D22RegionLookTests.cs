using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-22/D-22b (docs/done-criteria/D-22.md) in scenes/region.json as built: the heroine ≈ 1/9 of the frame, the painted ground, the drawn grass, the morning mist at the start of the game.</summary>
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
            // D-22b: the card of the figure faces the camera — measure its drawn mesh on the screen (D-22 measured a vertical 1.7 m, ×cos 35°)
            var cam = Camera.main;
            var hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(hero);
            var sprite = hero.GetComponentInChildren<BillboardSprite>();
            Assert.NotNull(sprite, "the heroine is a billboard");
            var card = sprite.Card.GetComponent<MeshFilter>();
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var v in card.sharedMesh.vertices)
            {
                float y = cam.WorldToScreenPoint(card.transform.TransformPoint(v)).y;
                lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
            }
            float share = (hi - lo) / cam.pixelHeight;
            Assert.That(share, Is.InRange(1f / 9f * 0.85f, 1f / 9f * 1.15f), $"heroine share of the frame {share:0.000}");
        }

        [Test]
        public void The_ground_is_painted_and_the_grass_is_drawn_cards_in_chunks()
        {
            var ground = GameObject.Find("Ground").GetComponent<Renderer>().sharedMaterial;
            Assert.IsTrue(ground.IsKeywordEnabled("_ZD_GROUND"), "the ground is painted by the mask");
            Assert.NotNull(ground.GetTexture("_GroundMask"));
            Assert.NotNull(ground.GetTexture("_GroundNoise"));
            Assert.IsNull(GameObject.Find("Paths"), "no road ribbons on top of the ground");
            Assert.IsFalse(Object.FindObjectsByType<SceneTags>(FindObjectsSortMode.None).Any(t => t.Has("ground_patch")), "no discs on the ground");

            var veg = GameObject.Find("Vegetation");
            Assert.NotNull(veg);
            var meshes = veg.GetComponentsInChildren<MeshFilter>();
            int cards = meshes.Sum(m => m.sharedMesh.uv2.Distinct().Count()); // every card's corners carry its root (uv2)
            Assert.That(meshes.Sum(m => m.sharedMesh.vertexCount), Is.LessThanOrEqualTo(cards * 8), "a card is its outline of ≤ 8 corners");
            Assert.Greater(meshes.Length, 10);
            Assert.That(cards, Is.InRange(8000, 30000), $"cards {cards}");
            Assert.IsEmpty(veg.GetComponentsInChildren<Collider>(), "grass is walked through");
            var vm = veg.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial).Distinct().Single();
            Assert.IsTrue(vm.IsKeywordEnabled("_ALPHATEST_ON") && vm.IsKeywordEnabled("_ZD_ROOTED"), "alpha-clipped cards rooted in the painted ground");
            Assert.IsFalse(vm.GetShaderPassEnabled("DepthNormals"), "no second pen outline around every tuft, no prepass for grass");

            // grass cells of the fire (D-06) are cards facing the camera
            var cells = Object.FindObjectsByType<SceneTags>(FindObjectsSortMode.None).Where(t => t.Has("grass_cell")).ToList();
            Assert.Greater(cells.Count, 30);
            foreach (var c in cells)
            {
                var face = c.transform.Find("card");
                Assert.NotNull(face, c.name);
                Assert.Less(Quaternion.Angle(face.rotation, Quaternion.Euler(35f, 45f, 0f)), 0.5f, c.name);
            }
        }

        [Test]
        public void The_mist_is_up_in_the_morning()
        {
            // D-22b: the mist is drawn by the post pass; one driver sets its strength by the clock
            var mist = Object.FindObjectsByType<MorningMist>(FindObjectsSortMode.None);
            Assert.AreEqual(1, mist.Length);
            Assert.AreEqual(1f, mist[0].Current, 0.05f, "the game starts at 0.35 — the mist is there");
            Assert.AreEqual(1f, Shader.GetGlobalFloat(MorningMist.MistId), 0.05f);
            Assert.IsFalse(Object.FindObjectsByType<SceneTags>(FindObjectsSortMode.None).Any(t => t.Has("mist")), "no stack of mist planes");
        }
    }
}
