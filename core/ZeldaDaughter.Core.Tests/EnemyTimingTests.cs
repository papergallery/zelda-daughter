using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C6: the view reads how long an enemy has been in its state and how far its windup has come (the readable swing, §6).</summary>
    public class EnemyTimingTests
    {
        static readonly WeaponSettings W = TestData.Load<WeaponSettings>("weapons.json");
        static readonly EnemySettings E = TestData.Load<EnemySettings>("enemies.json");
        static readonly SkillSettings SK = TestData.Load<SkillSettings>("skills.json");
        static readonly WoundSettings WS = TestData.Load<WoundSettings>("wounds.json");

        const float Dt = 0.05f;

        static HeroCombat Hero(float x) => new HeroCombat(W, new Skills(SK), new HeroCondition(WS)) { Position = new Vec2(x, 0) };

        [Fact]
        public void A_new_enemy_has_zero_time_in_state_and_no_windup()
        {
            var e = new Enemy("e", E, "wolf", new Vec2(0, 0));
            Assert.Equal(0f, e.StateSeconds);
            Assert.Equal(0f, e.WindupProgress);
        }

        [Fact]
        public void State_seconds_count_up_and_start_over_on_a_state_change()
        {
            var e = new Enemy("e", E, "wolf", new Vec2(100, 0));
            var hero = Hero(0);
            for (int i = 0; i < 10; i++) e.Tick(Dt, hero, 0.3f);
            Assert.Equal(EnemyState.Idle, e.State);
            Assert.Equal(10 * Dt, e.StateSeconds, 3);
            for (int i = 0; i < 100 && e.State == EnemyState.Idle; i++) e.Tick(Dt, hero, 0.3f);
            Assert.Equal(EnemyState.Wander, e.State);
            Assert.True(e.StateSeconds < 2 * Dt);
        }

        [Fact]
        public void Windup_progress_rises_from_zero_to_one_and_is_zero_outside_the_windup()
        {
            var e = new Enemy("e", E, "wolf", new Vec2(1.0f, 0));
            var hero = Hero(0);
            float last = -1f;
            bool sawWindup = false;
            for (int i = 0; i < 200; i++)
            {
                e.Tick(Dt, hero, 0.3f);
                if (e.State == EnemyState.Windup)
                {
                    sawWindup = true;
                    Assert.InRange(e.WindupProgress, 0f, 1f);
                    Assert.True(e.WindupProgress >= last);
                    last = e.WindupProgress;
                }
                else
                {
                    Assert.Equal(0f, e.WindupProgress);
                    if (sawWindup) break;
                }
            }
            Assert.True(sawWindup);
            Assert.True(last > 0.8f, $"the swing was seen up to {last}");
        }

        [Fact]
        public void A_staggered_enemy_counts_time_in_the_stagger_up()
        {
            var e = new Enemy("e", E, "wolf", new Vec2(100, 0));
            var hero = Hero(0);
            e.Receive(1f, null, 0f, 1.0f);   // stun 1 s
            Assert.Equal(EnemyState.Staggered, e.State);
            Assert.Equal(0f, e.StateSeconds);
            e.Tick(Dt, hero, 0.3f);
            e.Tick(Dt, hero, 0.3f);
            Assert.Equal(2 * Dt, e.StateSeconds, 3);
            Assert.Equal(0f, e.WindupProgress);
        }
    }
}
