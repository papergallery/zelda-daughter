using System.IO;
using UnityEngine;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.Tests
{
    /// <summary>Tests save into their own folder, never into the player's slot (D-00). The override is reset on every Play Mode start.</summary>
    public static class TestSaves
    {
        public static string Root => Path.Combine(Application.temporaryCachePath, "zd-test-saves");

        /// <summary>Point the game at the test folder and empty it — call before loading a scene.</summary>
        public static void UseCleanFolder()
        {
            GameSession.SaveRootOverride = Root;
            Clear();
        }

        public static void Clear()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            Directory.CreateDirectory(Root);
        }
    }
}
