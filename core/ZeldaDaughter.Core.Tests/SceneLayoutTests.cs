using System;
using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Movement;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-10: model catalog, areas, terrain under the hero (water, bridge) and deterministic scatter.</summary>
    public class SceneLayoutTests
    {
        static string Repo => Directory.GetParent(TestPaths.CoreRoot)!.FullName;

        public static ModelCatalog Catalog() => ModelCatalog.Load(TestPaths.DataRoot);

        static SceneConfig Scene(string name) => SceneConfig.Parse(File.ReadAllText(Path.Combine(Repo, "scenes", name + ".json")));

        static SceneConfig Parse(string json) => SceneConfig.Parse("{\"name\":\"t\",\"ground\":{\"sizeX\":60,\"sizeZ\":60,\"terrain\":\"grass\"},\"camera\":{\"pitch\":35,\"distance\":20,\"size\":8},\"hero\":{\"shape\":\"capsule\"}," + json + "}");

        // --- catalog ---

        [Fact]
        public void Catalog_resolves_every_model_to_a_file_with_measured_bounds()
        {
            var problems = Catalog().Validate(path => File.Exists(Path.Combine(Repo, "ZeldaDaughter", path)));
            Assert.Empty(problems);
        }

        [Fact]
        public void Models_are_real_size_metres()
        {
            var c = Catalog();
            Assert.InRange(c.Bounds("tree_oak").SizeY, 2.5f, 7f);
            Assert.InRange(c.Bounds("prop_barrel").SizeY, 0.6f, 1.2f);
            Assert.InRange(c.Bounds("town_wall").SizeY, 2.8f, 3.2f);
            Assert.InRange(c.Bounds("fence_simple").SizeY, 0.6f, 1.4f);
        }

        [Fact]
        public void Composite_house_bounds_cover_its_tiles_and_stand_on_the_ground()
        {
            var b = Catalog().Bounds("house_small");
            Assert.InRange(b.SizeX, 6f, 7.5f);
            Assert.InRange(b.SizeZ, 6f, 7.5f);
            Assert.InRange(b.MinY, -0.2f, 0.1f);
            Assert.True(b.MaxY > 3f, "roof above the 3 m walls");
        }

        [Fact]
        public void Catalog_names_a_dangling_part()
        {
            var c = ModelCatalog.Parse("{\"models\":{\"a\":{\"parts\":[{\"model\":\"nope\"}]},\"b\":{\"path\":\"x.fbx\"}}}", "{\"bounds\":{}}");
            var p = c.Validate();
            Assert.Contains(p, x => x.Contains("'a'") && x.Contains("nope"));
            Assert.Contains(p, x => x.Contains("'b'") && x.Contains("границ"));
        }

        // --- areas ---

        [Fact]
        public void Rect_circle_and_strip_know_what_is_inside()
        {
            var rect = new Area { Shape = "rect", Center = new Pt(10, 0), Size = new Pt(4, 2) };
            Assert.True(rect.Contains(11.9f, 0.9f));
            Assert.False(rect.Contains(12.1f, 0f));
            Assert.Equal(1f, rect.SignedDistance(13f, 0f), 3);

            var turned = new Area { Shape = "rect", Center = new Pt(0, 0), Size = new Pt(4, 2), Rotation = 90 };
            Assert.True(turned.Contains(0f, 1.9f), "turned 90°, the long side lies along z");
            Assert.False(turned.Contains(1.9f, 0f));

            var circle = new Area { Radius = 3, Center = new Pt(1, 1) };
            Assert.Equal("circle", circle.Kind);
            Assert.True(circle.Contains(3.9f, 1f));
            Assert.False(circle.Contains(4.1f, 1f));

            var strip = new Area { Points = { new Pt(0, 0), new Pt(10, 0), new Pt(10, 10) }, Width = 4 };
            Assert.Equal("strip", strip.Kind);
            Assert.True(strip.Contains(5f, 1.9f));
            Assert.False(strip.Contains(5f, 2.1f));
            Assert.True(strip.Contains(11.9f, 7f), "second segment");
            Assert.Equal(80f, strip.SquareMetres(), 2);
        }

        // --- terrain under the hero ---

        [Fact]
        public void River_slows_the_hero_and_the_bridge_deck_does_not()
        {
            var scene = Scene("models-test");
            var map = TerrainMap.From(scene);
            Assert.Equal("grass", map.At(-20f, -15f));
            Assert.Equal("road", map.At(0f, -15f));
            Assert.Equal("water", map.At(-20f, 8.5f));
            Assert.Equal("road", map.At(6f, 8.6f));

            var speed = new SpeedModel(ZeldaDaughter.Core.Data.DataSet.Load(TestPaths.DataRoot).Movement);
            float inWater = speed.Speed(0.5f, map.At(-20f, 8.5f), Array.Empty<float>());
            float onGrass = speed.Speed(0.5f, map.At(-20f, -15f), Array.Empty<float>());
            float onBridge = speed.Speed(0.5f, map.At(6f, 8.6f), Array.Empty<float>());
            Assert.True(inWater < onGrass * 0.5f, $"water {inWater} vs grass {onGrass}");
            Assert.Equal(onGrass, onBridge, 3);
        }

        [Fact]
        public void Terrain_map_survives_json()
        {
            var map = TerrainMap.From(Scene("models-test"));
            var back = TerrainMap.FromJson(map.ToJson());
            foreach (var (x, z) in new[] { (6f, 8.6f), (-20f, 8.5f), (0f, -15f), (30f, 25f) })
                Assert.Equal(map.At(x, z), back.At(x, z));
        }

        // --- scatter ---

        [Fact]
        public void Scatter_is_deterministic_and_the_seed_changes_it()
        {
            var scene = Scene("models-test");
            var cat = Catalog();
            var a = Scatterer.Generate(scene, cat);
            var b = Scatterer.Generate(scene, cat);
            Assert.NotEmpty(a);
            Assert.Equal(Scatterer.Digest(a), Scatterer.Digest(b));
            scene.Scatter[0].Seed = 99;
            Assert.NotEqual(Scatterer.Digest(a), Scatterer.Digest(Scatterer.Generate(scene, cat)));
        }

        [Fact]
        public void Scatter_density_counts_per_hundred_square_metres_inside_the_area()
        {
            var scene = Parse("\"scatter\":[{\"id\":\"s\",\"area\":{\"shape\":\"rect\",\"center\":{\"x\":0,\"z\":0},\"size\":{\"x\":20,\"z\":10}},\"models\":[{\"id\":\"grass\"}],\"density\":10,\"seed\":5}]");
            var list = Scatterer.Generate(scene, null);
            Assert.Equal(20, list.Count); // 200 m² × 10 / 100
            Assert.All(list, p => { Assert.InRange(p.X, -10f, 10f); Assert.InRange(p.Z, -5f, 5f); });
            Assert.Equal("s_000", list[0].Id);
        }

        [Fact]
        public void Scatter_keeps_off_roads_water_buildings_and_zones()
        {
            var scene = Scene("models-test");
            var list = Scatterer.Generate(scene, Catalog());
            var meadow = list.Where(p => p.ScatterId == "meadow_grass").ToList();
            Assert.True(meadow.Count > 100, $"meadow has {meadow.Count}");
            var road = scene.Paths.Single(p => p.Id == "road_main");
            var river = scene.Water.Single();
            var bridge = scene.Zones.Single(z => z.Id == "bridge_deck");
            foreach (var p in meadow)
            {
                Assert.True(road.SignedDistance(p.X, p.Z) >= 0.8f - 1e-3f, $"{p.Id} on the road");
                Assert.True(river.SignedDistance(p.X, p.Z) >= 1f - 1e-3f, $"{p.Id} in the river");
                Assert.False(bridge.Contains(p.X, p.Z), $"{p.Id} on the bridge");
            }
            // The house stands in the meadow's reach: nothing grows inside its footprint.
            var b = Catalog().Bounds("house_small");
            var house = scene.Objects.Single(o => o.Id == "house_a");
            Assert.DoesNotContain(list, p => Math.Abs(p.X - house.Position.X) < b.SizeX / 2 && Math.Abs(p.Z - house.Position.Z) < b.SizeZ / 2);
        }

        [Fact]
        public void Scatter_respects_spacing_weights_and_the_ground_edge()
        {
            var scene = Parse("\"scatter\":[{\"id\":\"s\",\"area\":{\"shape\":\"rect\",\"center\":{\"x\":0,\"z\":0},\"size\":{\"x\":200,\"z\":200}}," +
                "\"models\":[{\"id\":\"a\",\"weight\":9},{\"id\":\"b\",\"weight\":1}],\"density\":2,\"seed\":3,\"minSpacing\":2}]");
            var list = Scatterer.Generate(scene, null);
            Assert.All(list, p => Assert.True(Math.Abs(p.X) <= 29f && Math.Abs(p.Z) <= 29f, "inside the 60 m ground with a margin"));
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    Assert.True((list[i].X - list[j].X) * (list[i].X - list[j].X) + (list[i].Z - list[j].Z) * (list[i].Z - list[j].Z) >= 4f - 1e-3f);
            int a = list.Count(p => p.ModelId == "a"), b = list.Count(p => p.ModelId == "b");
            Assert.True(a > b * 4, $"a={a} b={b}");
        }

        // --- config ---

        [Fact]
        public void Old_scenes_keep_working_without_the_new_sections()
        {
            foreach (var name in new[] { "prologue-grey", "g1-capsule" })
            {
                var c = Scene(name);
                Assert.Empty(c.Paths); Assert.Empty(c.Water); Assert.Empty(c.Zones); Assert.Empty(c.Scatter);
                Assert.Equal(c.Ground.Terrain, TerrainMap.From(c).At(1f, 1f));
                Assert.Empty(Scatterer.Generate(c, Catalog()));
            }
        }

        [Fact]
        public void Layout_problems_are_named()
        {
            var scene = Parse("\"paths\":[{\"id\":\"r\",\"points\":[{\"x\":0,\"z\":0}],\"width\":2}],\"water\":[{\"id\":\"r\",\"shape\":\"circle\",\"radius\":2}]," +
                "\"zones\":[{\"id\":\"z\",\"shape\":\"rect\",\"size\":{\"x\":1,\"z\":1},\"terrain\":\"lava\"}]," +
                "\"objects\":[{\"id\":\"o\",\"model\":\"ghost\"},{\"id\":\"p\",\"model\":\"tree_oak\",\"shape\":\"cube\"}]," +
                "\"scatter\":[{\"id\":\"s\",\"area\":{\"ref\":\"nowhere\"},\"models\":[{\"id\":\"ghost\"}],\"density\":0},{\"id\":\"t\",\"area\":{\"ref\":\"z\"},\"models\":[{\"id\":\"grass\"}],\"density\":1,\"avoid\":{\"zones\":[\"nope\"]}}]");
            var p = scene.Validate(null, Catalog(), new[] { "ground", "grass", "road", "water", "mud" });
            Assert.Contains(p, x => x.Contains("'r'") && x.Contains("минимум 2 точки"));
            Assert.Contains(p, x => x.Contains("'r'") && x.Contains("повтор id"));
            Assert.Contains(p, x => x.Contains("'z'") && x.Contains("lava"));
            Assert.Contains(p, x => x.Contains("'o'") && x.Contains("ghost"));
            Assert.Contains(p, x => x.Contains("'p'") && x.Contains("model нельзя"));
            Assert.Contains(p, x => x.Contains("'s'") && x.Contains("nowhere"));
            Assert.Contains(p, x => x.Contains("'s'") && x.Contains("density"));
            Assert.Contains(p, x => x.Contains("'t'") && x.Contains("nope"));
        }

        [Fact]
        public void Models_test_scene_shows_every_kind_and_stays_out_of_the_build()
        {
            var c = Scene("models-test");
            Assert.False(c.Build.Include);
            Assert.True(string.IsNullOrEmpty(c.Save.Slot));
            Assert.NotEmpty(c.Water);
            Assert.NotEmpty(c.Paths);
            Assert.NotEmpty(c.Zones);
            var cat = Catalog();
            var tags = c.Objects.Where(o => o.Model != null).SelectMany(o => cat.Get(o.Model!).Tags).ToHashSet();
            foreach (var kind in new[] { "tree", "bush", "rock", "fence", "crop", "bridge", "building", "prop", "bed", "campfire" })
                Assert.Contains(kind, tags);
        }
    }
}
