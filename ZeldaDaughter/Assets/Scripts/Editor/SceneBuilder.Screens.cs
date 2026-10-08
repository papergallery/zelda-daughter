using UnityEngine;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-15: the screens of the Game object — the station, trade, map and notebook windows and the sleep presenter (docs/demo/unity-architecture.md §5).
    /// The map sheet is drawn from this scene's config by <see cref="MapBaker"/> (the same call for every scene, so the map always matches the land).
    /// </summary>
    public static partial class SceneBuilder
    {
        static partial void AddScreens(BuildContext ctx)
        {
            var game = ctx.Game;
            var map = MapBaker.Bake(ctx.Config, ctx.Catalog);

            game.AddComponent<StationWindow>().Configure(ctx.Session, ctx.UI, ctx.Windows, ctx.Art);
            game.AddComponent<TradeWindow>().Configure(ctx.Session, ctx.UI, ctx.Windows, ctx.Art);
            game.AddComponent<MapWindow>().Configure(ctx.Session, ctx.UI, ctx.Windows, map.Sprite, map.GroundSize);
            game.AddComponent<NotebookWindow>().Configure(ctx.Session, ctx.UI, ctx.Windows);
            game.AddComponent<RestPresenter>().Configure(ctx.Session, ctx.Fader, ctx.Windows, ctx.UI, ctx.HeroCtl);
        }
    }
}
