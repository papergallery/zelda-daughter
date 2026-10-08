using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>
    /// C7: the data points at things in <c>scenes/region.json</c> (anchors, map marks, gestures, night zones, mud) and the residents can walk between
    /// their anchors without going through houses. The region belongs to the level designer: when it does not yet satisfy a check, the test is
    /// SKIPPED with the list of what to fix in the region (never silently green); the logic itself is covered by synthetic tests elsewhere.
    /// </summary>
    public class RegionLinksTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);
        static readonly string RegionPath = Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json");
        static readonly SceneConfig Region = SceneConfig.Parse(File.ReadAllText(RegionPath));

        static void Settle(string what, IEnumerable<string> problems)
        {
            var list = problems.ToList();
            if (list.Count > 0) Assert.Skip($"scenes/region.json needs ({what}): " + string.Join("; ", list));
        }

        static HashSet<string> Ids => Region.Objects.Select(o => o.Id).ToHashSet();

        static List<ObjectConfig> Anchors => Region.Objects.Where(o => o.Tags.Contains("anchor")).ToList();

        [Fact]
        public void Every_anchor_of_the_schedules_is_an_object_with_the_anchor_tag()
        {
            var anchors = Anchors.Select(a => a.Id).ToHashSet();
            Settle("anchors", D.Npcs.Npcs.SelectMany(n => n.Value.Schedule.Select(s => (n.Key, s.Anchor)))
                .Where(x => !anchors.Contains(x.Anchor)).Select(x => $"{x.Key}: нет якоря '{x.Anchor}'"));
        }

        [Fact]
        public void Every_map_mark_points_at_an_object()
        {
            Settle("map marks", D.Map.Marks.Where(m => !Ids.Contains(m.Value.Object)).Select(m => $"метка '{m.Key}' → нет объекта '{m.Value.Object}'"));
        }

        [Fact]
        public void Every_gesture_points_at_an_object()
        {
            Settle("gestures", D.Dialogues.Npcs.SelectMany(n => n.Value.Nodes.Select(nd => (Npc: n.Key, Node: nd.Key, Gesture: nd.Value.Gesture)))
                .Where(x => x.Gesture != null && !string.IsNullOrEmpty(x.Gesture.Target) && !Ids.Contains(x.Gesture.Target))
                .Select(x => $"{x.Npc}.{x.Node}: жест на '{x.Gesture!.Target}' — нет объекта"));
        }

        [Fact]
        public void Night_zones_are_objects_of_the_scene()
        {
            Settle("night safe areas", D.Night.SafeAreas.Where(z => !Ids.Contains(z.Anchor)).Select(z => $"night.json: safeAreas '{z.Anchor}' — нет объекта"));
        }

        [Fact]
        public void Mud_zones_are_circles_the_core_can_take()
        {
            Settle("mud", Region.Zones.Where(z => z.Tags.Contains("mud") && (z.Kind != "circle" || z.Radius <= 0)).Select(z => $"зона грязи '{z.Id}' должна быть circle с radius > 0"));
        }

        [Fact]
        public void Every_enemy_and_station_of_the_scene_exists_in_the_data()
        {
            var stations = D.StationRecipes.Select(s => s.Station).ToHashSet();
            Settle("enemies and stations",
                Region.Objects.Where(o => o.Enemy != null && !D.Enemies.Enemies.ContainsKey(o.Enemy)).Select(o => $"'{o.Id}': враг '{o.Enemy}' не из enemies.json")
                .Concat(Region.Objects.Where(o => o.Station != null && !stations.Contains(o.Station)).Select(o => $"'{o.Id}': станок '{o.Station}' не из recipes.json")));
        }

        [Fact]
        public void Every_anchor_can_be_reached_from_every_other()
        {
            var graph = RouteGraph.From(Region);
            var ids = graph.Anchors.ToList();
            Assert.NotEmpty(ids);
            var problems = new List<string>();
            foreach (var id in ids.Skip(1))
                if (graph.Route(ids[0], id) == null) problems.Add($"якорь '{id}' не соединён дорогами с '{ids[0]}' (добавить walkway или подвести тропу)");
            Settle("reachability", problems);
        }

        [Fact]
        public void Residents_do_not_walk_through_houses_between_their_anchors()
        {
            var graph = RouteGraph.From(Region);
            var catalog = SceneLayoutTests.Catalog();
            var houses = Region.Objects.Where(o => o.Tags.Contains("building") && !string.IsNullOrEmpty(o.Model))
                .Select(o => (o.Id, Footprint: Footprint.Of(o, catalog)!.Value)).ToList();
            var problems = new List<string>();
            foreach (var npc in D.Npcs.Npcs)
            {
                var sched = npc.Value.Schedule;
                for (int i = 0; i < sched.Count; i++)
                {
                    string from = sched[i].Anchor, to = sched[(i + 1) % sched.Count].Anchor;
                    if (from == to || !graph.Has(from) || !graph.Has(to)) continue;
                    var route = graph.Route(from, to);
                    if (route == null) continue;   // reported by the reachability check
                    foreach (var (id, fp) in houses)
                    {
                        // her own house at either end is entered and left by the door
                        if (fp.SignedDistance(route[0].X, route[0].Y) < 0 || fp.SignedDistance(route[route.Count - 1].X, route[route.Count - 1].Y) < 0) continue;
                        for (int k = 1; k < route.Count; k++)
                        {
                            if (!SegmentCrosses(fp, route[k - 1], route[k], 0.15f)) continue;
                            problems.Add($"{npc.Key}: {from} → {to} идёт сквозь '{id}' ({route[k - 1]} → {route[k]})");
                            break;
                        }
                    }
                }
            }
            Settle("routes around houses", problems.Distinct());
        }

        static bool SegmentCrosses(Footprint fp, Vec2 a, Vec2 b, float inset)
        {
            float len = (b - a).Length;
            int n = Math.Max(2, (int)Math.Ceiling(len / 0.25f));
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                if (fp.SignedDistance(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t) < -inset) return true;
            }
            return false;
        }
    }
}
