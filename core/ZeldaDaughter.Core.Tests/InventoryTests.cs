using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Inventory;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-09: project-design.md §7 «Инвентарь»; April bugs not repeated.</summary>
    public class InventoryTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static Bag New() => new Bag(D.Inventory, D.Items);

        [Fact]
        public void Stacks_fill_slots_by_item_stack_size()
        {
            var b = New();
            Assert.True(b.Add("stick", 15));             // stack 10 → two slots
            Assert.Equal(2, b.UsedSlots);
            Assert.Equal(15, b.Count("stick"));
            Assert.Equal(15 * D.Items["stick"].Weight, b.Weight, 3);
        }

        [Fact]
        public void Adding_is_all_or_nothing()
        {
            // April AddItem added part and returned false — the pick-up lost the rest.
            var b = New();
            for (int i = 0; i < D.Inventory.Slots - 1; i++) Assert.True(b.Add("knife"));
            Assert.False(b.Add("stick", 15)); // needs two slots, one left
            Assert.Equal(0, b.Count("stick"));
            Assert.True(b.Add("stick", 10));
        }

        [Fact]
        public void Removing_is_all_or_nothing()
        {
            var b = New();
            b.Add("ore", 3);
            Assert.False(b.Remove("ore", 4));
            Assert.Equal(3, b.Count("ore"));
            Assert.True(b.Remove("ore", 3));
            Assert.Equal(0, b.UsedSlots);
        }

        [Fact]
        public void Unknown_item_is_refused()
        {
            Assert.False(New().Add("item_knife"));
        }

        [Fact]
        public void Overload_slows_from_threshold_and_capacity_grows_with_skill()
        {
            var b = New();
            b.Add("ore", 5); b.Add("ore", 5); b.Add("ore", 5); b.Add("ore", 5); // 20 × 2 kg = 40 kg
            float cap = D.Inventory.BaseCapacity;
            Assert.Equal(0.8f, b.LoadRatio(1f), 3);
            Assert.Equal(1f, b.SpeedMultiplier(1f), 3);
            Assert.False(b.IsOverloaded(1f));
            b.Add("metal", 5); b.Add("metal", 2); // +10.5 kg → 50.5 kg
            Assert.True(b.IsOverloaded(1f));
            Assert.InRange(b.SpeedMultiplier(1f), 0.49f, 0.51f);
            // carry_capacity skill ×1.5: same load is no longer an overload (April ignored the skill).
            Assert.False(b.IsOverloaded(1.5f));
            Assert.Equal(1f, b.SpeedMultiplier(1.5f), 3);
        }

        [Fact]
        public void Far_over_capacity_still_moves()
        {
            var b = New();
            for (int i = 0; i < D.Inventory.Slots; i++) b.Add("ore", 5); // 200 kg
            Assert.Equal(0.3f, b.SpeedMultiplier(1f), 3);
        }
    }
}
