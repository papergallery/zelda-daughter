using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// T-06/T-07: player builds without touching project settings, materials or the render pipeline (April AndroidBuilder
    /// did all three). Debug builds get ZD_DEBUG and Development; release builds get neither. Output — C:\dev\zelda-builds\.
    /// Log: "[ZD:Build] &lt;target&gt; &lt;kind&gt; result=… size=…MB time=…s path=…".
    /// </summary>
    public static class BuildScript
    {
        static string OutRoot => Path.GetFullPath(Path.Combine("..", "..", "zelda-builds"));

        /// <summary>D-19: both release builds in one command (Windows, then the Android APK, IL2CPP ARM64 by ProjectSetup); one [ZD:Build] line each.</summary>
        public static string ReleaseAll() => WindowsRelease() + "\n" + AndroidRelease();

        public static string WindowsRelease() => Build(BuildTarget.StandaloneWindows64, false);
        public static string WindowsDebug() => Build(BuildTarget.StandaloneWindows64, true);
        public static string AndroidRelease() => Build(BuildTarget.Android, false);
        public static string AndroidDebug() => Build(BuildTarget.Android, true);

        /// <summary>Release build of the grey prologue only — for the author to try with a mouse (drag = finger).</summary>
        public static string WindowsPlaytest() => Build(BuildTarget.StandaloneWindows64, false, new[] { "Assets/Scenes/prologue-grey.unity" }, "playtest");

        /// <summary>D-19: a release build (no Development flag, so no profiler socket and no firewall window) with ZD_DEBUG — only for PerfProbe (`-zd-perf-seconds N`). Never shipped.</summary>
        public static string WindowsPerf() => Build(BuildTarget.StandaloneWindows64, false, null, "perf", true);

        public static string Build(BuildTarget target, bool debug) => Build(target, debug, null, null);

        public static string Build(BuildTarget target, bool debug, string[] scenes, string folder) => Build(target, debug, scenes, folder, false);

        public static string Build(BuildTarget target, bool debug, string[] scenes, string folder, bool perfDefine)
        {
            DataSync.Sync();
            // Scenes are built from config as their own step (SceneBuilder.BuildAll) and committed: rebuilding here would give
            // every build new fileIDs in the .unity files and a dirty working copy (2026-10-08).
            var missing = EditorBuildSettings.scenes.Where(sc => sc.enabled && !File.Exists(sc.path)).Select(sc => sc.path).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException("[ZD:Build] scenes missing — run SceneBuilder.BuildAll: " + string.Join(", ", missing));
            string kind = debug ? "debug" : perfDefine ? "perf" : "release";
            string dir = Path.Combine(OutRoot, folder ?? $"{target}-{kind}");
            string file = target == BuildTarget.Android ? "ZeldaDaughter.apk" : "ZeldaDaughter.exe";
            var options = new BuildPlayerOptions
            {
                scenes = scenes ?? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(dir, file),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = debug ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = debug || perfDefine ? new[] { "ZD_DEBUG" } : Array.Empty<string>(),
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded) BuildBudget.Log(report, dir);   // D-26 п. 12: what the build weighs
            string line = $"[ZD:Build] {target} {kind} result={s.result} size={s.totalSize / (1024f * 1024f):0.0}MB time={s.totalTime.TotalSeconds:0}s path={options.locationPathName}";
            if (s.result == BuildResult.Succeeded) UnityEngine.Debug.Log(line);
            else UnityEngine.Debug.LogError(line + $" errors={s.totalErrors}");
            return line;
        }
    }
}
