using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-26 п. 12 (docs/demo/best-practices-feel.md: «Бюджет сборки»): what a build weighs and what weighs it. After every player build
    /// <see cref="Log"/> reads the Build Report — the packed assets by folder (our art, <c>ThirdParty</c> pack by pack, the engine) and by type, the
    /// biggest ones — writes <c>build-report.txt</c> next to the build and prints a few <c>[ZD:Build] budget …</c> lines. The target for the demo's APK is
    /// under <see cref="ApkTargetMb"/> MB (a cheaper install: every +6 MB ≈ −1 % conversion, Google Play 2017). Nothing here changes the project: it only reads.
    /// </summary>
    public static class BuildBudget
    {
        public const float ApkTargetMb = 150f;

        /// <summary>The packed assets of the report grouped for reading; the same text is returned and written to <paramref name="outFolder"/>/build-report.txt.</summary>
        public static string Log(BuildReport report, string outFolder)
        {
            var sb = new StringBuilder();
            var s = report.summary;
            float total = s.totalSize / (1024f * 1024f);
            sb.AppendLine($"[ZD:Build] budget {s.platform} total={total:0.0}MB target<{ApkTargetMb:0}MB {(s.platform == BuildTarget.Android ? (total < ApkTargetMb ? "OK" : "OVER") : "(not Android)")}");

#pragma warning disable CS0618, CS0612
            var rows = new List<(string path, string type, ulong bytes)>();
            foreach (var pa in report.packedAssets)
                foreach (var c in pa.contents)
                    rows.Add((c.sourceAssetPath ?? "", c.type != null ? c.type.Name : "?", c.packedSize));
#pragma warning restore CS0618, CS0612

            ulong packed = 0;
            foreach (var r in rows) packed += r.bytes;
            sb.AppendLine($"[ZD:Build] budget packed assets={packed / 1048576f:0.0}MB in {rows.Count} entries (the rest of the file is the engine, code and the APK's own structure)");

            foreach (var line in Group(rows, r => Folder(r.path), 14, "folder")) sb.AppendLine(line);
            foreach (var line in Group(rows, r => r.type, 10, "type")) sb.AppendLine(line);

            sb.AppendLine("[ZD:Build] budget biggest assets:");
            foreach (var r in rows.OrderByDescending(x => x.bytes).Take(25))
                sb.AppendLine($"[ZD:Build] budget   {r.bytes / 1048576f,7:0.00}MB {r.type,-14} {r.path}");

            // purchased packs that got into the build: pack by pack
            var packs = rows.Where(r => r.path.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal))
                .GroupBy(r => r.path.Split('/').Skip(2).FirstOrDefault() ?? "?")
                .Select(g => (name: g.Key, bytes: (ulong)g.Sum(x => (long)x.bytes), count: g.Count()))
                .OrderByDescending(x => x.bytes);
            ulong third = 0;
            foreach (var p in packs) third += p.bytes;
            sb.AppendLine($"[ZD:Build] budget ThirdParty in the build: {third / 1048576f:0.0}MB");
            foreach (var p in packs.Take(12)) sb.AppendLine($"[ZD:Build] budget   {p.bytes / 1048576f,7:0.00}MB {p.count,5} files  {p.name}");

            string text = sb.ToString();
            try
            {
                Directory.CreateDirectory(outFolder);
                File.WriteAllText(Path.Combine(outFolder, "build-report.txt"), text);
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[ZD:Build] budget: could not write build-report.txt: " + e.Message); }
            foreach (var line in text.Split('\n')) if (line.Trim().Length > 0) UnityEngine.Debug.Log(line.TrimEnd());
            return text;
        }

        // "Assets/ThirdParty/Pack/…" → "ThirdParty/Pack"; "Assets/Art/Sprites/x/…" → "Art/Sprites"; engine resources have no path
        static string Folder(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(engine/built-in)";
            var parts = path.Split('/');
            if (parts.Length >= 3 && parts[0] == "Assets" && parts[1] == "ThirdParty") return "ThirdParty/" + parts[2];
            if (parts.Length >= 3 && parts[0] == "Assets" && (parts[1] == "Art" || parts[1] == "Resources")) return parts[1] + "/" + parts[2];
            if (parts.Length >= 2 && parts[0] == "Assets") return parts[1];
            return parts[0] == "Packages" && parts.Length >= 2 ? "Packages/" + parts[1] : parts[0];
        }

        static IEnumerable<string> Group(List<(string path, string type, ulong bytes)> rows, Func<(string path, string type, ulong bytes), string> key, int take, string what)
        {
            foreach (var g in rows.GroupBy(key).Select(g => (name: g.Key, bytes: (ulong)g.Sum(x => (long)x.bytes), count: g.Count())).OrderByDescending(x => x.bytes).Take(take))
                yield return $"[ZD:Build] budget by {what} {g.bytes / 1048576f,7:0.00}MB {g.count,5}  {g.name}";
        }
    }
}
