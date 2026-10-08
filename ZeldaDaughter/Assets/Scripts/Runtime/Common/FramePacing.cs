using UnityEngine;

namespace ZeldaDaughter
{
    /// <summary>
    /// D-26: Unity on Android draws a fixed 30 fps when <c>targetFrameRate</c> is -1 and vSync is 0 (Unity 6 ScriptReference), which adds up to 33 ms
    /// to the touch screen's own 65–120 ms of delay. The game asks for 60 (data/combat-feel.json) and switches vSync off so the number is honoured;
    /// «Optimized Frame Pacing» (Swappy) is a Player Setting set by ProjectSetup.
    /// </summary>
    public static class FramePacing
    {
        public static int Target { get; private set; }

        public static void Apply(int target)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = target;
            Target = target;
            ZdLog.Info("Perf", $"target={target}");
        }
    }
}
