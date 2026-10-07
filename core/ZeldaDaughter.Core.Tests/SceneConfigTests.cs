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
    }
}
