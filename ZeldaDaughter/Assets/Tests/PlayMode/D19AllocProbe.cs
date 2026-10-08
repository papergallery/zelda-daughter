using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-19 diagnostic: which component types allocate in idle frames (disable one type at a time). Found BardSource (a boxed enumerator in Update, 32 B per frame): the W0 idle test was red for that, not for the order of tests.</summary>
    public class D19AllocProbe
    {
        static IEnumerator Measure(int frames, System.Action<long> result)
        {
            long total = 0;
            using (var rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                for (int i = 0; i < frames; i++) { yield return null; total += rec.LastValue; }
            }
            result(total);
        }

        [UnityTest, Explicit("diagnostic: run by hand, the result is the [ZD:AllocProbe] line in the log")]
        public IEnumerator Which_types_allocate()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            GameData.Current.Session.RemarkCheckSeconds = 1000f;
            yield return new WaitForSeconds(1.3f);
            for (int i = 0; i < 60; i++) yield return null;
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m.enabled && m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("ZeldaDaughter")).ToList();
            var sb = new StringBuilder();
            long baseline = 0;
            yield return Measure(120, t => baseline = t);
            baseline = long.MaxValue;
            for (int k = 0; k < 3; k++) { long x = 0; yield return Measure(120, t => x = t); baseline = System.Math.Min(baseline, x); }
            sb.AppendLine($"baseline all on: {baseline}");
            foreach (var g in all.GroupBy(m => m.GetType()))
            {
                foreach (var m in g) m.enabled = false;
                for (int i = 0; i < 5; i++) yield return null;
                long x = 0; yield return Measure(120, t => x = t);
                foreach (var m in g) m.enabled = true;
                for (int i = 0; i < 5; i++) yield return null;
                sb.AppendLine($"  {g.Key.Name} x{g.Count()}: off -> {x} (saves {baseline - x} B)");
            }
            foreach (var m in all) m.enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
            long bare = long.MaxValue;
            for (int k = 0; k < 3; k++) { long x = 0; yield return Measure(120, t => x = t); bare = System.Math.Min(bare, x); }
            foreach (var m in all) m.enabled = true;
            sb.AppendLine($"bare (all off, min of 3): {bare}");
            Debug.Log("[ZD:AllocProbe]\n" + sb);
        }

        /// <summary>D-26b: the exact per-component number. The profiler counter carries the editor's own noise (+-1 MB); here every Update/LateUpdate/FixedUpdate of every ZeldaDaughter component is called once more by hand
        /// and the bytes are counted on this thread (GC.GetAllocatedBytesForCurrentThread) around the call alone.</summary>
        [UnityTest, Explicit("diagnostic: run by hand, the result is the [ZD:AllocProbe] line in the log")]
        public IEnumerator Exact_bytes_per_component_method()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            GameData.Current.Session.RemarkCheckSeconds = 1000f;
            yield return new WaitForSeconds(1.3f);
            for (int i = 0; i < 60; i++) yield return null;
            var comps = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m.enabled && m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("ZeldaDaughter")).ToList();
            var calls = new System.Collections.Generic.List<(string name, System.Action act)>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var m in comps)
                foreach (var n in new[] { "Update", "LateUpdate", "FixedUpdate" })
                {
                    var mi = m.GetType().GetMethod(n, flags);
                    if (mi == null || mi.GetParameters().Length != 0) continue;
                    calls.Add((m.GetType().Name + "." + n, (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), m, mi)));
                }
            var sum = new long[calls.Count];
            for (int f = 0; f < 240; f++)
            {
                yield return null;
                for (int i = 0; i < calls.Count; i++)
                {
                    long b = System.GC.GetAllocatedBytesForCurrentThread();
                    calls[i].act();
                    long used = System.GC.GetAllocatedBytesForCurrentThread() - b;
                    if (f >= 30) sum[i] += used;   // the first frames: first-use caches
                }
            }
            var sb = new StringBuilder("exact bytes over 210 idle frames, extra call per frame\n");
            long all = 0;
            for (int i = 0; i < calls.Count; i++) { all += sum[i]; if (sum[i] > 0) sb.AppendLine($"  {calls[i].name}: {sum[i]} B"); }
            sb.AppendLine($"total {all} B in {calls.Count} methods");
            Debug.Log("[ZD:AllocProbe]\n" + sb);
        }
    }
}
