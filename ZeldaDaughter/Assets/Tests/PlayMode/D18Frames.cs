using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-18 frames (criterion 4) in the region scene: the swipe hand at the spawn, the tap hand at a pick-up, the long-press hand on the hero,
    /// the hero's remark cloud. Same way as D15Frames: the root canvas goes to the main camera for the shot. The PNGs land in
    /// <c>Application.temporaryCachePath/zd-frames-d18</c> and the path is logged as <c>[ZD:Frames]</c>. Checks nothing but that the frames are made.
    /// </summary>
    public class D18Frames
    {
        const int W = 1080, H = 2340;
        GameSession _s;
        HeroController _hero;
        string _dir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _dir = Path.Combine(Application.temporaryCachePath, "zd-frames-d18");
            Directory.CreateDirectory(_dir);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_s);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.8f);
        }

        [TearDown]
        public void TearDown() => TestSaves.Clear();

        IEnumerator Shot(string name)
        {
            yield return null;
            yield return null;
            var cam = Camera.main;
            var canvas = _s.UI.Canvas;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = oldTarget;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            string path = Path.Combine(_dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            ZdLog.Info("Frames", path);
        }

        [UnityTest]
        public IEnumerator Frames_of_the_hints()
        {
            var hints = Object.FindFirstObjectByType<HintView>();
            Assert.AreEqual("swipe", hints.ShownHint);
            yield return Shot("D-18-swipe-hint");

            _s.State.Hints.Did("swipe");
            Tappable pick = null;
            foreach (var t in _s.Index.Tappables)
                if (t != null && t.Kind == TapKind.Pickup && t.Enabled && t.gameObject.activeInHierarchy) { pick = t; break; }
            Assert.NotNull(pick, "the region has a pick-up");
            var p = pick.transform.position;
            _hero.Teleport(new Vector3(p.x - 1.2f, 1f, p.z - 1.2f), 45f);
            float until = Time.time + 6f;
            while (hints.ShownHint != "tap" && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.6f);
            yield return Shot("D-18-tap-hint");

            _s.State.Hints.Did("tap");
            _s.State.Bag.Add("stick");
            _s.BagChanged("frames");
            yield return new WaitForSeconds(0.9f);
            yield return Shot("D-18-longpress-hint");
            _s.State.Hints.Did("long_press_hero");

            _s.Events.RaiseHeroSaid("hunger_hungry", "Желудок сводит. Надо бы поесть.");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("D-18-remark");
        }
    }
}
