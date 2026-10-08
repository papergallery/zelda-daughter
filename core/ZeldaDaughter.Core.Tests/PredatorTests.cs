using System;
using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Combat;
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
        public void Wolves_come_at_night_one_at_a_time_20_to_35_metres_from_her_and_not_more_than_the_target()
        {
            var g = Night();
            var spawned = Spawns(g, 200);
            Assert.Equal(D.Night.MaxAtNight, spawned.Count);
            Assert.Equal(spawned.Count, spawned.Select(s => s.EnemyId).Distinct().Count());
            foreach (var s in spawned)
            {
                float d = (s.Position - g.HeroPosition).Length;
                Assert.InRange(d, D.Night.MinHeroDistance - 1e-3f, D.Night.MaxHeroDistance + 1e-3f);
                Assert.Equal(D.Night.Enemy, s.DefId);
            }
            // …not all on one side: the angle comes from the roll
            Assert.True(spawned.Select(s => Math.Round(Math.Atan2(s.Position.Y, s.Position.X), 1)).Distinct().Count() > 1);
            // the first one is not instant: the interval between spawns
            var g2 = Night();
            var first = Spawns(g2, D.Night.SpawnIntervalSeconds - 2);
            Assert.True(first.Count <= 1);
        }

        [Fact]
        public void The_place_is_picked_uniformly_over_the_ring_and_from_the_rolls_alone()
        {
            var p = Night().Nature.Predators;
            var hero = new Vec2(10, -4);
            Assert.Equal(p.RingPoint(hero, 0.3, 0.7), p.RingPoint(hero, 0.3, 0.7));
            Assert.Equal(D.Night.MinHeroDistance, (p.RingPoint(hero, 0.1, 0) - hero).Length, 3);
            Assert.Equal(D.Night.MaxHeroDistance, (p.RingPoint(hero, 0.1, 1) - hero).Length, 3);
            for (double a = 0; a < 1; a += 0.0625)
                for (double r = 0; r <= 1; r += 0.125)
                    Assert.InRange((p.RingPoint(hero, a, r) - hero).Length, D.Night.MinHeroDistance - 1e-3f, D.Night.MaxHeroDistance + 1e-3f);
            // uniform by area: half of the points lie inside the radius whose circle is half of the ring's area
            double mid = Math.Sqrt((D.Night.MinHeroDistance * D.Night.MinHeroDistance + D.Night.MaxHeroDistance * D.Night.MaxHeroDistance) / 2);
            Assert.Equal(mid, (p.RingPoint(hero, 0.5, 0.5) - hero).Length, 2);
        }

        [Fact]
        public void By_day_nothing_spawns()
        {
            var g = Night(0.5);
            Assert.Empty(Spawns(g, 300));
        }

        [Fact]
        public void No_wolves_in_the_light_of_a_fire_in_the_town_outside_the_ground_or_where_the_view_forbids()
        {
            var g = Night();
            var at = new Vec2(25, 0);
            g.Camp.Restore(1, null, new[] { new Campfire("fire", at, 5000f, 10f) });
            bool Lit(Vec2 p) => g.Camp.IsLitNear(p, D.Camp.LightRadius);
            Assert.False(g.Nature.Predators.Allowed(new Vec2(25, D.Camp.LightRadius - 1), Lit));
            Assert.True(g.Nature.Predators.Allowed(new Vec2(-25, 0), Lit));
            var s1 = Spawns(g, 400);
            Assert.NotEmpty(s1);
            Assert.All(s1, s => Assert.True((s.Position - at).Length > D.Camp.LightRadius));

            // the hero in the middle of the town: it is a safe circle, nobody is called
            var town = Night();
            town.Nature.Predators.AddSafe(D.Night.SafeAreas[0].Anchor, town.HeroPosition);
            Assert.True(D.Night.SafeAreas[0].Radius >= D.Night.MaxHeroDistance, "the town reaches farther than a wolf is called");
            Assert.Empty(Spawns(town, 200));
            Assert.True(town.Nature.Predators.IsSafe(new Vec2(D.Night.SafeAreas[0].Radius - 1, 0)));
            Assert.False(town.Nature.Predators.IsSafe(new Vec2(D.Night.SafeAreas[0].Radius + 1, 0)));

            // the hero at the edge of the ground: nothing outside the bounds
            var edge = Night();
            edge.HeroPosition = new Vec2(D.Night.Bounds.MaxX, 0);
            Assert.All(Spawns(edge, 300), s => Assert.True(D.Night.Bounds.Contains(s.Position), s.Position.ToString()));

            // the view forbids a half-plane (the river): none there
            var wet = Night();
            wet.Nature.Predators.Forbidden = p => p.X > 0;
            var s3 = Spawns(wet, 300);
            Assert.NotEmpty(s3);
            Assert.All(s3, s => Assert.True(s.Position.X <= 0));
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
        public void In_the_morning_far_wolves_vanish_and_near_ones_walk_away_and_vanish_once_far()
        {
            var g = Night();
            var spawned = Spawns(g, 200);
            Assert.Equal(D.Night.MaxAtNight, spawned.Count);
            g.Clock.SetTime(1, 0.5);
            // the roster's own wolves: one right next to her, the others where they were called (20+ m)
            var near = g.Enemies.Get(spawned[0].EnemyId)!;
            near.SetPosition(g.HeroPosition + new Vec2(3, 0));
            var events = new System.Collections.Generic.List<WorldEvent>();
            for (int i = 0; i < 3; i++) events.AddRange(g.TickWorld(0.25f, 0.5));
            var gone = events.Where(e => e.Kind == WorldEventKind.PredatorDespawned).Select(e => e.Id).ToList();
            var sent = events.Where(e => e.Kind == WorldEventKind.PredatorDismissed).Select(e => e.Id).ToList();
            Assert.DoesNotContain(near.Id, gone);
            Assert.Contains(near.Id, sent);
            Assert.Equal(EnemyState.Leaving, near.State);
            Assert.Equal(2, gone.Count);                                   // the far ones (> despawnDistance) are simply gone
            Assert.All(gone, id => Assert.Null(g.Enemies.Get(id)));
            // it walks off and is removed when it is far; nothing is said twice
            var hero = new HeroCombatProbe(g);
            int ticks = 0;
            while (g.Enemies.Get(near.Id) != null && ticks++ < 2000)
            {
                g.Enemies.Tick(0.05f, 0.5, hero.Notices);
                events.AddRange(g.TickWorld(0.05f, 0.5));
            }
            Assert.Null(g.Enemies.Get(near.Id));
            Assert.Equal(1, events.Count(e => e.Kind == WorldEventKind.PredatorDismissed && e.Id == near.Id));
            Assert.Equal(1, events.Count(e => e.Kind == WorldEventKind.PredatorDespawned && e.Id == near.Id));
            Assert.Equal(0, g.Nature.Predators.AliveCount);
        }

        sealed class HeroCombatProbe
        {
            public readonly System.Collections.Generic.List<ZeldaDaughter.Core.Combat.EnemyNotice> Notices = new System.Collections.Generic.List<ZeldaDaughter.Core.Combat.EnemyNotice>();
            public HeroCombatProbe(GameState g) { g.Combat.Position = g.HeroPosition; }
        }

        [Fact]
        public void The_night_wolf_hunts_her_from_where_it_was_called_and_reaches_her()
        {
            var g = Night();
            g.Combat.Position = g.HeroPosition;
            string id = Spawns(g, 25).First().EnemyId;
            var wolf = g.Enemies.Get(id)!;
            float start = (wolf.Position - g.HeroPosition).Length;
            Assert.InRange(start, D.Night.MinHeroDistance - 1e-3f, D.Night.MaxHeroDistance + 1e-3f);
            var notices = new System.Collections.Generic.List<ZeldaDaughter.Core.Combat.EnemyNotice>();
            bool windup = false;
            for (int i = 0; i < 400 && !windup; i++)
            {
                g.Enemies.Tick(0.05f, 0.5, notices);
                windup |= notices.Any(n => n.EnemyId == id && n.Event.Kind == ZeldaDaughter.Core.Combat.EnemyEventKind.WindupStarted);
            }
            Assert.True(windup, "in at most 20 s a wolf called 20–35 m away has come up and wound up its blow");
            Assert.True((wolf.Position - g.HeroPosition).Length <= wolf.Def.Range + 0.01f);
        }

        [Fact]
        public void A_fire_between_stops_the_hunt_at_the_edge_of_its_reach()
        {
            var g = Night();
            g.Combat.Position = g.HeroPosition;
            g.Camp.Restore(1, null, new[] { new Campfire("fire", g.HeroPosition, 5000f, 10f) });
            var wolf = g.Enemies.Spawn("w", "wolf", new Vec2(D.Night.MaxHeroDistance, 0))!;
            wolf.Hunt();
            var notices = new System.Collections.Generic.List<ZeldaDaughter.Core.Combat.EnemyNotice>();
            for (int i = 0; i < 1200; i++)
            {
                g.Enemies.Tick(0.05f, 0.5, notices);
                Assert.DoesNotContain(notices, n => n.Event.Kind == ZeldaDaughter.Core.Combat.EnemyEventKind.WindupStarted);
            }
            float d = (wolf.Position - g.HeroPosition).Length;
            Assert.InRange(d, D.Enemies.Fire.CampfireRadius - 0.01f, D.Enemies.Fire.CampfireRadius + 1.5f);
        }

        [Fact]
        public void Bounds_and_safe_anchors_belong_to_the_region_scene()
        {
            var path = Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var ids = SceneConfig.Parse(json).Objects.ToDictionary(o => o.Id);
            foreach (var a in D.Night.SafeAreas) Assert.True(ids.ContainsKey(a.Anchor), $"scenes/region.json: нет объекта '{a.Anchor}' (data/night.json safeAreas)");
            var ground = Newtonsoft.Json.Linq.JObject.Parse(json)["ground"]!;
            float sx = (float)ground["sizeX"]!, sz = (float)ground["sizeZ"]!;
            Assert.InRange(D.Night.Bounds.MinX, -sx / 2, -sx / 2 + 20); Assert.InRange(D.Night.Bounds.MaxX, sx / 2 - 20, sx / 2);
            Assert.InRange(D.Night.Bounds.MinZ, -sz / 2, -sz / 2 + 20); Assert.InRange(D.Night.Bounds.MaxZ, sz / 2 - 20, sz / 2);
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
    }
}
