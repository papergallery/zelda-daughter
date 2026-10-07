using System.IO;
using System.Text.Json;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>Reads data/*.json the way the game will (numbers live in data, not in code — ADR-0008).</summary>
    static class TestData
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true };

        public static T Load<T>(string file) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(TestPaths.DataRoot, file)), Options)
            ?? throw new InvalidDataException(file);
    }
}
