using System;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Movement;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-03: swipe → ground direction and speed with modifiers.</summary>
    public class MovementTests
    {
        static readonly MovementSettings S = TestData.Load<MovementSettings>("movement.json");

        [Fact]
        public void Screen_up_is_camera_forward_on_the_ground()
        {
            // Camera yawed 45°: screen up goes along world (+x, +z) diagonal.
            var basis = CameraBasis.FromYawDegrees(45);
            var dir = basis.ToGround(new Vec2(0, 1));
            Assert.Equal(Math.Sqrt(0.5), dir.X, 3);
            Assert.Equal(Math.Sqrt(0.5), dir.Y, 3);
            var right = basis.ToGround(new Vec2(1, 0));
            Assert.Equal(Math.Sqrt(0.5), right.X, 3);
            Assert.Equal(-Math.Sqrt(0.5), right.Y, 3);
        }

        [Fact]
        public void Zero_yaw_keeps_axes()
        {
            var d = CameraBasis.FromYawDegrees(0).ToGround(new Vec2(1, 0));
            Assert.Equal(1f, d.X, 4);
            Assert.Equal(0f, d.Y, 4);
        }

        [Fact]
        public void Gentle_swipe_walks_full_swipe_runs_ramp_between()
        {
            var m = new SpeedModel(S);
            Assert.Equal(S.WalkSpeed, m.BaseSpeed(0.3f), 3);
            Assert.Equal(S.WalkSpeed, m.BaseSpeed(S.RunStartStrength), 3);
            Assert.Equal(S.RunSpeed, m.BaseSpeed(1f), 3);
            float mid = m.BaseSpeed((S.RunStartStrength + 1f) / 2f);
            Assert.Equal((S.WalkSpeed + S.RunSpeed) / 2f, mid, 3);
        }

        [Fact]
        public void Water_slows_by_terrain_multiplier()
        {
            var m = new SpeedModel(S);
            float speed = m.Speed(1f, "water", Array.Empty<float>());
            Assert.Equal(S.RunSpeed * 0.4f, speed, 3);
        }

        [Fact]
        public void Modifiers_multiply_and_are_clamped_each()
        {
            var m = new SpeedModel(S);
            Assert.Equal(S.WalkSpeed * 0.5f * 0.8f, m.Speed(0.5f, "ground", new[] { 0.5f, 0.8f }), 3);
            Assert.Equal(S.WalkSpeed * S.MultiplierMax, m.Speed(0.5f, "ground", new[] { 10f }), 3);
        }

        [Fact]
        public void Total_never_drops_below_floor_so_the_hero_never_stands()
        {
            // April: fracture 0.5 × hunger 0.7 × mud 0.6 × overload 0.5 ≈ 0.1 — the hero barely moved.
            var m = new SpeedModel(S);
            float speed = m.Speed(0.5f, "mud", new[] { 0.5f, 0.7f, 0.5f });
            Assert.Equal(S.WalkSpeed * S.MinTotalMultiplier, speed, 3);
        }

        [Fact]
        public void Unknown_terrain_is_an_error_not_a_silent_one()
        {
            var m = new SpeedModel(S);
            Assert.Throws<ArgumentException>(() => m.Speed(0.5f, "lava", Array.Empty<float>()));
        }

        [Fact]
        public void Step_moves_along_direction_by_speed_and_dt()
        {
            var m = new SpeedModel(S);
            var intent = new MoveIntent(new Vec2(1, 0), 1f);
            var p = m.Step(Vec2.Zero, intent, "ground", Array.Empty<float>(), 0.5f);
            Assert.Equal(S.RunSpeed * 0.5f, p.X, 3);
            Assert.Equal(0f, p.Y, 3);
            Assert.Equal(Vec2.Zero, m.Step(Vec2.Zero, MoveIntent.None, "ground", Array.Empty<float>(), 0.5f));
        }
    }
}
