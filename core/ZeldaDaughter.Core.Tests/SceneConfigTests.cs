using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>T-04: every scenes/*.json parses and validates — checked on the server before Unity sees it.</summary>
    public class SceneConfigTests
    {
        static string ScenesDir => Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes");

        [Fact]
        public void All_scene_configs_are_valid()
        {
            var files = Directory.GetFiles(ScenesDir, "*.json");
            Assert.NotEmpty(files);
            foreach (var f in files)
            {
                var c = SceneConfig.Parse(File.ReadAllText(f));
                Assert.Equal(Path.GetFileNameWithoutExtension(f), c.Name);
                Assert.Empty(c.Validate());
            }
        }

        [Fact]
        public void G1_scene_has_the_april_iso_camera()
        {
            var c = SceneConfig.Parse(File.ReadAllText(Path.Combine(ScenesDir, "g1-capsule.json")));
            Assert.True(c.Camera.Orthographic);
            Assert.Equal(35f, c.Camera.Pitch);
            Assert.Equal(45f, c.Camera.Yaw);
            Assert.Equal("capsule", c.Hero.Shape);
        }

        [Fact]
        public void Prologue_distances_follow_the_design()
        {
            // §2: «через ~15 секунд ходьбы по дороге — крестьянин… город совсем рядом — в первые минуты».
            var c = SceneConfig.Parse(File.ReadAllText(Path.Combine(ScenesDir, "prologue-grey.json")));
            var walk = ZeldaDaughter.Core.Data.DataSet.Load(TestPaths.DataRoot).Movement.WalkSpeed;
            float Dist(string id)
            {
                var p = c.Objects.Single(o => o.Id == id).Position;
                return (float)System.Math.Sqrt((p.X - c.Hero.Spawn.X) * (p.X - c.Hero.Spawn.X) + (p.Z - c.Hero.Spawn.Z) * (p.Z - c.Hero.Spawn.Z));
            }
            Assert.InRange(Dist("npc_peasant") / walk, 13f, 17f);
            Assert.InRange(Dist("town_gate") / walk, 20f, 60f);
            foreach (var target in new[] { "town_gate", "town_square" }) Assert.Contains(c.Objects, o => o.Id == target);
        }

        [Fact]
        public void Pickup_items_exist()
        {
            var items = ZeldaDaughter.Core.Data.DataSet.Load(TestPaths.DataRoot).Items;
            foreach (var f in Directory.GetFiles(ScenesDir, "*.json"))
                foreach (var o in SceneConfig.Parse(File.ReadAllText(f)).Objects.Where(o => o.Item != null))
                    Assert.True(items.ContainsKey(o.Item!), $"{Path.GetFileName(f)}: {o.Id} → {o.Item}");
        }

        [Fact]
        public void Problems_are_named()
        {
            var c = SceneConfig.Parse("{\"name\":\"t\",\"ground\":{\"sizeX\":10,\"sizeZ\":10},\"camera\":{\"pitch\":35,\"distance\":20,\"size\":8}," +
                "\"hero\":{\"shape\":\"capsule\"},\"objects\":[{\"id\":\"a\",\"shape\":\"cube\",\"position\":{\"x\":50}}," +
                "{\"id\":\"a\",\"shape\":\"blob\"},{\"id\":\"b\",\"shape\":\"cube\",\"prefab\":\"x.prefab\"},{\"id\":\"c\",\"prefab\":\"missing.prefab\",\"color\":\"red\"}]}");
            var p = c.Validate(prefab => prefab != "missing.prefab");
            Assert.Contains(p, x => x.Contains("'a'") && x.Contains("за краем"));
            Assert.Contains(p, x => x.Contains("'a'") && x.Contains("повтор"));
            Assert.Contains(p, x => x.Contains("blob"));
            Assert.Contains(p, x => x.Contains("'b'") && x.Contains("ровно один"));
            Assert.Contains(p, x => x.Contains("missing.prefab"));
            Assert.Contains(p, x => x.Contains("'c'") && x.Contains("цвет"));
        }

        static SceneConfig Cfg(string name, bool include, int order) =>
            SceneConfig.Parse($"{{\"name\":\"{name}\",\"build\":{{\"include\":{(include ? "true" : "false")},\"order\":{order}}}}}");

        [Fact]
        public void Build_list_is_ordered_by_config_and_skips_excluded_scenes()
        {
            var list = SceneConfig.BuildList(new[] { Cfg("b", true, 2), Cfg("test", false, 0), Cfg("a", true, 0), Cfg("c", true, 1) });
            Assert.Equal(new[] { "a", "c", "b" }, list);
        }

        [Fact]
        public void Build_list_refuses_two_scenes_with_the_same_order()
        {
            Assert.Throws<System.InvalidOperationException>(() => SceneConfig.BuildList(new[] { Cfg("a", true, 0), Cfg("b", true, 0) }));
        }

        [Fact]
        public void Real_build_list_starts_with_the_prologue_and_has_no_test_capsule()
        {
            var all = Directory.GetFiles(ScenesDir, "*.json").Select(f => SceneConfig.Parse(File.ReadAllText(f)));
            var list = SceneConfig.BuildList(all);
            Assert.Equal("prologue-grey", list[0]);
            Assert.DoesNotContain("g1-capsule", list);
        }

        [Fact]
        public void Test_capsule_does_not_save_and_the_prologue_has_a_slot()
        {
            var g1 = SceneConfig.Parse(File.ReadAllText(Path.Combine(ScenesDir, "g1-capsule.json")));
            var pro = SceneConfig.Parse(File.ReadAllText(Path.Combine(ScenesDir, "prologue-grey.json")));
            Assert.True(string.IsNullOrEmpty(g1.Save.Slot));
            Assert.False(string.IsNullOrEmpty(pro.Save.Slot));
        }
    }
}
