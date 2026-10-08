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
            // D-22b: карточка спрайта повёрнута к камере целиком (BillboardSprite: rotation = камера), её высота на экране — полные 1,7 м
            // (не × cos наклона, как считал D-22); кадр по высоте — 2 × size
            var c = Region();
            Assert.True(c.Camera.Orthographic);
            Assert.Equal(35f, c.Camera.Pitch);
            Assert.Equal(45f, c.Camera.Yaw);
            float share = 1.7f / (2f * c.Camera.Size);
            Assert.InRange(share, (1f / 9f) * 0.85f, (1f / 9f) * 1.15f);
        }

        static ModelCatalog Catalog() => ModelCatalog.Load(TestPaths.DataRoot);

        [Fact]
        public void The_ground_is_one_painted_surface_and_grass_overlaps_the_road_edge()
        {
            // D-22b: тона земли, кромка дороги и булыжник — шейдером по маске (GroundMask), а не дисками (они давали пунктир пера)
            var c = Region();
            Assert.DoesNotContain(c.Objects, o => o.Tags.Contains("ground_patch"));
            Assert.DoesNotContain(c.Objects, o => o.Shape == "cylinder" && o.Scale.Y < 0.05f);
            Assert.True(GroundMask.Wanted(c));
            Assert.Contains(c.Zones, z => z.Id == "square_zone" && z.Tags.Contains(GroundMask.PavedTag));
            // рваный травяной край: кустики по обе стороны кромки главной дороги, часть — на самом краю ленты
            var edge = c.Scatter.Single(s => s.Id == "road_edge_tufts");
            Assert.True(edge.Avoid.Paths < 0f && edge.Avoid.Paths > -0.6f, "tufts may lean over the edge, not grow in the middle of the road");
            var road = c.Paths.Single(p => p.Id == "road_main");
            var tufts = Scatterer.Generate(c, Catalog()).Where(p => p.ScatterId == "road_edge_tufts").ToList();
            Assert.True(tufts.Count >= 300, $"кустиков у кромки: {tufts.Count}");
            Assert.All(tufts, t => Assert.InRange(road.SignedDistance(t.X, t.Z), -0.36f, 0.85f));
            Assert.True(tufts.Count(t => road.SignedDistance(t.X, t.Z) < 0f) >= tufts.Count / 10, "some tufts overhang the road");
            Assert.True(c.Objects.Count(o => o.Tags.Contains("pebble")) >= 100);
            // камешки у дороги — рядом с ней, но не на ленте
            foreach (var o in c.Objects.Where(o => o.Tags.Contains("pebble")))
            {
                float best = float.MaxValue;
                for (int i = 0; i + 1 < road.Points.Count; i++) best = System.Math.Min(best, Dist(o.Position.X, o.Position.Z, road.Points[i], road.Points[i + 1]));
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
        public void Grass_flowers_and_reeds_are_drawn_billboards_flowers_yellow_and_purple()
        {
            var c = Region();
            var cat = Catalog();
            // вся низкая растительность россыпей — акварельные спрайты (ADR-0001), Kenney-трава и цветы не используются
            foreach (var s in c.Scatter)
                foreach (var m in s.Models)
                {
                    var tags = cat.Get(m.Id).Tags;
                    if (tags.Contains("grass") || tags.Contains("flower") || tags.Contains("bush") || tags.Contains("mushroom"))
                        Assert.True(cat.Get(m.Id).IsSprite, $"{s.Id}: {m.Id} — не спрайт");
                }
            foreach (var s in c.Scatter.Where(s => s.Id.StartsWith("meadow_flowers") || s.Id.StartsWith("flower_cluster") || s.Id == "road_band_flowers"))
                Assert.All(s.Models, m => Assert.True(m.Id.Contains("yellow") || m.Id.Contains("purple"), s.Id + " " + m.Id));
            var placed = Scatterer.Generate(c, cat);
            Assert.Contains(placed, p => p.ModelId.Contains("yellow"));
            Assert.Contains(placed, p => p.ModelId.Contains("purple"));
            Assert.True(placed.Count(p => p.ScatterId == "river_reeds" && (p.ModelId == "veg_reed" || p.ModelId == "veg_cattail")) >= 150, "reeds along the river");
            // клетки сухой травы (огонь D-06) — тоже спрайты
            Assert.All(c.Objects.Where(o => o.Tags.Contains("grass_cell")), o => Assert.True(cat.Get(o.Model!).IsSprite, o.Id));
            // плотность луга у дороги — кустик/цветок через 1–2 м (концепт f1): от 0,2 до 1,2 на м²
            var band = c.Scatter.Single(s => s.Id == "road_band_grass");
            Assert.InRange(band.Density, 20f, 120f);
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
        public void Campfires_stand_on_bare_trodden_earth_with_no_grass_to_catch()
        {
            // D-22b: искры поджигают сухую траву в радиусе campfireSparkRadius (data/elements.json) — у костра голая земля ≥ 2 м (концепт f1n)
            var c = Region();
            var cat = Catalog();
            var placed = Scatterer.Generate(c, cat);
            var fires = c.Objects.Where(o => o.Tags.Contains("campfire")).ToList();
            Assert.True(fires.Count >= 2);
            foreach (var f in fires)
            {
                var bare = c.Zones.Where(z => z.Tags.Contains(GroundMask.BareTag)).ToList();
                Assert.Contains(bare, z => z.SignedDistance(f.Position.X, f.Position.Z) <= -2f);
                foreach (var cell in c.Objects.Where(o => o.Tags.Contains("grass_cell")))
                    Assert.True(Dist2(cell.Position.X, cell.Position.Z, f.Position.X, f.Position.Z) > 2.5f, $"{cell.Id} у костра {f.Id}");
                foreach (var p in placed.Where(p => cat.Get(p.ModelId).IsSprite))
                    Assert.True(Dist2(p.X, p.Z, f.Position.X, f.Position.Z) > 2f, $"{p.Id} у костра {f.Id}");
            }
        }

        static float Dist2(float ax, float az, float bx, float bz) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Fact]
        public void The_morning_mist_is_not_a_stack_of_planes()
        {
            // D-22b: туман рисует постпроход по высоте и мировому шуму (MorningMist задаёт силу по часам) — плоскости давали overdraw
            var c = Region();
            Assert.DoesNotContain(c.Objects, o => o.Tags.Contains("mist"));
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
