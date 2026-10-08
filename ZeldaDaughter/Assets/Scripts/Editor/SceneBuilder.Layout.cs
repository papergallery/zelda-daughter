using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-10: the model, road, water, zone and scatter parts of the one scene builder (docs/scene-config.md). Models come from
    /// data/models.json by id; their look is decided in <see cref="ModelLook"/> at import; colliders are simple Box/Capsule from the
    /// measured bounds (no MeshCollider: cheap, and the CharacterController slides along boxes instead of catching on mesh edges).
    /// </summary>
    public static partial class SceneBuilder
    {
        const float PathY = 0.03f;
        const float WaterY = 0.06f;

        static ModelCatalog LoadCatalog() => ModelCatalog.Load(DataDir);

        /// <summary>Roads and rivers as ribbons, zones as tagged areas, the terrain map for the hero, and the scattered decor.</summary>
        static void BuildLayout(SceneConfig config, ModelCatalog catalog, HeroController hero)
        {
            var map = TerrainMap.From(config);
            var holder = new GameObject("Terrain");
            var zones = holder.AddComponent<TerrainZones>();
            zones.Configure(map.ToJson());
            hero.SetZones(zones);

            if (config.Paths.Count > 0)
            {
                var root = new GameObject("Paths").transform;
                for (int i = 0; i < config.Paths.Count; i++) // each later path a hair higher, so crossing ribbons don't z-fight
                {
                    var s = config.Paths[i];
                    Ribbon(root, s, PathY + 0.004f * i, string.IsNullOrEmpty(s.Color) ? "#b09a6e" : s.Color);
                }
            }
            if (config.Water.Count > 0)
            {
                var root = new GameObject("Water").transform;
                foreach (var s in config.Water) Ribbon(root, s, WaterY, string.IsNullOrEmpty(s.Color) ? "#5f8fa3" : s.Color);
            }
            if (config.Zones.Count > 0)
            {
                var root = new GameObject("Zones").transform;
                foreach (var z in config.Zones)
                {
                    var go = new GameObject(z.Id);
                    go.transform.SetParent(root, false);
                    go.AddComponent<ZoneArea>().Configure(z.Id, z.Tags.ToArray(), ((Area)z).ToJson());
                }
            }

            var placements = Scatterer.Generate(config, catalog);
            if (placements.Count > 0)
            {
                var root = new GameObject("Scatter").transform;
                foreach (var p in placements)
                {
                    var go = SpawnModel(p.Id, p.ModelId, catalog, p.Collide);
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(p.X, 0f, p.Z);
                    go.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                    go.transform.localScale = Vector3.one * p.Scale;
                    foreach (var t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
                Debug.Log($"[ZD:Scene] scatter {config.Name} placed={placements.Count} digest={Scatterer.Digest(placements)}");
            }
        }

        /// <summary>
        /// D-22: primitives of the config used as ground patches and mist: <c>collide: false</c> removes the collider of a primitive (it only was for models),
        /// objects tagged <c>ground_patch</c> / <c>mist</c> cast no shadow, patches are batched statically (hundreds of discs of three colours).
        /// </summary>
        static void DressPrimitive(GameObject go, ObjectConfig o)
        {
            if (o.Marker || !string.IsNullOrEmpty(o.Model) || string.IsNullOrEmpty(o.Shape) || o.Shape == "empty") return;
            if (o.Collide == false)
                foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            bool patch = o.Tags.Contains("ground_patch");
            if (patch || o.Tags.Contains("mist"))
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            if (patch) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>A model (or a composite of models) as a holder object with the FBX instances inside and simple colliders.</summary>
        static GameObject SpawnModel(string name, string modelId, ModelCatalog catalog, bool collide)
        {
            var root = new GameObject(name);
            Assemble(root.transform, modelId, catalog);
            if (collide) AddColliders(root, modelId, catalog);
            return root;
        }

        static void Assemble(Transform holder, string modelId, ModelCatalog catalog)
        {
            var def = catalog.Get(modelId);
            if (!def.IsComposite)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(def.Path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                instance.name = "model";
                instance.transform.SetParent(holder, false); // keeps the FBX root's own rotation/scale — the measured bounds include them
                return;
            }
            for (int i = 0; i < def.Parts.Count; i++)
            {
                var part = def.Parts[i];
                var go = new GameObject($"{i:00}_{part.Model}");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(part.Offset.X, part.Y, part.Offset.Z);
                go.transform.localRotation = Quaternion.Euler(0f, part.Yaw, 0f);
                Assemble(go.transform, part.Model, catalog);
            }
        }

        static void AddColliders(GameObject owner, string modelId, ModelCatalog catalog)
        {
            var def = catalog.Get(modelId);
            var kind = def.Collider.Kind;
            if (kind == "none") return;
            if (kind == "parts")
            {
                for (int i = 0; i < def.Parts.Count; i++)
                {
                    var part = def.Parts[i];
                    if (part.Collider == "none") continue;
                    AddColliders(owner.transform.GetChild(i).gameObject, part.Model, catalog);
                }
                return;
            }
            var b = catalog.Bounds(modelId);
            float shrink = def.Collider.Shrink;
            var center = new Vector3(b.CenterX, b.CenterY, b.CenterZ);
            if (kind == "box")
            {
                var box = owner.AddComponent<BoxCollider>();
                box.center = center;
                box.size = new Vector3(b.SizeX * shrink, b.SizeY, b.SizeZ * shrink);
            }
            else // capsule
            {
                var cap = owner.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.radius = def.Collider.Radius > 0f ? def.Collider.Radius : Mathf.Min(b.SizeX, b.SizeZ) / 2f * shrink;
                cap.height = Mathf.Max(def.Collider.Height > 0f ? def.Collider.Height : b.SizeY, cap.radius * 2f);
                cap.center = new Vector3(b.CenterX, cap.height / 2f, b.CenterZ);
            }
        }

        /// <summary>A flat ribbon along the polyline, <c>width</c> metres wide, lifted a few cm above the ground plane; no collider.</summary>
        static void Ribbon(Transform parent, StripConfig s, float y, string color)
        {
            var pts = s.Points;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir;
                if (i == 0) dir = Dir(pts[0], pts[1]);
                else if (i == pts.Count - 1) dir = Dir(pts[i - 1], pts[i]);
                else dir = (Dir(pts[i - 1], pts[i]) + Dir(pts[i], pts[i + 1])).normalized;
                var left = new Vector2(-dir.y, dir.x); // 90° counter-clockwise seen from above
                float miter = i == 0 || i == pts.Count - 1 ? 1f : Mathf.Min(1f / Mathf.Max(Vector2.Dot(left, new Vector2(-Dir(pts[i - 1], pts[i]).y, Dir(pts[i - 1], pts[i]).x)), 0.5f), 2f);
                float h = s.Width / 2f * miter;
                verts.Add(new Vector3(pts[i].X + left.x * h, y, pts[i].Z + left.y * h));
                verts.Add(new Vector3(pts[i].X - left.x * h, y, pts[i].Z - left.y * h));
            }
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3; // a,b = this point's left,right; c,d = the next
                AddUp(verts, tris, a, c, b);
                AddUp(verts, tris, b, c, d);
            }
            var mesh = new Mesh { name = s.Id };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(s.Id, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFor(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Vector2 Dir(Pt a, Pt b) => new Vector2(b.X - a.X, b.Z - a.Z).normalized;

        /// <summary>Adds a triangle wound so that it faces up whatever order the caller gave.</summary>
        static void AddUp(List<Vector3> v, List<int> tris, int i, int j, int k)
        {
            var n = Vector3.Cross(v[j] - v[i], v[k] - v[i]);
            if (n.y >= 0f) { tris.Add(i); tris.Add(j); tris.Add(k); }
            else { tris.Add(i); tris.Add(k); tris.Add(j); }
        }
    }
}
