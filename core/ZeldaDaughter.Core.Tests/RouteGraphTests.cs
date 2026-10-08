using System;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C7: where residents walk — a graph over the roads, trails and walkways of a scene, anchors attached to it (no NavMesh, ADR decision 19).</summary>
    public class RouteGraphTests
    {
        static SceneConfig Parse(string json) => SceneConfig.Parse("{\"name\":\"t\",\"ground\":{\"sizeX\":200,\"sizeZ\":200,\"terrain\":\"grass\"},\"camera\":{\"pitch\":35,\"distance\":20,\"size\":8},\"hero\":{\"shape\":\"capsule\"}," + json + "}");

        static string Anchor(string id, float x, float z) => $"{{\"id\":\"{id}\",\"marker\":true,\"position\":{{\"x\":{x},\"z\":{z}}},\"tags\":[\"anchor\"]}}";
        static string Strip(string id, params (float x, float z)[] pts) =>
            $"{{\"id\":\"{id}\",\"width\":2,\"points\":[{string.Join(",", pts.Select(p => $"{{\"x\":{p.x},\"z\":{p.z}}}"))}]}}";

        static float Length(System.Collections.Generic.IReadOnlyList<Vec2> r)
        {
            float len = 0;
            for (int i = 1; i < r.Count; i++) len += (r[i] - r[i - 1]).Length;
            return len;
        }

        [Fact]
        public void A_route_along_one_road_goes_through_its_bend()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (20, 0), (20, 20)) + "],\"objects\":[" + Anchor("a", 0, 0) + "," + Anchor("b", 20, 20) + "]");
            var g = RouteGraph.From(c);
            var r = g.Route("a", "b")!;
            Assert.NotNull(r);
            Assert.Equal(new Vec2(0, 0), r[0]);
            Assert.Equal(new Vec2(20, 20), r[r.Count - 1]);
            Assert.Contains(new Vec2(20, 0), r);
            Assert.Equal(40f, Length(r), 2);
        }

        [Fact]
        public void An_anchor_beside_the_road_steps_onto_it_by_the_shortest_way()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (40, 0)) + "],\"objects\":[" + Anchor("a", 5, 4) + "," + Anchor("b", 35, -4) + "]");
            var r = RouteGraph.From(c).Route("a", "b")!;
            Assert.Equal(4f + 30f + 4f, Length(r), 2);
            Assert.Equal(new Vec2(5, 4), r[0]);
            Assert.Equal(new Vec2(35, -4), r[r.Count - 1]);
        }

        [Fact]
        public void A_route_to_the_same_anchor_is_one_point()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (40, 0)) + "],\"objects\":[" + Anchor("a", 5, 0) + "]");
            var r = RouteGraph.From(c).Route("a", "a")!;
            Assert.Single(r);
        }

        [Fact]
        public void A_trail_that_ends_on_a_road_joins_it()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (40, 0)) + "," + Strip("trail", (20, 30), (20, 1)) + "],\"objects\":[" + Anchor("a", 0, 0) + "," + Anchor("b", 20, 30) + "]");
            var r = RouteGraph.From(c).Route("a", "b")!;
            Assert.NotNull(r);
            Assert.True(Length(r) < 20f + 30f + 1.5f);
        }

        [Fact]
        public void Crossing_roads_join_where_they_cross()
        {
            var c = Parse("\"paths\":[" + Strip("ew", (-30, 0), (30, 0)) + "," + Strip("ns", (0, -30), (0, 30)) + "],\"objects\":[" + Anchor("w", -30, 0) + "," + Anchor("n", 0, 30) + "]");
            var r = RouteGraph.From(c).Route("w", "n")!;
            Assert.NotNull(r);
            Assert.Equal(60f, Length(r), 2);   // 30 along one road, 30 along the other, through the crossing
        }

        [Fact]
        public void Two_separate_roads_are_two_islands_until_a_walkway_joins_them()
        {
            string objects = "\"objects\":[" + Anchor("a", 0, 0) + "," + Anchor("b", 100, 0) + "]";
            var apart = Parse("\"paths\":[" + Strip("one", (0, 0), (30, 0)) + "," + Strip("two", (70, 0), (100, 0)) + "]," + objects);
            Assert.Null(RouteGraph.From(apart).Route("a", "b"));

            var joined = Parse("\"paths\":[" + Strip("one", (0, 0), (30, 0)) + "," + Strip("two", (70, 0), (100, 0)) + "],\"walkways\":[" + Strip("link", (30, 0), (70, 0)) + "]," + objects);
            var r = RouteGraph.From(joined).Route("a", "b")!;
            Assert.NotNull(r);
            Assert.Equal(100f, Length(r), 2);
        }

        [Fact]
        public void An_anchor_far_from_every_way_is_unreachable_but_known()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (40, 0)) + "],\"objects\":[" + Anchor("a", 5, 0) + "," + Anchor("lost", 5, 80) + "]");
            var g = RouteGraph.From(c);
            Assert.True(g.Has("lost"));
            Assert.Null(g.Route("a", "lost"));
            Assert.Null(g.Route("a", "nobody"));
            Assert.False(g.Has("nobody"));
        }

        [Fact]
        public void The_shorter_of_two_ways_is_taken()
        {
            // a square loop: top edge is 40 long, the way round the other three sides is 120
            var c = Parse("\"paths\":[" + Strip("loop", (0, 0), (40, 0), (40, 40), (0, 40), (0, 0)) + "],\"objects\":[" + Anchor("a", 0, 0) + "," + Anchor("b", 40, 0) + "]");
            var r = RouteGraph.From(c).Route("a", "b")!;
            Assert.Equal(40f, Length(r), 2);
        }

        [Fact]
        public void The_graph_is_the_same_twice()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (40, 0)) + "," + Strip("trail", (20, 30), (20, 1)) + "],\"objects\":[" + Anchor("a", 0, 0) + "," + Anchor("b", 20, 30) + "]");
            var a = RouteGraph.From(c).Route("a", "b")!;
            var b = RouteGraph.From(c).Route("a", "b")!;
            Assert.Equal(a, b);
        }

        [Fact]
        public void Anchors_are_listed_by_tag()
        {
            var c = Parse("\"paths\":[" + Strip("road", (0, 0), (40, 0)) + "],\"objects\":[" + Anchor("a", 5, 0) + ",{\"id\":\"npc_x\",\"marker\":true,\"position\":{\"x\":1,\"z\":1},\"tags\":[\"npc\"]}]");
            var g = RouteGraph.From(c);
            Assert.Equal(new[] { "a" }, g.Anchors.ToArray());
            Assert.Equal(new Vec2(5, 0), g.PositionOf("a"));
        }
    }
}
