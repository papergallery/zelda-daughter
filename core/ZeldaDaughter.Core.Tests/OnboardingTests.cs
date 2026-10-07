using ZeldaDaughter.Core.Onboarding;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-14: project-design.md §6 «Онбординг».</summary>
    public class OnboardingTests
    {
        static readonly OnboardingSettings S = TestData.Load<OnboardingSettings>("onboarding.json");

        [Fact]
        public void Swipe_hint_is_shown_at_start_and_leaves_only_after_a_swipe()
        {
            var o = new Hints(S);
            Assert.Equal("swipe", o.Visible);
            o.Did("tap");
            Assert.Equal("swipe", o.Visible);
            o.Did("swipe");
            Assert.Null(o.Visible);
        }

        [Fact]
        public void Next_hint_waits_for_its_condition()
        {
            var o = new Hints(S);
            o.Did("swipe");
            Assert.Null(o.Visible);
            o.Set("tappable_nearby", true);
            Assert.Equal("tap", o.Visible);
            o.Set("tappable_nearby", false);
            Assert.Null(o.Visible);            // not in the way when nothing is near
            o.Set("tappable_nearby", true);
            o.Did("tap");
            o.Set("has_item", true);
            Assert.Equal("long_press", o.Visible);
            o.Did("long_press_hero");
            Assert.Null(o.Visible);
            Assert.True(o.AllDone);
        }

        [Fact]
        public void An_action_done_early_never_shows_its_hint()
        {
            var o = new Hints(S);
            o.Did("long_press_hero");
            o.Did("swipe");
            o.Set("has_item", true);
            Assert.Null(o.Visible);
        }

        [Fact]
        public void Done_hints_survive_save_and_load()
        {
            var o = new Hints(S);
            o.Did("swipe");
            var again = new Hints(S);
            again.Restore(o.Done);
            Assert.Null(again.Visible);
        }
    }
}
