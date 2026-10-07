#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Input
{
    /// <summary>
    /// One-finger gestures of project-design.md §1:
    /// long press — only if the touch began on the hero, held ≥ LongPressSeconds, moved ≤ the threshold;
    /// any movement past the threshold is a swipe, even if the touch began on the hero;
    /// tap — a touch shorter than TapMaxSeconds that began on an object and did not move.
    /// After a long press the finger drives the radial menu: no swipe, a release event instead.
    /// Feed samples in time order; call <see cref="Tick"/> every frame so a still finger can become a long press.
    /// </summary>
    public sealed class GestureRecognizer
    {
        enum State { Idle, Pending, Swiping, LongPressed }

        readonly GestureSettings _s;
        readonly float _threshold;
        readonly float _fullStrength;

        State _state = State.Idle;
        int _finger;
        double _startTime;
        Vec2 _start;
        Vec2 _last;
        TouchHit _hit;

        /// <param name="screenDpi">Device density; pixel thresholds in data are at the reference density.</param>
        public GestureRecognizer(GestureSettings settings, float screenDpi)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.ReferenceDpi <= 0) throw new ArgumentException("ReferenceDpi must be positive", nameof(settings));
            float scale = screenDpi > 0 ? screenDpi / settings.ReferenceDpi : 1f;
            _threshold = settings.MoveThresholdPx * scale;
            _fullStrength = Math.Max(settings.SwipeFullStrengthPx * scale, 1e-3f);
        }

        public bool IsSwiping => _state == State.Swiping;

        public IReadOnlyList<GestureEvent> Feed(TouchSample t)
        {
            var events = new List<GestureEvent>(2);
            if (_state == State.Idle)
            {
                if (t.Phase == TouchPhase.Began)
                {
                    _state = State.Pending;
                    _finger = t.FingerId;
                    _startTime = t.Time;
                    _start = _last = t.Position;
                    _hit = t.Hit;
                }
                return events;
            }
            if (t.FingerId != _finger) return events; // one finger: others are ignored while one is down

            _last = t.Position;
            switch (t.Phase)
            {
                case TouchPhase.Began: // a new touch on the same id without an end: start over
                    _state = State.Idle;
                    return Feed(t);
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    CheckLongPress(t.Time, events);
                    if (_state == State.Pending && (t.Position - _start).Length > _threshold)
                    {
                        _state = State.Swiping;
                        events.Add(new GestureEvent(GestureKind.SwipeStarted, t.Time, t.Position));
                    }
                    if (_state == State.Swiping) events.Add(SwipeUpdate(t.Time, t.Position));
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.Phase == TouchPhase.Ended) CheckLongPress(t.Time, events);
                    if (_state == State.Swiping)
                        events.Add(new GestureEvent(GestureKind.SwipeEnded, t.Time, t.Position));
                    else if (_state == State.LongPressed)
                        events.Add(new GestureEvent(GestureKind.LongPressReleased, t.Time, t.Position));
                    else if (t.Phase == TouchPhase.Ended && _hit.Kind == TouchHitKind.Object
                             && t.Time - _startTime < _s.TapMaxSeconds && (t.Position - _start).Length <= _threshold)
                        events.Add(new GestureEvent(GestureKind.Tap, t.Time, _start, targetId: _hit.TargetId));
                    _state = State.Idle;
                    break;
            }
            return events;
        }

        /// <summary>Advance time without a new sample (a finger held still produces none on most devices).</summary>
        public IReadOnlyList<GestureEvent> Tick(double time)
        {
            var events = new List<GestureEvent>(1);
            CheckLongPress(time, events);
            return events;
        }

        void CheckLongPress(double time, List<GestureEvent> events)
        {
            if (_state == State.Pending && _hit.Kind == TouchHitKind.Hero && time - _startTime >= _s.LongPressSeconds
                && (_last - _start).Length <= _threshold)
            {
                _state = State.LongPressed;
                events.Add(new GestureEvent(GestureKind.LongPressOnHero, time, _start));
            }
        }

        GestureEvent SwipeUpdate(double time, Vec2 pos)
        {
            Vec2 delta = pos - _start;
            float strength = Math.Min(delta.Length / _fullStrength, 1f);
            return new GestureEvent(GestureKind.SwipeUpdated, time, pos, delta.Normalized, strength);
        }
    }
}
