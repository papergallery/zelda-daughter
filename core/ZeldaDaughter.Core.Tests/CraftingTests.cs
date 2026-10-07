using ZeldaDaughter.Core.Crafting;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Inventory;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-10: project-design.md §7 «Принцип крафта», «Полевой», «Станочный», «Логические цепочки».</summary>
    public class CraftingTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static (Crafting.Crafting c, Bag b) New()
        {
            var b = new Bag(D.Inventory, D.Items);
            return (new Crafting.Crafting(D), b);
        }

        [Fact]
        public void Field_combination_works_in_either_order()
        {
            var (c, b) = New();
            b.Add("stick"); b.Add("cloth");
            var r = c.Combine("cloth", "stick", b);
            Assert.Equal(CraftOutcome.Done, r.Outcome);
            Assert.Equal("torch_unlit", r.Item);
            Assert.Equal(1, b.Count("torch_unlit"));
            Assert.Equal(0, b.Count("stick") + b.Count("cloth"));
        }

        [Fact]
        public void Tools_stay_in_the_bag()
        {
            // April CraftingSystem.cs:58-59 destroyed the knife and the axe.
            var (c, b) = New();
            b.Add("knife"); b.Add("stick");
            Assert.Equal(CraftOutcome.Done, c.Combine("knife", "stick", b).Outcome);
            Assert.Equal(1, b.Count("knife"));
            Assert.Equal(1, b.Count("sharpened_stick"));
            b.Add("axe"); b.Add("firewood");
            Assert.Equal(CraftOutcome.Done, c.Combine("axe", "firewood", b).Outcome);
            Assert.Equal(1, b.Count("axe"));
            Assert.Equal(2, b.Count("planks"));
        }

        [Fact]
        public void Same_item_pair_needs_two_of_it()
        {
            var (c, b) = New();
            b.Add("healing_herbs");
            Assert.Equal(CraftOutcome.MissingIngredients, c.Combine("healing_herbs", "healing_herbs", b).Outcome);
            b.Add("healing_herbs");
            Assert.Equal(CraftOutcome.Done, c.Combine("healing_herbs", "healing_herbs", b).Outcome);
            Assert.Equal(1, b.Count("antidote"));
        }

        [Fact]
        public void No_combination_says_so_and_changes_nothing()
        {
            var (c, b) = New();
            b.Add("stone"); b.Add("berries");
            Assert.Equal(CraftOutcome.NoRecipe, c.Combine("stone", "berries", b).Outcome);
            Assert.Equal(1, b.Count("stone"));
        }

        [Fact]
        public void Weapons_only_on_the_anvil()
        {
            var (c, b) = New();
            b.Add("metal"); b.Add("stick");
            Assert.Equal(CraftOutcome.NeedsStation, c.Combine("metal", "stick", b).Outcome);
            Assert.Equal(CraftOutcome.NoRecipe, c.AtStation("smelter", new[] { "metal", "stick" }, b).Outcome);
            var r = c.AtStation("anvil", new[] { "stick", "metal" }, b);
            Assert.Equal(CraftOutcome.Done, r.Outcome);
            Assert.Equal(1, b.Count("sword"));
        }

        [Fact]
        public void Result_that_does_not_fit_rolls_back()
        {
            // April: a full bag lost the result.
            var (c2, b2) = New();
            b2.Add("knife"); b2.Add("stick", 2);
            while (b2.UsedSlots < D.Inventory.Slots) b2.Add("axe");
            // One of two sticks is used, its slot stays taken, the knife stays — the result needs a slot there is none of.
            var r = c2.Combine("knife", "stick", b2);
            Assert.Equal(CraftOutcome.NoRoom, r.Outcome);
            Assert.Equal(2, b2.Count("stick"));
            Assert.Equal(0, b2.Count("sharpened_stick"));
        }

        [Fact]
        public void World_chains_light_a_campfire_and_a_torch()
        {
            var (c, b) = New();
            b.Add("flint"); b.Add("torch_unlit");
            var fire = c.InWorld("firewood_placed", "flint", b);
            Assert.Equal(CraftOutcome.Done, fire.Outcome);
            Assert.Equal("campfire", fire.WorldResult);
            Assert.Equal(1, b.Count("flint"));
            var torch = c.InWorld("fire", "torch_unlit", b);
            Assert.Equal("torch", torch.Item);
            Assert.Equal(1, b.Count("torch"));
            Assert.Equal(0, b.Count("torch_unlit"));
        }

        [Fact]
        public void Hint_when_holding_a_fitting_item_near_an_object()
        {
            var (c, _) = New();
            Assert.True(c.HasWorldUse("fire", "torch_unlit"));
            Assert.False(c.HasWorldUse("fire", "stone"));
        }

        [Fact]
        public void First_time_is_reported_as_discovery()
        {
            var (c, b) = New();
            b.Add("stick", 2); b.Add("cloth", 2);
            Assert.True(c.Combine("stick", "cloth", b).Discovered);
            Assert.False(c.Combine("stick", "cloth", b).Discovered);
        }
    }
}
