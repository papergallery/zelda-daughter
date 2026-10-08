using System.IO;
using UnityEngine;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.Tests
{
    /// <summary>Tests save into their own folder, never into the player's slot (D-00). The override is reset on every Play Mode start.</summary>
    public static class TestSaves
    {
        public static string Root => Path.Combine(Application.temporaryCachePath, "zd-test-saves");

        /// <summary>
        /// Point the game at the test folder and empty it — call before loading a scene (every fixture's SetUp does).
        /// D-19: also a world nobody has touched. <see cref="GameData.Current"/> is one static data set shared by all scenes; tests turn its knobs
        /// (RemarkCheckSeconds = 1000, a wolf every 0.3 s, a fire in 4 s) and some never gave them back — then
        /// <c>GameSessionTests.Hungry_hero_says_so</c> waited 2×1000 s for a remark (the "hang" of the full run), the hint hand never came
        /// (it is set by the same check) and the idle-allocation tests measured the wolves and the rain of other tests.
        /// (An assembly-wide ITestAction is not run for UnityTest here — checked 2026-10-08.)
        /// </summary>
        public static void UseCleanFolder()
        {
            GameSession.SaveRootOverride = Root;
            Clear();
            GameData.Reset();
            Time.timeScale = 1f;
        }

        public static void Clear()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            Directory.CreateDirectory(Root);
        }
    }
}
