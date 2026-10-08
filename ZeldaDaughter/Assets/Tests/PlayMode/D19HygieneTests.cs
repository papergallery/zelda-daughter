using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-19: what one test changes in the shared world (data set, time scale) does not reach the next (<see cref="TestSaves.UseCleanFolder"/>).</summary>
    public class D19HygieneTests
    {
        static float _fromFile;

        [UnityTest, Order(1)]
        public IEnumerator A_test_may_break_the_shared_data_and_the_time_scale()
        {
            TestSaves.UseCleanFolder();
            _fromFile = GameData.Current.Session.RemarkCheckSeconds;
            Assert.Less(_fromFile, 100f, "the file's value, not a leftover");
            GameData.Current.Session.RemarkCheckSeconds = 1000f;
            GameData.Current.Night.SpawnIntervalSeconds = 0.3f;
            Time.timeScale = 0f;
            yield return null;
        }

        [UnityTest, Order(2)]
        public IEnumerator The_next_test_gets_the_data_from_the_files_and_the_normal_time()
        {
            TestSaves.UseCleanFolder(); // what every fixture's SetUp does
            yield return null;
            Assert.AreEqual(_fromFile, GameData.Current.Session.RemarkCheckSeconds, 1e-4f, "RemarkCheckSeconds is back");
            Assert.Greater(GameData.Current.Night.SpawnIntervalSeconds, 1f, "the wolf interval is back");
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
