using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C2: the enemies of the game as the core keeps them — spawn, tick, the hero's blow with the weapon in hand, death, night wolves.</summary>
    public class EnemyRosterTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState G()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            g.HeroPosition = new Vec2(0, 0);
            g.Combat.Position = new Vec2(0, 0);
            return g;
        }

        [Fact]
        public void Spawn_registers_the_enemy_and_a_second_spawn_with_the_same_id_returns_it()
        {
            var g = G();
            var boar = g.Enemies.Spawn("boar_1", "boar", new Vec2(5, 0));
            Assert.NotNull(boar);
            Assert.Same(boar, g.Enemies.Get("boar_1"));
            Assert.Single(g.Enemies.Active);
            Assert.Same(boar, g.Enemies.Spawn("boar_1", "boar", new Vec2(9, 9)));
            Assert.Single(g.Enemies.Active);
            Assert.Null(g.Enemies.Get("nobody"));
        }

        [Fact]
        public void Spawn_of_a_killed_enemy_returns_null()
        {
            var g = G();
            g.Killed.Add("boar_1");
            Assert.Null(g.Enemies.Spawn("boar_1", "boar", new Vec2(5, 0)));
            Assert.Empty(g.Enemies.Active);
        }

        [Fact]
        public void Remove_takes_the_enemy_out_without_a_kill()
        {
            var g = G();
            g.Enemies.Spawn("boar_1", "boar", new Vec2(5, 0));
            Assert.True(g.Enemies.Remove("boar_1"));
            Assert.False(g.Enemies.Remove("boar_1"));
            Assert.Empty(g.Enemies.Active);
            Assert.DoesNotContain("boar_1", g.Killed);
        }

        [Fact]
        public void Tick_reports_what_the_enemy_does_with_its_id()
        {
            var g = G();
            var wolf = g.Enemies.Spawn("wolf_1", "wolf", new Vec2(3, 0))!;
            var notices = new List<EnemyNotice>();
            g.Enemies.Tick(0.05f, 0.3, notices);
            Assert.Contains(notices, n => n.EnemyId == "wolf_1" && n.Event.Kind == EnemyEventKind.Alerted);
            Assert.Equal(EnemyState.Alert, wolf.State);
        }

        [Fact]
        public void Tick_clears_the_list_it_is_given()
        {
            var g = G();
            g.Enemies.Spawn("wolf_1", "wolf", new Vec2(3, 0));
            var notices = new List<EnemyNotice>();
            g.Enemies.Tick(0.05f, 0.3, notices);
            Assert.NotEmpty(notices);
            g.Enemies.Tick(0.05f, 0.3, notices);
            Assert.DoesNotContain(notices, n => n.Event.Kind == EnemyEventKind.Alerted);
        }

        [Fact]
        public void The_blow_uses_the_weapon_in_hand()
        {
            var bare = G();
            var armed = G();
            armed.Bag.Add("sword");
            foreach (var g in new[] { bare, armed }) g.Enemies.Spawn("b", "boar", new Vec2(0.8f, 0));
            var a = bare.Enemies.Strike("b", 0.0);
            var b = armed.Enemies.Strike("b", 0.0);
            Assert.Equal(StrikeOutcome.Hit, a.Outcome);
            Assert.Equal(StrikeOutcome.Hit, b.Outcome);
            Assert.True(b.Damage > a.Damage);
            Assert.True(armed.Enemies.Get("b")!.Hp < bare.Enemies.Get("b")!.Hp);
        }

        [Fact]
        public void A_far_enemy_is_out_of_range_and_an_unknown_one_unavailable()
        {
            var g = G();
            g.Enemies.Spawn("b", "boar", new Vec2(8, 0));
            Assert.Equal(StrikeOutcome.OutOfRange, g.Enemies.Strike("b", 0.0).Outcome);
            Assert.Equal(StrikeOutcome.Unavailable, g.Enemies.Strike("ghost", 0.0).Outcome);
        }

        [Fact]
        public void A_killing_blow_remembers_the_kill_and_leaves_a_carcass()
        {
            var g = G();
            g.Bag.Add("sword");
            g.Enemies.Spawn("b", "boar", new Vec2(0.8f, 0));
            StrikeResult last = default;
            for (int i = 0; i < 10 && g.Enemies.Get("b") != null; i++)
            {
                g.Combat.Tick(5f);
                last = g.Enemies.Strike("b", 0.0);
            }
            Assert.True(last.Killed);
            Assert.Contains("b", g.Killed);
            Assert.Null(g.Enemies.Get("b"));
            Assert.Empty(g.Enemies.Active);
            var carcass = g.Carcasses.Get("b");
            Assert.NotNull(carcass);
            Assert.Equal(new Vec2(0.8f, 0), carcass!.Position);
            Assert.Null(g.Enemies.Spawn("b", "boar", new Vec2(0.8f, 0)));
        }

        [Fact]
        public void An_enemy_that_bleeds_out_in_a_tick_is_a_kill_too()
        {
            var g = G();
            var boar = g.Enemies.Spawn("b", "boar", new Vec2(40, 0))!;
            boar.Receive(1f, WoundType.Cut, 1f, 0f);
            var notices = new List<EnemyNotice>();
            for (int i = 0; i < 2000 && g.Enemies.Get("b") != null; i++) g.Enemies.Tick(0.05f, 0.3, notices);
            Assert.Null(g.Enemies.Get("b"));
            Assert.Contains("b", g.Killed);
            Assert.NotNull(g.Carcasses.Get("b"));
        }

        [Fact]
        public void The_view_can_forbid_places_to_walk_on()
        {
            var g = G();
            g.HeroPosition = new Vec2(12, 0);
            g.Combat.Position = new Vec2(12, 0);
            var wolf = g.Enemies.Spawn("w", "wolf", new Vec2(0, 0))!;
            g.Enemies.Blocked = p => p.X > 3f;
            var notices = new List<EnemyNotice>();
            for (int i = 0; i < 600; i++) { g.Enemies.Tick(0.05f, 0.3, notices); Assert.True(wolf.Position.X <= 3f + 1e-4f); }
            Assert.True(wolf.Position.X > 1f);   // it did come as far as the wall
        }

        [Fact]
        public void Enemies_walk_freely_without_a_Blocked_check()
        {
            var g = G();
            g.HeroPosition = new Vec2(12, 0);
            g.Combat.Position = new Vec2(12, 0);
            var wolf = g.Enemies.Spawn("w", "wolf", new Vec2(0, 0))!;
            var notices = new List<EnemyNotice>();
            for (int i = 0; i < 600; i++) g.Enemies.Tick(0.05f, 0.3, notices);
            Assert.True(wolf.Position.X > 3f);
        }

        // --- night wolves ---

        static GameState Night()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 0.95);
            g.HeroPosition = new Vec2(0, 0);
            g.Combat.Position = new Vec2(0, 0);
            return g;
        }

        static string SpawnOne(GameState g)
        {
            for (int i = 0; i < 40; i++)
            {
                var ev = g.TickWorld(D.Night.SpawnIntervalSeconds, (0.3 + i * 0.37) % 1.0);
                foreach (var e in ev) if (e.Kind == WorldEventKind.PredatorSpawned) return e.Id;
            }
            throw new Xunit.Sdk.XunitException("no wolf was called");
        }

        [Fact]
        public void The_world_tick_creates_the_night_wolf_in_the_roster()
        {
            var g = Night();
            string id = SpawnOne(g);
            var wolf = g.Enemies.Get(id);
            Assert.NotNull(wolf);
            Assert.Equal(D.Night.Enemy, wolf!.DefId);
            Assert.Contains(wolf, g.Enemies.Active);
        }

        [Fact]
        public void A_night_wolf_that_walked_off_is_despawned_by_where_it_really_is()
        {
            var g = Night();
            string id = SpawnOne(g);
            var wolf = g.Enemies.Get(id)!;
            // it is near the hero now (the roster knows) — morning must not send it away
            wolf.SetPosition(new Vec2(1, 0));
            g.Clock.SetTime(1, 0.5);
            g.TickWorld(1f, 0.5);
            Assert.NotNull(g.Enemies.Get(id));
            wolf.SetPosition(new Vec2(D.Night.DespawnDistance + 20, 0));
            var ev = g.TickWorld(1f, 0.5);
            Assert.Contains(ev, e => e.Kind == WorldEventKind.PredatorDespawned && e.Id == id);
            Assert.Null(g.Enemies.Get(id));
        }

        [Fact]
        public void Killing_a_night_wolf_frees_its_place()
        {
            var g = Night();
            string id = SpawnOne(g);
            int alive = g.Nature.Predators.AliveCount;
            g.Bag.Add("sword");
            var wolf = g.Enemies.Get(id)!;
            wolf.SetPosition(new Vec2(0.8f, 0));
            for (int i = 0; i < 10 && g.Enemies.Get(id) != null; i++) { g.Combat.Tick(5f); g.Enemies.Strike(id, 0.0); }
            Assert.Null(g.Enemies.Get(id));
            Assert.Equal(alive - 1, g.Nature.Predators.AliveCount);
            Assert.NotNull(g.Carcasses.Get(id));
        }

        [Fact]
        public void A_quiet_enemy_tick_allocates_nothing()
        {
            var g = G();
            g.HeroPosition = new Vec2(500, 0);
            g.Combat.Position = new Vec2(500, 0);
            g.Enemies.Spawn("b1", "boar", new Vec2(0, 0));
            g.Enemies.Spawn("b2", "boar", new Vec2(10, 0));
            var notices = new List<EnemyNotice>();
            for (int i = 0; i < 60; i++) g.Enemies.Tick(0.05f, (i * 0.37) % 1.0, notices);
            long used = 0;
            for (int i = 0; i < 300; i++)
            {
                long b = System.GC.GetAllocatedBytesForCurrentThread();
                g.Enemies.Tick(0.05f, (i * 0.37) % 1.0, notices);
                long a = System.GC.GetAllocatedBytesForCurrentThread();
                if (notices.Count == 0) used += a - b;
            }
            Assert.Equal(0, used);
        }
    }
}
