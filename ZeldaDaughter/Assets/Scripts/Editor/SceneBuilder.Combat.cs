using UnityEngine;
using ZeldaDaughter.Combat;

namespace ZeldaDaughter.Editor
{
    /// <summary>D-13: the fight in the scene — the presenter of enemies and carcasses and the two folders their views live in. The enemies' spawn points are the scene's own tagged markers (config field <c>enemy</c>).</summary>
    public static partial class SceneBuilder
    {
        static partial void AddCombat(BuildContext ctx)
        {
            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(ctx.Game.transform, false);
            var carcasses = new GameObject("Carcasses");
            carcasses.transform.SetParent(ctx.Game.transform, false);
            var presenter = ctx.Game.AddComponent<CombatPresenter>();
            presenter.Configure(ctx.Session, ctx.Art, ctx.Cam, enemies.transform, carcasses.transform);
            ctx.ById["combat"] = ctx.Game;
        }
    }
}
