#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Movement
{
    /// <summary>data/movement.json (C-03).</summary>
    public sealed class MovementSettings
    {
        public float WalkSpeed { get; set; }
        public float RunSpeed { get; set; }
        /// <summary>Swipe strength from which speed ramps from walk to run (reaches run at 1).</summary>
        public float RunStartStrength { get; set; }
        public float MultiplierMin { get; set; }
        public float MultiplierMax { get; set; }
        /// <summary>Floor of the product of all multipliers: a wounded, hungry, loaded hero in mud still moves.</summary>
        public float MinTotalMultiplier { get; set; }
        public Dictionary<string, float> Terrain { get; set; } = new Dictionary<string, float>();
    }

    /// <summary>Where the hero wants to go: unit ground direction (x, z) and swipe strength 0..1.</summary>
    public readonly struct MoveIntent
    {
        public readonly Vec2 Direction;
        public readonly float Strength;

        public MoveIntent(Vec2 direction, float strength) { Direction = direction.Normalized; Strength = Math.Max(0f, Math.Min(1f, strength)); }

        public static readonly MoveIntent None = new MoveIntent(Vec2.Zero, 0f);
        public bool IsMoving => Strength > 0f && Direction.Length > 0f;
    }

    /// <summary>Maps screen swipe axes to the ground plane (x, z) for a camera turned by yaw around the vertical.</summary>
    public readonly struct CameraBasis
    {
        readonly Vec2 _forward;
        readonly Vec2 _right;

        CameraBasis(Vec2 forward, Vec2 right) { _forward = forward; _right = right; }

        public static CameraBasis FromYawDegrees(double yawDegrees)
        {
            double a = yawDegrees * Math.PI / 180.0;
            var forward = new Vec2((float)Math.Sin(a), (float)Math.Cos(a));
            var right = new Vec2((float)Math.Cos(a), (float)-Math.Sin(a));
            return new CameraBasis(forward, right);
        }

        /// <summary>Screen vector (x right, y up) → ground direction (x, z), normalised.</summary>
        public Vec2 ToGround(Vec2 screen) => (_right * screen.X + _forward * screen.Y).Normalized;

        public MoveIntent Intent(Vec2 swipeDirection, float strength) => new MoveIntent(ToGround(swipeDirection), strength);
    }

    /// <summary>Speed from swipe strength, terrain and the multipliers other systems report (wounds, hunger, load).</summary>
    public sealed class SpeedModel
    {
        readonly MovementSettings _s;

        public SpeedModel(MovementSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public float BaseSpeed(float strength)
        {
            if (strength <= _s.RunStartStrength) return _s.WalkSpeed;
            float t = (strength - _s.RunStartStrength) / Math.Max(1f - _s.RunStartStrength, 1e-6f);
            return _s.WalkSpeed + (_s.RunSpeed - _s.WalkSpeed) * Math.Min(t, 1f);
        }

        public float Multiplier(string terrain, IEnumerable<float> modifiers)
        {
            if (!_s.Terrain.TryGetValue(terrain, out float total))
                throw new ArgumentException($"Unknown terrain '{terrain}' — add it to data/movement.json", nameof(terrain));
            foreach (float m in modifiers)
                total *= Math.Max(_s.MultiplierMin, Math.Min(_s.MultiplierMax, m));
            return Math.Max(total, _s.MinTotalMultiplier);
        }

        /// <summary>Terrain × one already combined (and clamped) factor — <c>GameState.SpeedMultiplier</c>; no enumerator.</summary>
        public float Multiplier(string terrain, float modifier)
        {
            if (!_s.Terrain.TryGetValue(terrain, out float total))
                throw new ArgumentException($"Unknown terrain '{terrain}' — add it to data/movement.json", nameof(terrain));
            return Math.Max(total * modifier, _s.MinTotalMultiplier);
        }

        public float Speed(float strength, string terrain, float modifier) => BaseSpeed(strength) * Multiplier(terrain, modifier);

        public Vec2 Step(Vec2 position, MoveIntent intent, string terrain, float modifier, float dt)
        {
            if (!intent.IsMoving || dt <= 0f) return position;
            return position + intent.Direction * (Speed(intent.Strength, terrain, modifier) * dt);
        }

        public float Speed(float strength, string terrain, IEnumerable<float> modifiers) => BaseSpeed(strength) * Multiplier(terrain, modifiers);

        /// <summary>New ground position after dt seconds.</summary>
        public Vec2 Step(Vec2 position, MoveIntent intent, string terrain, IEnumerable<float> modifiers, float dt)
        {
            if (!intent.IsMoving || dt <= 0f) return position;
            return position + intent.Direction * (Speed(intent.Strength, terrain, modifiers) * dt);
        }
    }
}
