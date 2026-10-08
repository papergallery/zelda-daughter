using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.World;
using Debug = UnityEngine.Debug;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-08: reference frames of the look (F1 road in the morning, F1n the same place at night by a fire, the same without the
    /// effect) and the frame-time measurement. Scene dressing (fire light, probe sprite) is added only to the opened copy; FrameCapture
    /// reopens the scene from disk afterwards. Log: "[ZD:Look] frame … " / "[ZD:Look] time …".
    /// </summary>
    public static class LookFrames
    {
        const string RendererPath = "Assets/Settings/URP_Mobile_Renderer.asset";

        public sealed class Place
        {
            public string Name;
            public Vector3 Hero;
            public float Ortho;
            public float Daylight;
            public bool Evening;
            public bool Fire;
            public Vector3 FireAt;
        }

        // models-test (scenes/models-test.json): road_main from (0,-8) to (6,2), campfire_a at (-4,16).
        public static readonly Place F1 = new Place { Name = "F1", Hero = new Vector3(0.5f, 0, -10f), Ortho = 10f, Daylight = 0.62f };
        public static readonly Place F1n = new Place { Name = "F1n", Hero = new Vector3(0.5f, 0, -10f), Ortho = 10f, Daylight = 0f, Fire = true, FireAt = new Vector3(-1.2f, 0.7f, -10.8f) };

        [MenuItem("Zelda/Look/Reference frames")]
        public static void ReferenceMenu() => Reference("../docs/demo/frames/", "Assets/Scenes/models-test.unity");

        /// <summary>F1 / F1n with the effect, F1 without it, and the other times of day for the sun check. Returns the log lines.</summary>
        public static string Reference(string dir, string scene)
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(dir);
            // The first render after opening a scene in a fresh state is empty: warm up once.
            FrameCapture.Capture(scene, Path.Combine(dir, "warmup.png"), 1f, F1.Hero, F1.Ortho);
            File.Delete(Path.Combine(dir, "warmup.png"));
            log.AppendLine(One(scene, dir, "D-08-F1", F1, true, true));
            log.AppendLine(One(scene, dir, "D-08-F1n", F1n, true, true));
            log.AppendLine(One(scene, dir, "D-08-F1-noeffect", F1, false, true));
            log.AppendLine(One(scene, dir, "D-08-F1n-noeffect", F1n, false, true));
            var day = new Place { Name = "day", Hero = F1.Hero, Ortho = F1.Ortho, Daylight = 1f };
            var evening = new Place { Name = "evening", Hero = F1.Hero, Ortho = F1.Ortho, Daylight = 0.35f, Evening = true };
            log.AppendLine(One(scene, dir, "D-08-time-day", day, true, true));
            log.AppendLine(One(scene, dir, "D-08-time-evening", evening, true, true));
            return log.ToString();
        }

        public static string One(string scene, string dir, string file, Place p, bool effect, bool probeSprite)
        {
            var settings = AssetDatabase.LoadAssetAtPath<WatercolorSettings>(LookSetup.SettingsPath);
            bool was = settings.enabled;
            settings.enabled = effect;
            try
            {
                return FrameCapture.Capture(scene, Path.Combine(dir, file + ".png"), p.Daylight, p.Hero, p.Ortho, 1080, 2340, cam =>
                {
                    var sun = Object.FindFirstObjectByType<SunController>();
                    if (sun != null) sun.Apply(p.Daylight, p.Evening);
                    if (p.Fire) AddFire(p.FireAt);
                    if (probeSprite) AddProbeSprite(p.Hero, cam);
                });
            }
            finally { settings.enabled = was; }
        }

        static void AddFire(Vector3 at)
        {
            var go = new GameObject("ProbeFire");
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f);
            l.range = 9f;
            l.intensity = 6f;
            l.shadows = LightShadows.None;
        }

        /// <summary>A drawn stand-in for the heroine (D-09 is not ready): flat washes with an ink edge, on a camera-facing alpha-clipped quad.</summary>
        static void AddProbeSprite(Vector3 hero, Camera cam)
        {
            var hide = Object.FindFirstObjectByType<ZeldaDaughter.Hero.HeroController>();
            if (hide != null) foreach (var r in hide.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "ProbeSprite";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            var mat = new Material(ModelLook.LoadShader());
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cull", 0f);
            mat.SetTexture("_BaseMap", ProbeTexture());
            ModelLook.Style(mat);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.transform.localScale = new Vector3(1f, 2f, 1f);
            q.transform.position = hero + Vector3.up * 1f;
            // Upright billboard: turns around Y only towards the camera.
            var fwd = cam.transform.forward; fwd.y = 0f;
            q.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        static Texture2D ProbeTexture()
        {
            const int W = 128, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            Color Skin = new Color(0.86f, 0.68f, 0.55f), Hair = new Color(0.22f, 0.14f, 0.10f), Tee = new Color(0.80f, 0.74f, 0.58f),
                  Jeans = new Color(0.34f, 0.42f, 0.52f), Shoe = new Color(0.85f, 0.82f, 0.75f), Pack = new Color(0.45f, 0.48f, 0.26f);
            Color Pick(int x, int y)
            {
                float cx = x - W / 2f;
                if ((cx * cx) / (22f * 22f) + ((y - 208f) * (y - 208f)) / (26f * 26f) < 1f) return y > 212f || Mathf.Abs(cx) > 18f ? Hair : Skin; // head
                if (y > 120 && y <= 176 && Mathf.Abs(cx) < 30f) return Mathf.Abs(cx) > 24f && y < 150 ? Pack : Tee;                                 // torso, pack edge
                if (y > 120 && y <= 170 && Mathf.Abs(cx) < 40f && Mathf.Abs(cx) >= 30f && y > 128) return Skin;                                      // arms
                if (y > 28 && y <= 120 && Mathf.Abs(cx) < 26f && Mathf.Abs(cx) > 3f) return Jeans;                                                    // legs
                if (y >= 10 && y <= 28 && Mathf.Abs(cx) < 30f && Mathf.Abs(cx) > 3f) return Shoe;                                                     // shoes
                return Color.clear;
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) px[y * W + x] = Pick(x, y);
            var ink = new Color(0.20f, 0.12f, 0.07f);
            var outlined = (Color[])px.Clone();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    if (px[y * W + x].a > 0.5f) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < W && ny < H && px[ny * W + nx].a > 0.5f) { outlined[y * W + x] = ink; dy = 3; break; }
                        }
                }
            tex.SetPixels(outlined);
            tex.Apply();
            return tex;
        }

        [MenuItem("Zelda/Look/Measure frame time")]
        public static void MeasureMenu() => Debug.Log(Measure("Assets/Scenes/models-test.unity", 120));

        /// <summary>
        /// Editor frame time of the F1 view, with and without the effect: N camera renders into a 1080×2340 target, a read-back at the end
        /// so the GPU has finished. Editor numbers (not a phone): they compare the two settings; the phone is D-19.
        /// </summary>
        public static string Measure(string scene, int frames)
        {
            var settings = AssetDatabase.LoadAssetAtPath<WatercolorSettings>(LookSetup.SettingsPath);
            bool was = settings.enabled;
            var sb = new StringBuilder();
            try
            {
                foreach (bool on in new[] { false, true, false, true })
                {
                    settings.enabled = on;
                    FrameCapture.Capture(scene, Path.Combine(Path.GetTempPath(), "zd-look-timing.png"), F1.Daylight, F1.Hero, F1.Ortho, 1080, 2340, cam =>
                    {
                        var rt = new RenderTexture(1080, 2340, 24);
                        var prev = cam.targetTexture;
                        cam.targetTexture = rt;
                        for (int i = 0; i < 20; i++) cam.Render(); // warm up
                        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null;
                        var sw = Stopwatch.StartNew();
                        for (int i = 0; i < frames; i++) cam.Render();
                        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null;
                        sw.Stop();
                        sb.AppendLine($"[ZD:Look] time effect={(on ? "on" : "off")} {sw.Elapsed.TotalMilliseconds / frames:0.00} ms/frame ({frames} renders, 1080x2340, editor)");
                        cam.targetTexture = prev;
                        Object.DestroyImmediate(rt);
                        Object.DestroyImmediate(tex);
                    });
                }
            }
            finally { settings.enabled = was; }
            return sb.ToString();
        }
    }
}
