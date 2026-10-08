using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Loot;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-05: project-design.md §6 «Лут с врагов» — no tool: the minimum (a fang), once; with a knife: hide, meat, bones, the carcass is gone.</summary>
    public class CarcassTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static (GameState g, string id) Killed(string def = "boar", float x = 4f, float z = -2f)
        {
            var g = new GameState(D);
            var e = new Enemy(def + "_1", D.Enemies, def, new Vec2(x, z));
            e.Receive(10000, null, 0, 0);
            Assert.True(e.IsCarcass);
            g.EnemyKilled(e);
            return (g, e.Id);
        }

        [Fact]
        public void A_kill_leaves_a_carcass_where_the_enemy_fell_and_records_the_kill()
        {
            var (g, id) = Killed("wolf");
            Assert.Contains(id, g.Killed);
            var c = Assert.Single(g.Carcasses.Active);
            Assert.Equal(id, c.Id);
            Assert.Equal("wolf", c.DefId);
            Assert.Equal(new Vec2(4f, -2f), c.Position);
            Assert.Equal(CarcassState.Fresh, c.State);
        }

        [Fact]
        public void Without_a_knife_a_tap_gives_the_minimum_once()
        {
            var (g, id) = Killed();
            var r = g.Carcasses.Tap(id);
            Assert.Equal(LootOutcome.Minimal, r.Outcome);
            foreach (var kv in D.Enemies.Enemies["boar"].Loot.Minimal) Assert.Equal(kv.Value, g.Bag.Count(kv.Key));
            Assert.Equal(0, g.Bag.Count("meat"));
            var again = g.Carcasses.Tap(id);
            Assert.Equal(LootOutcome.Nothing, again.Outcome);
            Assert.Equal(D.Enemies.Enemies["boar"].Loot.Minimal["fang"], g.Bag.Count("fang"));
            Assert.Equal(CarcassState.Looted, g.Carcasses.Get(id)!.State);
        }

        [Fact]
        public void With_a_knife_the_full_set_comes_and_the_carcass_is_gone()
        {
            foreach (var def in new[] { "boar", "wolf" })
            {
                var (g, id) = Killed(def);
                g.Bag.Add("knife");
                var gone = new System.Collections.Generic.List<string>();
                g.Carcasses.Disappeared += gone.Add;
                var r = g.Carcasses.Tap(id);
                Assert.Equal(LootOutcome.Butchered, r.Outcome);
                foreach (var kv in D.Enemies.Enemies[def].Loot.Full) Assert.Equal(kv.Value, g.Bag.Count(kv.Key));
                Assert.Equal(1, g.Bag.Count("knife"));       // a tool is not used up
                Assert.Empty(g.Carcasses.Active);
                Assert.Equal(new[] { id }, gone);
                Assert.Equal(LootOutcome.Gone, g.Carcasses.Tap(id).Outcome);
                Assert.Contains(id, g.Killed);               // it does not come back after a load either
            }
        }

        [Fact]
        public void The_minimum_taken_earlier_is_not_given_twice_when_the_knife_comes_later()
        {
            var (g, id) = Killed();
            g.Carcasses.Tap(id);
            g.Bag.Add("knife");
            Assert.Equal(LootOutcome.Butchered, g.Carcasses.Tap(id).Outcome);
            foreach (var kv in D.Enemies.Enemies["boar"].Loot.Full) Assert.Equal(kv.Value, g.Bag.Count(kv.Key));
        }

        [Fact]
        public void No_room_loses_nothing_and_leaves_the_carcass_to_come_back_to()
        {
            var (g, id) = Killed();
            g.Bag.Add("knife");
            var filler = D.Items.Values.Where(i => i.Kind != "currency" && !new[] { "hide", "meat", "bone", "fang", "fat" }.Contains(i.Id) && i.Id != "knife").Select(i => i.Id).ToList();
            foreach (var f in filler) { if (g.Bag.UsedSlots >= D.Inventory.Slots) break; g.Bag.Add(f); }
            Assert.Equal(D.Inventory.Slots, g.Bag.UsedSlots);
            var before = SaveGame.Capture(g);
            Assert.Equal(LootOutcome.NoRoom, g.Carcasses.Tap(id).Outcome);
            Assert.Equal(before, SaveGame.Capture(g));
            foreach (var f in filler.Take(5)) g.Bag.Remove(f);
            Assert.Equal(LootOutcome.Butchered, g.Carcasses.Tap(id).Outcome);
        }

        [Fact]
        public void Unknown_carcass_is_nothing()
        {
            Assert.Equal(LootOutcome.Gone, new GameState(D).Carcasses.Tap("ghost").Outcome);
        }

        [Fact]
        public void Carcasses_survive_a_save_and_a_butchered_one_does_not_come_back()
        {
            var (g, id) = Killed("boar", 7.5f, 1.25f);
            g.Carcasses.Tap(id);                          // looted, still lying there
            var e2 = new Enemy("wolf_3", D.Enemies, "wolf", new Vec2(-3, 3)); e2.Receive(1000, null, 0, 0);
            g.EnemyKilled(e2);
            g.Bag.Add("knife");
            g.Carcasses.Tap("wolf_3");                    // butchered
            string a = SaveGame.Capture(g);
            var fresh = new GameState(D);
            SaveGame.Restore(fresh, a);
            Assert.Equal(a, SaveGame.Capture(fresh));
            var c = Assert.Single(fresh.Carcasses.Active);
            Assert.Equal(id, c.Id);
            Assert.Equal(CarcassState.Looted, c.State);
            Assert.Equal(new Vec2(7.5f, 1.25f), c.Position);
            fresh.Bag.Remove("knife");
            Assert.Equal(LootOutcome.Nothing, fresh.Carcasses.Tap(id).Outcome);   // the minimum is not given again after a load
            Assert.Contains("wolf_3", fresh.Killed);
        }

        [Fact]
        public void Old_saves_with_kills_but_no_carcasses_load_without_them()
        {
            var g = new GameState(D);
            g.Killed.Add("wolf_1");
            var json = SaveGame.Capture(g).Replace($"\"Version\": {SaveGame.Version}", "\"Version\": 2");
            var fresh = new GameState(D);
            SaveGame.Restore(fresh, json);
            Assert.Contains("wolf_1", fresh.Killed);
            Assert.Empty(fresh.Carcasses.Active);
        }

        [Fact]
        public void Raw_meat_feeds_worse_than_cooked_and_a_campfire_recipe_cooks_it()
        {
            var raw = D.Hunger.Food["meat"];
            var cooked = D.Hunger.Food["cooked_meat"];
            Assert.True(cooked.Satiety > raw.Satiety);
            Assert.True(cooked.Heal > raw.Heal);
            Assert.Contains(D.WorldRecipes, r => r.Target == "campfire" && r.With == "meat" && r.Result == "cooked_meat");
        }

        [Fact]
        public void Loot_tables_reference_real_items_and_the_butcher_tool_is_a_tool()
        {
            Assert.Equal("tool", D.Items[D.Enemies.ButcherTool].Kind);
            foreach (var e in D.Enemies.Enemies.Values)
            {
                Assert.NotEmpty(e.Loot.Minimal);
                Assert.NotEmpty(e.Loot.Full);
                foreach (var kv in e.Loot.Minimal) Assert.True(!e.Loot.Full.ContainsKey(kv.Key) || e.Loot.Full[kv.Key] >= kv.Value, "full ⊇ minimal: " + kv.Key);
            }
        }

        [Fact]
        public void A_broken_loot_table_is_named_by_the_loader()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["enemies.json"] = files["enemies.json"].Replace("\"hide\"", "\"hyde\"");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("enemies.json") && p.Contains("hyde"));
        }
    }
}
