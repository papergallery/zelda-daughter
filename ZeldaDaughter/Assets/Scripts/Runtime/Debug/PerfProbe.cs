#if ZD_DEBUG
using System;
using UnityEngine;
using UnityEngine.Profiling;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.DebugTools
{
    /// <summary>
    /// Debug builds only (T-06/T-08, D-19): every 5 s "[ZD:Perf] fps=… low=… mem=…MB" and once "[ZD:Perf] first_frame=…s" (seconds since the engine
    /// started, the first frame after the scene loaded and the hero stands).
    /// With <c>-zd-perf-seconds N</c> on the command line it also walks the heroine east along the road for N seconds, writes
    /// "[ZD:Perf] summary frames=… avg_ms=… avg_fps=… worst1pct_ms=… max_ms=… mem=…MB" (the worst 1 % — the mean of the slowest hundredth of the
    /// frames) and quits — the measure of docs/test-runs/…-demo-perf.md. Starts by itself — no scene wiring, nothing of it in a release build.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        private const float Window = 5f;
        private const int Capacity = 120000;
        private float _start;
        private int _frames;
        private float _worst;
        private bool _firstLogged;
        private float _walkSeconds;
        private float _walkStart;
        private float[] _times;
        private int _count;
        private HeroController _hero;
        private CharacterController _cc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[ZD] PerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfProbe>();
        }

        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-zd-perf-seconds" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s))
                    _walkSeconds = s;
            if (_walkSeconds > 0f)
            {
                _times = new float[Capacity];
                Application.runInBackground = true; // a window that lost focus must not pause the measure
                QualitySettings.vSyncCount = 0;     // the cost of a frame, not the monitor's refresh rate
                Application.targetFrameRate = -1;
            }
        }

        private void Update()
        {
            if (!_firstLogged)
            {
                _firstLogged = true;
                ZdLog.Info("Perf", $"first_frame={Time.realtimeSinceStartup:0.00}s");
                _start = Time.unscaledTime;
                _walkStart = _start;
                return;
            }
            _frames++;
            float dt = Time.unscaledDeltaTime;
            if (dt > _worst) _worst = dt;
            if (_times != null && _count < _times.Length) _times[_count++] = dt;
            float elapsed = Time.unscaledTime - _start;
            if (_walkSeconds > 0f) Walk(dt);
            if (elapsed >= Window)
            {
                float mb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
                ZdLog.Info("Perf", $"fps={_frames / elapsed:0.0} low={(_worst > 0 ? 1f / _worst : 0):0.0} mem={mb:0}MB");
                _start = Time.unscaledTime;
                _frames = 0;
                _worst = 0;
            }
            if (_walkSeconds > 0f && Time.unscaledTime - _walkStart >= _walkSeconds) Finish();
        }

        private void Walk(float dt)
        {
            if (_hero == null) { _hero = FindFirstObjectByType<HeroController>(); if (_hero != null) _cc = _hero.GetComponent<CharacterController>(); }
            if (_cc == null) return;
            _cc.Move(new Vector3(2.5f * dt, -1f * dt, 0f));
        }

        private void Finish()
        {
            int n = _count;
            Array.Sort(_times, 0, n);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += _times[i];
            int tail = Mathf.Max(1, n / 100);
            double worst = 0;
            for (int i = n - tail; i < n; i++) worst += _times[i];
            worst /= tail;
            double avg = sum / Mathf.Max(1, n);
            float mb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            float reserved = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);
            ZdLog.Info("Perf", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "summary frames={0} avg_ms={1:0.00} avg_fps={2:0.0} worst1pct_ms={3:0.00} worst1pct_fps={4:0.0} max_ms={5:0.00} mem={6:0}MB reserved={7:0}MB x={8:0.0}",
                n, avg * 1000, 1 / avg, worst * 1000, 1 / worst, _times[n - 1] * 1000, mb, reserved, _hero != null ? _hero.transform.position.x : 0f));
            Application.Quit();
        }
    }
}
#endif
