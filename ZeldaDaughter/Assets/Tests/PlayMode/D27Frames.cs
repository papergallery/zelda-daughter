using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-27 frames (criterion 4): the heroine runs sideways in the region at 2.5 m/s, 30 frames a second — first as the cut-out rig, then the
    /// same run as the drawn video frames of D-25 (the rig switched off). For each frame a crop around her at the game's 1080 × 2340 and, for
    /// a few, the whole frame. PNGs land in <c>Application.temporaryCachePath/zd-frames-d27/{rig,video}</c>, paths logged as <c>[ZD:Frames]</c>;
    /// tools/art/d27_sheets.py makes the sheets and GIFs. Checks nothing but that the frames are made.
    /// </summary>
    public class D27Frames
    {
        const int W = 1080, H = 2340, CropW = 360, CropH = 420;
        GameSession _s;
        HeroController _hero;
        BillboardSprite _sprite;
        CutoutFigure _cut;
        string _dir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            _dir = Path.Combine(Application.temporaryCachePath, "zd-frames-d27");
            Directory.CreateDirectory(_dir);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/region.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _sprite = _hero.GetComponent<HeroView>().Sprite;
            _cut = _sprite.GetComponent<CutoutFigure>();
            Assert.NotNull(_cut, "the region's hero has the rig");
            _hero.UseDpi(160f);
            _s.State.Clock.SetTime(1, 0.5); // midday: the same light for both runs
            yield return new WaitForSeconds(0.8f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            TestSaves.Clear();
        }

        static Vector3 CamRight() { var r = Camera.main.transform.right; r.y = 0f; return r.normalized; }

        /// <summary>Renders the game camera at W × H; returns the frame and where (pixels) the hero's feet are on it.</summary>
        Texture2D Render(out Vector2 feet)
        {
            var cam = Camera.main;
            var canvas = _s.UI.Canvas;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;
            var vp = cam.WorldToViewportPoint(_sprite.transform.position);
            feet = new Vector2(vp.x * W, vp.y * H);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = oldTarget;
            cam.ResetAspect();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rt.Release();
            Object.Destroy(rt);
            return tex;
        }

        void Save(Texture2D full, Vector2 feet, string path, bool whole)
        {
            if (whole) File.WriteAllBytes(path.Replace(".png", "-full.png"), full.EncodeToPNG());
            int x0 = Mathf.Clamp(Mathf.RoundToInt(feet.x - CropW / 2f), 0, W - CropW);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(feet.y - 40f), 0, H - CropH);   // the feet 40 px above the crop's bottom
            var crop = new Texture2D(CropW, CropH, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, y0, CropW, CropH));
            crop.Apply();
            File.WriteAllBytes(path, crop.EncodeToPNG());
            Object.Destroy(crop);
        }

        IEnumerator Run(string tag, bool rig)
        {
            _cut.enabled = rig;
            var dir = Path.Combine(_dir, tag);
            Directory.CreateDirectory(dir);
            var cc = _hero.GetComponent<CharacterController>();
            cc.minMoveDistance = 0f;
            var right = CamRight();
            Time.captureFramerate = 30;
            var log = new StringBuilder("frame,t,x,phase,showing,shown,frame_index\n");
            // a run-up of 1.2 m, then two cycles (2 × 1.7 m) frame by frame; then she stops and stands 1.5 s (the breath)
            float speed = 2.5f, total = 1.2f + 3.4f, done = 0f;
            int i = 0;
            var start = _sprite.transform.position;
            while (done < total)
            {
                float step = Mathf.Min(total - done, speed * Time.deltaTime);
                cc.Move(right * step);
                done += step;
                yield return new WaitForEndOfFrame();
                if (done < 1.2f) continue;
                var tex = Render(out var feet);
                Save(tex, feet, Path.Combine(dir, $"run-{i:00}.png"), i == 0 || i == 10);
                Object.Destroy(tex);
                log.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.000},{2:0.000},{3:0.000},{4},{5},{6}", i, Time.time,
                    Vector3.Dot(_sprite.transform.position - start, right), _cut.Gait != null ? _cut.Gait.Phase : -1f, _cut.Showing ? 1 : 0, _sprite.Shown, _sprite.FrameIndex));
                i++;
            }
            for (int k = 0; k < 45; k++)
            {
                yield return new WaitForEndOfFrame();
                if (k % 3 != 0) continue;
                var tex = Render(out var feet);
                Save(tex, feet, Path.Combine(dir, $"stand-{k / 3:00}.png"), k == 42);
                Object.Destroy(tex);
            }
            Time.captureFramerate = 0;
            File.WriteAllText(Path.Combine(dir, "log.csv"), log.ToString());
            ZdLog.Info("Frames", $"{tag}: {i} run frames in {dir}, moved {Vector3.Dot(_sprite.transform.position - start, right):0.00} m");
            // back to the start for the other run
            _hero.Teleport(start + Vector3.up * 1f, 0f);
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest, Explicit("frames of D-27: run by hand with -filter D27Frames")]
        public IEnumerator Frames_of_the_side_run_rig_and_video()
        {
            yield return Run("rig", true);
            yield return Run("video", false);
            _cut.enabled = true;
        }
    }
}
