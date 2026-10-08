using System.Collections;
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
    /// <summary>
    /// D-28 (docs/done-criteria/D-28.md п. 4) in scenes/pilot-f1.json as built: painted cards play like the 3D models —
    /// the heroine behind a trunk is covered and in front of it is not, a crown over her dissolves, the rock's collider is the 3D one and stops
    /// her at its face; a card is out of the shadow pass and its own model stays as a shadow-only stand-in.
    /// The look is checked by rendering the game camera twice (heroine shown / hidden) and comparing the pixels at her body.
    /// </summary>
    public class D28PaintedTests
    {
        const string Scene = "Assets/Scenes/pilot-f1.unity";
        HeroController _hero;
        Camera _cam;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Application.runInBackground = true;
            TestSaves.UseCleanFolder();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _hero = Object.FindFirstObjectByType<HeroController>();
            _cam = Camera.main;
            Assert.NotNull(_hero);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.5f);
        }

        [TearDown] public void TearDown() => TestSaves.Clear();

        static Vector3 Forward => new Vector3(1f, 0f, 1f).normalized; // away from the camera (yaw 45) on the ground

        [Test]
        public void Painted_objects_keep_the_3D_collider_and_a_shadow_only_model()
        {
            var catalog = ModelCatalog.Load("../data");
            var config = SceneConfig.Parse(System.IO.File.ReadAllText("../scenes/pilot-f1.json"));
            int checkedBoxes = 0;
            foreach (var o in config.Objects.Where(o => o.Model != null && catalog.Get(o.Model).Collider.Kind == "box"))
            {
                var go = GameObject.Find("Objects/" + o.Id);
                Assert.NotNull(go, o.Id);
                var box = go.GetComponent<BoxCollider>();
                Assert.NotNull(box, $"{o.Id}: collider as in 3D");
                var b = catalog.Bounds(o.Model);
                float k = catalog.Get(o.Model).Collider.Shrink;
                Assert.AreEqual(b.SizeX * k, box.size.x, 1e-3f, o.Id);
                Assert.AreEqual(b.SizeZ * k, box.size.z, 1e-3f, o.Id);
                Assert.NotNull(go.transform.Find("painted"), $"{o.Id}: painted card");
                var model = go.transform.Find("model").GetComponentsInChildren<Renderer>();
                Assert.IsTrue(model.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly), $"{o.Id}: model is the shadow stand-in");
                checkedBoxes++;
            }
            Assert.GreaterOrEqual(checkedBoxes, 8);
            var card = GameObject.Find("Objects/tree_1/painted").GetComponent<MeshRenderer>();
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, card.shadowCastingMode, "a card casts no shadow of its own");
            Assert.AreEqual(Quaternion.Euler(35f, 45f, 0f).eulerAngles.ToString(), card.transform.rotation.eulerAngles.ToString(), "the card faces the camera");
        }

        static TouchSample S(TouchPhase phase, Vector2 p, TouchHit hit = default) =>
            new TouchSample(0, phase, Time.realtimeSinceStartupAsDouble, new Vec2(p.x, p.y), hit);

        /// <summary>Walks towards the rock (swipe «up» = away from the camera) and stops at its face: the 3D box plus the controller's radius.</summary>
        [UnityTest]
        public IEnumerator The_rock_stops_her_where_its_3D_collider_is()
        {
            var rock = GameObject.Find("Objects/rock_6").GetComponent<BoxCollider>();
            var c = rock.bounds.center;
            var start = new Vector3(c.x, 1f, c.z) - Forward * 3.5f;
            _hero.Teleport(start, 45f);
            yield return new WaitForSeconds(0.3f);
            var o = (Vector2)_cam.WorldToScreenPoint(_hero.transform.position) + new Vector2(0f, -220f);
            _hero.Feed(S(TouchPhase.Began, o, _hero.HitAt(new Vec2(o.x, o.y))));
            _hero.Feed(S(TouchPhase.Moved, o + new Vector2(0, 60f)));
            float end = Time.time + 4f;
            while (Time.time < end) { _hero.Feed(S(TouchPhase.Stationary, o + new Vector2(0, 60f))); yield return null; }
            _hero.Feed(S(TouchPhase.Ended, o + new Vector2(0, 60f)));
            yield return null;
            var p = _hero.transform.position;
            var face = rock.ClosestPoint(new Vector3(p.x, rock.bounds.center.y, p.z));
            float gap = new Vector2(p.x - face.x, p.z - face.z).magnitude;
            float radius = _hero.GetComponent<CharacterController>().radius;
            Debug.Log($"[ZD:Test] D-28 rock stop: hero {p.x:0.00},{p.z:0.00} face gap {gap:0.00} m radius {radius:0.00}");
            Assert.Greater(Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(start.x, 0, start.z)), 0.8f, "she walked");
            Assert.That(gap, Is.InRange(radius - 0.12f, radius + 0.12f), "stopped at the 3D collider's face");
        }

        /// <summary>
        /// Share of the pixels in a small window at her body (height <paramref name="h"/> m above her feet) that change when she is hidden:
        /// ≈ 0 — covered by what is in front, ≈ 1 — she is seen.
        /// </summary>
        float SeenShare(float h)
        {
            var feet = _hero.transform.position - Vector3.up;
            Shader.SetGlobalVector("_ZD_HeroPos", new Vector4(feet.x, feet.y, feet.z, 1f));
            var at = _cam.WorldToScreenPoint(feet + _cam.transform.up * h);
            var renderers = _hero.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            Color[] Shot()
            {
                var rt = RenderTexture.GetTemporary(_cam.pixelWidth, _cam.pixelHeight, 24);
                var prev = _cam.targetTexture;
                _cam.targetTexture = rt;
                _cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(9, 9, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(at.x - 4, at.y - 4, 9, 9), 0, 0);
                tex.Apply();
                var px = tex.GetPixels();
                RenderTexture.active = null;
                _cam.targetTexture = prev;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(tex);
                return px;
            }
            var shown = Shot();
            foreach (var r in renderers) r.enabled = false;
            var hidden = Shot();
            foreach (var r in renderers) r.enabled = true;
            int diff = 0;
            for (int i = 0; i < shown.Length; i++)
                if (Mathf.Abs(shown[i].r - hidden[i].r) + Mathf.Abs(shown[i].g - hidden[i].g) + Mathf.Abs(shown[i].b - hidden[i].b) > 0.06f) diff++;
            return diff / (float)shown.Length;
        }

        IEnumerator PlaceAt(Vector3 feet)
        {
            _hero.Teleport(feet + Vector3.up, 225f);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.4f); // the camera follows
        }

        [UnityTest]
        public IEnumerator Behind_the_trunk_she_is_covered_in_front_she_is_not_the_crown_over_her_dissolves()
        {
            var tree = GameObject.Find("Objects/tree_1").transform.position;
            // In front: 1.2 m nearer the camera than the trunk — she covers the trunk.
            yield return PlaceAt(tree - Forward * 1.2f);
            float front = SeenShare(0.6f);
            // Behind: 0.9 m farther — her legs are behind the drawn trunk (screen up 0.5 m from the root), her head under the crown.
            yield return PlaceAt(tree + Forward * 0.9f);
            float behindLegs = SeenShare(0.35f);
            float behindHead = SeenShare(1.45f);
            Debug.Log($"[ZD:Test] D-28 occlusion: front={front:0.00} behind legs={behindLegs:0.00} behind head (crown dissolves)={behindHead:0.00}");
            Assert.Greater(front, 0.6f, "in front of the tree she is seen");
            Assert.Less(behindLegs, 0.25f, "behind the trunk she is covered");
            Assert.Greater(behindHead, 0.5f, "the crown over her dissolves");
        }
    }
}
