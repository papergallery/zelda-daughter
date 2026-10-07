using System.IO;

namespace ZeldaDaughter.Core.Tests
{
    static class TestPaths
    {
        /// <summary>core/ — found by walking up from the test binary to the folder holding ZeldaDaughter.sln.</summary>
        public static string CoreRoot
        {
            get
            {
                var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ZeldaDaughter.sln")))
                    dir = dir.Parent;
                return dir?.FullName ?? throw new DirectoryNotFoundException("core/ZeldaDaughter.sln not found above " + System.AppContext.BaseDirectory);
            }
        }

        /// <summary>data/ at the repository root.</summary>
        public static string DataRoot => Path.Combine(Directory.GetParent(CoreRoot)!.FullName, "data");
    }
}
