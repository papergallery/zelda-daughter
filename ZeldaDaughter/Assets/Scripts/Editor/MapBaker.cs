using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// The map of the region (project-design.md §4, D-15): the contours of the scene config drawn from above, in code, as an ink drawing on old paper —
    /// the river with its banks, the road and the trails (dashed), the forest as a wash with little trees, the houses as roofs, the field as hatching,
    /// the bridge, the fountain. No relief, no labels (the marks with names are laid over it at run time from what talk has opened), and no "you are
    /// here": the camp, the hero's start and the clearing are not marked. Same config → same bytes (own hash noise, no <c>System.Random</c>).
    /// The sheet is turned a quarter so the long road runs up the page: east is up, north is to the left (<see cref="MapWindow.ToSheet"/> is the
    /// inverse of <see cref="Sheet.P"/>). Result: <c>Assets/Art/Map/&lt;scene&gt;-map.png</c>, imported as a sprite.
    /// </summary>
    public static class MapBaker
    {
        public const string Dir = "Assets/Art/Map";
        /// <summary>Pixels per metre of the sheet (360 × 200 m → 1000 × 1800 px).</summary>
        public const float PixelsPerMetre = 5f;

        static readonly Color PaperColor = new Color(0.90f, 0.83f, 0.67f, 1f);
        static readonly Color InkColor = new Color(0.17f, 0.12f, 0.08f, 1f);
        static readonly Color WaterWash = new Color(0.50f, 0.64f, 0.68f, 1f);
        static readonly Color RoadWash = new Color(0.78f, 0.64f, 0.42f, 1f);
        static readonly Color PlazaWash = new Color(0.80f, 0.74f, 0.60f, 1f);
        static readonly Color ForestWash = new Color(0.55f, 0.64f, 0.38f, 1f);
        static readonly Color RoofWash = new Color(0.76f, 0.50f, 0.38f, 1f);
        static readonly Color FieldWash = new Color(0.78f, 0.70f, 0.38f, 1f);

        public readonly struct Result
        {
            public readonly Sprite Sprite;
            public readonly Vector2 GroundSize;
            public Result(Sprite sprite, Vector2 groundSize) { Sprite = sprite; GroundSize = groundSize; }
        }

        [MenuItem("Zelda/Art/Bake region map")]
        public static void BakeRegion()
        {
            var config = SceneConfig.Parse(File.ReadAllText("../scenes/region.json"));
            var r = Bake(config, ModelCatalog.Load("../data"));
            Debug.Log($"[ZD:Map] baked region map {(r.Sprite != null ? r.Sprite.rect.width + "x" + r.Sprite.rect.height : "failed")}");
        }

        /// <summary>Draws the sheet for the scene and imports it. Never throws for a scene without roads or water (a test scene gets paper and a frame).</summary>
        public static Result Bake(SceneConfig config, ModelCatalog catalog)
        {
            var sheet = new Sheet(config.Ground.SizeX, config.Ground.SizeZ, PixelsPerMetre);
            DrawPaper(sheet);
            DrawForests(sheet, config);
            DrawFields(sheet, config);
            DrawWater(sheet, config);
            DrawRoads(sheet, config);
            DrawBridges(sheet, config);
            DrawHouses(sheet, config, catalog);
            DrawFountains(sheet, config);
            DrawTrees(sheet, config, catalog);
            DrawFrame(sheet);
            DrawCompass(sheet);
            var tex = sheet.ToTexture();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{config.Name}-map.png";
            bool changed = !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png);
            if (changed) File.WriteAllBytes(path, png);
            if (changed || AssetDatabase.LoadAssetAtPath<Sprite>(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.maxTextureSize < 2048 || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return new Result(AssetDatabase.LoadAssetAtPath<Sprite>(path), new Vector2(config.Ground.SizeX, config.Ground.SizeZ));
        }

        // ------------------------------------------------------------------ the sheet: paper, wash and ink layers

        sealed class Sheet
        {
            public readonly int W, H;
            public readonly float Ppm, HalfX, HalfZ;
            public readonly Color[] Paper;
            public readonly Color[] WashColor;
            public readonly float[] WashAlpha;
            public readonly float[] Ink;

            public Sheet(float sizeX, float sizeZ, float ppm)
            {
                Ppm = ppm;
                HalfX = sizeX * 0.5f;
                HalfZ = sizeZ * 0.5f;
                W = Mathf.Max(64, Mathf.RoundToInt(sizeZ * ppm));
                H = Mathf.Max(64, Mathf.RoundToInt(sizeX * ppm));
                Paper = new Color[W * H];
                WashColor = new Color[W * H];
                WashAlpha = new float[W * H];
                Ink = new float[W * H];
            }

            /// <summary>World (x, z) → pixel. East (+x) is up the sheet, north (+z) is to the left.</summary>
            public Vector2 P(float x, float z) => new Vector2((HalfZ - z) * Ppm, (x + HalfX) * Ppm);

            /// <summary>Pixel → world (x, z).</summary>
            public Vector2 World(float px, float py) => new Vector2(py / Ppm - HalfX, HalfZ - px / Ppm);

            public void Stamp(float cx, float cy, float r, float strength)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r + 1f));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r + 1f));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = strength * Mathf.Clamp01(r + 0.6f - d);
                        int i = y * W + x;
                        if (a > Ink[i]) Ink[i] = a;
                    }
            }

            public void Wash(float cx, float cy, float r, Color color, float alpha)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r + 1f));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r + 1f));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = alpha * Mathf.Clamp01((r + 1.5f - d) / 2.5f);
                        WashPixel(y * W + x, color, a);
                    }
            }

            public void WashPixel(int i, Color color, float a)
            {
                if (a <= WashAlpha[i]) return;
                WashAlpha[i] = a;
                WashColor[i] = color;
            }

            public Texture2D ToTexture()
            {
                var px = new Color[W * H];
                for (int i = 0; i < px.Length; i++)
                {
                    var c = Paper[i];
                    float wa = WashAlpha[i];
                    if (wa > 0f) c = Color.Lerp(c, WashColor[i], wa);
                    float ia = Mathf.Clamp01(Ink[i]) * 0.93f;
                    if (ia > 0f) c = Color.Lerp(c, InkColor, ia);
                    c.a = 1f;
                    px[i] = c;
                }
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply(false);
                return tex;
            }
        }

        // ------------------------------------------------------------------ noise (own, so the bytes never depend on a platform's random)

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(xi, yi, seed), b = Hash01(xi + 1, yi, seed), c = Hash01(xi, yi + 1, seed), d = Hash01(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ------------------------------------------------------------------ line work

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static List<Vector2> Smooth(List<Vector2> p)
        {
            if (p.Count < 3) return p;
            var o = new List<Vector2>();
            for (int i = 0; i < p.Count - 1; i++)
            {
                var p0 = p[Mathf.Max(i - 1, 0)];
                var p1 = p[i];
                var p2 = p[i + 1];
                var p3 = p[Mathf.Min(i + 2, p.Count - 1)];
                for (int k = 0; k < 12; k++) o.Add(CatmullRom(p0, p1, p2, p3, k / 12f));
            }
            o.Add(p[p.Count - 1]);
            return o;
        }

        /// <summary>The line moved sideways by <paramref name="d"/> pixels (positive: to the left of its direction).</summary>
        static List<Vector2> Offset(List<Vector2> p, float d)
        {
            var o = new List<Vector2>(p.Count);
            for (int i = 0; i < p.Count; i++)
            {
                var dir = p[Mathf.Min(i + 1, p.Count - 1)] - p[Mathf.Max(i - 1, 0)];
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
                dir.Normalize();
                o.Add(p[i] + new Vector2(-dir.y, dir.x) * d);
            }
            return o;
        }

        static List<Vector2> Pixels(Sheet s, IEnumerable<Pt> points) => points.Select(p => s.P(p.X, p.Z)).ToList();

        /// <summary>Walks the polyline in steps of one pixel or less, calling the action with the position and the arc length so far.</summary>
        static void Walk(List<Vector2> pts, float step, Action<Vector2, float> at)
        {
            float s = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 1e-4f) continue;
                int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n;
                    at(Vector2.Lerp(a, b, t), s + len * t);
                }
                s += len;
            }
            if (pts.Count > 0) at(pts[pts.Count - 1], s);
        }

        /// <summary>A pen line: a little unsteady (the hand), optionally dashed.</summary>
        static void InkLine(Sheet sh, List<Vector2> pts, float r, float strength, float wobble, int seed, float dashOn = 0f, float dashOff = 0f)
        {
            Walk(pts, 0.6f, (p, s) =>
            {
                if (dashOn > 0f && (s % (dashOn + dashOff)) >= dashOn) return;
                float wx = (Noise(s * 0.03f, 0.5f, seed) - 0.5f) * 2f * wobble;
                float wy = (Noise(s * 0.03f, 7.5f, seed + 1) - 0.5f) * 2f * wobble;
                float rr = r * (0.85f + 0.3f * Noise(s * 0.11f, 3.1f, seed + 2));
                sh.Stamp(p.x + wx, p.y + wy, rr, strength);
            });
        }

        static void WashLine(Sheet sh, List<Vector2> pts, float r, Color color, float alpha) => Walk(pts, 1f, (p, s) => sh.Wash(p.x, p.y, r, color, alpha));

        // ------------------------------------------------------------------ the parts of the drawing

        static void DrawPaper(Sheet s)
        {
            for (int y = 0; y < s.H; y++)
                for (int x = 0; x < s.W; x++)
                {
                    float n = Noise(x * 0.004f, y * 0.004f, 1) * 0.5f + Noise(x * 0.02f, y * 0.02f, 2) * 0.3f + Noise(x * 0.15f, y * 0.15f, 3) * 0.2f;
                    float shade = 0.92f + 0.12f * n;
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(s.W - 1 - x, s.H - 1 - y));
                    float v = Mathf.Clamp01(1f - edge / 90f);
                    float vignette = 1f - 0.30f * v * v;
                    float f = shade * vignette;
                    s.Paper[y * s.W + x] = new Color(PaperColor.r * f, PaperColor.g * f * 0.985f, PaperColor.b * f * 0.95f, 1f);
                }
        }

        static bool IsTreeScatter(ScatterConfig sc) =>
            sc.Models.Count > 0 && sc.Models[0].Id.StartsWith("tree", StringComparison.Ordinal) && sc.Area.Kind == "rect";

        /// <summary>Thick woods (4 trees per 100 m² and more) are tinted; every wood gets little trees.</summary>
        static void DrawForests(Sheet s, SceneConfig config)
        {
            foreach (var sc in config.Scatter)
            {
                if (!IsTreeScatter(sc) || sc.Density < 3f) continue;
                var area = sc.Area;
                var b = area.Bounds();
                var lo = s.P(b.MaxX, b.MaxZ);
                var hi = s.P(b.MinX, b.MinZ);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.x, hi.x)) - 40), x1 = Mathf.Min(s.W - 1, Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)) + 40);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.y, hi.y)) - 40), y1 = Mathf.Min(s.H - 1, Mathf.CeilToInt(Mathf.Max(lo.y, hi.y)) + 40);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var w = s.World(x, y);
                        float d = area.SignedDistance(w.x, w.y);
                        float n = Noise(x * 0.02f, y * 0.02f, 11) * 2f - 1f;
                        float t = Mathf.Clamp01((-(d + n * 6f)) / 6f);
                        if (t > 0f) s.WashPixel(y * s.W + x, ForestWash, 0.32f * t);
                    }
            }
        }

        static void DrawFields(Sheet s, SceneConfig config)
        {
            foreach (var z in config.Zones.Where(z => z.Tags.Contains("field") && z.Kind == "rect"))
            {
                var b = z.Bounds();
                var lo = s.P(b.MaxX, b.MaxZ);
                var hi = s.P(b.MinX, b.MinZ);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.x, hi.x))), x1 = Mathf.Min(s.W - 1, Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.y, hi.y))), y1 = Mathf.Min(s.H - 1, Mathf.CeilToInt(Mathf.Max(lo.y, hi.y)));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var w = s.World(x, y);
                        if (z.SignedDistance(w.x, w.y) <= 0f) s.WashPixel(y * s.W + x, FieldWash, 0.28f);
                    }
                double a = z.Rotation * Math.PI / 180.0;
                float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                float hx = z.Size.X / 2f, hz = z.Size.Z / 2f;
                int row = 0;
                for (float lz = -hz + 0.7f; lz < hz - 0.3f; lz += 1.4f)
                {
                    var p0 = Local(s, z.Center, cos, sin, -hx + 0.4f, lz);
                    var p1 = Local(s, z.Center, cos, sin, hx - 0.4f, lz);
                    InkLine(s, new List<Vector2> { p0, p1 }, 0.8f, 0.5f, 0.8f, 20 + row++);
                }
                var rect = new List<Vector2> { Local(s, z.Center, cos, sin, -hx, -hz), Local(s, z.Center, cos, sin, hx, -hz), Local(s, z.Center, cos, sin, hx, hz), Local(s, z.Center, cos, sin, -hx, hz), Local(s, z.Center, cos, sin, -hx, -hz) };
                InkLine(s, rect, 1.0f, 0.7f, 1.0f, 40);
            }
        }

        /// <summary>A point of a rotated rectangle (local metres from its centre) as a pixel; Unity's yaw, as in <see cref="Area.SignedDistance"/>.</summary>
        static Vector2 Local(Sheet s, Pt center, float cos, float sin, float lx, float lz) =>
            s.P(center.X + lx * cos + lz * sin, center.Z - lx * sin + lz * cos);

        static void DrawWater(Sheet s, SceneConfig config)
        {
            int seed = 100;
            foreach (var w in config.Water)
            {
                if (w.Points.Count < 2) continue;
                var line = Smooth(Pixels(s, w.Points));
                float half = w.Width * s.Ppm * 0.5f;
                WashLine(s, line, half, WaterWash, 0.55f);
                InkLine(s, Smooth(Offset(Pixels(s, w.Points), half)), 1.3f, 0.9f, 1.6f, ++seed);
                InkLine(s, Smooth(Offset(Pixels(s, w.Points), -half)), 1.3f, 0.9f, 1.6f, ++seed);
                // ripples: little waves along the middle
                float next = 40f;
                Walk(line, 1f, (p, arc) =>
                {
                    if (arc < next) return;
                    next = arc + 75f;
                    float side = (Hash01((int)arc, 5, seed) - 0.5f) * half * 0.9f;
                    var c = p + new Vector2(side, 0f);
                    var wave = new List<Vector2>();
                    for (int k = -6; k <= 6; k++) wave.Add(c + new Vector2(k * 1.6f, 2.2f * Mathf.Sin(k * 0.52f)));
                    InkLine(s, wave, 0.7f, 0.6f, 0f, 0);
                });
            }
        }

        static void DrawRoads(Sheet s, SceneConfig config)
        {
            int seed = 200;
            foreach (var path in config.Paths)
            {
                if (path.Points.Count < 2 || path.Id.StartsWith("door", StringComparison.Ordinal)) continue;
                float width = path.Width * s.Ppm;
                var pts = Pixels(s, path.Points);
                if (path.Width >= 12f)
                {
                    // a square: the strip as a washed plate with an outline
                    var line = Smooth(pts);
                    WashLine(s, line, width * 0.5f, PlazaWash, 0.6f);
                    InkLine(s, Smooth(Offset(pts, width * 0.5f)), 1.2f, 0.8f, 1.2f, ++seed, 14f, 6f);
                    InkLine(s, Smooth(Offset(pts, -width * 0.5f)), 1.2f, 0.8f, 1.2f, ++seed, 14f, 6f);
                }
                else if (path.Id.StartsWith("trail", StringComparison.Ordinal))
                {
                    InkLine(s, Smooth(pts), 1.1f, 0.75f, 1.4f, ++seed, 11f, 9f);
                }
                else
                {
                    var line = Smooth(pts);
                    WashLine(s, line, width * 0.5f, RoadWash, 0.5f);
                    InkLine(s, Smooth(Offset(pts, width * 0.5f)), 1.2f, 0.8f, 1.5f, ++seed);
                    InkLine(s, Smooth(Offset(pts, -width * 0.5f)), 1.2f, 0.8f, 1.5f, ++seed);
                }
            }
        }

        static void DrawBridges(Sheet s, SceneConfig config)
        {
            foreach (var z in config.Zones.Where(z => z.Tags.Contains("bridge") && z.Kind == "rect"))
            {
                var b = z.Bounds();
                var lo = s.P(b.MaxX, b.MaxZ);
                var hi = s.P(b.MinX, b.MinZ);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.x, hi.x)) - 2), x1 = Mathf.Min(s.W - 1, Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)) + 2);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.y, hi.y)) - 2), y1 = Mathf.Min(s.H - 1, Mathf.CeilToInt(Mathf.Max(lo.y, hi.y)) + 2);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var w = s.World(x, y);
                        if (z.SignedDistance(w.x, w.y) > 0f) continue;
                        int i = y * s.W + x;
                        s.Ink[i] = 0f;
                        s.WashAlpha[i] = 0f;
                        s.WashPixel(i, RoadWash, 0.75f);
                    }
                double a = z.Rotation * Math.PI / 180.0;
                float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                float hx = z.Size.X / 2f, hz = z.Size.Z / 2f;
                InkLine(s, new List<Vector2> { Local(s, z.Center, cos, sin, -hx, -hz), Local(s, z.Center, cos, sin, hx, -hz) }, 1.4f, 0.95f, 0.6f, 301);
                InkLine(s, new List<Vector2> { Local(s, z.Center, cos, sin, -hx, hz), Local(s, z.Center, cos, sin, hx, hz) }, 1.4f, 0.95f, 0.6f, 302);
                int k = 0;
                for (float lx = -hx + 0.8f; lx < hx - 0.4f; lx += 1.2f, k++)
                    InkLine(s, new List<Vector2> { Local(s, z.Center, cos, sin, lx, -hz), Local(s, z.Center, cos, sin, lx, hz) }, 0.8f, 0.7f, 0.3f, 310 + k);
            }
        }

        struct Box { public float Cx, Cz, Hx, Hz, Cos, Sin; }

        static bool BoxOf(ObjectConfig o, ModelCatalog catalog, out Box box)
        {
            box = default;
            if (string.IsNullOrEmpty(o.Model) || catalog == null || !catalog.Has(o.Model)) return false;
            Box3 b;
            try { b = catalog.Bounds(o.Model); }
            catch (Exception) { return false; }
            double a = o.Rotation.Y * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            float lx = b.CenterX * o.Scale.X, lz = b.CenterZ * o.Scale.Z;
            box = new Box
            {
                Cx = o.Position.X + lx * cos + lz * sin,
                Cz = o.Position.Z - lx * sin + lz * cos,
                Hx = Mathf.Max(0.5f, b.SizeX * o.Scale.X / 2f),
                Hz = Mathf.Max(0.5f, b.SizeZ * o.Scale.Z / 2f),
                Cos = cos,
                Sin = sin,
            };
            return true;
        }

        static Vector2 Corner(Sheet s, Box b, float sx, float sz)
        {
            float lx = sx * b.Hx, lz = sz * b.Hz;
            return s.P(b.Cx + lx * b.Cos + lz * b.Sin, b.Cz - lx * b.Sin + lz * b.Cos);
        }

        static void DrawHouses(Sheet s, SceneConfig config, ModelCatalog catalog)
        {
            int seed = 400;
            foreach (var o in config.Objects)
            {
                if (!o.Tags.Contains("building") || !BoxOf(o, catalog, out var b)) continue;
                var c0 = Corner(s, b, -1, -1);
                var c1 = Corner(s, b, 1, -1);
                var c2 = Corner(s, b, 1, 1);
                var c3 = Corner(s, b, -1, 1);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x))) - 1);
                int x1 = Mathf.Min(s.W - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y))) - 1);
                int y1 = Mathf.Min(s.H - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y))) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var w = s.World(x, y);
                        float dx = w.x - b.Cx, dz = w.y - b.Cz;
                        float lx = dx * b.Cos - dz * b.Sin, lz = dx * b.Sin + dz * b.Cos;
                        if (Mathf.Abs(lx) > b.Hx || Mathf.Abs(lz) > b.Hz) continue;
                        int i = y * s.W + x;
                        s.Ink[i] = 0f;
                        s.WashAlpha[i] = 0f;
                        s.WashPixel(i, RoofWash, 0.7f);
                    }
                InkLine(s, new List<Vector2> { c0, c1, c2, c3, c0 }, 1.5f, 0.95f, 0.7f, ++seed);
                // the ridge runs along the longer side
                var ridge = b.Hx >= b.Hz
                    ? new List<Vector2> { Corner(s, b, -1, 0), Corner(s, b, 1, 0) }
                    : new List<Vector2> { Corner(s, b, 0, -1), Corner(s, b, 0, 1) };
                InkLine(s, ridge, 1.0f, 0.8f, 0.5f, ++seed);
            }
        }

        static void DrawFountains(Sheet s, SceneConfig config)
        {
            int seed = 500;
            foreach (var o in config.Objects.Where(o => o.Tags.Contains("fountain")))
            {
                var c = s.P(o.Position.X, o.Position.Z);
                InkLine(s, Circle(c, 2.6f * s.Ppm, 28), 1.3f, 0.9f, 0.8f, ++seed);
                InkLine(s, Circle(c, 1.2f * s.Ppm, 20), 1.0f, 0.8f, 0.6f, ++seed);
            }
        }

        static List<Vector2> Circle(Vector2 c, float r, int n, float jitter = 0f, int seed = 0)
        {
            var pts = new List<Vector2>();
            for (int k = 0; k <= n; k++)
            {
                float a = (k % n) / (float)n * Mathf.PI * 2f;
                float rr = r * (1f + jitter * (Hash01(k % n, 3, seed) - 0.5f));
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            return pts;
        }

        /// <summary>Little trees (a crown and a trunk) in the woods, spaced by how thick the wood is, away from roads, water and houses.</summary>
        static void DrawTrees(Sheet s, SceneConfig config, ModelCatalog catalog)
        {
            var boxes = new List<Box>();
            foreach (var o in config.Objects)
                if (o.Tags.Contains("building") && BoxOf(o, catalog, out var b)) boxes.Add(b);
            var clear = config.Zones.Where(z => z.Tags.Contains("glade") || z.Tags.Contains("lair") || z.Tags.Contains("camp") || z.Tags.Contains("square")).ToList();

            int seed = 600;
            foreach (var sc in config.Scatter)
            {
                if (!IsTreeScatter(sc)) continue;
                var area = sc.Area;
                float spacing = 1.6f * 10f / Mathf.Sqrt(Mathf.Max(0.2f, sc.Density)); // metres between little trees
                var b = area.Bounds();
                int ix = 0;
                for (float x = b.MinX; x < b.MaxX; x += spacing, ix++)
                {
                    int iz = 0;
                    for (float z = b.MinZ; z < b.MaxZ; z += spacing, iz++)
                    {
                        float jx = (Hash01(ix, iz, seed) - 0.5f) * spacing * 0.8f, jz = (Hash01(ix, iz, seed + 1) - 0.5f) * spacing * 0.8f;
                        float px = x + jx, pz = z + jz;
                        if (area.SignedDistance(px, pz) > -2f) continue;
                        if (Math.Abs(px) > s.HalfX - 6f || Math.Abs(pz) > s.HalfZ - 6f) continue;
                        if (Blocked(config, boxes, clear, px, pz)) continue;
                        var c = s.P(px, pz);
                        if (Vector2.Distance(c, MapWindow.CompassPixel) < 90f) continue;
                        float r = 6f + 3f * Hash01(ix, iz, seed + 2);
                        InkLine(s, Circle(c + new Vector2(0f, r), r, 14, 0.35f, ix * 31 + iz), 0.9f, 0.85f, 0.5f, ++seed % 90 + 600);
                        InkLine(s, new List<Vector2> { c, c + new Vector2(0f, r * 0.5f) }, 0.9f, 0.85f, 0.2f, 650);
                    }
                }
            }
        }

        static bool Blocked(SceneConfig config, List<Box> boxes, List<ZoneConfig> clear, float x, float z)
        {
            foreach (var p in config.Paths)
                if (!p.Id.StartsWith("door", StringComparison.Ordinal) && p.SignedDistance(x, z) < 3f) return true;
            foreach (var w in config.Water)
                if (w.SignedDistance(x, z) < 3f) return true;
            foreach (var b in boxes)
            {
                float dx = x - b.Cx, dz = z - b.Cz;
                float lx = dx * b.Cos - dz * b.Sin, lz = dx * b.Sin + dz * b.Cos;
                if (Mathf.Abs(lx) < b.Hx + 4f && Mathf.Abs(lz) < b.Hz + 4f) return true;
            }
            foreach (var zone in clear)
                if (zone.SignedDistance(x, z) < 2f) return true;
            return false;
        }

        static void DrawFrame(Sheet s)
        {
            Func<float, List<Vector2>> ring = inset => new List<Vector2>
            {
                new Vector2(inset, inset), new Vector2(s.W - 1 - inset, inset), new Vector2(s.W - 1 - inset, s.H - 1 - inset),
                new Vector2(inset, s.H - 1 - inset), new Vector2(inset, inset),
            };
            InkLine(s, ring(20f), 1.7f, 0.95f, 1.4f, 700);
            InkLine(s, ring(30f), 0.9f, 0.8f, 1.2f, 701);
        }

        /// <summary>A small compass: a ring, an arrow to the north (left on this sheet) and a cross; the letter itself is drawn by the window.</summary>
        static void DrawCompass(Sheet s)
        {
            var c = MapWindow.CompassPixel;
            InkLine(s, Circle(c, 34f, 28), 1.3f, 0.9f, 0.8f, 710);
            InkLine(s, new List<Vector2> { c + new Vector2(-52f, 0f), c + new Vector2(40f, 0f) }, 1.2f, 0.9f, 0.4f, 711);
            InkLine(s, new List<Vector2> { c + new Vector2(0f, -40f), c + new Vector2(0f, 40f) }, 1.0f, 0.8f, 0.4f, 712);
            InkLine(s, new List<Vector2> { c + new Vector2(-52f, 0f), c + new Vector2(-36f, 9f) }, 1.3f, 0.95f, 0.2f, 713);
            InkLine(s, new List<Vector2> { c + new Vector2(-52f, 0f), c + new Vector2(-36f, -9f) }, 1.3f, 0.95f, 0.2f, 714);
        }
    }
}
