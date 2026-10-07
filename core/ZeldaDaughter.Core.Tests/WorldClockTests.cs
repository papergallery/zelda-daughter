using System.Linq;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-04: project-design.md §2 «Цикл дня и ночи».</summary>
    public class WorldClockTests
    {
        static readonly WorldSettings S = TestData.Load<WorldSettings>("world.json");

        [Fact]
        public void Cycle_length_is_within_design_range()
        {
            Assert.InRange(S.DayLengthSeconds, 20 * 60, 30 * 60);
        }

        [Fact]
        public void Starts_in_the_morning_of_day_one()
        {
            var c = new WorldClock(S);
            Assert.Equal(1, c.Day);
            Assert.Equal(S.StartTimeOfDay, c.TimeOfDay, 6);
            Assert.Equal(DayPhase.Day, c.Phase);
        }

        [Fact]
        public void Full_real_cycle_brings_the_same_time_next_day()
        {
            var c = new WorldClock(S);
            for (int i = 0; i < 1500; i++) c.Advance(S.DayLengthSeconds / 1500.0);
            Assert.Equal(2, c.Day);
            Assert.Equal(S.StartTimeOfDay, c.TimeOfDay, 6);
        }

        [Fact]
        public void Phases_go_day_dusk_night_dawn_day()
        {
            var c = new WorldClock(S);
            var seen = Enumerable.Range(0, 3000)
                .SelectMany(_ => c.Advance(S.DayLengthSeconds / 1000.0))
                .Where(e => e.Kind == ClockEventKind.PhaseChanged)
                .Select(e => e.Phase).Take(5).ToArray();
            Assert.Equal(new[] { DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn, DayPhase.Day, DayPhase.Dusk }, seen);
        }

        [Fact]
        public void One_big_step_counts_every_midnight()
        {
            // April DayNightCycle wrapped past midnight only once and never counted days.
            var c = new WorldClock(S);
            var ev = c.Advance(S.DayLengthSeconds * 3.0);
            Assert.Equal(4, c.Day);
            Assert.Equal(3, ev.Count(e => e.Kind == ClockEventKind.NewDay));
        }

        [Fact]
        public void Sleep_skips_game_hours()
        {
            var c = new WorldClock(S);
            c.SetTime(1, 22.0 / 24.0); // 22:00
            c.SkipHours(S.SleepHours);
            Assert.Equal(2, c.Day);
            Assert.Equal(6.0 / 24.0, c.TimeOfDay, 6);
            Assert.Equal(DayPhase.Dawn, c.Phase);
        }

        [Fact]
        public void Daylight_is_zero_at_night_one_at_day_and_ramps_at_dawn_and_dusk()
        {
            var c = new WorldClock(S);
            c.SetTime(1, 0.1); Assert.Equal(0f, c.Daylight, 3);
            c.SetTime(1, 0.5); Assert.Equal(1f, c.Daylight, 3);
            c.SetTime(1, (S.DawnStart + S.DayStart) / 2); Assert.Equal(0.5f, c.Daylight, 2);
            c.SetTime(1, (S.DuskStart + S.NightStart) / 2); Assert.Equal(0.5f, c.Daylight, 2);
        }

        [Fact]
        public void Game_hours_are_absolute()
        {
            var c = new WorldClock(S);
            c.SetTime(3, 0.5);
            Assert.Equal(2 * 24 + 12, c.TotalHours, 6);
        }
    }
}
