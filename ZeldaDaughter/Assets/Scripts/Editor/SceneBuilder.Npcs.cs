using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Input;
using ZeldaDaughter.NPC;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-12: the residents and the talk of the one scene builder (docs/demo/unity-architecture.md §5). Every object <c>npc_&lt;id&gt;</c> of the
    /// config whose id is in data/npcs.json loses its capsule (mesh and collider: residents are walked through) and gets a child «Figure» with a
    /// <see cref="BillboardSprite"/> of the character <c>&lt;id&gt;</c> and an <see cref="NpcView"/>; the roads between the anchors her schedule uses
    /// are found here by the core's <see cref="RouteGraph"/> and stored in <see cref="NpcPresenter"/>; the talk view is added to the Game object.
    /// </summary>
    public static partial class SceneBuilder
    {
        static partial void AddNpcs(BuildContext ctx)
        {
            var art = ctx.Art;
            var views = new List<NpcView>();
            var anchorsUsed = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var id in ctx.Data.Npcs.Npcs.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var tags = ctx.Tagged.FirstOrDefault(t => t.Id == "npc_" + id);
                if (tags == null) continue; // this scene does not have her
                var go = tags.gameObject;

                foreach (var c in go.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
                var filter = go.GetComponent<MeshFilter>();
                if (filter != null) UnityEngine.Object.DestroyImmediate(filter);

                var feet = go.transform.position; // the config puts a capsule's centre at y = 1; the figure stands on its origin
                go.transform.position = new Vector3(feet.x, 0f, feet.z);

                var figure = new GameObject("Figure");
                figure.layer = go.layer;
                figure.transform.SetParent(go.transform, false);
                var sprite = figure.AddComponent<BillboardSprite>();
                sprite.Configure(art.Characters, art.Sprites, ctx.Cam, id);

                var tap = go.GetComponent<Tappable>();
                tap.Configure("npc_" + id, TapKind.Npc, -1f, 1.0f);
                var view = go.AddComponent<NpcView>();
                view.Configure(id, sprite, tap);
                views.Add(view);
                ctx.ById["npc_" + id] = go;

                foreach (var e in ctx.Data.Npcs.Npcs[id].Schedule) anchorsUsed.Add(e.Anchor);
            }

            var graph = RouteGraph.From(ctx.Config);
            var anchors = anchorsUsed.Where(graph.Has).ToList();
            var routes = new List<NpcRouteData>();
            foreach (var a in anchors)
                foreach (var b in anchors)
                {
                    if (a == b) continue;
                    var road = graph.Route(a, b);
                    if (road == null) { Debug.LogWarning($"[ZD:Scene] no road {a} → {b} for the residents"); continue; }
                    routes.Add(new NpcRouteData(a, b, road.Select(p => new Vector3(p.X, 0f, p.Y)).ToArray()));
                }

            var presenter = ctx.Game.AddComponent<NpcPresenter>();
            presenter.Configure(ctx.Session, views.ToArray(), routes.ToArray());

            var bubble = ctx.Game.AddComponent<TalkBubbleView>();
            bubble.Configure(ctx.UI, art.TalkIcons, art.Ui, ctx.Cam, ctx.Hero.transform);
            ctx.UI.UseTalkView(bubble);
            ctx.Game.GetComponent<TalkPresenter>().Wire(bubble, presenter);
            Debug.Log($"[ZD:Scene] npcs={views.Count} roads={routes.Count}");
        }
    }
}
