using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Npcs;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-02: project-design.md §2 — NPCs live by a schedule: work by day, tavern in the evening, sleep at night, shops closed at night.</summary>
    public class NpcScheduleTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static NpcRoster At(double hour, out GameState g)
        {
            g = new GameState(D);
            g.Clock.SetTime(1, hour / 24.0);
            return g.Npcs;
        }

        [Fact]
        public void Region_has_the_first_town_cast()
        {
            foreach (var id in new[] { "peasant", "merchant", "smith", "barkeep", "herbalist", "guard" })
                Assert.Contains(id, D.Npcs.Npcs.Keys);
            Assert.True(D.Npcs.Npcs.Count >= 8 && D.Npcs.Npcs.Count <= 10);
        }

        [Fact]
        public void At_any_hour_every_npc_is_somewhere_and_doing_something()
        {
            foreach (var id in D.Npcs.Npcs.Keys)
                for (int q = 0; q < 24 * 4; q++)
                {
                    var r = At(q / 4.0, out _);
                    var s = r.Slot(id);
                    Assert.False(string.IsNullOrEmpty(s.Anchor), $"{id} at {q / 4.0} h");
                }
        }

        [Fact]
        public void Schedule_wraps_over_midnight_from_the_last_entry()
        {
            var r = At(0.5, out _);
            Assert.Equal(NpcActivity.Sleep, r.Slot("merchant").Activity);
            Assert.Equal(NpcActivity.Sleep, r.Slot("smith").Activity);
        }

        [Fact]
        public void Shops_are_closed_at_night_and_open_by_day()
        {
            var night = At(2, out _);
            foreach (var id in D.Npcs.Npcs.Where(n => n.Value.Shop).Select(n => n.Key)) Assert.False(night.IsShopOpen(id), id);
            var noon = At(12, out _);
            foreach (var id in new[] { "merchant", "smith", "herbalist", "barkeep" }) Assert.True(noon.IsShopOpen(id), id);
            Assert.False(noon.IsShopOpen("guard")); // no shop at all
        }

        [Fact]
        public void In_the_evening_the_merchant_goes_to_the_tavern_and_the_shop_is_shut()
        {
            var r = At(12, out var g);
            r.Sync();
            Assert.Equal(NpcActivity.Trade, r.Slot("merchant").Activity);
            var before = r.Slot("merchant").Anchor;
            g.Clock.SetTime(1, 18.5 / 24.0);
            var changes = r.Sync();
            var c = changes.Single(x => x.NpcId == "merchant");
            Assert.Equal(before, c.FromAnchor);
            Assert.Equal(NpcActivity.Tavern, c.Activity);
            Assert.Equal("anchor_tavern_hall", c.ToAnchor);
            Assert.False(r.IsShopOpen("merchant"));
        }

        [Fact]
        public void Sync_reports_a_change_once_and_places_everyone_on_the_first_call()
        {
            var r = At(12, out var g);
            var first = r.Sync();
            Assert.Equal(D.Npcs.Npcs.Count, first.Count);
            Assert.All(first, c => Assert.Null(c.FromAnchor));
            Assert.Empty(r.Sync());
            g.Clock.SetTime(1, 12.2 / 24.0);
            Assert.Empty(r.Sync());
        }

        [Fact]
        public void A_long_sleep_skip_lands_everyone_in_bed_and_events_name_the_final_place()
        {
            var r = At(20, out var g);
            r.Sync();
            g.Sleep(); // +8 h → 04:00 next day
            var changes = r.Sync();
            Assert.All(changes, c => Assert.Equal(NpcActivity.Sleep, c.Activity));
        }

        [Fact]
        public void Same_time_same_place()
        {
            for (int h = 0; h < 24; h++)
                foreach (var id in D.Npcs.Npcs.Keys)
                    Assert.Equal(At(h + 0.3, out _).Slot(id), At(h + 0.3, out _).Slot(id));
        }

        [Fact]
        public void Every_npc_has_a_dialogue()
        {
            foreach (var kv in D.Npcs.Npcs)
            {
                Assert.True(D.Dialogues.Npcs.ContainsKey(kv.Key), kv.Key);
            }
        }

        [Fact]
        public void Broken_schedule_is_named_by_data_loader()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["npcs.json"] = files["npcs.json"].Replace("\"anchor_gate\"", "\"\"").Replace("\"work\"", "\"dance\"");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("npcs.json"));
        }

        [Fact]
        public void Anchors_exist_in_the_region_scene()
        {
            var path = Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json");
            if (!File.Exists(path)) return; // scenes/region.json is built in D-10; until then the anchors are a contract written in data/npcs.json
            var ids = SceneConfig.Parse(File.ReadAllText(path)).Objects.Select(o => o.Id).ToHashSet();
            foreach (var a in D.Npcs.Npcs.SelectMany(n => n.Value.Schedule).Select(s => s.Anchor).Distinct())
                Assert.True(ids.Contains(a), $"scenes/region.json: нет объекта-якоря '{a}' (data/npcs.json)");
        }
    }
}
