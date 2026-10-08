using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-26 frames (criterion 14) in the region scene: the sequence of a blow (the freeze and the shake, frame by frame with a log of positions),
    /// the wolf's eyes at the edge of a fire's light at night, the hands of the hints at their targets (swipe with a trail, tap with its ring,
    /// long press with the filling ring). PNGs land in <c>Application.temporaryCachePath/zd-frames-d26</c>; the paths are logged as <c>[ZD:Frames]</c>.
    /// Checks nothing but that the frames are made.
    /// </summary>
    public class D26Frames
    {
        const int W = 1080, H = 2340;
        GameSession _s;
        HeroController _hero;
        CombatPresenter _combat;
        string _dir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _dir = Path.Combine(Application.temporaryCachePath, "zd-frames-d26");
            Directory.CreateDirectory(_dir);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _combat = Object.FindFirstObjectByType<CombatPresenter>();
            Assert.NotNull(_s);
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.8f);
        }

        [TearDown]
        public void TearDown() => TestSaves.Clear();

        /// <summary>One shot of the game camera with the UI, at <paramref name="w"/> × <paramref name="h"/>.</summary>
        void Capture(string name, int w, int h)
        {
            var cam = Camera.main;
            var canvas = _s.UI.Canvas;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            float oldAspect = cam.aspect;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = oldTarget;
            cam.ResetAspect();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            yield return null;
            Capture(name, W, H);
            ZdLog.Info("Frames", Path.Combine(_dir, name + ".png"));
        }

        static Vector3 GroundForward(Camera cam)
        {
            var f = cam.transform.forward; f.y = 0f;
            return f.normalized;
        }

        // ------------------------------------------------------------------ the hands of the hints
        [UnityTest, Explicit("frames of D-26: run by hand with -filter D26Frames")]
        public IEnumerator Frames_of_the_hint_hands()
        {
            var hints = Object.FindFirstObjectByType<HintView>();
            _s.HintIdleSeconds = 0f;
            Assert.AreEqual("swipe", hints.ShownHint);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("D-26-swipe-hint");

            _s.State.Hints.Did("swipe");
            Tappable pick = null;
            foreach (var t in _s.Index.Tappables)
                if (t != null && t.Kind == TapKind.Pickup && t.Enabled && t.gameObject.activeInHierarchy) { pick = t; break; }
            Assert.NotNull(pick, "the region has a pick-up");
            var p = pick.transform.position;
            _hero.Teleport(new Vector3(p.x - 1.6f, 1f, p.z - 1.6f), 45f);
            float until = Time.time + 6f;
            while (hints.ShownHint != "tap" && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.9f);
            yield return Shot("D-26-tap-hint");

            _s.State.Hints.Did("tap");
            _s.State.Bag.Add("stick");
            _s.BagChanged("frames");
            until = Time.time + 6f;
            while (hints.ShownHint != "long_press" && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.6f);
            yield return Shot("D-26-longpress-hint");
            _s.State.Hints.Did("long_press_hero");
        }

        // ------------------------------------------------------------------ the eyes at the edge of the light
        [UnityTest, Explicit("frames of D-26: run by hand with -filter D26Frames")]
        public IEnumerator Frame_of_the_eyes_of_a_wolf_at_the_edge_of_a_fires_light()
        {
            var g = _s.State;
            g.Clock.SetTime(1, 0.02);                              // deep night
            var fireAt = new Vec2(-163f, 6f);
            g.Bag.Add("firewood"); g.Bag.Add("flint");
            var placed = g.Camp.Place("firewood", fireAt);
            Assert.AreEqual(ZeldaDaughter.Core.World.PlaceOutcome.Placed, placed.Outcome);
            var used = g.Camp.Use(placed.Object.Id, "flint");
            _s.Events.RaisePlaced(placed.Object);
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            _s.BagChanged("frames");
            _hero.Teleport(new Vector3(fireAt.X - 1.2f, 1f, fireAt.Y - 1.2f), 45f);
            yield return new WaitForSeconds(1.0f);
            var fwd = GroundForward(Camera.main);
            float light = g.Data.Camp.LightRadius;
            var at = fireAt + new Vec2(fwd.x, fwd.z) * (light + 1.0f);   // up the screen, just past the edge of the light
            var wolf = g.Enemies.Spawn("eyes_frame", "wolf", at);
            Assert.NotNull(wolf);
            wolf.Hunt();
            float until = Time.time + 3f;
            while (Time.time < until && (wolf.Position - fireAt).Length > light + 2.5f) yield return null;
            yield return new WaitForSeconds(1.2f);
            var eyes = _combat.FindEnemy("eyes_frame")?.GetComponent<WolfEyes>();
            ZdLog.Info("Frames", $"eyes alpha={(eyes != null ? eyes.Alpha : -1f):0.00} wolf at {(wolf.Position - fireAt).Length:0.0} m from the fire, light {light:0.0} m, state {wolf.State}");
            yield return Shot("D-26-wolf-eyes");
        }

        // ------------------------------------------------------------------ a blow, frame by frame
        [UnityTest, Explicit("frames of D-26: run by hand with -filter D26Frames")]
        public IEnumerator Sequence_of_a_blow_freeze_and_shake()
        {
            var g = _s.State;
            g.Clock.SetTime(1, 0.5);
            var boarView = _combat.EnemyViews.FirstOrDefault(v => v.Enemy != null && v.Enemy.DefId == "boar");
            Assert.NotNull(boarView, "the region has a boar");
            var e = boarView.Enemy;
            string id = e.Id;
            _hero.Teleport(new Vector3(e.Position.X + 1.0f, 1f, e.Position.Y), 0f);
            yield return new WaitForSeconds(1.0f);
            // the boar stands still for the shot
            e.Blocked = _ => true;
            var feel = Object.FindFirstObjectByType<CombatFeel>();
            var cam = Camera.main;
            var log = new StringBuilder("frame,t_ms,stop,trauma,hero_x,hero_z,boar_x,boar_z,cam_x,cam_y,cam_z,hero_frame,boar_frame\n");
            var sprite = _hero.GetComponent<HeroView>().Sprite;
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, new Vector3(e.Position.X, 0f, e.Position.Y), "fists"));
            float t0 = Time.unscaledTime;
            _s.Events.RaiseHeroStruck(id, new StrikeResult(StrikeOutcome.Hit, 8f, null, 0f, 0f, false));
            int i = 0;
            while (Time.unscaledTime - t0 < 0.42f)
            {
                yield return new WaitForEndOfFrame();
                string n = $"D-26-hit-{i:00}";
                Capture(n, 540, 1170);
                var hp = _hero.transform.position; var bp = boarView.transform.position; var cp = cam.transform.position;
                log.AppendLine($"{i},{(Time.unscaledTime - t0) * 1000f:0},{(feel.HitStopActive ? 1 : 0)},{feel.TraumaValue:0.000},{hp.x:0.000},{hp.z:0.000},{bp.x:0.000},{bp.z:0.000},{cp.x:0.000},{cp.y:0.000},{cp.z:0.000},{sprite.FrameIndex},{boarView.Sprite.FrameIndex}");
                i++;
            }
            File.WriteAllText(Path.Combine(_dir, "D-26-hit-log.csv"), log.ToString());
            ZdLog.Info("Frames", $"hit sequence {i} frames in {_dir}");
        }
    }
}
