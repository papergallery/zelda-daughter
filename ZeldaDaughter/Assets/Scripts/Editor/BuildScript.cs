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

        public static string WindowsRelease() => Build(BuildTarget.StandaloneWindows64, false);
        public static string WindowsDebug() => Build(BuildTarget.StandaloneWindows64, true);
        public static string AndroidRelease() => Build(BuildTarget.Android, false);
        public static string AndroidDebug() => Build(BuildTarget.Android, true);

        public static string Build(BuildTarget target, bool debug)
        {
            DataSync.Sync();
            SceneBuilder.BuildAll();
            string kind = debug ? "debug" : "release";
            string dir = Path.Combine(OutRoot, $"{target}-{kind}");
            string file = target == BuildTarget.Android ? "ZeldaDaughter.apk" : "ZeldaDaughter.exe";
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(dir, file),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = debug ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = debug ? new[] { "ZD_DEBUG" } : Array.Empty<string>(),
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string line = $"[ZD:Build] {target} {kind} result={s.result} size={s.totalSize / (1024f * 1024f):0.0}MB time={s.totalTime.TotalSeconds:0}s path={options.locationPathName}";
            if (s.result == BuildResult.Succeeded) UnityEngine.Debug.Log(line);
            else UnityEngine.Debug.LogError(line + $" errors={s.totalErrors}");
            return line;
        }
    }
}
