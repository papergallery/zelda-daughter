using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-26b: bytes allocated by the scripts' own frame phases (Update, LateUpdate, FixedUpdate of every MonoBehaviour), counted on the main thread by
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/> between two markers inserted into the player loop around each phase.
    /// The profiler's "GC Allocated In Frame" counter also carries everything the editor does itself (the bridge, the Game view, the test runner): measured
    /// 2026-10-08 as ~270-300 KB per frame with every game component, light, renderer and camera switched off - against a test limit of 512 B per 120 frames.
    /// The editor's work happens outside these phases, so the number here is the game's alone (and what a phone would see from scripts).
    /// </summary>
    public static class ScriptPhaseAlloc
    {
        private static PlayerLoopSystem _saved;
        private static bool _on;
        private static long _begin;
        private static ProfilerRecorder _rec;
        public static long RecTotal { get; private set; }
        private static long _recBegin;

        /// <summary>Bytes allocated inside the wrapped phases since <see cref="Start"/>.</summary>
        public static long Total { get; private set; }

        public static void Start()
        {
            if (_on) return;
            Wrapped = 0;
            _saved = PlayerLoop.GetCurrentPlayerLoop();
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Wrap(ref loop, typeof(Update.ScriptRunBehaviourUpdate));
            Wrap(ref loop, typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate));
            Wrap(ref loop, typeof(FixedUpdate.ScriptRunBehaviourFixedUpdate));
            PlayerLoop.SetPlayerLoop(loop);
            _rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            Total = 0;
            RecTotal = 0;
            Calls = 0;
            _on = true;
        }

        public static void Stop()
        {
            if (!_on) return;
            PlayerLoop.SetPlayerLoop(_saved);
            _rec.Dispose();
            _on = false;
        }

        private struct Begin { }
        private struct End { }

        public static int Calls { get; private set; }
        public static int Wrapped { get; private set; }

        private static void OnBegin() { Calls++; _recBegin = _rec.CurrentValue; _begin = GC.GetAllocatedBytesForCurrentThread(); }
        private static void OnEnd() { RecTotal += _rec.CurrentValue - _recBegin; Total += GC.GetAllocatedBytesForCurrentThread() - _begin; }

        private static void Wrap(ref PlayerLoopSystem root, Type phase)
        {
            var phases = root.subSystemList;
            for (int p = 0; p < phases.Length; p++)
            {
                var subs = phases[p].subSystemList;
                if (subs == null) continue;
                for (int i = 0; i < subs.Length; i++)
                {
                    if (subs[i].type != phase) continue;
                    var list = new List<PlayerLoopSystem>(subs);
                    list.Insert(i + 1, new PlayerLoopSystem { type = typeof(End), updateDelegate = OnEnd });
                    list.Insert(i, new PlayerLoopSystem { type = typeof(Begin), updateDelegate = OnBegin });
                    phases[p].subSystemList = list.ToArray();
                    root.subSystemList = phases;
                    Wrapped++;
                    return;
                }
            }
        }
    }
}
