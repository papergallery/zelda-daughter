using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.UI;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>D-18 (docs/demo/unity-architecture.md §7, docs/done-criteria/D-18.md): the hand of the hints, the hero's cloud, no numbers outside the windows. Scene test-demo.</summary>
    public class D18HintsTests
    {
        GameSession _s;
        HeroController _hero;
        HintView _hints;
        RemarkBubble _bubble;
        float _seconds, _check;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _hints = Object.FindFirstObjectByType<HintView>();
            _bubble = Object.FindFirstObjectByType<RemarkBubble>();
            Assert.NotNull(_s, "test-demo has a GameSession");
            Assert.NotNull(_hints, "SceneBuilder.Hints puts a HintView on Game");
            Assert.NotNull(_bubble, "SceneBuilder.Hints puts a RemarkBubble on Game");
            _hero.UseDpi(160f);
            _seconds = _s.State.Data.Session.RemarkBubbleSeconds;
            _check = _s.State.Data.Session.RemarkCheckSeconds;
            yield return new WaitForSeconds(0.4f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_s != null)
            {
                _s.State.Data.Session.RemarkBubbleSeconds = _seconds; // the data set is shared by all tests
                _s.State.Data.Session.RemarkCheckSeconds = _check;
            }
            TestSaves.Clear();
        }

        IEnumerator Swipe()
        {
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            var o = new Vec2(h.x, h.y - 250f);
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Time.realtimeSinceStartupAsDouble, o, TouchHit.Ground));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 40, o.Y), default));
            yield return null;
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 40, o.Y), default));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Swipe_hand_is_at_the_lower_edge_and_goes_after_a_swipe()
        {
            yield return null;
            Assert.AreEqual("swipe", _hints.ShownHint);
            Assert.IsTrue(_hints.HandActive);
            Assert.Greater(_hints.HandAlpha, 0.2f, "the hand is visible");
            Assert.Less(_hints.HandScreenPoint.y, Screen.height * 0.35f, "near the lower edge");
            Assert.IsFalse(_hints.HandImage.raycastTarget, "the hint never takes a touch");

            yield return Swipe();
            Assert.IsNull(_hints.ShownHint, "gone at once after the swipe");
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(_hints.HandActive, "faded out");
            Assert.IsNull(_hints.ShownHint, "and does not come back");
        }

        [UnityTest]
        public IEnumerator Tap_hand_is_at_the_nearest_thing_and_goes_after_a_tap()
        {
            _s.State.Hints.Did("swipe");
            _hero.Teleport(new Vector3(2f, 1f, 9.3f), 0f); // 0.7 m from the cloth at (2, 0, 10); the NPCs walk, so the target is a pick-up
            _s.State.Data.Session.RemarkCheckSeconds = 0.1f;
            yield return new WaitForSeconds(1.6f); // the pending check was set from the old interval
            Assert.AreEqual("tap", _hints.ShownHint);
            Assert.NotNull(_hints.TapTarget);
            Assert.AreEqual("pickup_cloth_1", _hints.TapTarget.Id);
            var want = (Vector2)Camera.main.WorldToScreenPoint(_hints.TapTarget.AimPoint);
            // D-26: the hand pokes — it comes up to the target from below and goes back; within one poke (1.4 s) the fingertip touches it
            float nearest = float.MaxValue;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                want = (Vector2)Camera.main.WorldToScreenPoint(_hints.TapTarget.AimPoint);
                nearest = Mathf.Min(nearest, Vector2.Distance(want, _hints.HandScreenPoint));
                yield return null;
            }
            Assert.Less(nearest, 40f, $"the fingertip touches the target {want} (nearest {nearest:0} px)");

            _s.Tap("pickup_cloth_1");
            yield return null;
            Assert.AreNotEqual("tap", _hints.ShownHint, "the tap hint is done (the cloth is in the bag, so the next hint, long press, may follow)");
        }

        [UnityTest]
        public IEnumerator Long_press_hand_is_on_the_hero_once_there_is_an_item()
        {
            _s.State.Hints.Did("swipe");
            _s.State.Hints.Did("tap");
            yield return null;
            Assert.IsNull(_hints.ShownHint, "no item yet");
            _s.State.Bag.Add("stick");
            _s.BagChanged("test");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual("long_press", _hints.ShownHint);
            var hero = (Vector2)Camera.main.WorldToScreenPoint(_hero.transform.position);
            Assert.Less(Vector2.Distance(hero, _hints.HandScreenPoint), Screen.height * 0.15f, "on the hero");

            _s.State.Hints.Did("long_press_hero");
            yield return new WaitForSeconds(0.6f);
            Assert.IsNull(_hints.ShownHint);
            Assert.IsFalse(_hints.HandActive);
        }

        [UnityTest]
        public IEnumerator Remark_cloud_fades_after_the_data_seconds_in_the_hand_written_font()
        {
            _s.State.Data.Session.RemarkBubbleSeconds = 0.6f;
            _s.Events.RaiseHeroSaid("test", "Есть хочется.");
            yield return null;
            Assert.AreEqual("Есть хочется.", _bubble.Text);
            Assert.AreEqual("Есть хочется.", _s.UI.HeroBubbleText, "T-10's facade still answers");
            var look = _s.UI.Look;
            if (look != null && look.Font != null)
                Assert.AreSame(look.Font, _bubble.Root.GetComponentInChildren<TextMeshProUGUI>(true).font, "the font of the look (Neucha)");
            Assert.IsFalse(_bubble.Root.GetComponent<Graphic>().raycastTarget);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("Есть хочется.", _bubble.Text, "still there before the time is up");
            yield return new WaitForSeconds(0.6f);
            Assert.IsNull(_bubble.Text, "gone after RemarkBubbleSeconds");
        }

        [UnityTest]
        public IEnumerator Hero_does_not_speak_more_often_than_the_core_pauses_allow()
        {
            int said = 0;
            _s.Events.HeroSaid += (t, l) => said++;
            _s.Say("craft_ok");
            bool second = _s.Say("craft_fail");
            _s.Say("craft_ok");
            yield return null;
            Assert.IsFalse(second, "GlobalGapSeconds holds the second line back");
            Assert.LessOrEqual(said, 1);
        }

        [UnityTest]
        public IEnumerator No_numbers_and_no_bars_outside_the_windows()
        {
            // everything the hero may see on the world layer: hint, cloud, replies, a talk bubble, a long line
            _s.Events.RaiseHeroSaid("test", "Голодна.");
            _s.UI.NpcSay(_hero.transform, "Дорога на запад.", 3f);
            yield return null;
            var ui = _s.UI;
            foreach (var layer in new[] { ui.World, ui.Overlay })
            {
                foreach (var t in layer.GetComponentsInChildren<TextMeshProUGUI>(true))
                    StringAssert.DoesNotMatch(@"\d", t.text ?? "", $"digits in {t.transform.name}: '{t.text}'");
                foreach (var sl in layer.GetComponentsInChildren<Slider>(true)) Assert.Fail("a slider (a bar) in " + sl.name);
                foreach (var img in layer.GetComponentsInChildren<Image>(true))
                    Assert.AreNotEqual(Image.Type.Filled, img.type, "a filled image (a bar) in " + img.name);
            }
            foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                StringAssert.DoesNotMatch(@"\d", t.text ?? "", $"digits in the world text {t.name}");
        }
    }
}
