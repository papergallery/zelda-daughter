using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C4: dragging an item onto the hero (§6, §7) — food is eaten, medicine treats its wound, anything else does nothing.</summary>
    public class UseOnHeroTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState G() => new GameState(D);

        [Fact]
        public void Food_is_eaten_hunger_drops_and_the_item_goes()
        {
            var g = G();
            g.Hunger.Restore(0.8f);
            g.Bag.Add("berries", 2);
            var r = g.UseOnHero("berries");
            Assert.Equal(HeroUseOutcome.Ate, r.Outcome);
            Assert.True(g.Hunger.Value < 0.8f);
            Assert.Equal(1, g.Bag.Count("berries"));
        }

        [Fact]
        public void Medicine_treats_the_matching_wound_and_is_spent()
        {
            var g = G();
            g.Condition.Wound(WoundType.Cut, 0.5f);
            g.Bag.Add("bandage", 2);
            var r = g.UseOnHero("bandage");
            Assert.Equal(HeroUseOutcome.Treated, r.Outcome);
            Assert.Equal(0f, g.Condition.Severity(WoundType.Cut));
            Assert.Equal(1, g.Bag.Count("bandage"));
        }

        [Fact]
        public void Medicine_without_its_wound_is_kept_and_the_hero_remarks()
        {
            var g = G();
            g.Bag.Add("bandage");
            var r = g.UseOnHero("bandage");
            Assert.Equal(HeroUseOutcome.NotUsable, r.Outcome);
            Assert.Equal(Topics.UseNothing, r.Topic);
            Assert.Equal(1, g.Bag.Count("bandage"));
        }

        [Fact]
        public void A_wrong_medicine_does_not_treat_another_wound()
        {
            var g = G();
            g.Condition.Wound(WoundType.Fracture, 0.5f);
            g.Bag.Add("bandage");
            Assert.Equal(HeroUseOutcome.NotUsable, g.UseOnHero("bandage").Outcome);
            Assert.True(g.Condition.Severity(WoundType.Fracture) > 0f);
        }

        [Fact]
        public void An_item_that_is_neither_does_nothing()
        {
            var g = G();
            g.Bag.Add("stick");
            var r = g.UseOnHero("stick");
            Assert.Equal(HeroUseOutcome.NotUsable, r.Outcome);
            Assert.Equal(Topics.UseNothing, r.Topic);
            Assert.Equal(1, g.Bag.Count("stick"));
        }

        [Fact]
        public void An_item_not_in_the_bag_does_nothing()
        {
            var g = G();
            Assert.Equal(HeroUseOutcome.NotUsable, g.UseOnHero("berries").Outcome);
            Assert.Equal(HeroUseOutcome.NotUsable, g.UseOnHero("no_such_item").Outcome);
        }

        [Fact]
        public void A_knocked_out_hero_uses_nothing()
        {
            var g = G();
            g.Bag.Add("berries");
            g.Condition.Damage(1000);
            Assert.Equal(HeroUseOutcome.NotUsable, g.UseOnHero("berries").Outcome);
            Assert.Equal(1, g.Bag.Count("berries"));
        }

        [Fact]
        public void The_remark_topic_has_lines_in_data()
        {
            Assert.NotEmpty(D.Remarks.Topics[Topics.UseNothing].Lines);
        }
    }
}
