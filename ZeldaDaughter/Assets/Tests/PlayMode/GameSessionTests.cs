using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;
using TouchPhase = ZeldaDaughter.Core.Input.TouchPhase;

namespace ZeldaDaughter.Tests
{
    /// <summary>T-10, docs/done-criteria/T-10.md items 1–8 in the grey prologue.</summary>
    public class GameSessionTests
    {
        GameSession _s;
        HeroController _hero;

        IEnumerator LoadScene()
        {
            Application.runInBackground = true;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/prologue-grey.unity", new LoadSceneParameters(LoadSceneMode.Single)); // not in the player build since D-10 (region is the first scene)
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            Assert.NotNull(_s, "prologue-grey has a GameSession (SceneBuilder)");
            _hero.UseDpi(160f);
        }

        [UnitySetUp] public IEnumerator SetUp() { TestSaves.UseCleanFolder(); yield return LoadScene(); }
        [TearDown] public void TearDown() => TestSaves.Clear();

        [UnityTest]
        public IEnumerator Clock_drives_the_sun()
        {
            var sun = Object.FindFirstObjectByType<SunController>();
            _s.State.Clock.SetTime(1, 0.05);
            yield return null;
            float night = sun.Intensity, nightPitch = sun.Pitch; var nightTint = sun.Tint;
            _s.State.Clock.SetTime(1, 0.5);
            yield return null;
            float noon = sun.Intensity, noonPitch = sun.Pitch;
            // D-08: the night light is a dim blue moon (the road and silhouettes must read), not black: clearly darker than noon, and blue.
            Assert.Less(night, noon * 0.4f, $"night {night} vs noon {noon}");
            Assert.Greater(night, 0f, "the moon still lights the night");
            Assert.Greater(nightTint.b, nightTint.r + 0.2f, $"night light is blue: {nightTint}");
            Assert.Less(nightPitch + 20f, noonPitch, $"the sun is low at night ({nightPitch}°) and high at noon ({noonPitch}°)");
        }

