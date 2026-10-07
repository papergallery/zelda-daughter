#nullable enable
using System;

namespace ZeldaDaughter.Core.Common
{
    /// <summary>A 2D vector without UnityEngine (screen pixels or ground-plane metres, by context).</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Vec2(X / len, Y / len) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Y * k);

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");
    }
}
