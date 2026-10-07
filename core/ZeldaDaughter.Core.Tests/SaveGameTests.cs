using System.IO;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-13: project-design.md §6 «Система сохранения» — same place, same state, one slot.</summary>
    public class SaveGameTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState Played()
        {
            var g = new GameState(D) { Zone = "g1-capsule", HeroPosition = new Vec2(3.5f, -7.25f), HeroHeight = 1f, HeroFacingDegrees = 135 };
            g.Clock.Advance(700);
            g.Condition.Wound(WoundType.Fracture, 0.6f);
            g.Condition.Damage(25);
            g.Hunger.Advance(900);
            for (int i = 0; i < 40; i++) g.Skills.Apply(SkillEvent.Attack(WeaponClass.Blunt, i % 3 == 0));
            g.Bag.Add("stick", 12); g.Bag.Add("knife"); g.Bag.Add("cloth", 2);
            g.Crafting.Combine("knife", "stick", g.Bag);
            g.Language.Heard("peasant", "l1"); g.Language.Heard("guard", "l2");
            g.Hints.Did("swipe");
            g.Picked.Add("pickup_stick");
            return g;
        }

        [Fact]
        public void Round_trip_restores_the_same_state()
        {
            string first = SaveGame.Capture(Played());
            var fresh = new GameState(D);
            SaveGame.Restore(fresh, first);
            Assert.Equal(first, SaveGame.Capture(fresh));
            Assert.Equal(0.6f, fresh.Condition.Severity(WoundType.Fracture), 4);
            Assert.Equal(11, fresh.Bag.Count("stick"));
            Assert.Equal("g1-capsule", fresh.Zone);
            Assert.Equal(3.5f, fresh.HeroPosition.X);
            Assert.Null(fresh.Hints.Visible);
            Assert.Contains("pickup_stick", fresh.Picked);
        }

        [Fact]
        public void Save_from_a_newer_game_is_refused()
        {
            var json = SaveGame.Capture(new GameState(D)).Replace($"\"Version\": {SaveGame.Version}", "\"Version\": 99");
            Assert.Throws<InvalidDataException>(() => SaveGame.Restore(new GameState(D), json));
        }

        [Fact]
        public void Atomic_write_keeps_the_previous_slot_as_backup_and_survives_a_broken_file()
        {
            string dir = Path.Combine(Path.GetTempPath(), "zd-save-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string slot = Path.Combine(dir, "slot.json");
                string a = SaveGame.Capture(new GameState(D));
                string b = SaveGame.Capture(Played());
                SaveGame.WriteAtomic(slot, a);
                SaveGame.WriteAtomic(slot, b);
                Assert.Equal(b, File.ReadAllText(slot));
                Assert.Equal(a, File.ReadAllText(slot + ".bak"));
                File.WriteAllText(slot, "{ broken by a crash");
                Assert.Equal(a, SaveGame.ReadSlot(slot));
                Assert.False(File.Exists(slot + ".tmp"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
