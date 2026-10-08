using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-22: the morning mist. D-22b: one <see cref="MorningMist"/> in the game object sets the strength of the mist that the watercolour post pass
    /// draws low over the ground (no flat quads: their overdraw on a phone, research §В.6) — in every scene with a session.
    /// </summary>
    public static partial class SceneBuilder
    {
        static partial void AddMist(BuildContext ctx)
        {
            if (ctx.Session == null) return;
            var go = new GameObject("MorningMist");
            go.transform.SetParent(ctx.Game.transform, false);
            go.AddComponent<MorningMist>().Configure(ctx.Session);
        }
    }
}
