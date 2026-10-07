#nullable enable
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Input
{
    /// <summary>Thresholds from data/input.json (project-design.md §1). Pixels are at <see cref="ReferenceDpi"/>.</summary>
    public sealed class GestureSettings
    {
        public double LongPressSeconds { get; set; }
        public double TapMaxSeconds { get; set; }
        public float MoveThresholdPx { get; set; }
        public float ReferenceDpi { get; set; }
        public float SwipeFullStrengthPx { get; set; }
    }

    public enum GestureKind { SwipeStarted, SwipeUpdated, SwipeEnded, Tap, LongPressOnHero, LongPressReleased }

    public readonly struct GestureEvent
    {
        public readonly GestureKind Kind;
        public readonly double Time;
        public readonly Vec2 Position;
        /// <summary>Swipe: unit vector from the touch start to the finger, screen axes (y up as given).</summary>
        public readonly Vec2 Direction;
        /// <summary>Swipe: 0..1, finger distance relative to the full-strength distance.</summary>
        public readonly float Strength;
        public readonly string? TargetId;

        public GestureEvent(GestureKind kind, double time, Vec2 position, Vec2 direction = default, float strength = 0f, string? targetId = null)
        {
            Kind = kind; Time = time; Position = position; Direction = direction; Strength = strength; TargetId = targetId;
        }

        public override string ToString() => $"{Kind} t={Time:0.###} dir={Direction} s={Strength:0.##} target={TargetId}";
    }
}
