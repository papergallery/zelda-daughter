using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-16: the elements' part of the one scene builder — the <c>Nature</c> object (rain, wet ground, mud, grass fire, burnt patches, campfire
    /// light, the tap that sets grass alight) and the hero's torch light, plus the four materials of the effects (Assets/Generated/Materials/nature_*.mat,
    /// shader Zelda/NatureFx). Grass cells (objects tagged <c>grass_cell</c>, made by the scene config from <c>dry_grass</c> zones) get a small trigger
    /// collider so that a tap can find them; the colliders do not block anything.
    /// </summary>
    public static partial class SceneBuilder
    {
        const string NatureShaderPath = "Assets/Shaders/ZeldaNatureFx.shader";
        const float GrassTapRadius = 0.8f;

        static partial void AddNature(BuildContext ctx)
        {
            var rain = NatureMaterial("nature_rain", Color.white, 0f, 0.45f, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false);
            var puff = NatureMaterial("nature_puff", Color.white, 1f, 0.45f, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false);
            var flame = NatureMaterial("nature_flame", Color.white, 1f, 0.6f, BlendMode.SrcAlpha, BlendMode.One, false);
            var burnt = NatureMaterial("nature_burnt", new Color(0.07f, 0.06f, 0.05f, 0.85f), 1f, 0.5f, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, true);

            foreach (var tags in ctx.Tagged)
            {
                if (!tags.Has("grass_cell") || tags.GetComponent<Collider>() != null) continue;
                var c = tags.gameObject.AddComponent<SphereCollider>();
                c.isTrigger = true;
                c.radius = GrassTapRadius;
                c.center = new Vector3(0f, 0.3f, 0f);
            }

            var go = new GameObject("Nature");
            go.transform.SetParent(ctx.Game.transform, false);
            go.AddComponent<NatureFx>().Configure(ctx.Session, ctx.Ground.GetComponent<Renderer>(), ctx.Hero.transform, ctx.Art.Fx, rain, puff, flame, burnt);
            go.AddComponent<HeroTorchLight>().Configure(ctx.Session, ctx.Hero.transform, ctx.Art.Fx, flame);
            ctx.ById["nature"] = go;
        }

        /// <summary>One material asset per effect; rebuilt from the numbers here on every build, so the code stays the single source.</summary>
        static Material NatureMaterial(string name, Color color, float disc, float edge, BlendMode src, BlendMode dst, bool instanced)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets/Generated", "Materials");
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(NatureShaderPath) ?? Shader.Find("Zelda/NatureFx");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader);
            mat.shader = shader;
            mat.SetColor("_Color", color);
            mat.SetFloat("_Disc", disc);
            mat.SetFloat("_Edge", edge);
            mat.SetFloat("_SrcBlend", (float)src);
            mat.SetFloat("_DstBlend", (float)dst);
            mat.enableInstancing = instanced;
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (created) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
