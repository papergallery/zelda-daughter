using UnityEngine;
using ZeldaDaughter.Debugging;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Entry point for the game. Configures target framerate and screen orientation.
    /// Attach to a GameObject in the starting scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int _targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = _targetFrameRate;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            ZDLog.Log("Boot", $"GameBootstrap initialized targetFPS={_targetFrameRate}");
        }
    }
}
