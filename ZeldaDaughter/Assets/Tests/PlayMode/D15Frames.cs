using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-15 frames of the screens in the region scene (criterion 7): the forge window, the shop, the map, the notebook and the sleep (eyes closing).
    /// The interface is an Overlay canvas, so for a frame the root canvas is switched to the main camera, which draws into a 1080×2340 texture (the
    /// phone's portrait shape whatever the Game view is); then it is switched back. The PNGs land in <c>Application.temporaryCachePath/zd-frames-d15</c>
    /// and the path is logged as <c>[ZD:Frames]</c>; the pipeline copies them to docs/demo/frames. Not a check of anything but that the frames are made.
    /// </summary>
    public class D15Frames
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
            _dir = Path.Combine(Application.temporaryCachePath, "zd-frames-d15");
            Directory.CreateDirectory(_dir);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_s);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.5f);
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

        IEnumerator StandBy(string objectId, float dx, float dz)
        {
            var p = _s.Index.Find(objectId).transform.position;
            _hero.Teleport(new Vector3(p.x + dx, 1f, p.z + dz), 45f);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Frames_of_the_screens()
        {
            var g = _s.State;
            var windows = Object.FindFirstObjectByType<WindowStack>();

            // the forge
            g.Bag.Add("ore");
            g.Bag.Add("metal", 2);
            g.Bag.Add("stick", 2);
            g.Bag.Add("short_stick");
            g.Bag.Add("cloth", 2);
            g.Bag.Add("berries", 5);
            yield return StandBy("station_anvil", -2f, -2f);
            _s.Tap("station_anvil");
            var station = Object.FindFirstObjectByType<StationWindow>();
            Assert.AreEqual("anvil", station.Kind);
            station.Put("metal");
            station.Put("stick");
            station.Strike();
            station.Strike();
            yield return Shot("D-15-forge");
            station.Strike();
            yield return Shot("D-15-forge-done");
            windows.CloseAll();

            // the shop
            g.Clock.SetTime(1, 12.0 / 24.0);
            g.Bag.Add("fang", 2);
            g.Bag.Add("hide");
            _s.Events.RaiseTradeRequested("herbalist", "berries");
            var trade = Object.FindFirstObjectByType<TradeWindow>();
            trade.AddGive("berries");
            trade.AddGive("berries");
            trade.AddTake("bandage");
            yield return Shot("D-15-trade");
            windows.CloseAll();

            // the map, bare and then with what talk has opened
            g.Bag.Add("map");
            _s.Events.RaiseRadialChosen("map");
            yield return Shot("D-15-map-bare");
            foreach (var mark in new[] { "town", "merchant_shop", "smithy", "tavern", "herbalist_house" }) g.Map.Open(mark);
            yield return Shot("D-15-map");
            windows.CloseAll();

            // the notebook
            foreach (var note in new[] { "peasant_town", "peasant_beasts", "merchant_map", "smith_forge", "herbalist_trade" }) g.Notebook.Add(note);
            _s.Events.RaiseRadialChosen("notebook");
            yield return Shot("D-15-notebook");
            windows.CloseAll();

            // sleep: the eyes half shut
            yield return StandBy("bed_tavern", -1.5f, -1.5f);
            var rest = Object.FindFirstObjectByType<RestPresenter>();
            rest.SetTimes(2.0f, 0.2f, 1.0f);
            _s.Tap("bed_tavern");
            Assert.IsTrue(rest.Sleeping);
            float until = Time.time + 5f;
            while (rest.LidsClosed < 0.55f && Time.time < until) yield return null;
            yield return Shot("D-15-sleep");
            until = Time.time + 10f;
            while (rest.Sleeping && Time.time < until) yield return null;
            Assert.IsFalse(rest.Sleeping);
        }
    }
}
