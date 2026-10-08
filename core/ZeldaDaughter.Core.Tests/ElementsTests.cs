using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-06 (elements): project-design.md §8 — fire on dry grass with wind, rain makes grass wet and the ground mud.</summary>
    public class ElementsTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState G()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            g.HeroPosition = new Vec2(-100, -100);
            return g;
        }

        /// <summary>A row of cells along x, one metre apart: g0, g1, …</summary>
        static void Row(GameState g, int n, float z = 0)
        {
            for (int i = 0; i < n; i++) g.Nature.Grass.AddCell($"g{i}", new Vec2(i, z));
        }

        static double Roll(int i) => (i * 0.6180339887 + 0.1337) % 1.0;   // never near 0: a roll below the rain chance would start a rain

        static void Run(GameState g, float seconds, float dt = 0.5f, int salt = 0)
        {
            for (int i = 0; i < (int)(seconds / dt); i++) g.TickWorld(dt, Roll(i + salt));
        }

        // --- weather ---

        [Fact]
        public void Rain_starts_by_a_roll_lasts_a_while_and_stops()
        {
            var g = G();
            var w = g.Nature.Weather;
            Assert.False(w.IsRaining);
            g.TickWorld(10, 0.99999);
            Assert.False(w.IsRaining);
            var ev = g.TickWorld(10, 0.0);
            Assert.True(w.IsRaining);
            Assert.Contains(ev, e => e.Kind == WorldEventKind.RainStarted);
            Assert.InRange(w.RainLeft, D.Elements.Rain.MinSeconds - 10, D.Elements.Rain.MaxSeconds);
            var stop = g.TickWorld(D.Elements.Rain.MaxSeconds + 1, 0.99999);
            Assert.False(w.IsRaining);
            Assert.Contains(stop, e => e.Kind == WorldEventKind.RainStopped);
        }

        [Fact]
        public void Rain_does_not_restart_while_it_is_raining_and_can_be_scripted()
        {
            var g = G();
            g.Nature.Weather.StartRain(100);
            var ev = g.TickWorld(1, 0.0);
            Assert.DoesNotContain(ev, e => e.Kind == WorldEventKind.RainStarted);
            g.Nature.Weather.StopRain();
            Assert.False(g.Nature.Weather.IsRaining);
        }

        [Fact]
        public void The_rain_length_follows_the_roll()
        {
            var a = G(); a.TickWorld(1, 0.0);
            var b = G(); b.TickWorld(1, D.Elements.Rain.ChancePerSecond * 0.99);
            Assert.True(a.Nature.Weather.IsRaining && b.Nature.Weather.IsRaining);
            Assert.True(a.Nature.Weather.RainLeft < b.Nature.Weather.RainLeft);
        }

        // --- grass and fire ---

        [Fact]
        public void Fire_runs_along_dry_grass_burns_out_and_does_not_burn_twice()
        {
            var g = G();
            g.Nature.Wind.Set(new Vec2(1, 0), 0f);
            Row(g, 6);
            Assert.True(g.Nature.Grass.Ignite("g0"));
            Assert.Equal(GrassState.Burning, g.Nature.Grass.StateOf("g0"));
            Run(g, 150);
            for (int i = 0; i < 6; i++) Assert.Equal(GrassState.Burnt, g.Nature.Grass.StateOf($"g{i}"));
            Assert.False(g.Nature.Grass.Ignite("g0"));
        }

        [Fact]
        public void A_gap_wider_than_the_neighbour_distance_stops_the_fire()
        {
            var g = G();
            g.Nature.Wind.Set(new Vec2(1, 0), 1f);
            g.Nature.Grass.AddCell("a", new Vec2(0, 0));
            g.Nature.Grass.AddCell("b", new Vec2(D.Elements.Grass.NeighborDistance + 0.5f, 0));
            g.Nature.Grass.Ignite("a");
            Run(g, 60);
            Assert.Equal(GrassState.Burnt, g.Nature.Grass.StateOf("a"));
            Assert.Equal(GrassState.Dry, g.Nature.Grass.StateOf("b"));
        }

        [Fact]
        public void The_wind_pushes_fire_downwind_faster_than_upwind()
        {
            var g = G();
            g.Nature.Wind.Set(new Vec2(1, 0), 1f);
            g.Nature.Grass.AddCell("west", new Vec2(-1, 0));
            g.Nature.Grass.AddCell("mid", new Vec2(0, 0));
            g.Nature.Grass.AddCell("east", new Vec2(1, 0));
            float down = g.Nature.Grass.SpreadChancePerSecond("mid", "east");
            float up = g.Nature.Grass.SpreadChancePerSecond("mid", "west");
            Assert.True(down > up * 2);
            g.Nature.Wind.Set(new Vec2(1, 0), 0f);
            Assert.Equal(D.Elements.Grass.SpreadPerSecond, g.Nature.Grass.SpreadChancePerSecond("mid", "east"), 4);
            g.Nature.Wind.Set(new Vec2(-1, 0), 1f);
            Assert.True(g.Nature.Grass.SpreadChancePerSecond("mid", "west") > g.Nature.Grass.SpreadChancePerSecond("mid", "east"));
            // never below the floor: fire still creeps upwind
            Assert.True(g.Nature.Grass.SpreadChancePerSecond("mid", "east") >= D.Elements.Grass.SpreadPerSecond * D.Elements.Grass.MinSpreadFactor - 1e-6f);
        }

        [Fact]
        public void In_a_strong_wind_the_fire_front_reaches_the_far_end_sooner_downwind()
        {
            float TimeToBurn(Vec2 wind)
            {
                var g = G();
                g.Nature.Wind.Set(wind, 1f);
                for (int i = 0; i < 9; i++) g.Nature.Grass.AddCell($"g{i}", new Vec2(i, 0));
                g.Nature.Grass.Ignite("g4");
                float t = 0;
                for (int i = 0; i < 4000; i++)
                {
                    g.TickWorld(0.25f, Roll(i));
                    t += 0.25f;
                    if (g.Nature.Grass.StateOf("g8") != GrassState.Dry) return t;
                }
                return float.MaxValue;
            }
            Assert.True(TimeToBurn(new Vec2(1, 0)) < TimeToBurn(new Vec2(-1, 0)));
        }

        [Fact]
        public void Long_rain_makes_grass_wet_wet_grass_does_not_burn()
        {
            var g = G();
            Row(g, 4);
            g.Nature.Weather.StartRain(300);
            var ev = new System.Collections.Generic.List<WorldEvent>();
            for (int i = 0; i < (int)(D.Elements.Rain.WetAfterSeconds + 2); i++) ev.AddRange(g.TickWorld(1, Roll(i)));
            Assert.Equal(GrassState.Wet, g.Nature.Grass.StateOf("g1"));
            Assert.Contains(ev, e => e.Kind == WorldEventKind.GrassWetted);
            Assert.False(g.Nature.Grass.Ignite("g1"));
            Assert.Equal(GrassState.Wet, g.Nature.Grass.StateOf("g1"));
        }

        [Fact]
        public void A_long_rain_puts_a_burning_cell_out()
        {
            var g = G();
            g.Nature.Grass.AddCell("lone", new Vec2(0, 0));
            g.Nature.Weather.StartRain(300);
            var ev = new System.Collections.Generic.List<WorldEvent>();
            for (int i = 0; i < (int)D.Elements.Rain.WetAfterSeconds - 1; i++) ev.AddRange(g.TickWorld(1, Roll(i)));
            Assert.True(g.Nature.Grass.Ignite("lone"));     // lit just before the grass would be soaked
            for (int i = 0; i < 3; i++) ev.AddRange(g.TickWorld(1, Roll(i)));
            Assert.Equal(GrassState.Burnt, g.Nature.Grass.StateOf("lone"));
            Assert.Contains(ev, e => e.Kind == WorldEventKind.GrassExtinguished);
        }

        [Fact]
        public void Grass_dries_again_some_time_after_the_rain()
        {
            var g = G();
            Row(g, 2);
            g.Nature.Weather.StartRain(30);
            for (int i = 0; i < 40; i++) g.TickWorld(1, 0.99);
            Assert.Equal(GrassState.Wet, g.Nature.Grass.StateOf("g0"));
            Assert.False(g.Nature.Weather.IsRaining);
            for (int i = 0; i < (int)D.Elements.Rain.DryAfterSeconds - 15; i++) g.TickWorld(1, 0.99);   // rain stopped ~10 s before this loop
            Assert.Equal(GrassState.Wet, g.Nature.Grass.StateOf("g0"));
            for (int i = 0; i < 10; i++) g.TickWorld(1, 0.99);
            Assert.Equal(GrassState.Dry, g.Nature.Grass.StateOf("g0"));
        }

        [Fact]
        public void A_short_shower_wets_nothing()
        {
            var g = G();
            Row(g, 2);
            g.Nature.Weather.StartRain(D.Elements.Rain.WetAfterSeconds / 2);
            for (int i = 0; i < 60; i++) g.TickWorld(1, 0.99);
            Assert.Equal(GrassState.Dry, g.Nature.Grass.StateOf("g0"));
        }

        [Fact]
        public void A_campfire_can_set_the_dry_grass_around_it_alight_but_not_wet_grass()
        {
            var dry = G();
            dry.Bag.Add("firewood"); dry.Bag.Add("flint");
            dry.Camp.Use(dry.Camp.Place("firewood", new Vec2(0, 0)).Object!.Id, "flint");
            dry.Nature.Grass.AddCell("near", new Vec2(1, 0));
            dry.Nature.Grass.AddCell("far", new Vec2(D.Elements.Grass.CampfireSparkRadius + 3, 0));
            Run(dry, 200);
            Assert.NotEqual(GrassState.Dry, dry.Nature.Grass.StateOf("near"));
            Assert.Equal(GrassState.Dry, dry.Nature.Grass.StateOf("far"));

            var wet = G();
            wet.Bag.Add("firewood"); wet.Bag.Add("flint");
            wet.Camp.Use(wet.Camp.Place("firewood", new Vec2(0, 0)).Object!.Id, "flint");
            wet.Nature.Grass.AddCell("near", new Vec2(1, 0));
            wet.Nature.Grass.Wet();
            Run(wet, 80);                                   // wet cells stay wet for 90 s after the rain, and wet grass never catches
            Assert.Equal(GrassState.Wet, wet.Nature.Grass.StateOf("near"));
        }

        [Fact]
        public void A_torch_lights_grass_a_bare_hand_does_not()
        {
            var g = G();
            Row(g, 2);
            Assert.False(g.IgniteGrass("g0"));
            g.Bag.Add("torch");
            Assert.True(g.IgniteGrass("g0"));
            Assert.Equal(GrassState.Burning, g.Nature.Grass.StateOf("g0"));
            Assert.False(g.IgniteGrass("nowhere"));
        }

        [Fact]
        public void A_torch_can_be_lit_from_burning_grass()
        {
            var g = G();
            Row(g, 2);
            g.Bag.Add("torch"); g.Bag.Add("torch_unlit");
            g.IgniteGrass("g0");
            Assert.Equal(UseOutcome.Done, g.UseOnWorld("g0", "torch_unlit").Outcome);
            Assert.Equal(2, g.Bag.Count("torch"));
            Assert.Equal(UseOutcome.NoTarget, g.UseOnWorld("g1", "torch_unlit").Outcome);
        }

        [Fact]
        public void Standing_in_a_burning_cell_burns_the_hero_but_not_every_frame()
        {
            var g = G();
            Row(g, 1);
            g.Nature.Grass.Ignite("g0");
            g.HeroPosition = new Vec2(0.3f, 0);
            var ev = g.TickWorld(0.5f, 0.99);
            Assert.Contains(ev, e => e.Kind == WorldEventKind.HeroScorched);
            Assert.True(g.Condition.Severity(WoundType.Burn) >= D.Elements.Grass.ScorchSeverity - 1e-4f);
            var again = g.TickWorld(0.5f, 0.99);
            Assert.DoesNotContain(again, e => e.Kind == WorldEventKind.HeroScorched);
            var far = G();
            Row(far, 1);
            far.Nature.Grass.Ignite("g0");
            far.HeroPosition = new Vec2(D.Elements.Grass.BurnRadius + 1, 0);
            far.TickWorld(0.5f, 0.99);
            Assert.Equal(0f, far.Condition.Severity(WoundType.Burn));
        }

        // --- mud ---

        [Fact]
        public void Rain_makes_mud_in_mud_zones_which_slows_walking_and_dries_after()
        {
            var g = G();
            g.Nature.Mud.AddZone("path", new Vec2(0, 0), 5);
            g.HeroPosition = new Vec2(1, 1);
            Assert.Equal(1f, g.Nature.Mud.SpeedAt(g.HeroPosition), 4);
            g.Nature.Weather.StartRain(D.Elements.Mud.RiseSeconds * 3);
            for (int i = 0; i < (int)D.Elements.Mud.RiseSeconds; i++) g.TickWorld(1, 0.99);
            Assert.Equal(1f, g.Nature.Mud.Level("path"), 2);
            Assert.Equal(D.Movement.Terrain["mud"], g.Nature.Mud.SpeedAt(g.HeroPosition), 3);
            Assert.Contains(g.SpeedModifiers(), m => System.Math.Abs(m - D.Movement.Terrain["mud"]) < 1e-3f);
            Assert.Equal(1f, g.Nature.Mud.SpeedAt(new Vec2(50, 50)), 4);   // outside the zone
            g.Nature.Weather.StopRain();
            for (int i = 0; i < (int)D.Elements.Mud.DryingSeconds / 2; i++) g.TickWorld(1, 0.99);
            Assert.InRange(g.Nature.Mud.Level("path"), 0.3f, 0.7f);
            for (int i = 0; i < (int)D.Elements.Mud.DryingSeconds; i++) g.TickWorld(1, 0.99);
            Assert.Equal(0f, g.Nature.Mud.Level("path"), 3);
        }

        // --- wind ---

        [Fact]
        public void The_wind_changes_now_and_then_by_roll_and_stays_within_limits()
        {
            var g = G();
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                var ev = g.TickWorld(D.Elements.Wind.ChangeEverySeconds, Roll(i));
                Assert.InRange(g.Nature.Wind.Strength, D.Elements.Wind.MinStrength - 1e-4f, D.Elements.Wind.MaxStrength + 1e-4f);
                Assert.Equal(1f, g.Nature.Wind.Direction.Length, 3);
                seen.Add(g.Nature.Wind.Direction.ToString());
                Assert.Contains(ev, e => e.Kind == WorldEventKind.WindChanged);
            }
            Assert.True(seen.Count > 5);
        }

        // --- save ---

        [Fact]
        public void Elements_round_trip_and_cells_registered_later_get_their_saved_state()
        {
            var g = G();
            Row(g, 5);
            g.Nature.Mud.AddZone("path", new Vec2(0, 0), 5);
            g.Nature.Grass.Ignite("g1");
            g.Nature.Weather.StartRain(100);
            Run(g, 30);
            g.Nature.Wind.Set(new Vec2(0, 1), 0.7f);
            string a = SaveGame.Capture(g);

            var fresh = G();
            SaveGame.Restore(fresh, a);
            Row(fresh, 5);                                       // the view registers the scene's cells after the load
            fresh.Nature.Mud.AddZone("path", new Vec2(0, 0), 5);
            Assert.Equal(a, SaveGame.Capture(fresh));
            for (int i = 0; i < 5; i++) Assert.Equal(g.Nature.Grass.StateOf($"g{i}"), fresh.Nature.Grass.StateOf($"g{i}"));
            Assert.True(fresh.Nature.Weather.IsRaining);
            Assert.Equal(0.7f, fresh.Nature.Wind.Strength, 3);
            Assert.Equal(g.Nature.Mud.Level("path"), fresh.Nature.Mud.Level("path"), 3);
        }

        [Fact]
        public void Same_inputs_same_world()
        {
            string Play()
            {
                var g = G();
                Row(g, 8);
                g.Nature.Grass.Ignite("g3");
                g.Nature.Weather.StartRain(40);
                Run(g, 120);
                return SaveGame.Capture(g);
            }
            Assert.Equal(Play(), Play());
        }

        [Fact]
        public void Elements_data_is_checked()
        {
            var files = System.IO.Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => System.IO.Path.GetFileName(f)!, System.IO.File.ReadAllText);
            files["elements.json"] = files["elements.json"].Replace("\"burnSeconds\": 12", "\"burnSeconds\": 0");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("elements.json") && p.Contains("burnSeconds"));
        }
    }
}
