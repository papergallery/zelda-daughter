using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-22b: the ground mask (roads, paved, water, field as signed distances) and the drawn billboards in the model catalog.</summary>
    public class GroundMaskTests
    {
        static SceneConfig Small() => new SceneConfig
        {
            Name = "t",
            Ground = new GroundConfig { SizeX = 20, SizeZ = 10 },
            Paths = new List<StripConfig> { new StripConfig { Id = "road", Points = new List<Pt> { new Pt(-10, 0), new Pt(10, 0) }, Width = 2 } },
            Water = new List<StripConfig> { new StripConfig { Id = "brook", Points = new List<Pt> { new Pt(-10, 4), new Pt(10, 4) }, Width = 1 } },
            Zones = new List<ZoneConfig>
            {
                new ZoneConfig { Id = "square", Shape = "circle", Center = new Pt(5, 0), Radius = 2, Tags = new List<string> { "paved" } },
                new ZoneConfig { Id = "plot", Shape = "rect", Center = new Pt(-6, -3), Size = new Pt(4, 2), Tags = new List<string> { "field" } },
                new ZoneConfig { Id = "wolves", Shape = "circle", Center = new Pt(0, -3), Radius = 1, Tags = new List<string> { "predator_zone" } },
            },
        };

        [Fact]
        public void Each_channel_holds_the_signed_distance_to_its_surface()
        {
            var m = GroundMask.Bake(Small(), 0.25f);
            Assert.Equal(80, m.Width);
            Assert.Equal(40, m.Height);
            Assert.Equal(80 * 40 * 4, m.Rgba.Length);
            // R — road: −1 m on its middle line, +1 m one metre off its edge
            Assert.InRange(m.At(0, -3f, 0.1f), -1.1f, -0.85f);
            Assert.InRange(m.At(0, -3f, 2.1f), 0.9f, 1.25f);
            // G — paved zone only (the predator zone is not paved), B — water, A — the field
            Assert.True(m.At(1, 5.1f, 0.1f) < -1.5f);
            Assert.True(m.At(1, 0.1f, -2.9f) > 3.6f, "a zone without the paved tag is not drawn"); // the predator circle (0; −3) is not paved: only the square (5; 0), 3,7 m away
            Assert.True(m.At(2, 0.1f, 4.1f) < 0f);
            Assert.True(m.At(3, -6.1f, -3.1f) < 0f);
            Assert.True(m.At(3, 6f, 3f) > 3.9f);
        }

        [Fact]
        public void Far_from_everything_is_the_far_value_and_bytes_round_trip()
        {
            Assert.Equal(255, GroundMask.Encode(float.PositiveInfinity));
            Assert.Equal(0, GroundMask.Encode(-100f));
            Assert.InRange(GroundMask.Encode(0f), 127, 128);
            for (float d = -3.9f; d < 3.9f; d += 0.37f) Assert.InRange(GroundMask.Decode(GroundMask.Encode(d)) - d, -0.02f, 0.02f);
            var empty = new SceneConfig { Name = "e", Ground = new GroundConfig { SizeX = 4, SizeZ = 4 } };
            Assert.False(GroundMask.Wanted(empty));
            Assert.True(GroundMask.Wanted(Small()));
            Assert.All(GroundMask.Bake(empty, 1f).Rgba, b => Assert.Equal(255, b));
        }

        [Fact]
        public void The_same_config_bakes_the_same_bytes()
        {
            Assert.Equal(GroundMask.Bake(Small(), 0.4f).Rgba, GroundMask.Bake(Small(), 0.4f).Rgba);
        }

        [Fact]
        public void The_region_mask_paves_the_square_and_keeps_the_road_off_the_fountain()
        {
            var c = SceneConfig.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json")));
            var m = GroundMask.Bake(c);
            var f = c.Objects.Single(o => o.Id == "fountain").Position;
            Assert.True(m.At(1, f.X, f.Z) < -3f, "the square around the fountain is paved");
            Assert.True(m.At(0, f.X, f.Z) > 2.5f, "no road runs through the fountain");
            var spawn = c.Hero.Spawn;
            Assert.True(m.At(0, spawn.X, spawn.Z) < 0f, "the heroine wakes on the road");
            Assert.True(m.At(2, 0.1f, 30f) < 0f, "the river is in the water channel");
        }

        [Fact]
        public void Shadow_spots_lie_under_trees_and_rocks_not_under_grass()
        {
            var c = ModelCatalog.Parse("{\"models\":{\"oak\":{\"path\":\"o.fbx\",\"tags\":[\"tree\"]},\"pebble\":{\"path\":\"p.fbx\",\"tags\":[\"rock\",\"small\"]}," +
                "\"veg_a\":{\"sprite\":\"a\",\"size\":[1,1],\"collider\":{\"kind\":\"none\"},\"tags\":[\"vegetation\",\"grass\"]}}}",
                "{\"bounds\":{\"o.fbx\":{\"center\":[0,2,0],\"size\":[3,4,3]},\"p.fbx\":{\"center\":[0,0,0],\"size\":[0.3,0.2,0.3]}}}");
            var scene = Small();
            scene.Objects.Add(new ObjectConfig { Id = "tree", Model = "oak", Position = new V3(-5, 0, -3) });
            var placed = new[] { new ScatterPlacement { ModelId = "pebble", X = 0, Z = 3, Scale = 1 }, new ScatterPlacement { ModelId = "veg_a", X = 6, Z = 3, Scale = 1 } };
            var spots = GroundMask.SpotsOf(scene, c, placed);
            Assert.Single(spots);
            Assert.Equal(3f * 0.34f, spots[0].R, 3);
            var m = GroundMask.Bake(scene, 0.25f);
            m.BakeSpots(spots);
            Assert.True(m.SpotAt(-5.1f, -2.9f) < -0.8f);
            Assert.InRange(m.SpotAt(-3.1f, -2.9f), 0.7f, 1.2f);
            Assert.True(m.SpotAt(6f, 3f) > 3.9f);
        }

        [Fact]
        public void Clumps_put_several_items_around_each_centre()
        {
            var scene = Small();
            var cat = ModelCatalog.Parse("{\"models\":{\"veg_a\":{\"sprite\":\"a\",\"size\":[0.3,0.3],\"collider\":{\"kind\":\"none\"}}}}", "{\"bounds\":{}}");
            scene.Scatter.Add(new ScatterConfig { Id = "fl", Area = new Area { Shape = "rect", Center = new Pt(0, -3), Size = new Pt(18, 3) }, Seed = 5, Density = 10,
                Models = new List<ScatterModel> { new ScatterModel { Id = "veg_a" } }, Clump = new ClumpConfig { Radius = 0.8f, Min = 5, Max = 9 } });
            var a = Scatterer.Generate(scene, cat);
            Assert.Equal(Scatterer.Digest(a), Scatterer.Digest(Scatterer.Generate(scene, cat)));
            Assert.InRange(a.Count, 15, 45);   // 54 m² × 10 / 100 ≈ 5 clumps of 5–9 (some members fall outside the strip)
            // nearly every item has at least 3 neighbours within the clump's diameter (a clump cut by the edge of the area may keep fewer)
            int grouped = a.Count(p => a.Count(q => (q.X - p.X) * (q.X - p.X) + (q.Z - p.Z) * (q.Z - p.Z) < 1.6f * 1.6f) >= 4);
            Assert.True(grouped >= a.Count * 0.85, $"{grouped} of {a.Count} in clumps");
            Assert.DoesNotContain(scene.Validate(), x => x.Contains("clump"));
        }

        // --- billboards in the catalog ---

        [Fact]
        public void A_drawn_billboard_is_a_model_with_a_size_and_no_collider()
        {
            var c = ModelCatalog.Parse("{\"models\":{\"veg_a\":{\"sprite\":\"a\",\"size\":[0.6,0.4],\"collider\":{\"kind\":\"none\"}}}}", "{\"bounds\":{}}");
            Assert.Empty(c.Validate());
            Assert.True(c.Get("veg_a").IsSprite);
            var b = c.Bounds("veg_a");
            Assert.Equal(0.6f, b.SizeX, 3);
            Assert.Equal(0.4f, b.SizeY, 3);
            Assert.Equal(0f, b.MinY);
            var bad = ModelCatalog.Parse("{\"models\":{\"x\":{\"sprite\":\"x\",\"size\":[0,1],\"collider\":{\"kind\":\"none\"}},\"y\":{\"sprite\":\"y\",\"size\":[1,1]},\"z\":{\"sprite\":\"z\",\"size\":[1,1],\"path\":\"z.fbx\",\"collider\":{\"kind\":\"none\"}}}}", "{\"bounds\":{\"z.fbx\":{\"center\":[0,0,0],\"size\":[1,1,1]}}}");
            var p = bad.Validate();
            Assert.Contains(p, x => x.Contains("'x'") && x.Contains("size"));
            Assert.Contains(p, x => x.Contains("'y'") && x.Contains("коллайдер"));
            Assert.Contains(p, x => x.Contains("'z'") && x.Contains("ровно один"));
        }

        [Fact]
        public void The_vegetation_of_the_catalog_matches_the_atlas()
        {
            var catalog = ModelCatalog.Load(TestPaths.DataRoot);
            var atlas = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName,
                "ZeldaDaughter", "Assets", "Art", "Vegetation", "vegetation.json")));
            var sprites = ((Newtonsoft.Json.Linq.JObject)atlas["sprites"]!).Properties().ToList();
            Assert.InRange(sprites.Count, 15, 40);
            foreach (var s in sprites)
            {
                var def = catalog.Get("veg_" + s.Name);
                Assert.True(def.IsSprite);
                Assert.Equal((float)s.Value["size_m"]![0]!, def.Size![0], 3);
                Assert.Equal((float)s.Value["size_m"]![1]!, def.Size![1], 3);
            }
            Assert.Empty(catalog.Validate());
        }
    }
}
