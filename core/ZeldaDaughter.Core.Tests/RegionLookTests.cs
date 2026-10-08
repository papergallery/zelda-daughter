using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-22: the region follows the concept frames (docs/concept/world-b) — the camera, the tones of the ground, the road edge, the square without a road through the fountain.</summary>
    public class RegionLookTests
    {
        static SceneConfig Region() => SceneConfig.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json")));

        [Fact]
        public void The_heroine_is_about_a_ninth_of_the_frame()
        {
            // спрайт героини 1,7 м (characters.json: 128 px / 75 ppm), на экране × cos(наклон); кадр по высоте — 2 × size
            var c = Region();
            Assert.True(c.Camera.Orthographic);
            Assert.Equal(35f, c.Camera.Pitch);
            Assert.Equal(45f, c.Camera.Yaw);
            float share = 1.7f * (float)System.Math.Cos(c.Camera.Pitch * System.Math.PI / 180.0) / (2f * c.Camera.Size);
            Assert.InRange(share, (1f / 9f) * 0.85f, (1f / 9f) * 1.15f);
        }

        [Fact]
        public void The_ground_has_three_tones_in_patches_and_the_road_has_a_ragged_edge()
        {
            var c = Region();
            var patches = c.Objects.Where(o => o.Tags.Contains("ground_patch")).ToList();
            Assert.True(patches.Count >= 200, $"пятен земли: {patches.Count}");
            var tones = patches.Where(o => o.Id.StartsWith("patch_")).Select(o => o.Color).Distinct().ToList();
            Assert.InRange(tones.Count, 2, 3);
            Assert.All(patches, o => Assert.Equal(false, o.Collide)); // пятна не мешают ходьбе и тапам
            Assert.True(c.Objects.Count(o => o.Id.StartsWith("edge_rim_")) >= 100);
            Assert.True(c.Objects.Count(o => o.Tags.Contains("pebble")) >= 100);
            // камешки у дороги — рядом с ней, но не на ленте
            var road = c.Paths.Single(p => p.Id == "road_main").Points;
            foreach (var o in c.Objects.Where(o => o.Tags.Contains("pebble")))
            {
                float best = float.MaxValue;
                for (int i = 0; i + 1 < road.Count; i++) best = System.Math.Min(best, Dist(o.Position.X, o.Position.Z, road[i], road[i + 1]));
                Assert.InRange(best, 1.4f, 3.6f);
            }
        }

        static float Dist(float x, float z, Pt a, Pt b)
        {
            float vx = b.X - a.X, vz = b.Z - a.Z, l2 = vx * vx + vz * vz;
            float t = l2 <= 0 ? 0 : System.Math.Max(0f, System.Math.Min(1f, ((x - a.X) * vx + (z - a.Z) * vz) / l2));
            float dx = x - (a.X + vx * t), dz = z - (a.Z + vz * t);
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        [Fact]
        public void Flowers_are_yellow_and_purple_and_the_grass_is_bigger_and_rarer()
        {
            var c = Region();
            foreach (var s in c.Scatter.Where(s => s.Id.StartsWith("meadow_flowers") || s.Id.StartsWith("flower_cluster")))
                Assert.All(s.Models, m => Assert.True(m.Id.Contains("yellow") || m.Id.Contains("purple"), s.Id + " " + m.Id));
            foreach (var id in new[] { "meadow_west", "meadow_east_bank" })
            {
                var s = c.Scatter.Single(x => x.Id == id);
                Assert.True(s.Scale.Min >= 1.4f && s.Density <= 2.2f, id);
                Assert.DoesNotContain(s.Models, m => m.Id.Contains("flower"));
            }
        }

        [Fact]
        public void The_square_has_no_trees_and_residents_walk_around_the_fountain()
        {
            var c = Region();
            var fountain = c.Objects.Single(o => o.Id == "fountain").Position;
            Assert.DoesNotContain(c.Objects, o => o.Model != null && o.Model.Contains("tree") && System.Math.Abs(o.Position.X - fountain.X) < 20 && System.Math.Abs(o.Position.Z - fountain.Z) < 14);
            // ни одна дорога и тропа не идёт через чашу фонтана (6 × 6 м) — и ни один маршрут между якорями
            foreach (var p in c.Paths)
                for (int i = 0; i + 1 < p.Points.Count; i++)
                    for (float t = 0; t <= 1f; t += 0.02f)
                    {
                        float x = p.Points[i].X + (p.Points[i + 1].X - p.Points[i].X) * t, z = p.Points[i].Z + (p.Points[i + 1].Z - p.Points[i].Z) * t;
                        Assert.False(System.Math.Abs(x - fountain.X) < 3.5f && System.Math.Abs(z - fountain.Z) < 3.5f, $"{p.Id} идёт через фонтан");
                    }
            var g = RouteGraph.From(c);
            var anchors = g.Anchors.Where(a => g.PositionOf(a).X > 30 && g.PositionOf(a).X < 125).ToList();
            Assert.Contains("anchor_fountain", anchors);
            foreach (var a in anchors)
                foreach (var b in anchors)
                {
                    var r = g.Route(a, b);
                    Assert.NotNull(r);
                    for (int i = 0; i + 1 < r!.Count; i++)
                        for (float t = 0; t <= 1f; t += 0.05f)
                        {
                            float x = r[i].X + (r[i + 1].X - r[i].X) * t, z = r[i].Y + (r[i + 1].Y - r[i].Y) * t;
                            Assert.False(System.Math.Abs(x - fountain.X) < 3.2f && System.Math.Abs(z - fountain.Z) < 3.2f, $"маршрут {a} → {b} идёт через фонтан");
                        }
                }
        }

        [Fact]
        public void Morning_mist_lies_over_the_meadow_at_the_spawn()
        {
            var c = Region();
            var mist = c.Objects.Where(o => o.Tags.Contains("mist")).ToList();
            Assert.InRange(mist.Count, 8, 30);
            Assert.All(mist, o => Assert.True(System.Math.Abs(o.Position.X - c.Hero.Spawn.X) < 90 && o.Collide == false, o.Id));
        }

        [Fact]
        public void Residents_stand_at_their_place_not_on_a_road()
        {
            var c = Region();
            foreach (var n in c.Objects.Where(o => o.Tags.Contains("npc")))
                foreach (var p in c.Paths)
                    for (int i = 0; i + 1 < p.Points.Count; i++)
                        Assert.True(Dist(n.Position.X, n.Position.Z, p.Points[i], p.Points[i + 1]) > p.Width / 2 + 0.3f, $"{n.Id} стоит на {p.Id}");
            // кузнец — у наковальни
            var smith = c.Objects.Single(o => o.Id == "npc_smith").Position;
            var anvil = c.Objects.Single(o => o.Id == "station_anvil").Position;
            Assert.InRange(System.Math.Sqrt((smith.X - anvil.X) * (smith.X - anvil.X) + (smith.Z - anvil.Z) * (smith.Z - anvil.Z)), 0.5, 2.0);
            var forge = c.Objects.Single(o => o.Id == "anchor_forge").Position;
            Assert.Equal(smith.X, forge.X);
            Assert.Equal(smith.Z, forge.Z);
        }
    }
}
