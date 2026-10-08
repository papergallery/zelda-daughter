using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-02: project-design.md §1 «Диспетчер жестов».</summary>
    public class GestureRecognizerTests
    {
        static readonly GestureSettings S = TestData.Load<GestureSettings>("input.json");
        const float Dpi = 160f; // reference density: thresholds in px as written in data

        static GestureRecognizer New() => new GestureRecognizer(S, Dpi);

        static List<GestureEvent> Run(GestureRecognizer r, params TouchSample[] samples)
        {
            var all = new List<GestureEvent>();
            foreach (var s in samples) all.AddRange(r.Feed(s));
            return all;
        }

        static TouchSample Begin(double t, float x, float y, TouchHit hit = default) => new TouchSample(0, TouchPhase.Began, t, new Vec2(x, y), hit);
        static TouchSample Move(double t, float x, float y) => new TouchSample(0, TouchPhase.Moved, t, new Vec2(x, y), default);
        static TouchSample End(double t, float x, float y) => new TouchSample(0, TouchPhase.Ended, t, new Vec2(x, y), default);

        [Fact]
        public void Data_matches_design_thresholds()
        {
            Assert.Equal(0.5, S.LongPressSeconds, 3);
            Assert.Equal(0.5, S.TapMaxSeconds, 3);
            Assert.Equal(20f, S.MoveThresholdPx);
        }

        [Fact]
        public void Long_press_on_hero_fires_after_hold_without_moving()
        {
            var r = New();
            var ev = Run(r, Begin(0, 100, 100, TouchHit.Hero), Move(0.3, 105, 102));
            Assert.Empty(ev);
            ev = r.Tick(0.49).ToList();
            Assert.Empty(ev);
            ev = r.Tick(0.5).ToList();
            Assert.Single(ev, e => e.Kind == GestureKind.LongPressOnHero);
        }

        [Fact]
        public void Long_press_needs_touch_to_start_on_hero()
        {
            var r = New();
            Run(r, Begin(0, 100, 100, TouchHit.Ground));
            Assert.DoesNotContain(r.Tick(1.0), e => e.Kind == GestureKind.LongPressOnHero);
        }

        [Fact]
        public void Started_on_hero_but_moved_is_a_swipe_not_a_long_press()
        {
            var r = New();
            var ev = Run(r, Begin(0, 100, 100, TouchHit.Hero), Move(0.1, 100, 125));
            Assert.Contains(ev, e => e.Kind == GestureKind.SwipeStarted);
            Assert.DoesNotContain(r.Tick(1.0), e => e.Kind == GestureKind.LongPressOnHero);
        }

        [Fact]
        public void Movement_at_threshold_is_not_yet_a_swipe()
        {
            var r = New();
            var ev = Run(r, Begin(0, 0, 0, TouchHit.Hero), Move(0.1, 20, 0));
            Assert.DoesNotContain(ev, e => e.Kind == GestureKind.SwipeStarted);
            Assert.Contains(r.Tick(0.5), e => e.Kind == GestureKind.LongPressOnHero);
        }

        [Fact]
        public void Swipe_reports_direction_from_touch_start_and_strength()
        {
            var r = New();
            var ev = Run(r, Begin(0, 0, 0), Move(0.05, 30, 0), Move(0.1, 0, 60), End(0.2, 0, 60));
            var kinds = ev.Select(e => e.Kind).ToArray();
            Assert.Equal(new[] { GestureKind.SwipeStarted, GestureKind.SwipeUpdated, GestureKind.SwipeUpdated, GestureKind.SwipeEnded }, kinds);
            var last = ev.Last(e => e.Kind == GestureKind.SwipeUpdated);
            Assert.Equal(0f, last.Direction.X, 3);
            Assert.Equal(1f, last.Direction.Y, 3);
            Assert.Equal(60f / S.SwipeFullStrengthPx, last.Strength, 3);
        }

        [Fact]
        public void Swipe_strength_is_capped_at_one()
        {
            var r = New();
            var ev = Run(r, Begin(0, 0, 0), Move(0.1, 1000, 0));
            Assert.Equal(1f, ev.Last(e => e.Kind == GestureKind.SwipeUpdated).Strength, 3);
        }

        [Fact]
        public void Short_touch_on_object_is_a_tap_with_its_target()
        {
            var r = New();
            var ev = Run(r, Begin(0, 50, 50, TouchHit.Object("npc_peasant")), End(0.2, 52, 51));
            var tap = Assert.Single(ev);
            Assert.Equal(GestureKind.Tap, tap.Kind);
            Assert.Equal("npc_peasant", tap.TargetId);
        }

        [Fact]
        public void Long_touch_on_object_is_not_a_tap()
        {
            var r = New();
            Assert.Empty(Run(r, Begin(0, 50, 50, TouchHit.Object("npc_peasant")), End(0.5, 50, 50)));
        }

        [Fact]
        public void Short_touch_on_ground_or_hero_does_nothing()
        {
            Assert.Empty(Run(New(), Begin(0, 50, 50, TouchHit.Ground), End(0.1, 50, 50)));
            Assert.Empty(Run(New(), Begin(0, 50, 50, TouchHit.Hero), End(0.1, 50, 50)));
        }

        [Fact]
        public void Moved_touch_on_object_is_a_swipe_not_a_tap()
        {
            var ev = Run(New(), Begin(0, 0, 0, TouchHit.Object("boar")), Move(0.1, 40, 0), End(0.2, 40, 0));
            Assert.DoesNotContain(ev, e => e.Kind == GestureKind.Tap);
            Assert.Contains(ev, e => e.Kind == GestureKind.SwipeEnded);
        }

        [Fact]
        public void After_long_press_finger_movement_is_menu_drag_not_swipe()
        {
            var r = New();
            Run(r, Begin(0, 100, 100, TouchHit.Hero));
            r.Tick(0.6);
            var ev = Run(r, Move(0.7, 200, 100), End(0.8, 200, 100));
            Assert.DoesNotContain(ev, e => e.Kind == GestureKind.SwipeStarted);
            var rel = Assert.Single(ev, e => e.Kind == GestureKind.LongPressReleased);
            Assert.Equal(200f, rel.Position.X);
        }

        [Fact]
        public void Threshold_scales_with_screen_density()
        {
            // 20 px at 160 dpi is ~3.2 mm; on a 480 dpi phone the same finger travel is 60 px.
            var r = new GestureRecognizer(S, 480f);
            var ev = Run(r, Begin(0, 0, 0, TouchHit.Hero), Move(0.1, 50, 0));
            Assert.DoesNotContain(ev, e => e.Kind == GestureKind.SwipeStarted);
            ev = Run(r, Move(0.2, 61, 0));
            Assert.Contains(ev, e => e.Kind == GestureKind.SwipeStarted);
        }

        [Fact]
        public void Second_finger_is_ignored_while_one_is_down()
        {
            var r = New();
            Run(r, Begin(0, 0, 0));
            var ev = r.Feed(new TouchSample(1, TouchPhase.Began, 0.05, new Vec2(300, 300), TouchHit.Object("x"))).ToList();
            ev.AddRange(r.Feed(new TouchSample(1, TouchPhase.Ended, 0.1, new Vec2(300, 300), default)));
            Assert.Empty(ev);
        }

        [Fact]
        public void Hero_hit_is_by_screen_projection_with_density_scaled_tolerance()
        {
            var hero = new Vec2(500, 900);
            Assert.True(S.IsOnHero(new Vec2(500 + S.HeroTouchRadiusPx, 900), hero, 160));
            Assert.False(S.IsOnHero(new Vec2(500 + S.HeroTouchRadiusPx + 1, 900), hero, 160));
            Assert.True(S.IsOnHero(new Vec2(500 + S.HeroTouchRadiusPx * 2, 900), hero, 320));
        }

        [Fact]
        public void Cancelled_swipe_ends_the_swipe()
        {
            var r = New();
            Run(r, Begin(0, 0, 0), Move(0.1, 40, 0));
            var ev = r.Feed(new TouchSample(0, TouchPhase.Canceled, 0.2, new Vec2(40, 0), default)).ToList();
            Assert.Single(ev, e => e.Kind == GestureKind.SwipeEnded);
        }

        [Fact]
        public void Idle_tick_allocates_nothing()
        {
            var r = New();
            Assert.Same(r.Tick(1.0), r.Tick(2.0));
            Assert.Empty(r.Tick(3.0));
        }
    }
}
