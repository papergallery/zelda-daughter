using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// data/*.json (repository root) → Assets/Resources/Data/*.json, so the game loads the same files as the server tests.
    /// On editor load and before every build; the copy is git-ignored and never edited by hand.
    /// </summary>
    [InitializeOnLoad]
    public sealed class DataSync : IPreprocessBuildWithReport
    {
        const string Source = "../data";
        const string Target = "Assets/Resources/Data";

        static DataSync() => EditorApplication.delayCall += () => Sync();

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Sync();

        [MenuItem("Zelda/Data/Sync data folder")]
        public static int Sync()
        {
            if (!Directory.Exists(Source)) return 0;
            Directory.CreateDirectory(Target);
            int changed = 0;
            foreach (var src in Directory.GetFiles(Source, "*.json"))
            {
                string dst = Path.Combine(Target, Path.GetFileName(src));
                string text = File.ReadAllText(src);
                if (File.Exists(dst) && File.ReadAllText(dst) == text) continue;
                File.WriteAllText(dst, text);
                changed++;
            }
            if (changed > 0)
            {
                AssetDatabase.Refresh();
                ZeldaDaughter.GameData.Reset();
                UnityEngine.Debug.Log($"[ZD:Data] synced {changed} file(s) to {Target}");
            }
            return changed;
        }
    }
}
