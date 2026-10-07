#if ZD_DEBUG
using UnityEngine;
using UnityEngine.Profiling;

namespace ZeldaDaughter.DebugTools
{
    /// <summary>
    /// Debug builds only (T-06/T-08): every 5 s "[ZD:Perf] fps=… low=… mem=…MB" and once "[ZD:Perf] first_frame=…s".
    /// Starts by itself — no scene wiring, nothing of it in a release build.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        private const float Window = 5f;
        private float _start;
        private int _frames;
        private float _worst;
        private bool _firstLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[ZD] PerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfProbe>();
        }

        private void Update()
        {
            if (!_firstLogged)
            {
                _firstLogged = true;
                ZdLog.Info("Perf", $"first_frame={Time.realtimeSinceStartup:0.00}s");
                _start = Time.unscaledTime;
                return;
            }
            _frames++;
            if (Time.unscaledDeltaTime > _worst) _worst = Time.unscaledDeltaTime;
            float elapsed = Time.unscaledTime - _start;
            if (elapsed < Window) return;
            float mb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            ZdLog.Info("Perf", $"fps={_frames / elapsed:0.0} low={(_worst > 0 ? 1f / _worst : 0):0.0} mem={mb:0}MB");
            _start = Time.unscaledTime;
            _frames = 0;
            _worst = 0;
        }
    }
}
#endif
