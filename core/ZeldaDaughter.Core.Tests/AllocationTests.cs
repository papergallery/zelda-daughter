using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C8 (docs/demo/unity-architecture.md §4): an ordinary frame allocates nothing in the core — no GC spikes on a phone.</summary>
    public class AllocationTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        const int Frames = 400;

        /// <summary>Bytes allocated by this thread while running <paramref name="frame"/> after a warm-up.</summary>
        static long Measure(Action<int> frame)
        {
            for (int i = 0; i < 50; i++) frame(i);   // JIT, lazy caches
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Frames; i++) frame(i + 50);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>Bytes allocated in the frames that reported no events (<paramref name="frame"/> returns the event count); frames with events may allocate.</summary>
        static long MeasureQuiet(Func<int, int> frame)
        {
            for (int i = 0; i < 50; i++) frame(i);
            long bytes = 0;
            for (int i = 0; i < Frames; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                int n = frame(i + 50);
                long used = GC.GetAllocatedBytesForCurrentThread() - before;
                if (n == 0) bytes += used;
            }
            return bytes;
        }

        static GameState G()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            g.HeroPosition = new Vec2(-100, -100);
            return g;
        }

        static double Roll(int i) => 0.2 + (i * 0.6180339887) % 0.7;   // far from 0: no rain starts

        [Fact]
        public void Clock_advance_without_events_returns_the_shared_empty_list()
        {
            var g = G();
            var a = g.Clock.Advance(0.01);
            var b = g.Clock.Advance(0.01);
            Assert.Empty(a);
            Assert.Same(a, b);
            Assert.Equal(0, Measure(_ => g.Clock.Advance(0.0001)));
        }

        [Fact]
        public void Clock_advance_into_a_list_collects_the_events()
        {
            var g = G();
            var into = new List<ClockEvent>();
            g.Clock.SetTime(1, 0.69);
            g.Clock.Advance(D.World.DayLengthSeconds * 0.02, into);
            Assert.Contains(into, e => e.Kind == ClockEventKind.PhaseChanged && e.Phase == DayPhase.Dusk);
        }

        [Fact]
        public void Condition_tick_allocates_nothing_when_nothing_happens()
        {
            var g = G();
            var a = g.Condition.Tick(0.016f, RestKind.None);
            Assert.Same(a, g.Condition.Tick(0.016f, RestKind.None));
            Assert.Equal(0, Measure(_ => g.Condition.Tick(0.016f, RestKind.None)));
        }

        [Fact]
        public void Condition_tick_into_a_list_reports_the_revival()
        {
            var g = G();
            g.Condition.Damage(1000);
            Assert.True(g.Condition.IsKnockedOut);
            var into = new List<ConditionEvent>();
            g.Condition.Tick(D.Wounds.KnockoutSeconds + 1, RestKind.None, into);
            Assert.Contains(into, e => e.Kind == ConditionEventKind.Revived);
        }

        [Fact]
        public void Npc_sync_allocates_nothing_between_slot_changes()
        {
            var g = G();
            g.Npcs.Sync();   // first call places everyone
            Assert.Equal(0, Measure(_ => g.Npcs.Sync()));
        }

        [Fact]
        public void World_tick_allocates_nothing_on_a_quiet_frame()
        {
            var g = G();
            for (int i = 0; i < 5; i++) g.Nature.Grass.AddCell($"g{i}", new Vec2(i * 0.5f, 0));
            g.Nature.Mud.AddZone("mud", new Vec2(30, 30), 4);
            g.Nature.Predators.AddZone(D.Night.Zones[0], new Vec2(50, 50));
            g.Camp.Restore(1, null, new[] { new Campfire("fire", new Vec2(5, 5), 5000f, 10f) });
            var first = g.TickWorld(0.25f, 0.5);
            Assert.Empty(first);
            Assert.Equal(0, MeasureQuiet(i => g.TickWorld(0.25f, Roll(i)).Count));
        }

        [Fact]
        public void Burning_grass_ticks_without_allocations_while_nothing_new_catches()
        {
            var g = G();
            g.Nature.Grass.AddCell("a", new Vec2(0, 0));
            g.Nature.Grass.AddCell("far", new Vec2(40, 0));
            g.Nature.Grass.Ignite("a");
            Assert.Equal(0, MeasureQuiet(i => { if (i % 40 == 0) g.Nature.Grass.Ignite("a"); return g.TickWorld(0.01f, Roll(i)).Count; }));
        }

        [Fact]
        public void Enemy_tick_allocates_nothing_while_it_wanders()
        {
            var hero = new HeroCombat(D.Weapons, new Skills(D.Skills), new HeroCondition(D.Wounds)) { Position = new Vec2(500, 0) };
            var boar = new Enemy("b", D.Enemies, "boar", new Vec2(0, 0));
            Assert.Same(boar.Tick(0.05f, hero, 0.3f), boar.Tick(0.05f, hero, 0.3f));
            Assert.Equal(0, Measure(i => boar.Tick(0.05f, hero, (float)Roll(i))));
        }

        [Fact]
        public void Gesture_feed_returns_the_shared_empty_list_when_nothing_happened()
        {
            var r = new GestureRecognizer(TestData.Load<GestureSettings>("input.json"), 160f);
            var idle = r.Feed(new TouchSample(0, TouchPhase.Moved, 0.0, new Vec2(1, 1), default));
            Assert.Empty(idle);
            Assert.Same(idle, r.Feed(new TouchSample(0, TouchPhase.Moved, 0.1, new Vec2(1, 1), default)));
            Assert.Equal(0, Measure(i => r.Tick(i * 0.016)));
            // finger down and held still on the ground: no event, nothing allocated
            r.Feed(new TouchSample(0, TouchPhase.Began, 10.0, new Vec2(100, 100), default));
            Assert.Equal(0, Measure(i => r.Feed(new TouchSample(0, TouchPhase.Stationary, 10.0 + i * 0.0001, new Vec2(100, 100), default))));
        }

        [Fact]
        public void Skills_apply_returns_the_shared_empty_list_when_nothing_grew()
        {
            var s = new Skills(D.Skills);
            var a = s.Apply(SkillEvent.Walked(0.001f, false));
            Assert.Same(a, s.Apply(SkillEvent.Walked(0.001f, false)));
            Assert.Equal(0, Measure(_ => s.Apply(SkillEvent.Walked(0.0001f, false))));
        }

        [Fact]
        public void Speed_multiplier_matches_the_modifiers_and_allocates_nothing()
        {
            var g = G();
            g.Condition.Wound(WoundType.Fracture, 1f);
            float product = 1f;
            foreach (float m in g.SpeedModifiers()) product *= m;
            Assert.Equal(product, g.SpeedMultiplier, 5);
            Assert.True(g.SpeedMultiplier < 1f);
            Assert.Equal(0, Measure(_ => { var x = g.SpeedMultiplier; }));
        }
    }
}
