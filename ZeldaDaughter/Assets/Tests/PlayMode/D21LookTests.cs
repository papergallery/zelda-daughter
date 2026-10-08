using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-21 (docs/done-criteria/D-21.md): the look after the concept. What can be measured is measured here — the pen line numbers, soft shadows,
    /// no shadows from ground cover, brown (not orange) wood, the night factor of the post pass, the sprite lit by the fire; the rest (does it
    /// look like the concept) is the frames (D21FrameTests → docs/demo/frames/D-21-*.png) and the critic.
    /// </summary>
    public class D21LookTests
    {
        [Test]
        public void The_pen_line_is_thin_dark_olive_sepia()
        {
            var s = ScriptableObject.CreateInstance<WatercolorSettings>();
            Assert.That(s.lineWidthPx, Is.InRange(1f, 1.5f), "1–1.5 px at 1080×2340");
            var want = new Color32(0x1c, 0x25, 0x15, 255);
            Assert.That(s.lineColor.r * 255f, Is.EqualTo(want.r).Within(3f));
            Assert.That(s.lineColor.g * 255f, Is.EqualTo(want.g).Within(3f));
            Assert.That(s.lineColor.b * 255f, Is.EqualTo(want.b).Within(3f));
            Assert.That(s.normalThreshold, Is.LessThan(0.3f), "creases between facets make a line");
            var asset = AssetDatabase.LoadAssetAtPath<WatercolorSettings>("Assets/Settings/WatercolorLook.asset");
            Assert.NotNull(asset);
            Assert.That(asset.lineWidthPx, Is.InRange(1f, 1.5f), "the asset in use, not only the defaults");
            Assert.That(asset.normalThreshold, Is.LessThan(0.3f));
            Object.DestroyImmediate(s);
        }

        [Test]
        public void Sun_shadows_are_soft_and_the_night_factor_follows_daylight()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            Assert.NotNull(pipeline);
            Assert.IsTrue(new SerializedObject(pipeline).FindProperty("m_SoftShadowsSupported").boolValue, "PCF on the URP asset");

            var go = new GameObject("sun-test");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Hard; // what region.json says
            var sun = go.AddComponent<SunController>();
            sun.Configure(light, new Vector3(50f, -30f, 0f), 1f, Color.gray);
            sun.Apply(1f);
            Assert.AreEqual(LightShadows.Soft, light.shadows);
            Assert.Less(light.shadowStrength, 1f, "a shadow is not fully black");
            Assert.AreEqual(0f, Shader.GetGlobalFloat("_ZD_Night"), 1e-3f);
            sun.Apply(0f);
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_ZD_Night"), 1e-3f);
            sun.Apply(0.6f, false);
            Assert.AreEqual(0f, Shader.GetGlobalFloat("_ZD_Night"), 1e-3f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Ground_cover_casts_no_shadow_but_a_tree_and_a_log_do()
        {
            foreach (var path in new[] { "KenneyNature/grass.fbx", "KenneyNature/grass_large.fbx", "KenneyNature/flower_redA.fbx", "KenneyNature/plant_flatShort.fbx" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/" + path);
                Assert.NotNull(prefab, path);
                foreach (var r in prefab.GetComponentsInChildren<Renderer>())
                    Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode, path);
            }
            var log = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/KenneyNature/log.fbx");
            Assert.NotNull(log);
            foreach (var r in log.GetComponentsInChildren<Renderer>())
                Assert.AreNotEqual(ShadowCastingMode.Off, r.shadowCastingMode, "a log casts its shadow");
        }

        [Test]
        public void A_log_is_brown_not_orange()
        {
            var log = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/KenneyNature/log.fbx");
            Assert.NotNull(log);
            int n = 0;
            foreach (var r in log.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").gamma : Color.white;
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    TestContext.WriteLine($"[ZD:Look] log material {m.name} rgb={c} h={h:0.00} s={s:0.00} v={v:0.00}");
                    Assert.IsFalse(h < 0.12f && s > 0.6f && v > 0.7f, $"{m.name} is orange (h={h:0.00} s={s:0.00} v={v:0.00})");
                    n++;
                }
            Assert.Greater(n, 0);
        }

        [Test]
        public void The_billboard_sprite_catches_the_fire()
        {
            var mat = SpriteLook.NewSpriteMaterial(0.5f);
            Assert.AreEqual("Zelda/Toon", mat.shader.name);
            Assert.IsTrue(mat.IsKeywordEnabled("_SPRITELIT"));
            Assert.IsTrue(mat.IsKeywordEnabled("_ALPHATEST_ON"));
            var asset = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Registries/SpriteLook_Sprite.mat");
            Assert.NotNull(asset);
            Assert.AreEqual("Zelda/Toon", asset.shader.name, "the sprite material in use");
            Assert.IsTrue(asset.IsKeywordEnabled("_SPRITELIT"));
            Object.DestroyImmediate(mat);
        }
    }

    /// <summary>D-21 frames: the same place as D-08 F1/F1n (the hero at the spawn, morning; the same at night by a campfire), in the game, 1080×2340. Not a check of anything but that the frames are made.</summary>
    public class D21FrameTests
    {
        static string FramesDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/demo/frames"));

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var canvas = Object.FindFirstObjectByType<SessionUI>().Canvas;
            var mode = canvas.renderMode;
            var rt = new RenderTexture(1080, 2340, 24);
            var prevTarget = cam.targetTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 1f;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            canvas.renderMode = mode;
            if (Directory.Exists(FramesDir)) File.WriteAllBytes(Path.Combine(FramesDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Debug.Log($"[ZD:Frame] {name} written");
        }

        [UnityTest]
        public IEnumerator Frames_F1_morning_and_F1n_night_by_a_fire()
        {
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1080, 2340, "ZD phone");
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return null;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(1f);
            var s = Object.FindFirstObjectByType<GameSession>();
            var hero = Object.FindFirstObjectByType<HeroController>();
            hero.UseDpi(160f);
            var g = s.State;
            g.Bag.Add("firewood", 2); g.Bag.Add("flint");

            g.Clock.SetTime(1, 0.27);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[ZD:Frame] F1 daylight={g.Clock.Daylight:0.00} hero={hero.transform.position}");
            yield return Shot("D-21-F1");

            var at = hero.transform.position + new Vector3(-1.6f, 0f, -0.4f);
            var placed = g.Camp.Place("firewood", new Vec2(at.x, at.z));
            s.Events.RaisePlaced(placed.Object);
            var used = g.Camp.Use(placed.Object.Id, "flint");
            s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            g.Clock.SetTime(1, 0.0);
            yield return new WaitForSeconds(1.5f);
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (!ps.transform.root.name.Contains("placed") && ps.transform.parent == null) { }
                string path = ps.name; for (var t = ps.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                if (!path.Contains("fire") && !path.Contains("placed")) continue;
                Debug.Log($"[ZD:Frame] ps {path} playing={ps.isPlaying} count={ps.particleCount} pos={ps.transform.position} shader={(r != null && r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-")} queue={(r != null && r.sharedMaterial != null ? r.sharedMaterial.renderQueue : 0)} mode={(r != null ? r.renderMode.ToString() : "-")} size={ps.main.startSize.constant:0.00} layer={ps.gameObject.layer}");
            }
            Debug.Log($"[ZD:Frame] F1n daylight={g.Clock.Daylight:0.00} fires={g.Camp.Campfires.Count}");
            yield return Shot("D-21-F1n");
            var look = AssetDatabase.LoadAssetAtPath<WatercolorSettings>("Assets/Settings/WatercolorLook.asset");
            look.enabled = false; // the same frame without the wash: is the flame drawn at all?
            yield return null;
            yield return Shot("D-21-F1n-noeffect");
            look.enabled = true;
        }
    }
}
