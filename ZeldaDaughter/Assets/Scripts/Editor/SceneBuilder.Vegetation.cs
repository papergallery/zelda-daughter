using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-22b: grass, flowers, shrubs, ferns and reeds as drawn billboards (ADR-0001, like the characters): models of data/models.json with
    /// <c>sprite</c> (veg_*) name a picture of the atlas Assets/Art/Vegetation/vegetation.png (tools/art/veg_build.py). A card stands on its
    /// base and faces the camera (the rotation of the iso camera, like <see cref="BillboardSprite"/>), normals up (lit like the ground, and
    /// the pen outline sees only its silhouette, not a crease where it meets the ground).
    /// <list type="bullet">
    /// <item>Scatter: thousands of cards are merged into one mesh per <see cref="VegChunk"/>-metre square (Scene/Vegetation/veg_i_j) — a few
    /// draw calls on screen, no GameObject per tuft, culled by chunk; a vertex colour varies the tone of each card a little.</item>
    /// <item>Objects with a sprite model (grass cells of D-06, herbs): a child «card» with the shared card mesh of that sprite
    /// (Assets/Generated/Vegetation/card_&lt;id&gt;.asset), turned to the camera after the object is placed — NatureFx tints and shrinks it as before.</item>
    /// </list>
    /// </summary>
    public static partial class SceneBuilder
    {
        const string VegJsonPath = "Assets/Art/Vegetation/vegetation.json";
        const string VegTexturePath = "Assets/Art/Vegetation/vegetation.png";
        const string VegMeshDir = "Assets/Generated/Vegetation";
        const float VegChunk = 24f;
        const float VegSink = 0.03f; // m: the base a little under the ground, so no light line shows under a tuft

        sealed class VegSprite { public Rect Uv; public Vector2 Size; public Vector2[] Hull; }
        static readonly Vector2 ObjectRoot = new Vector2(1e6f, 1e6f); // uv2 of an object's card: "root = the object's origin" (Zelda/Toon _ZD_ROOTED)

        static Dictionary<string, VegSprite> _vegAtlas;
        static Material _vegMaterial;

        static Dictionary<string, VegSprite> VegAtlas()
        {
            if (_vegAtlas != null) return _vegAtlas;
            var json = JObject.Parse(File.ReadAllText(VegJsonPath));
            float w = (float)json["size"][0], h = (float)json["size"][1];
            _vegAtlas = new Dictionary<string, VegSprite>();
            foreach (var p in ((JObject)json["sprites"]).Properties())
            {
                var r = p.Value["rect"];
                _vegAtlas[p.Name] = new VegSprite
                {
                    Uv = new Rect((float)r[0] / w, (float)r[1] / h, (float)r[2] / w, (float)r[3] / h),
                    Size = new Vector2((float)p.Value["size_m"][0], (float)p.Value["size_m"][1]),
                    Hull = p.Value["hull"] is JArray outline && outline.Count >= 3
                        ? outline.Select(q => new Vector2((float)q[0], (float)q[1])).ToArray()
                        : new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                };
            }
            return _vegAtlas;
        }

        /// <summary>
        /// The material of the vegetation cards of a scene: the toon shader in sprite-lit mode, alpha clip, two-sided, vertex colour on; rooted in
        /// the painted ground of the scene (its mask, noise and colours: <paramref name="ground"/>) when there is one. The cards are left out of
        /// the depth, depth-normals and shadow passes: the drawing has its own pen line (no second outline around every tuft, research §В.3),
        /// the prepass does not draw thousands of alpha-clipped cards again, and grass casts no shadow.
        /// </summary>
        static Material VegMaterial(string sceneName, Material ground)
        {
            TextureSetup(VegTexturePath, srgb: true, mips: true, TextureWrapMode.Clamp, compressed: true, alpha: true);
            string path = $"{MaterialsDir}/vegetation_{sceneName}.mat";
            var shader = ModelLook.LoadShader();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader);
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(VegTexturePath));
            SpriteLook.ToSpriteLit(mat, shader); // same light as the characters: ground-like, the campfire and the torch from any side
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_UseVertexColor", 1f);
            mat.EnableKeyword("_VERTEXCOLOR");
            mat.SetFloat("_ZWrite", 1f);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = 2450;
            mat.enableInstancing = false;
            bool rooted = ground != null && ground.IsKeywordEnabled("_ZD_GROUND");
            mat.SetFloat("_Rooted", rooted ? 1f : 0f);
            if (rooted)
            {
                mat.EnableKeyword("_ZD_ROOTED");
                foreach (var t in new[] { "_GroundMask", "_GroundMask2", "_GroundNoise" }) mat.SetTexture(t, ground.GetTexture(t));
                mat.SetVector("_GroundRect", ground.GetVector("_GroundRect"));
                foreach (var (n, _) in GroundColours) mat.SetColor(n, ground.GetColor(n));
            }
            else mat.DisableKeyword("_ZD_ROOTED");
            mat.SetFloat("_Wind", 0.035f);
            mat.SetFloat("_Rim", 0f); // the warm fire rim is for the figures, not for every tuft
            mat.SetShaderPassEnabled("DepthNormals", false);
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
            if (created) { EnsureFolder(MaterialsDir); AssetDatabase.CreateAsset(mat, path); } else EditorUtility.SetDirty(mat);
            _vegMaterial = mat;
            return mat;
        }

        static Quaternion CardRotation(CameraConfig cam) => Quaternion.Euler(cam.Pitch, cam.Yaw, 0f);

        /// <summary>
        /// One card in the frame of <paramref name="rot"/>: the convex outline of the drawing (≤ 8 corners, vegetation.json "hull" — less
        /// alpha-clipped area than a quad), base centre at <paramref name="at"/>, x across, y up the card. uv2 = <paramref name="root"/>, vertex
        /// alpha = height on the card (0 at the base, 1 at the top) for the shader's ground colour and wind; mirrored cards mirror the positions.
        /// </summary>
        static void CardPoly(List<Vector3> v, List<Vector2> uv, List<Vector2> uv2, List<Color32> col, List<Vector3> n, List<int> tris, Vector3 at, Quaternion rot,
            VegSprite s, float scale, bool flip, Color tint, Vector2 root)
        {
            Vector3 right = rot * Vector3.right * (s.Size.x * scale), up = rot * Vector3.up * (s.Size.y * scale);
            Vector3 b = at + Vector3.down * VegSink;
            int i = v.Count;
            foreach (var h in s.Hull)
            {
                float x = flip ? 0.5f - h.x : h.x - 0.5f;
                v.Add(b + right * x + up * h.y);
                uv.Add(new Vector2(s.Uv.xMin + h.x * s.Uv.width, s.Uv.yMin + h.y * s.Uv.height));
                uv2.Add(root);
                col.Add(new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(h.y)));
                n.Add(Vector3.up);
            }
            for (int k = 1; k + 1 < s.Hull.Length; k++) { tris.Add(i); tris.Add(i + k); tris.Add(i + k + 1); } // convex: a fan (two-sided material)
        }

        /// <summary>A small, stable variation of tone per card (from its id): a little lighter / darker, a little warmer / cooler.</summary>
        static Color VegTint(string id)
        {
            uint h = 2166136261;
            foreach (char c in id) unchecked { h = (h ^ c) * 16777619; }
            float v = 0.9f + 0.16f * ((h & 0xff) / 255f);
            float warm = ((h >> 8 & 0xff) / 255f - 0.5f) * 0.08f;
            return new Color(Mathf.Clamp01(v + warm), Mathf.Clamp01(v), Mathf.Clamp01(v - warm), 1f);
        }

        /// <summary>The scattered cards of the scene merged into one mesh per chunk under «Vegetation».</summary>
        static void BuildVegetation(SceneConfig config, IReadOnlyList<ScatterPlacement> placements, ModelCatalog catalog, Transform hero)
        {
            if (placements.Count == 0) return;
            var atlas = VegAtlas();
            var rot = CardRotation(config.Camera);
            var root = new GameObject("Vegetation").transform;
            var chunks = new SortedDictionary<(int, int), List<ScatterPlacement>>();
            foreach (var p in placements)
            {
                var key = (Mathf.FloorToInt(p.X / VegChunk), Mathf.FloorToInt(p.Z / VegChunk));
                if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<ScatterPlacement>();
                list.Add(p);
            }
            int quads = 0, vertices = 0;
            foreach (var kv in chunks)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var col = new List<Color32>(); var n = new List<Vector3>(); var tris = new List<int>();
                var origin = new Vector3((kv.Key.Item1 + 0.5f) * VegChunk, 0f, (kv.Key.Item2 + 0.5f) * VegChunk);
                foreach (var p in kv.Value)
                {
                    var s = atlas[catalog.Get(p.ModelId).Sprite];
                    CardPoly(v, uv, uv2, col, n, tris, new Vector3(p.X, 0f, p.Z) - origin, rot, s, p.Scale, p.Yaw >= 180f, VegTint(p.Id), new Vector2(p.X, p.Z));
                }
                var mesh = new Mesh { name = $"veg_{kv.Key.Item1}_{kv.Key.Item2}", indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetColors(col); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
                vertices += v.Count;
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.transform.localPosition = origin;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = _vegMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                quads += kv.Value.Count;
            }
            Debug.Log($"[ZD:Scene] vegetation {config.Name} cards={quads} chunks={chunks.Count} vertices={vertices}");
        }

        /// <summary>One card of a sprite model as a shared mesh asset (base centre at the origin, facing −z; turned to the camera by its object).</summary>
        static Mesh CardMesh(string spriteId)
        {
            EnsureFolder(VegMeshDir);
            string path = $"{VegMeshDir}/card_{spriteId}.asset";
            var s = VegAtlas()[spriteId];
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var col = new List<Color32>(); var n = new List<Vector3>(); var tris = new List<int>();
            CardPoly(v, uv, uv2, col, n, tris, Vector3.zero, Quaternion.identity, s, 1f, false, Color.white, ObjectRoot);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "card_" + spriteId;
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetColors(col); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>The child «card» of a sprite model (no collider: grass and flowers are walked through).</summary>
        static void AssembleCard(Transform holder, ModelDef def)
        {
            var go = new GameObject("card", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(holder, false);
            go.GetComponent<MeshFilter>().sharedMesh = CardMesh(def.Sprite);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _vegMaterial != null ? _vegMaterial : VegMaterial("objects", null);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>After an object is placed: its sprite cards face the camera whatever the object's own yaw.</summary>
        static void FaceCards(GameObject go, CameraConfig cam)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.gameObject.name == "card") mf.transform.rotation = CardRotation(cam);
        }
    }
}
