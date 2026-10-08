using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-28 (ADR-0010, пилот «уголок f1»): рисованные куски мира на прежних 3D-коллайдерах и рисованная земля — для сцены, у которой в
    /// конфиге есть секция <c>painted</c> (другие сцены не меняются; SceneConfig её не читает — ядро не трогаем, пока пилот не победил):
    /// <code>
    /// "painted": { "atlas": "Assets/Art/Painted/painted.json",
    ///              "models": { "tree_oak": ["tree_big"], "rock_large_a": ["rock_big_a", "rock_mid_a"], … },   // id модели → рисунки (выбор по id)
    ///              "flip": ["rock", "plant"],          // виды, которые можно отражать (свет на рисунке ровный — стиль-библия)
    ///              "fit": ["rock", "prop"],            // виды, ширина рисунка которых = ширина модели на экране (по model-bounds)
    ///              "shadow": "blob",                   // "model" (по умолчанию) — 3D-модель отбрасывает тень солнца (ShadowsOnly);
    ///                                                  // "blob" — мягкое пятно под основанием (ref2game: «contact shadows» кодом), модель не рисуется
    ///              "ground": { "meadow": "meadow", "road": "road" } }
    /// </code>
    /// <list type="bullet">
    /// <item>Объект с моделью из <c>models</c>: модель остаётся невидимым заместителем тени (ShadowsOnly) и коллайдером (как в 3D, по
    /// model-bounds), рядом — карточка «painted» из атласа, повёрнутая к камере (наклон и поворот камеры сцены), основание — в корне.</item>
    /// <item>Россыпь без коллайдера (трава, цветы): GameObject не нужен — карточки сливаются в один меш на квадрат 24 м («PaintedScatter»).</item>
    /// <item>Земля: материал Zelda/Painted (_PAINTED_GROUND) — заливки луга и дороги по мировым XZ, маска дороги запекается здесь из
    /// <c>paths</c> (Assets/Generated/Painted/mask_&lt;сцена&gt;.png); ленты дорог прячутся.</item>
    /// </list>
    /// Приёмы — ref2game (MIT, github.com/studioigor/ref2game): куски рисует нейросеть, свет/тени/движение — код; код свой.
    /// Лог: "[ZD:Scene] painted &lt;сцена&gt; cards=N merged=M ground=on".
    /// </summary>
    public static partial class SceneBuilder
    {
        const string PaintedShaderPath = "Assets/Shaders/ZeldaPainted.shader";
        const string PaintedGenDir = "Assets/Generated/Painted";
        const float PaintedSink = 0.03f;     // основание чуть под землёй — без светлой щели
        const float PaintedChunk = 24f;
        const int PaintedMaskRes = 512;

        sealed class PaintedSprite { public Rect Uv; public Vector2 Size; public float PivotX; public string Kind; public Vector2[] Hull; }

        sealed class PaintedSetup
        {
            public Dictionary<string, PaintedSprite> Sprites;
            public Dictionary<string, string[]> Models;
            public HashSet<string> Flip;
            public HashSet<string> Fit;
            public bool Blob;
            public HashSet<string> BlobKinds;
            public string AtlasTexture;
            public JObject GroundAtlas;
            public JObject Ground;
        }

        /// <summary>The <c>painted</c> section of the scene config, or null — then the scene is built as before.</summary>
        static PaintedSetup ReadPainted(string configPath)
        {
            var root = JObject.Parse(File.ReadAllText(configPath));
            if (!(root["painted"] is JObject p)) return null;
            string atlasPath = (string)p["atlas"] ?? "Assets/Art/Painted/painted.json";
            var atlas = JObject.Parse(File.ReadAllText(atlasPath));
            float w = (float)atlas["size"][0], h = (float)atlas["size"][1];
            var sprites = new Dictionary<string, PaintedSprite>();
            foreach (var s in ((JObject)atlas["sprites"]).Properties())
            {
                var r = s.Value["rect"];
                sprites[s.Name] = new PaintedSprite
                {
                    Uv = new Rect((float)r[0] / w, (float)r[1] / h, (float)r[2] / w, (float)r[3] / h),
                    Size = new Vector2((float)s.Value["size_m"][0], (float)s.Value["size_m"][1]),
                    PivotX = (float?)s.Value["pivot_x"] ?? 0.5f,
                    Kind = (string)s.Value["kind"] ?? "",
                    Hull = s.Value["hull"] is JArray hull && hull.Count >= 3
                        ? hull.Select(q => new Vector2((float)q[0], (float)q[1])).ToArray()
                        : new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                };
            }
            var models = new Dictionary<string, string[]>();
            foreach (var m in ((JObject)p["models"] ?? new JObject()).Properties())
            {
                var ids = m.Value.Select(x => (string)x).ToArray();
                foreach (var id in ids)
                    if (!sprites.ContainsKey(id)) throw new InvalidOperationException($"[ZD:Scene] painted: no sprite '{id}' in {atlasPath} (model {m.Name})");
                models[m.Name] = ids;
            }
            return new PaintedSetup
            {
                Sprites = sprites, Models = models,
                Flip = new HashSet<string>(((JArray)p["flip"] ?? new JArray()).Select(x => (string)x)),
                Fit = new HashSet<string>(((JArray)p["fit"] ?? new JArray()).Select(x => (string)x)),
                Blob = (string)p["shadow"] == "blob",
                BlobKinds = new HashSet<string>(((JArray)p["blobKinds"] ?? new JArray("tree", "rock", "prop")).Select(x => (string)x)),
                AtlasTexture = (string)atlas["texture"], GroundAtlas = atlas["ground"] as JObject, Ground = p["ground"] as JObject,
            };
        }

        static uint PaintedHash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) unchecked { h = (h ^ c) * 16777619; }
            return h;
        }

        static void PaintedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(PaintedGenDir)) AssetDatabase.CreateFolder("Assets/Generated", "Painted");
        }

        static void PaintedTexture(string path, bool alpha, bool repeat)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new InvalidOperationException("[ZD:Scene] painted: no texture " + path);
            bool dirty = ti.textureType != TextureImporterType.Default || ti.sRGBTexture != true || ti.mipmapEnabled != true ||
                         ti.alphaIsTransparency != alpha || ti.wrapMode != (repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) || ti.maxTextureSize != 2048;
            if (!dirty) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = alpha;
            ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }

        static Material PaintedMaterial(string name, Action<Material> setup)
        {
            PaintedFolder();
            string path = $"{PaintedGenDir}/{name}.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PaintedShaderPath) ?? Shader.Find("Zelda/Painted");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader);
            mat.shader = shader;
            setup(mat);
            if (created) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>One card in card space (x across, y up, base centre at <paramref name="at"/>): the convex outline of the drawing, uv1 = (height m, root).</summary>
        static void PaintedCard(List<Vector3> v, List<Vector2> uv, List<Vector4> card, List<Color32> col, List<Vector3> n, List<int> tris,
            Vector3 at, Quaternion rot, PaintedSprite s, float scale, bool flip, Color tint)
        {
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
            Vector3 b = at + Vector3.down * PaintedSink;
            int i = v.Count;
            foreach (var h in s.Hull)
            {
                float x = (flip ? (1f - h.x) - (1f - s.PivotX) : h.x - s.PivotX) * s.Size.x * scale;
                float y = h.y * s.Size.y * scale;
                v.Add(b + right * x + up * y);
                uv.Add(new Vector2(s.Uv.xMin + h.x * s.Uv.width, s.Uv.yMin + h.y * s.Uv.height));
                card.Add(new Vector4(y, at.x, at.y, at.z));
                col.Add(tint);
                n.Add(Vector3.up);
            }
            for (int k = 1; k + 1 < s.Hull.Length; k++) { tris.Add(i); tris.Add(i + k); tris.Add(i + k + 1); }
        }

        static Mesh PaintedMesh(string name, List<Vector3> v, List<Vector2> uv, List<Vector4> card, List<Color32> col, List<Vector3> n, List<int> tris)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, card); mesh.SetColors(col); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Shared card mesh of one sprite (base centre at the origin, unrotated; the object's «painted» child turns it to the camera).</summary>
        static Mesh PaintedCardAsset(string spriteId, PaintedSprite s, bool flip)
        {
            PaintedFolder();
            string name = $"card_{spriteId}{(flip ? "_f" : "")}";
            string path = $"{PaintedGenDir}/{name}.asset";
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var card = new List<Vector4>(); var col = new List<Color32>(); var n = new List<Vector3>(); var tris = new List<int>();
            PaintedCard(v, uv, card, col, n, tris, Vector3.zero, Quaternion.identity, s, 1f, flip, Color.white);
            var fresh = PaintedMesh(name, v, uv, card, col, n, tris);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, card); mesh.SetColors(col); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            UnityEngine.Object.DestroyImmediate(fresh);
            return mesh;
        }

        static Color PaintedTint(string id)
        {
            uint h = PaintedHash(id);
            float v = 0.92f + 0.12f * ((h & 0xff) / 255f);
            float warm = ((h >> 8 & 0xff) / 255f - 0.5f) * 0.06f;
            return new Color(Mathf.Clamp01(v + warm), Mathf.Clamp01(v), Mathf.Clamp01(v - warm), 1f);
        }

        /// <summary>After the whole scene is placed: models → painted cards, scatter → merged cards, ground → painted washes.</summary>
        static void BuildPainted(string configPath, SceneConfig config, ModelCatalog catalog, GameObject ground)
        {
            var setup = ReadPainted(configPath);
            if (setup == null) return;
            _scatterModels = Scatterer.Generate(config, catalog).ToDictionary(q => q.Id, q => q.ModelId);
            PaintedTexture(setup.AtlasTexture, alpha: true, repeat: false);
            var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(setup.AtlasTexture);
            var cardMat = PaintedMaterial($"painted_cards_{config.Name}", m =>
            {
                m.SetTexture("_BaseMap", atlasTex);
                m.DisableKeyword("_PAINTED_GROUND");
                m.SetFloat("_Ground", 0f);
                m.SetFloat("_Cull", 0f);
                m.SetFloat("_Cutoff", 0.5f);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });
            var rot = Quaternion.Euler(config.Camera.Pitch, config.Camera.Yaw, 0f);

            // 1. Objects and colliding scatter: the model stays as a shadow-only stand-in and the collider; a «painted» card is added.
            int cards = 0;
            var merge = new List<(GameObject go, string sprite, bool flip)>();
            foreach (var holder in new[] { "Objects", "Scatter" }.Select(GameObject.Find).Where(g => g != null))
            {
                foreach (Transform t in holder.transform.Cast<Transform>().ToList())
                {
                    string modelId = ModelOf(t.name, config, holder.name == "Scatter");
                    if (modelId == null || !setup.Models.TryGetValue(modelId, out var options)) continue;
                    uint h = PaintedHash(t.name);
                    string spriteId = options[h % (uint)options.Length];
                    var s = setup.Sprites[spriteId];
                    bool flip = setup.Flip.Contains(s.Kind) && (h >> 12 & 1) == 1;
                    bool solid = t.GetComponent<Collider>() != null;
                    if (holder.name == "Scatter" && !solid) { merge.Add((t.gameObject, spriteId, flip)); continue; }
                    bool modelShadow = !setup.Blob && (solid || s.Kind == "tree");
                    foreach (var r in t.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = modelShadow ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off;
                        if (!modelShadow) r.enabled = false;
                    }
                    var go = new GameObject("painted", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(t, false);
                    go.transform.rotation = rot;
                    if (setup.Fit.Contains(s.Kind)) // the drawing as wide on screen as the model it stands for (its collider): ref2game «normalise in code»
                    {
                        float w = PaintedScreenWidth(t, catalog.Bounds(modelId), rot * Vector3.right);
                        go.transform.localScale = Vector3.one * (w / (s.Size.x * Mathf.Max(t.lossyScale.y, 1e-3f)));
                    }
                    go.GetComponent<MeshFilter>().sharedMesh = PaintedCardAsset(spriteId, s, flip);
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = cardMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (setup.Blob && setup.BlobKinds.Contains(s.Kind)) PaintedBlob(t, s.Size.x * go.transform.localScale.x * t.lossyScale.y, rot);
                    cards++;
                }
            }

            // 2. Non-colliding scatter (grass, flowers): one mesh per chunk, the per-tuft GameObjects go.
            int merged = 0;
            if (merge.Count > 0)
            {
                var root = new GameObject("PaintedScatter").transform;
                foreach (var chunk in merge.GroupBy(m => (Mathf.FloorToInt(m.go.transform.position.x / PaintedChunk), Mathf.FloorToInt(m.go.transform.position.z / PaintedChunk)))
                                           .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
                {
                    var origin = new Vector3((chunk.Key.Item1 + 0.5f) * PaintedChunk, 0f, (chunk.Key.Item2 + 0.5f) * PaintedChunk);
                    var v = new List<Vector3>(); var uv = new List<Vector2>(); var card = new List<Vector4>(); var col = new List<Color32>(); var n = new List<Vector3>(); var tris = new List<int>();
                    foreach (var (go, sprite, flip) in chunk.OrderBy(c => c.go.name, StringComparer.Ordinal))
                    {
                        var p = go.transform.position;
                        PaintedCard(v, uv, card, col, n, tris, new Vector3(p.x, 0f, p.z) - origin, rot, setup.Sprites[sprite], go.transform.localScale.y, flip, PaintedTint(go.name));
                        merged++;
                    }
                    var part = new GameObject($"painted_{chunk.Key.Item1}_{chunk.Key.Item2}", typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(root, false);
                    part.transform.localPosition = origin;
                    part.GetComponent<MeshFilter>().sharedMesh = PaintedMesh(part.name, v, uv, card, col, n, tris);
                    var mr = part.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = cardMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                foreach (var (go, _, _) in merge) UnityEngine.Object.DestroyImmediate(go);
            }

            // 3. Ground: painted washes by world XZ, the road from the paths; the ribbons are hidden (the terrain map is unchanged).
            bool groundOn = false;
            if (setup.Ground != null && setup.GroundAtlas != null && ground != null)
            {
                var meadow = (JObject)setup.GroundAtlas[(string)setup.Ground["meadow"] ?? "meadow"];
                var road = (JObject)setup.GroundAtlas[(string)setup.Ground["road"] ?? "road"];
                PaintedTexture((string)meadow["texture"], alpha: false, repeat: true);
                PaintedTexture((string)road["texture"], alpha: false, repeat: true);
                var mask = PaintedRoadMask(config);
                var rect = new Vector4(-config.Ground.SizeX / 2f, -config.Ground.SizeZ / 2f, config.Ground.SizeX, config.Ground.SizeZ);
                var gm = PaintedMaterial($"painted_ground_{config.Name}", m =>
                {
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>((string)meadow["texture"]));
                    m.SetTexture("_RoadMap", AssetDatabase.LoadAssetAtPath<Texture2D>((string)road["texture"]));
                    m.SetTexture("_MaskMap", mask);
                    m.SetVector("_MaskRect", rect);
                    m.SetVector("_Tile", new Vector4((float)meadow["tile_m"], (float)road["tile_m"], 0, 0));
                    m.EnableKeyword("_PAINTED_GROUND");
                    m.SetFloat("_Ground", 1f);
                    m.SetFloat("_Cull", 2f);
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = (int)RenderQueue.Geometry;
                });
                ground.GetComponent<Renderer>().sharedMaterial = gm;
                var paths = GameObject.Find("Paths");
                if (paths != null) foreach (var r in paths.GetComponentsInChildren<Renderer>()) r.enabled = false;
                groundOn = true;
            }
            Debug.Log($"[ZD:Scene] painted {config.Name} cards={cards} merged={merged} ground={(groundOn ? "on" : "off")}");
        }

        /// <summary>The model id of a placed object: config objects by id, scatter placements by their id (Scatterer — regenerated, deterministic).</summary>
        static string ModelOf(string name, SceneConfig config, bool scatter)
        {
            if (!scatter) return config.Objects.FirstOrDefault(o => o.Id == name)?.Model;
            return _scatterModels != null && _scatterModels.TryGetValue(name, out var m) ? m : null;
        }

        static Dictionary<string, string> _scatterModels;

        static Material _paintedBlobMat;
        static Mesh _paintedQuad;

        /// <summary>
        /// A soft cool-umber contact shadow under a painted object (ref2game effects: shadows are code, shaped by the footprint): a flat ellipse at the
        /// root, <paramref name="width"/> × 0.62·width, lying across the screen, transparent, no depth write (the ground and the hero's blob work the same way).
        /// </summary>
        static void PaintedBlob(Transform t, float width, Quaternion cardRot)
        {
            if (_paintedBlobMat == null)
            {
                PaintedFolder();
                string tex = $"{PaintedGenDir}/blob.png";
                if (!File.Exists(tex))
                {
                    const int N = 128;
                    var img = new Texture2D(N, N, TextureFormat.RGBA32, false);
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                            img.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                        }
                    img.Apply();
                    File.WriteAllBytes(tex, img.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(img);
                    AssetDatabase.ImportAsset(tex);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(tex);
                    ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
                }
                string path = $"{PaintedGenDir}/blob.mat";
                _paintedBlobMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (_paintedBlobMat == null) { _paintedBlobMat = ZeldaDaughter.Rendering.SpriteLook.NewShadowMaterial(); AssetDatabase.CreateAsset(_paintedBlobMat, path); }
                _paintedBlobMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
                _paintedBlobMat.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.22f, 0.42f));
                EditorUtility.SetDirty(_paintedBlobMat);
                _paintedQuad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            }
            var go = new GameObject("blob", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t, false);
            go.transform.rotation = Quaternion.Euler(90f, cardRot.eulerAngles.y, 0f);
            go.transform.position = t.position + Vector3.up * 0.02f;
            var ls = t.lossyScale.y > 1e-3f ? 1f / t.lossyScale.y : 1f;
            go.transform.localScale = new Vector3(width * 0.8f * ls, width * 0.5f * ls, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = _paintedQuad;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _paintedBlobMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>Width of the model's measured box (model-bounds.json) across the screen, metres, as placed (yaw and scale of the object).</summary>
        static float PaintedScreenWidth(Transform t, Box3 b, Vector3 right)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                var c = new Vector3((k & 1) == 0 ? b.MinX : b.MaxX, (k & 2) == 0 ? b.MinY : b.MaxY, (k & 4) == 0 ? b.MinZ : b.MaxZ);
                float d = Vector3.Dot(t.TransformPoint(c), right);
                lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d);
            }
            return hi - lo;
        }

        /// <summary>Road coverage over the ground: 0.5 at the edge of each path, rising inward, falling outward over 0.75 m (the shader rags the edge).</summary>
        static Texture2D PaintedRoadMask(SceneConfig config)
        {
            PaintedFolder();
            float sx = config.Ground.SizeX, sz = config.Ground.SizeZ;
            var px = new Color32[PaintedMaskRes * PaintedMaskRes];
            for (int j = 0; j < PaintedMaskRes; j++)
                for (int i = 0; i < PaintedMaskRes; i++)
                {
                    float x = -sx / 2f + (i + 0.5f) / PaintedMaskRes * sx, z = -sz / 2f + (j + 0.5f) / PaintedMaskRes * sz;
                    float best = 0f;
                    foreach (var p in config.Paths)
                    {
                        float d = float.MaxValue;
                        for (int k = 0; k + 1 < p.Points.Count; k++) d = Mathf.Min(d, SegDist(x, z, p.Points[k], p.Points[k + 1]));
                        best = Mathf.Max(best, Mathf.Clamp01(0.5f + (p.Width / 2f - d) / 1.5f));
                    }
                    byte b = (byte)Mathf.RoundToInt(best * 255f);
                    px[j * PaintedMaskRes + i] = new Color32(b, b, b, 255);
                }
            var tex = new Texture2D(PaintedMaskRes, PaintedMaskRes, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            string path = $"{PaintedGenDir}/mask_{config.Name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.sRGBTexture || ti.wrapMode != TextureWrapMode.Clamp || ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.sRGBTexture = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float SegDist(float x, float z, Pt a, Pt b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float t = Mathf.Clamp01(((x - a.X) * dx + (z - a.Z) * dz) / Mathf.Max(dx * dx + dz * dz, 1e-6f));
            float ex = a.X + t * dx - x, ez = a.Z + t * dz - z;
            return Mathf.Sqrt(ex * ex + ez * ez);
        }
    }
}
