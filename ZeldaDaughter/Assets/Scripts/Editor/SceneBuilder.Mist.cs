using UnityEngine;
using UnityEngine.Rendering;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-22: the morning mist — objects of the scene config tagged <c>mist</c> (flat quads) get the soft alpha material nature_mist
    /// (shader Zelda/NatureFx, see <see cref="NatureMaterial"/>) and <see cref="MorningMist"/> (its strength follows the clock).
    /// </summary>
    public static partial class SceneBuilder
    {
        static partial void AddMist(BuildContext ctx)
        {
            var color = new Color(0.95f, 0.92f, 0.85f, 0.4f);
            Material mat = null;
            int n = 0;
            foreach (var tags in ctx.Tagged)
            {
                if (!tags.Has("mist")) continue;
                mat ??= NatureMaterial("nature_mist", color, 1f, 0.95f, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false);
                var r = tags.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                tags.gameObject.AddComponent<MorningMist>().Configure(ctx.Session, r, color, n++ * 1.7f);
            }
        }
    }
}