        [UnityTest]
        public IEnumerator Swipe_hint_goes_after_a_swipe_and_stays_gone_after_reload()
        {
            yield return null;
            Assert.AreEqual(_s.State.Hints.TextOf("swipe"), _s.UI.CurrentHint);
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            var o = new Vec2(h.x, h.y - 250f);
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Time.realtimeSinceStartupAsDouble, o, TouchHit.Ground));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 40, o.Y), default));
            yield return null;
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 40, o.Y), default));
            yield return null;
            Assert.AreNotEqual(_s.State.Hints.TextOf("swipe"), _s.UI.CurrentHint);
            _s.Save("test");
            yield return LoadScene();
            yield return null;
            Assert.AreNotEqual(_s.State.Hints.TextOf("swipe"), _s.UI.CurrentHint);
        }

        [UnityTest]
        public IEnumerator Hungry_hero_says_so()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[ZD:Remark\] hunger_hungry: "));
            _s.State.Hunger.Restore(0.8f);
            yield return new WaitForSeconds(_s.State.Data.Session.RemarkCheckSeconds * 2 + 0.2f);
            Assert.IsFalse(string.IsNullOrEmpty(_s.UI.HeroBubbleText));
        }

        [UnityTest]
        public IEnumerator Picked_stick_goes_to_the_bag_and_does_not_come_back()
        {
            LogAssert.Expect(LogType.Log, "[ZD:Pickup] pickup_stick → stick");
            _s.Tap("pickup_stick");
            yield return null;
            Assert.AreEqual(1, _s.State.Bag.Count("stick"));
            Assert.IsFalse(GameObject.Find("Objects").transform.Find("pickup_stick").gameObject.activeSelf);
            Assert.AreEqual(_s.State.Data.Items["stick"].Pickup, _s.UI.HeroBubbleText);
            _s.Save("test");
            yield return LoadScene();
            Assert.IsFalse(GameObject.Find("Objects").transform.Find("pickup_stick").gameObject.activeSelf);
            Assert.AreEqual(1, _s.State.Bag.Count("stick"));
        }

        [UnityTest]
        public IEnumerator Talking_to_the_peasant_goes_by_icons_and_ends_pointing_at_the_town()
        {
            float before = _s.State.Language.Understanding;
            _s.Tap("npc_peasant");
            yield return null;
            var raw = _s.State.Data.Dialogues.Npcs["peasant"].Nodes["start"].Line;
            Assert.IsNotNull(_s.UI.NpcBubbleText);
            Assert.IsFalse(_s.UI.NpcBubbleText.Contains(raw), "stage 1: gibberish, not the line");
            Assert.IsTrue(_s.UI.ReplyButtons.Count > 0);
            LogAssert.Expect(LogType.Log, "[ZD:Talk] peasant point town_gate");
            for (int i = 0; i < 20 && _s.UI.ReplyButtons.Count > 0; i++)
            {
                var town = _s.UI.ReplyButtons.FirstOrDefault(b => b.name == "Reply_town") ?? _s.UI.ReplyButtons[0];
                town.onClick.Invoke();
                yield return null;
            }
            Assert.AreEqual(0, _s.UI.ReplyButtons.Count, "conversation ended");
            Assert.Greater(_s.State.Language.Understanding, before);
        }

        [UnityTest]
        public IEnumerator Reply_buttons_take_the_touch_not_the_hero()
        {
            _s.Tap("npc_peasant");
            yield return null;
            yield return null; // layout
            var b = _s.UI.ReplyButtons[0].GetComponent<RectTransform>();
            Vector3 c = b.position;
            var onButton = new Vec2(c.x, c.y);
            Assert.IsTrue(_hero.IsOverUI(onButton), "a reply button is under the finger");
            var ground = new Vec2(Screen.width * 0.5f, Screen.height * 0.4f);
            Assert.IsFalse(_hero.IsOverUI(ground), "open ground is not UI");

            // The same path as Update takes: a touch beginning over a button never reaches the hero's gestures.
            var where = _hero.transform.position;
            double Now() => Time.realtimeSinceStartupAsDouble;
            Assert.IsFalse(_hero.OnTouch(0, TouchPhase.Began, Now(), onButton), "the button takes the touch");
            _hero.OnTouch(0, TouchPhase.Moved, Now(), new Vec2(onButton.X + 120f, onButton.Y + 120f));
            for (int i = 0; i < 20; i++)
            {
                _hero.OnTouch(0, TouchPhase.Stationary, Now(), new Vec2(onButton.X + 120f, onButton.Y + 120f));
                yield return null;
            }
            _hero.OnTouch(0, TouchPhase.Ended, Now(), new Vec2(onButton.X + 120f, onButton.Y + 120f));
            Assert.IsFalse(_hero.IsMoving, "a drag that began on a button does not walk the hero");
            Assert.Less(Vector3.Distance(_hero.transform.position, where), 0.05f, "the hero stayed put");

            // Control: the same drag from open ground does start walking.
            Assert.IsTrue(_hero.OnTouch(0, TouchPhase.Began, Now(), ground), "open ground gives the touch to the hero");
            _hero.OnTouch(0, TouchPhase.Moved, Now(), new Vec2(ground.X + 60f, ground.Y + 60f));
            yield return null;
            Assert.IsTrue(_hero.IsMoving, "control: the drag from open ground walks");
            _hero.OnTouch(0, TouchPhase.Ended, Now(), new Vec2(ground.X + 60f, ground.Y + 60f));
        }

        [UnityTest]
        public IEnumerator Save_and_load_bring_back_place_time_inventory_and_knowledge()
        {
            var spawn = _hero.transform.position;
            var h = Camera.main.WorldToScreenPoint(_hero.transform.position);
            var o = new Vec2(h.x, h.y - 250f);
            _hero.Feed(new TouchSample(0, TouchPhase.Began, Time.realtimeSinceStartupAsDouble, o, TouchHit.Ground));
            _hero.Feed(new TouchSample(0, TouchPhase.Moved, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 28, o.Y + 28), default));
            float end = Time.time + 3f;
            while (Time.time < end) { _hero.Feed(new TouchSample(0, TouchPhase.Stationary, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 28, o.Y + 28), default)); yield return null; }
            _hero.Feed(new TouchSample(0, TouchPhase.Ended, Time.realtimeSinceStartupAsDouble, new Vec2(o.X + 28, o.Y + 28), default));
            yield return null;
            var where = _hero.transform.position;
            Assert.Greater(Vector3.Distance(where, spawn), 2f, "the hero walked away from the spawn before saving");
            _s.Tap("pickup_stick");
            _s.State.Language.Heard("peasant", "start");
            _s.State.Clock.SetTime(3, 0.4321);
            yield return null;
            float lang = _s.State.Language.Understanding;
            Assert.Greater(lang, 0f);
            LogAssert.Expect(LogType.Log, "[ZD:Save] saved test");
            _s.Save("test");

            // The file itself holds exactly what was set (the live clock moves on a little every frame).
            var onDisk = new Core.Save.GameState(_s.State.Data);
            Core.Save.SaveGame.Restore(onDisk, File.ReadAllText(_s.SlotPath));
            Assert.AreEqual(3, onDisk.Clock.Day);
            Assert.AreEqual(0.4321, onDisk.Clock.TimeOfDay, 0.0005);
            Assert.AreEqual(1, onDisk.Bag.Count("stick"));

            yield return LoadScene();
            Assert.Less(Vector3.Distance(_hero.transform.position, where), 0.2f, "same place");
            Assert.Greater(Vector3.Distance(_hero.transform.position, spawn), 2f, "not back at the spawn");
            Assert.AreEqual(3, _s.State.Clock.Day);
            Assert.AreEqual(0.4321, _s.State.Clock.TimeOfDay, 0.002);
            Assert.AreEqual(1, _s.State.Bag.Count("stick"), "inventory came back");
            Assert.AreEqual(lang, _s.State.Language.Understanding, 1e-4);
        }

        [UnityTest]
        public IEnumerator A_newer_save_is_refused_and_left_alone()
        {
            _s.Save("seed");
            string path = _s.SlotPath;
            string newer = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path), "\"Version\":\\s*\\d+", "\"Version\": 99");
            Assert.AreNotEqual(File.ReadAllText(path), newer, "the slot has a Version field to bump");
            File.WriteAllText(path, newer);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[ZD:Save\] slot refused"));
            yield return LoadScene();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[ZD:Save\] not saved"));
            _s.Save("again"); // logs a warning, writes nothing
            Assert.AreEqual(newer, File.ReadAllText(path), "the newer game's save is untouched");
        }
    }
}
