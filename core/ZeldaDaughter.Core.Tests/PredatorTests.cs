using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-06 (night): project-design.md §2 «Ночью опаснее: больше хищников».</summary>
    public class PredatorTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        record Spawn(string EnemyId, string DefId, Vec2 Position);

        static GameState Night(double timeOfDay = 0.95)
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, timeOfDay);
            g.HeroPosition = new Vec2(0, 0);
            g.Nature.Predators.AddZone(D.Night.Zones[0], new Vec2(60, 0));
            g.Nature.Predators.AddZone(D.Night.Zones[1], new Vec2(0, 70));
            return g;
        }

        static System.Collections.Generic.List<Spawn> Spawns(GameState g, double seconds, double roll = 0.3)
        {
            var all = new System.Collections.Generic.List<Spawn>();
            for (int i = 0; i < (int)seconds; i++)
                foreach (var e in g.TickWorld(1, (roll + i * 0.37) % 1.0))
                    if (e.Kind == WorldEventKind.PredatorSpawned) all.Add(new Spawn(e.Id, e.Detail, e.Position));
            return all;
        }

        [Fact]
        public void By_day_there_are_no_night_wolves_at_deep_night_the_full_number()
        {
            var g = Night();
            var p = g.Nature.Predators;
            Assert.Equal(D.Night.MaxAtNight, p.TargetCount(0f));
            Assert.Equal(0, p.TargetCount(1f));
            Assert.Equal(0, p.TargetCount(D.Night.DaylightBelow));
            int dusk = p.TargetCount(D.Night.DaylightBelow / 2);
            Assert.InRange(dusk, 1, D.Night.MaxAtNight - 1);
            int prev = int.MaxValue;
            for (float d = 0; d < D.Night.DaylightBelow; d += 0.01f) { int t = p.TargetCount(d); Assert.True(t <= prev); prev = t; }
        }

        [Fact]
        public void Wolves_come_at_night_one_at_a_time_in_the_scene_zones_and_not_more_than_the_target()
        {
            var g = Night();
            var spawned = Spawns(g, 200);
            Assert.Equal(D.Night.MaxAtNight, spawned.Count);
            Assert.Equal(spawned.Count, spawned.Select(s => s.EnemyId).Distinct().Count());
            foreach (var s in spawned)
            {
                bool inZone = new[] { new Vec2(60, 0), new Vec2(0, 70) }.Any(c => (s.Position - c).Length <= D.Night.ZoneRadius + 1e-3f);
                Assert.True(inZone, s.Position.ToString());
                Assert.True((s.Position - g.HeroPosition).Length >= D.Night.MinHeroDistance);
                Assert.Equal(D.Night.Enemy, s.DefId);
            }
            // the first one is not instant: the interval between spawns
            var g2 = Night();
            var first = Spawns(g2, D.Night.SpawnIntervalSeconds - 2);
            Assert.True(first.Count <= 1);
        }

        [Fact]
        public void By_day_nothing_spawns()
        {
            var g = Night(0.5);
            Assert.Empty(Spawns(g, 300));
        }

        [Fact]
        public void No_wolves_close_to_the_hero_or_in_the_light_of_a_fire()
        {
            var g = Night();
            g.HeroPosition = new Vec2(60, 0);                        // standing in the wolf zone
            var near = Spawns(g, 100);
            Assert.All(near, s => Assert.True((s.Position - g.HeroPosition).Length >= D.Night.MinHeroDistance));

            var h = Night();
            h.Bag.Add("firewood"); h.Bag.Add("flint");
            h.Camp.Use(h.Camp.Place("firewood", new Vec2(60, 0)).Object!.Id, "flint");
            h.HeroPosition = new Vec2(-200, 0);
            h.Bag.Add("firewood", 5);
            var burning = h.Camp.Campfires.Single();
            for (int i = 0; i < 5; i++) h.Camp.Use(burning.Id, "firewood");
            var s2 = Spawns(h, 200);
            Assert.All(s2, s => Assert.True((s.Position - new Vec2(60, 0)).Length > D.Camp.LightRadius));
        }

        [Fact]
        public void A_killed_wolf_is_replaced_later_with_a_new_id()
        {
            var g = Night();
            var first = Spawns(g, 200);
            Assert.Equal(D.Night.MaxAtNight, first.Count);
            g.Nature.Predators.Released(first[0].EnemyId);
            var more = Spawns(g, 100);
            Assert.Single(more);
            Assert.DoesNotContain(more[0].EnemyId, first.Select(f => f.EnemyId));
        }

        [Fact]
        public void In_the_morning_far_wolves_leave_and_near_ones_stay()
        {
            var g = Night();
            var spawned = Spawns(g, 200);
            Assert.NotEmpty(spawned);
            g.Clock.SetTime(1, 0.5);
            g.HeroPosition = spawned[0].Position;                      // one wolf right next to her
            var events = new System.Collections.Generic.List<WorldEvent>();
            for (int i = 0; i < 5; i++) events.AddRange(g.TickWorld(1, 0.5));
            var gone = events.Where(e => e.Kind == WorldEventKind.PredatorDespawned).Select(e => e.Id).ToList();
            Assert.DoesNotContain(spawned[0].EnemyId, gone);
            var expected = spawned.Where(sp => (sp.Position - g.HeroPosition).Length > D.Night.DespawnDistance).Select(sp => sp.EnemyId).OrderBy(x => x).ToList();
            Assert.Equal(expected, gone.OrderBy(x => x).ToList());
            Assert.NotEmpty(expected);
        }

        [Fact]
        public void Zones_the_scene_did_not_register_are_ignored()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 0.95);
            Assert.Empty(Spawns(g, 200));
        }

        [Fact]
        public void The_same_rolls_give_the_same_night()
        {
            string Play()
            {
                var g = Night();
                var s = Spawns(g, 150, 0.21);
                return string.Join(";", s.Select(x => $"{x.EnemyId}@{x.Position}")) + SaveGame.Capture(g);
            }
            Assert.Equal(Play(), Play());
        }

        [Fact]
        public void Ids_stay_unique_across_a_save_and_a_load()
        {
            var g = Night();
            var first = Spawns(g, 100);
            string a = SaveGame.Capture(g);
            var fresh = Night();
            SaveGame.Restore(fresh, a);
            Assert.Equal(a, SaveGame.Capture(fresh));
            var more = Spawns(fresh, 200);
            Assert.NotEmpty(more);
            Assert.Empty(more.Select(m => m.EnemyId).Intersect(first.Select(f => f.EnemyId)));
        }

        [Fact]
        public void Night_data_is_checked()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["night.json"] = files["night.json"].Replace("\"enemy\": \"wolf\"", "\"enemy\": \"wolfy\"");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("night.json") && p.Contains("wolfy"));
        }

        [Fact]
        public void Predator_zones_exist_in_the_region_scene()
        {
            var path = Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json");
            if (!File.Exists(path)) return; // scenes/region.json comes with D-10; until then the zone ids are the contract in data/night.json
            var ids = SceneConfig.Parse(File.ReadAllText(path)).Objects.Select(o => o.Id).ToHashSet();
            foreach (var z in D.Night.Zones) Assert.True(ids.Contains(z), $"scenes/region.json: нет объекта-зоны '{z}' (data/night.json)");
        }
    }
}
