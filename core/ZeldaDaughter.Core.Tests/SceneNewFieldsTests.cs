using System.Linq;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C7: new scene config fields — enemy markers, stations, empty objects, walkways — and the old scenes untouched.</summary>
    public class SceneNewFieldsTests
    {
        static SceneConfig Parse(string json) => SceneConfig.Parse("{\"name\":\"t\",\"ground\":{\"sizeX\":60,\"sizeZ\":60,\"terrain\":\"grass\"},\"camera\":{\"pitch\":35,\"distance\":20,\"size\":8},\"hero\":{\"shape\":\"capsule\"}," + json + "}");

        static readonly string[] Enemies = { "boar", "wolf" };
        static readonly string[] Stations = { "anvil", "smelter" };

        static System.Collections.Generic.IReadOnlyList<string> Check(SceneConfig c) =>
            c.Validate(null, SceneLayoutTests.Catalog(), new[] { "ground", "grass", "road", "water", "mud" }, Enemies, Stations);

        [Fact]
        public void An_object_can_be_an_enemy_spawn_or_a_station()
        {
            var c = Parse("\"objects\":[{\"id\":\"spawn_boar\",\"marker\":true,\"enemy\":\"boar\",\"position\":{\"x\":3,\"z\":3},\"tags\":[\"enemy_spawn\"]}," +
                          "{\"id\":\"anvil_1\",\"shape\":\"empty\",\"station\":\"anvil\",\"position\":{\"x\":5,\"z\":5},\"tags\":[\"station\"]}]");
            Assert.Equal("boar", c.Objects[0].Enemy);
            Assert.Equal("anvil", c.Objects[1].Station);
            Assert.Empty(Check(c));
        }

        [Fact]
        public void Unknown_enemy_or_station_is_named()
        {
            var c = Parse("\"objects\":[{\"id\":\"a\",\"marker\":true,\"enemy\":\"dragon\"},{\"id\":\"b\",\"shape\":\"empty\",\"station\":\"loom\"}]");
            var p = Check(c);
            Assert.Contains(p, x => x.Contains("'a'") && x.Contains("dragon"));
            Assert.Contains(p, x => x.Contains("'b'") && x.Contains("loom"));
        }

        [Fact]
        public void Without_the_lists_the_new_fields_are_not_checked()
        {
            var c = Parse("\"objects\":[{\"id\":\"a\",\"marker\":true,\"enemy\":\"dragon\"}]");
            Assert.Empty(c.Validate(null, SceneLayoutTests.Catalog(), new[] { "ground", "grass" }));
        }

        [Fact]
        public void The_empty_shape_is_a_shape_and_needs_no_prefab()
        {
            var c = Parse("\"objects\":[{\"id\":\"e\",\"shape\":\"empty\",\"position\":{\"x\":1,\"z\":1}}]");
            Assert.Empty(Check(c));
            var both = Parse("\"objects\":[{\"id\":\"e\",\"shape\":\"empty\",\"prefab\":\"x.prefab\"}]");
            Assert.Contains(Check(both), x => x.Contains("ровно один"));
        }

        [Fact]
        public void Walkways_are_invisible_strips_checked_like_paths()
        {
            var ok = Parse("\"walkways\":[{\"id\":\"shortcut\",\"points\":[{\"x\":0,\"z\":0},{\"x\":10,\"z\":0}],\"width\":2}]");
            Assert.Single(ok.Walkways);
            Assert.Empty(Check(ok));

            var bad = Parse("\"walkways\":[{\"id\":\"w\",\"points\":[{\"x\":0,\"z\":0}],\"width\":0}]");
            Assert.NotEmpty(Check(bad));

            var dup = Parse("\"paths\":[{\"id\":\"w\",\"points\":[{\"x\":0,\"z\":0},{\"x\":5,\"z\":0}],\"width\":2}],\"walkways\":[{\"id\":\"w\",\"points\":[{\"x\":0,\"z\":1},{\"x\":5,\"z\":1}],\"width\":2}]");
            Assert.Contains(Check(dup), x => x.Contains("повтор"));
        }

        [Fact]
        public void A_walkway_does_not_change_the_terrain_underfoot()
        {
            var c = Parse("\"walkways\":[{\"id\":\"w\",\"points\":[{\"x\":0,\"z\":0},{\"x\":10,\"z\":0}],\"width\":2}]");
            Assert.Equal("grass", TerrainMap.From(c).At(5, 0));
        }

        [Fact]
        public void A_scene_without_the_new_fields_reads_as_before()
        {
            var c = Parse("\"objects\":[{\"id\":\"rock\",\"shape\":\"cube\"}]");
            Assert.Null(c.Objects[0].Enemy);
            Assert.Null(c.Objects[0].Station);
            Assert.Empty(c.Walkways);
        }
    }
}
