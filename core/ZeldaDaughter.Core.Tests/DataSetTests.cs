using System.Collections.Generic;
using System.IO;
using ZeldaDaughter.Core.Data;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-05: data/ loads with the game loader; broken references are named with file and id.</summary>
    public class DataSetTests
    {
        static Dictionary<string, string> RealFiles()
        {
            var files = new Dictionary<string, string>();
            foreach (var f in Directory.GetFiles(TestPaths.DataRoot, "*.json")) files[Path.GetFileName(f)] = File.ReadAllText(f);
            return files;
        }

        static DataSet LoadWith(Dictionary<string, string> files) => DataSet.Load(name => files[name]);

        [Fact]
        public void Real_data_loads_without_problems()
        {
            var d = DataSet.Load(TestPaths.DataRoot);
            Assert.True(d.Items.Count >= 25);
            Assert.NotEmpty(d.FieldRecipes);
            Assert.Equal(0.5, d.Input.LongPressSeconds, 3);
            Assert.Equal(2.5f, d.Movement.WalkSpeed);
            Assert.Equal(1500, d.World.DayLengthSeconds);
        }

        [Fact]
        public void Design_section7_combinations_are_present()
        {
            var d = DataSet.Load(TestPaths.DataRoot);
            bool Has(string a, string b, string o) => System.Linq.Enumerable.Any(d.FieldRecipes, r => ((r.A == a && r.B == b) || (r.A == b && r.B == a)) && r.Out == o);
            Assert.True(Has("stick", "cloth", "torch_unlit"));
            Assert.True(Has("knife", "stick", "sharpened_stick"));
            Assert.True(Has("axe", "firewood", "planks"));
        }

        [Fact]
        public void Tools_are_not_consumed()
        {
            // April CraftingSystem destroyed the knife and the axe (docs/april-review.md §1).
            var d = DataSet.Load(TestPaths.DataRoot);
            foreach (var r in d.FieldRecipes)
                foreach (var tool in new[] { r.A, r.B })
                    if (d.Items[tool].Kind == "tool") Assert.Contains(tool, r.Keep);
        }

        [Fact]
        public void Missing_item_in_recipe_is_named()
        {
            var files = RealFiles();
            files["recipes.json"] = files["recipes.json"].Replace("\"out\": \"bandage\"", "\"out\": \"item_bandage\"");
            var e = Assert.Throws<DataException>(() => LoadWith(files));
            Assert.Contains(e.Problems, p => p.Contains("recipes.json") && p.Contains("item_bandage"));
        }

        [Fact]
        public void Prefixed_or_duplicate_ids_are_rejected()
        {
            var files = RealFiles();
            files["items.json"] = files["items.json"].Replace("\"id\": \"short_stick\"", "\"id\": \"item_stick\"").Replace("\"id\": \"herbs\"", "\"id\": \"stick\"");
            var e = Assert.Throws<DataException>(() => LoadWith(files));
            Assert.Contains(e.Problems, p => p.Contains("item_stick") && p.Contains("префикс"));
            Assert.Contains(e.Problems, p => p.Contains("'stick'") && p.Contains("повтор"));
        }

        [Fact]
        public void Malformed_json_is_reported_with_file()
        {
            var files = RealFiles();
            files["world.json"] = "{ not json";
            var e = Assert.Throws<DataException>(() => LoadWith(files));
            Assert.Contains(e.Problems, p => p.StartsWith("world.json"));
        }

        [Fact]
        public void Weapons_are_item_ids_or_fists_and_enemies_obey_the_design_limits()
        {
            var d = DataSet.Load(TestPaths.DataRoot);
            Assert.Contains("fists", d.Weapons.Weapons.Keys);
            Assert.All(d.Enemies.Enemies.Values, e => Assert.True(e.Windup >= d.Enemies.MinWindup && e.ChaseSpeed < d.Movement.RunSpeed));

            var files = RealFiles();
            files["weapons.json"] = files["weapons.json"].Replace("\"sword\":", "\"item_sword\":");
            files["enemies.json"] = files["enemies.json"].Replace("\"windup\": 0.6", "\"windup\": 0.3").Replace("\"chaseSpeed\": 4.5", "\"chaseSpeed\": 6");
            var e = Assert.Throws<DataException>(() => LoadWith(files));
            Assert.Contains(e.Problems, p => p.Contains("weapons.json") && p.Contains("item_sword"));
            Assert.Contains(e.Problems, p => p.Contains("enemies.json") && p.Contains("'wolf'") && p.Contains("замах"));
            Assert.Contains(e.Problems, p => p.Contains("enemies.json") && p.Contains("'boar'") && p.Contains("бега"));
        }

        [Fact]
        public void Session_rules_come_from_data()
        {
            var s = DataSet.Load(TestPaths.DataRoot).Session;
            Assert.True(s.TappableHintRadius > 0);
            Assert.True(s.IsNear(s.TappableHintRadius - 0.1f));
            Assert.False(s.IsNear(s.TappableHintRadius + 0.1f));
            Assert.True(s.IsNight(s.NightDaylightBelow - 0.01f));
            Assert.False(s.IsNight(s.NightDaylightBelow + 0.01f));
        }

        [Fact]
        public void Session_with_a_bad_radius_is_named()
        {
            var files = RealFiles();
            files["session.json"] = files["session.json"].Replace("\"tappableHintRadius\"", "\"tappableHintRadius_\"");
            var ex = Assert.Throws<DataException>(() => LoadWith(files));
            Assert.Contains(ex.Problems, p => p.Contains("session.json"));
        }
    }
}
