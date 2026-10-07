using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-01, ADR-0008: the core stays free of the engine and of hidden clocks and randomness.</summary>
    public class CoreRulesTests
    {
        static string CoreDir => Path.Combine(TestPaths.CoreRoot, "ZeldaDaughter.Core");

        static string[] Sources() => Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories);

        [Fact]
        public void Core_has_no_engine_references()
        {
            var bad = Sources().Where(f => Regex.IsMatch(File.ReadAllText(f), @"\busing\s+Unity|\bUnityEngine\b")).ToArray();
            Assert.Empty(bad);
        }

        [Fact]
        public void Core_takes_time_and_randomness_from_outside()
        {
            // Determinism: the caller passes dt and a seeded random; tests replay exactly.
            var pattern = new Regex(@"\bnew\s+(System\.)?Random\s*\(|\bDateTime\.(Now|UtcNow|Today)\b|\bEnvironment\.TickCount\b|\bStopwatch\b|\bGuid\.NewGuid\b");
            var bad = Sources().Where(f => pattern.IsMatch(File.ReadAllText(f))).ToArray();
            Assert.Empty(bad);
        }

        [Fact]
        public void Asmdef_has_no_engine_references()
        {
            var asmdef = File.ReadAllText(Path.Combine(CoreDir, "ZeldaDaughter.Core.asmdef"));
            Assert.Matches("\"noEngineReferences\"\\s*:\\s*true", asmdef);
        }
    }
}
