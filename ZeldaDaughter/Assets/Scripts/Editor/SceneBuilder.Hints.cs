using UnityEngine;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Editor
{
    /// <summary>D-18: the onboarding hand and the hero's remark cloud on the Game object.</summary>
    public static partial class SceneBuilder
    {
        static partial void AddHints(BuildContext ctx)
        {
            ctx.Game.AddComponent<HintView>().Configure(ctx.Session, ctx.UI, ctx.Art.TalkIcons);
            ctx.Game.AddComponent<RemarkBubble>().Configure(ctx.Session, ctx.UI);
        }
    }
}
