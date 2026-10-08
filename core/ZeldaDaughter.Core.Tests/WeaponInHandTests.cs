using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C3: the weapon in hand is the strongest one in the bag (§6 «оружие — что в рюкзаке»), bare fists otherwise.</summary>
    public class WeaponInHandTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        [Fact]
        public void An_empty_bag_means_fists()
        {
            Assert.Equal(WeaponSettings.Fists, new GameState(D).WeaponInHand);
        }

        [Fact]
        public void Things_that_are_not_weapons_do_not_count()
        {
            var g = new GameState(D);
            g.Bag.Add("berries");
            g.Bag.Add("cloth");
            Assert.Equal(WeaponSettings.Fists, g.WeaponInHand);
        }

        [Fact]
        public void The_strongest_by_damage_wins()
        {
            var g = new GameState(D);
            g.Bag.Add("stick");
            Assert.Equal("stick", g.WeaponInHand);
            g.Bag.Add("knife");
            Assert.Equal("knife", g.WeaponInHand);
            g.Bag.Add("stone");
            Assert.Equal("knife", g.WeaponInHand);
        }

        [Fact]
        public void A_tie_goes_to_the_smaller_id()
        {
            var g = new GameState(D);
            Assert.Equal(D.Weapons.Weapons["hammer"].Damage, D.Weapons.Weapons["sword"].Damage);
            g.Bag.Add("sword");
            g.Bag.Add("hammer");
            Assert.Equal("hammer", g.WeaponInHand);
        }

        [Fact]
        public void Losing_the_weapon_falls_back_to_the_next_one()
        {
            var g = new GameState(D);
            g.Bag.Add("stick");
            g.Bag.Add("sword");
            Assert.Equal("sword", g.WeaponInHand);
            g.Bag.Remove("sword");
            Assert.Equal("stick", g.WeaponInHand);
        }

        [Fact]
        public void The_lookup_allocates_nothing()
        {
            var g = new GameState(D);
            g.Bag.Add("stick");
            g.Bag.Add("knife");
            for (int i = 0; i < 20; i++) { var w = g.WeaponInHand; }
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { var w = g.WeaponInHand; }
            Assert.Equal(0, System.GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
