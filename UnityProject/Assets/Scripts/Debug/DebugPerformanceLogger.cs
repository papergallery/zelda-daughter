#if ZD_DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using UnityEngine.Profiling;

namespace ZeldaDaughter.Debugging
{
    public class DebugPerformanceLogger : MonoBehaviour
    {
        [SerializeField] private float _logInterval = 10f;
        [SerializeField] private int _fpsSampleCount = 30;

        private float _timer;
        private float _fpsAccumulator;
        private int _fpsSamples;

        private void Update()
        {
            // Накапливаем FPS-сэмплы
            _fpsAccumulator += 1f / Time.unscaledDeltaTime;
            _fpsSamples++;

            _timer += Time.unscaledDeltaTime;
            if (_timer < _logInterval) return;

            float fps = _fpsSamples > 0 ? _fpsAccumulator / _fpsSamples : 0f;
            long memBytes = Profiler.GetTotalAllocatedMemoryLong();
            long memMB = memBytes / (1024 * 1024);

            ZDLog.Log("Perf", $"FPS={fps:F0} mem={memMB}MB");

            _timer = 0f;
            _fpsAccumulator = 0f;
            _fpsSamples = 0;
        }
    }
}
#endif
