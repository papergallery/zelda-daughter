using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.World
{
    /// <summary>data/world.json (C-04). Time of day is 0..1 from midnight.</summary>
    public sealed class WorldSettings
    {
        public double DayLengthSeconds { get; set; }
        public double StartTimeOfDay { get; set; }
        public double DawnStart { get; set; }
        public double DayStart { get; set; }
        public double DuskStart { get; set; }
        public double NightStart { get; set; }
        public double SleepHours { get; set; }
    }

    public enum DayPhase { Night, Dawn, Day, Dusk }

    public enum ClockEventKind { PhaseChanged, NewDay }

    public readonly struct ClockEvent
    {
        public readonly ClockEventKind Kind;
        public readonly int Day;
        public readonly DayPhase Phase;

        public ClockEvent(ClockEventKind kind, int day, DayPhase phase) { Kind = kind; Day = day; Phase = phase; }
        public override string ToString() => $"{Kind} day={Day} {Phase}";
    }

    /// <summary>World time: real seconds in, game day and phase out (project-design.md §2). Days are counted from 1.</summary>
    public sealed class WorldClock
    {
        readonly WorldSettings _s;

        public WorldClock(WorldSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.DayLengthSeconds <= 0) throw new ArgumentException("DayLengthSeconds must be positive", nameof(settings));
            Day = 1;
            TimeOfDay = settings.StartTimeOfDay;
        }

        public int Day { get; private set; }
        public double TimeOfDay { get; private set; }
        public DayPhase Phase => PhaseAt(TimeOfDay);
        public double TotalHours => (Day - 1) * 24.0 + TimeOfDay * 24.0;

        /// <summary>0 at night, 1 by day, linear through dawn and dusk — what the view uses for light.</summary>
        public float Daylight
        {
            get
            {
                double t = TimeOfDay;
                if (t < _s.DawnStart || t >= _s.NightStart) return 0f;
                if (t < _s.DayStart) return (float)((t - _s.DawnStart) / (_s.DayStart - _s.DawnStart));
                if (t < _s.DuskStart) return 1f;
                return (float)(1.0 - (t - _s.DuskStart) / (_s.NightStart - _s.DuskStart));
            }
        }

        public DayPhase PhaseAt(double t)
        {
            if (t < _s.DawnStart || t >= _s.NightStart) return DayPhase.Night;
            if (t < _s.DayStart) return DayPhase.Dawn;
            if (t < _s.DuskStart) return DayPhase.Day;
            return DayPhase.Dusk;
        }

        /// <summary>Restore (save/load, tests).</summary>
        public void SetTime(int day, double timeOfDay)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (timeOfDay < 0 || timeOfDay >= 1) throw new ArgumentOutOfRangeException(nameof(timeOfDay));
            Day = day;
            TimeOfDay = timeOfDay;
        }

        /// <summary>Advance by real seconds; returns every phase change and new day crossed, in order.</summary>
        public IReadOnlyList<ClockEvent> Advance(double realSeconds) => AdvanceDays(realSeconds / _s.DayLengthSeconds);

        /// <summary>Skip game hours (sleep, knockout).</summary>
        public IReadOnlyList<ClockEvent> SkipHours(double hours) => AdvanceDays(hours / 24.0);

        IReadOnlyList<ClockEvent> AdvanceDays(double days)
        {
            var events = new List<ClockEvent>();
            if (days <= 0) return events;
            double[] bounds = { _s.DawnStart, _s.DayStart, _s.DuskStart, _s.NightStart, 1.0 };
            while (days > 0)
            {
                double next = 1.0;
                foreach (double b in bounds)
                    if (b > TimeOfDay) { next = b; break; }
                double step = next - TimeOfDay;
                if (days < step)
                {
                    TimeOfDay += days;
                    break;
                }
                days -= step;
                DayPhase before = Phase;
                if (next >= 1.0)
                {
                    Day++;
                    TimeOfDay = 0.0;
                    events.Add(new ClockEvent(ClockEventKind.NewDay, Day, Phase));
                }
                else
                {
                    TimeOfDay = next;
                }
                if (Phase != before) events.Add(new ClockEvent(ClockEventKind.PhaseChanged, Day, Phase));
            }
            return events;
        }
    }
}
