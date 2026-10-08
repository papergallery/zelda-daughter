using UnityEngine;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Editor
{
    /// <summary>D-11: the hero as a drawn figure. The capsule of the config stays as the body (the character controller) but is not drawn.</summary>
    public static partial class SceneBuilder
    {
        public const string HeroSpriteName = "HeroSprite";
        public const string HeroCharacterId = "heroine";
        /// <summary>D-27 pilot: the side view as a cut-out rig (tools/art/d27_rig.py); without the files the side stays drawn frames.</summary>
        public const string HeroSideRig = "Assets/Art/Sprites/heroine/rig/heroine_side.rig.json";
        public const string HeroSideAtlas = "Assets/Art/Sprites/heroine/rig/heroine_side_rig.png";

        static partial void AddHeroView(BuildContext ctx)
        {
            var hero = ctx.Hero;
            foreach (var r in hero.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            // the capsule's pivot is its centre and it is 2 m tall (see BuildScene): the feet are 1 m below
            var go = new GameObject(HeroSpriteName);
            go.transform.SetParent(hero.transform, false);
            go.transform.localPosition = new Vector3(0f, -1f, 0f);
            var sprite = go.AddComponent<BillboardSprite>();
            sprite.Configure(ctx.Art.Characters, ctx.Art.Sprites, ctx.Cam, HeroCharacterId);

            var rig = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(HeroSideRig);
            var atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(HeroSideAtlas);
            if (rig != null && atlas != null) go.AddComponent<CutoutFigure>().Configure(sprite, rig, atlas, Facing.Side);

            hero.AddComponent<HeroView>().Configure(ctx.Session, ctx.HeroCtl, sprite, ctx.Art.Sprites);
            ctx.Fader.Bind(ctx.Session);
            ctx.ById["hero_sprite"] = go;
        }
    }
}
