#nullable enable
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Input
{
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public enum TouchHitKind { None, Ground, Hero, Object }

    /// <summary>What the touch landed on when it began — resolved by the view (screen projection of colliders, §1).</summary>
    public readonly struct TouchHit
    {
        public readonly TouchHitKind Kind;
        public readonly string? TargetId;
        /// <summary>The object acts on the touch itself, not on the release (an enemy: the blow goes at once, D-26). Taps on everything else wait for the release.</summary>
        public readonly bool OnPress;

        TouchHit(TouchHitKind kind, string? targetId, bool onPress = false) { Kind = kind; TargetId = targetId; OnPress = onPress; }

        public static TouchHit Ground => new TouchHit(TouchHitKind.Ground, null);
        public static TouchHit Hero => new TouchHit(TouchHitKind.Hero, null);
        public static TouchHit Object(string id, bool onPress = false) => new TouchHit(TouchHitKind.Object, id, onPress);
    }

    /// <summary>One touch sample in screen pixels; time in seconds from any fixed origin.</summary>
    public readonly struct TouchSample
    {
        public readonly int FingerId;
        public readonly TouchPhase Phase;
        public readonly double Time;
        public readonly Vec2 Position;
        public readonly TouchHit Hit;

        public TouchSample(int fingerId, TouchPhase phase, double time, Vec2 position, TouchHit hit)
        {
            FingerId = fingerId; Phase = phase; Time = time; Position = position; Hit = hit;
        }
    }
}
