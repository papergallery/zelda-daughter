using ZeldaDaughter.Core.Condition;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-08: project-design.md §6 «Еда и голод».</summary>
    public class HungerTests
    {
        static readonly HungerSettings S = TestData.Load<HungerSettings>("hunger.json");

        [Fact]
        public void Eating_is_needed_rarely_not_every_few_minutes()
        {
            // April: hungry after 600 s. Here: not hungry within one game day (1500 s) from the start.
            var h = new Hunger(S);
            h.Advance(1500);
            Assert.True(h.Level < HungerLevel.Hungry);
        }

        [Fact]
        public void Levels_rise_in_order()
        {
            var h = new Hunger(S);
            Assert.Equal(HungerLevel.Fed, h.Level);
            h.Advance(S.SecondsToFull * (S.PeckishAt - S.Start) + 1); Assert.Equal(HungerLevel.Peckish, h.Level);
            h.Advance(S.SecondsToFull * (S.HungryAt - S.PeckishAt)); Assert.Equal(HungerLevel.Hungry, h.Level);
            h.Advance(S.SecondsToFull); Assert.Equal(HungerLevel.Starving, h.Level);
            Assert.Equal(1f, h.Value, 3);
        }

        [Fact]
        public void Below_threshold_no_penalty_above_it_gradual_degradation_of_everything()
        {
            var h = new Hunger(S);
            Assert.Equal(1f, h.Multiplier);
            h.Restore(S.HungryAt);
            Assert.Equal(1f, h.Multiplier, 3);
            h.Restore((S.HungryAt + 1f) / 2f);
            Assert.Equal((1f + S.MultiplierAtFull) / 2f, h.Multiplier, 3);
            h.Restore(1f);
            Assert.Equal(S.MultiplierAtFull, h.Multiplier, 3);
        }

        [Fact]
        public void Food_satisfies_and_heals_a_little()
        {
            var h = new Hunger(S);
            h.Restore(0.8f);
            var effect = h.Eat("meat");
            Assert.True(effect.Eaten);
            Assert.Equal(0.8f - S.Food["meat"].Satiety, h.Value, 3);
            Assert.Equal(S.Food["meat"].Heal, effect.Heal);
            Assert.False(h.Eat("stick").Eaten);
        }

        [Fact]
        public void Hunger_never_goes_below_zero()
        {
            var h = new Hunger(S);
            h.Eat("meat"); h.Eat("meat");
            Assert.Equal(0f, h.Value);
        }
    }
}
