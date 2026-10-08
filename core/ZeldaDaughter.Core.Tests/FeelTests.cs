using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Feel;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Language;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-26 (docs/done-criteria/D-26.md, docs/demo/best-practices-feel.md): the feel of the fight and of the one-finger control.</summary>
    public class FeelTests
    {
        static readonly FeelSettings F = TestData.Load<FeelSettings>("combat-feel.json");
        static readonly GestureSettings GS = TestData.Load<GestureSettings>("input.json");
        static readonly WeaponSettings W = TestData.Load<WeaponSettings>("weapons.json");
        static readonly EnemySettings E = TestData.Load<EnemySettings>("enemies.json");
        static readonly SkillSettings SK = TestData.Load<SkillSettings>("skills.json");
        static readonly WoundSettings WS = TestData.Load<WoundSettings>("wounds.json");
        static readonly LanguageSettings LS = TestData.Load<LanguageSettings>("language.json");

        // ---------------------------------------------------------------- 1. frame rate
        [Fact]
        public void Target_frame_rate_is_sixty() => Assert.Equal(60, F.TargetFrameRate);

        // ---------------------------------------------------------------- 2. hit-stop
        [Fact]
        public void Hit_stop_lengths_follow_the_criteria()
        {
            Assert.InRange(F.HitStop.Hit, 0.060f, 0.080f);
            Assert.InRange(F.HitStop.HeroStruck, 0.100f, 0.120f);
            Assert.InRange(F.HitStop.Kill, 0.150f, 0.200f);
            Assert.Equal(0f, F.HitStop.Miss);
            Assert.InRange(F.HitStop.ImpactDelay, 0f, 0.2f);   // the freeze comes at the moment of the impact, inside the swing
        }

        [Fact]
        public void Hit_stop_picks_its_length_by_what_happened()
        {
            Assert.Equal(F.HitStop.Hit, F.HitStopFor(StrikeOutcome.Hit, killed: false));
            Assert.Equal(F.HitStop.Kill, F.HitStopFor(StrikeOutcome.Hit, killed: true));
            Assert.Equal(0f, F.HitStopFor(StrikeOutcome.Miss, killed: false));
            Assert.Equal(0f, F.HitStopFor(StrikeOutcome.OutOfRange, killed: false));
        }

        [Fact]
        public void Hit_stop_counts_down_in_real_time_and_a_longer_request_wins()
        {
            var h = new HitStop();
            Assert.False(h.Active);
            h.Request(0.07f);
            Assert.True(h.Active);
            h.Request(0.03f);                    // a shorter one does not shorten it
            Assert.Equal(0.07f, h.Remaining, 4);
            h.Request(0.17f);                    // a kill extends it
            Assert.Equal(0.17f, h.Remaining, 4);
            h.Tick(0.1f);
            Assert.True(h.Active);
            h.Tick(0.1f);
            Assert.False(h.Active);
            Assert.Equal(0f, h.Remaining);
            h.Request(0f);
            Assert.False(h.Active);
        }

        // ---------------------------------------------------------------- 3. camera shake
        [Fact]
        public void Shake_numbers_follow_the_criteria()
        {
            Assert.InRange(F.Shake.MaxMeters, 0.08f, 0.12f);
            Assert.Equal(0.3f, F.Shake.DecaySeconds, 3);
            Assert.True(F.Shake.Hit < F.Shake.HeroStruck && F.Shake.HeroStruck < F.Shake.Knockout);
        }

        [Fact]
        public void Shake_is_trauma_squared_times_max_and_decays_to_zero_in_the_decay_time()
        {
            var t = new Trauma(F.Shake);
            Assert.Equal(0f, t.Amplitude);
            t.Add(0.5f);
            Assert.Equal(0.25f * F.Shake.MaxMeters, t.Amplitude, 5);
            t.Add(0.9f);                         // sum is clamped to 1
            Assert.Equal(F.Shake.MaxMeters, t.Amplitude, 5);
            t.Tick(F.Shake.DecaySeconds * 0.5f);
            Assert.Equal(0.25f * F.Shake.MaxMeters, t.Amplitude, 4);
            t.Tick(F.Shake.DecaySeconds);
            Assert.Equal(0f, t.Value);
            Assert.Equal(0f, t.Amplitude);
        }

        [Fact]
        public void Shake_offset_is_smooth_noise_within_the_amplitude_and_zero_without_trauma()
        {
            var t = new Trauma(F.Shake);
            t.Offset(1.0, out float x0, out float y0);
            Assert.Equal(0f, x0); Assert.Equal(0f, y0);
            t.Add(1f);
            float prevX = 0, prevY = 0, maxStep = 0, seen = 0;
            for (int i = 0; i < 60; i++)
            {
                t.Offset(i / 60.0, out float x, out float y);
                Assert.InRange(x, -F.Shake.MaxMeters - 1e-5f, F.Shake.MaxMeters + 1e-5f);
                Assert.InRange(y, -F.Shake.MaxMeters - 1e-5f, F.Shake.MaxMeters + 1e-5f);
                if (i > 0) maxStep = System.Math.Max(maxStep, System.Math.Abs(x - prevX) + System.Math.Abs(y - prevY));
                seen = System.Math.Max(seen, System.Math.Abs(x));
                prevX = x; prevY = y;
            }
            Assert.True(seen > 0.2f * F.Shake.MaxMeters, "the shake actually moves");
            Assert.True(maxStep < 1.2f * F.Shake.MaxMeters, "smooth: no jump of the whole amplitude between two frames");
            // deterministic
            var u = new Trauma(F.Shake); u.Add(1f);
            t.Offset(0.37, out float a, out _); u.Offset(0.37, out float b, out _);
            Assert.Equal(a, b);
        }

        [Fact]
        public void The_off_switch_silences_the_shake()
        {
            var t = new Trauma(F.Shake) { Enabled = false };
            t.Add(1f);
            Assert.Equal(0f, t.Amplitude);
            t.Offset(0.5, out float x, out float y);
            Assert.Equal(0f, x); Assert.Equal(0f, y);
        }

        // ---------------------------------------------------------------- 4. haptics
        [Fact]
        public void Haptics_are_short_and_stronger_on_a_blow_to_the_hero_and_none_on_a_miss()
        {
            Assert.InRange(F.Haptics.HitMs, 5, 40);
            Assert.True(F.Haptics.HeroStruckMs > F.Haptics.HitMs);
            Assert.True(F.Haptics.HeroStruckAmplitude > F.Haptics.HitAmplitude);
            Assert.Equal(0, F.HapticMsFor(StrikeOutcome.Miss, killed: false));
            Assert.Equal(F.Haptics.HitMs, F.HapticMsFor(StrikeOutcome.Hit, killed: false));
        }

        // ---------------------------------------------------------------- 5. attack on touch + tap buffer
        static TouchSample Begin(double t, TouchHit hit) => new TouchSample(0, TouchPhase.Began, t, new Vec2(100, 100), hit);
        static TouchSample Move(double t, float x, float y) => new TouchSample(0, TouchPhase.Moved, t, new Vec2(x, y), default);
        static TouchSample End(double t) => new TouchSample(0, TouchPhase.Ended, t, new Vec2(100, 100), default);

        [Fact]
        public void A_touch_that_begins_on_an_enemy_taps_at_once_not_on_release()
        {
            var r = new GestureRecognizer(GS, 160f);
            var down = r.Feed(Begin(1.0, TouchHit.Object("boar_1", onPress: true)));
            var tap = Assert.Single(down);
            Assert.Equal(GestureKind.Tap, tap.Kind);
            Assert.Equal("boar_1", tap.TargetId);
            Assert.Equal(1.0, tap.Time);
            Assert.Empty(r.Feed(End(1.2)));      // the release adds nothing: one touch — one blow
        }

        [Fact]
        public void An_ordinary_object_still_taps_on_release()
        {
            var r = new GestureRecognizer(GS, 160f);
            Assert.Empty(r.Feed(Begin(1.0, TouchHit.Object("peasant"))));
            var up = Assert.Single(r.Feed(End(1.2)));
            Assert.Equal(GestureKind.Tap, up.Kind);
            Assert.Equal("peasant", up.TargetId);
        }

        [Fact]
        public void After_a_press_on_an_enemy_the_finger_can_still_swipe_and_gives_no_second_tap()
        {
            var r = new GestureRecognizer(GS, 160f);
            r.Feed(Begin(1.0, TouchHit.Object("boar_1", onPress: true)));
            var ev = r.Feed(Move(1.1, 100, 160));
            Assert.Contains(ev, e => e.Kind == GestureKind.SwipeStarted);
            var end = r.Feed(End(1.3));
            Assert.DoesNotContain(end, e => e.Kind == GestureKind.Tap);
            // a long hold after the press is not a tap on release either
            var r2 = new GestureRecognizer(GS, 160f);
            r2.Feed(Begin(5.0, TouchHit.Object("boar_1", onPress: true)));
            Assert.Empty(r2.Feed(End(5.9)));
        }

        [Fact]
        public void The_tap_buffer_is_150_ms()
        {
            Assert.Equal(0.15f, W.TapBufferSeconds, 3);
        }

        [Fact]
        public void A_tap_in_the_last_150_ms_of_the_cooldown_strikes_when_it_ends()
        {
            var b = new StrikeBuffer(W.TapBufferSeconds);
            Assert.True(b.Offer("boar_1", cooldownLeft: 0.10f));
            Assert.Null(b.Tick(0.05f, cooldownLeft: 0.05f));       // not yet
            Assert.Equal("boar_1", b.Tick(0.05f, cooldownLeft: 0f)); // the cooldown is over: the blow goes
            Assert.Null(b.Tick(0.05f, cooldownLeft: 0f));          // and it is gone
        }

        [Fact]
        public void A_tap_earlier_than_the_buffer_is_dropped_and_the_last_tap_wins()
        {
            var b = new StrikeBuffer(W.TapBufferSeconds);
            Assert.False(b.Offer("boar_1", cooldownLeft: 0.5f));   // too early: no pre-recorded blows
            Assert.Null(b.Tick(0.5f, cooldownLeft: 0f));
            Assert.True(b.Offer("a", 0.1f));
            Assert.True(b.Offer("b", 0.08f));
            Assert.Equal("b", b.Tick(0.1f, 0f));
        }

        [Fact]
        public void The_buffered_tap_is_cleared_by_a_knockout_and_by_time()
        {
            var b = new StrikeBuffer(0.15f);
            b.Offer("x", 0.1f);
            b.Clear();
            Assert.Null(b.Tick(0.2f, 0f));
            b.Offer("y", 0.1f);
            Assert.Null(b.Tick(0.4f, 0.1f));                       // the cooldown somehow did not end for 0.4 s: the tap is stale
            Assert.Null(b.Tick(0.1f, 0f));
        }

        [Fact]
        public void The_buffer_works_with_the_real_hero_cooldown()
        {
            var hero = new HeroCombat(W, new Skills(SK), new HeroCondition(WS)) { Position = new Vec2(0, 0) };
            var boar = new Enemy("b", E, "boar", new Vec2(0.8f, 0));
            Assert.Equal(StrikeOutcome.Hit, hero.Strike("fists", boar, 0f).Outcome);
            Assert.Equal(StrikeOutcome.Cooldown, hero.Strike("fists", boar, 0f).Outcome);
            var buf = new StrikeBuffer(W.TapBufferSeconds);
            hero.Tick(hero.CooldownLeft - 0.1f);                   // 100 ms before the end
            Assert.True(buf.Offer("b", hero.CooldownLeft));
            float dt = 0.02f; string? go = null;
            for (int i = 0; i < 20 && go == null; i++) { hero.Tick(dt); go = buf.Tick(dt, hero.CooldownLeft); }
            Assert.Equal("b", go);
            Assert.Equal(StrikeOutcome.Hit, hero.Strike("fists", boar, 0f).Outcome);
        }

        // ---------------------------------------------------------------- 6. dodge with a margin
        const float Dt = 0.05f;
        static HeroCombat Hero(float x = 0) => new HeroCombat(W, new Skills(SK), new HeroCondition(WS)) { Position = new Vec2(x, 0) };

        [Fact]
        public void The_windups_are_long_enough_for_a_phone()
        {
            Assert.Equal(0.9f, E.Enemies["boar"].Windup, 3);
            Assert.Equal(0.7f, E.Enemies["wolf"].Windup, 3);
            Assert.Equal(0.3f, E.DodgeForgiveness, 3);
        }

        [Fact]
        public void A_blow_counts_only_inside_range_minus_the_forgiveness_when_the_enemy_cannot_step_in()
        {
            foreach (var id in new[] { "boar", "wolf" })
            {
                float reach = E.Enemies[id].Range - E.DodgeForgiveness;
                foreach (var (gap, expected) in new[] { (reach + 0.1f, EnemyEventKind.Dodged), (reach - 0.1f, EnemyEventKind.Struck) })
                {
                    // the enemy is pinned (a wall behind it): it cannot close the gap during the windup — the rule alone decides
                    var hero = Hero(); var en = new Enemy("e", E, id, new Vec2(gap, 0)); en.Provoke();
                    for (float t = 0; t < 1f && en.State != EnemyState.Windup; t += Dt) en.Tick(Dt, hero, 0.3f);
                    Assert.Equal(EnemyState.Windup, en.State);
                    en.Blocked = _ => true;
                    var log = new List<EnemyEventKind>();
                    for (float t = 0; t < 3f && !log.Contains(EnemyEventKind.Struck) && !log.Contains(EnemyEventKind.Dodged); t += Dt)
                        foreach (var ev in en.Tick(Dt, hero, 0.3f)) log.Add(ev.Kind);
                    Assert.Contains(expected, log);
                }
            }
        }

        [Fact]
        public void Stepping_out_of_the_old_range_during_the_windup_is_a_dodge()
        {
            foreach (var id in new[] { "boar", "wolf" })
            {
                var hero = Hero(); var en = new Enemy("e", E, id, new Vec2(1f, 0)); en.Provoke();
                var log = new List<EnemyEventKind>();
                bool moved = false;
                for (float t = 0; t < 6 && !log.Contains(EnemyEventKind.Struck) && !log.Contains(EnemyEventKind.Dodged); t += Dt)
                {
                    foreach (var ev in en.Tick(Dt, hero, 0.3f)) log.Add(ev.Kind);
                    if (!moved && en.State == EnemyState.Windup) { hero.Position = new Vec2(en.Position.X - (E.Enemies[id].Range + 0.1f), 0); moved = true; }
                }
                Assert.True(moved, id);
                Assert.Contains(EnemyEventKind.Dodged, log);
                Assert.DoesNotContain(EnemyEventKind.Struck, log);
            }
        }

        [Fact]
        public void A_hero_standing_at_the_edge_of_the_old_range_is_still_hit_because_the_enemy_steps_in()
        {
            foreach (var id in new[] { "boar", "wolf" })
            {
                var hero = Hero();
                var en = new Enemy("e", E, id, new Vec2(E.Enemies[id].Range - 0.02f, 0)); en.Provoke();
                var log = new List<EnemyEventKind>();
                for (float t = 0; t < 6 && !log.Contains(EnemyEventKind.Struck); t += Dt)
                    foreach (var ev in en.Tick(Dt, hero, 0.3f)) log.Add(ev.Kind);
                Assert.Contains(EnemyEventKind.Struck, log);
                Assert.True((en.Position - hero.Position).Length <= E.Enemies[id].Range - E.DodgeForgiveness + 1e-3f, id);
            }
        }

        [Fact]
        public void Walking_away_from_the_windup_is_a_dodge_even_at_walking_pace()
        {
            var hero = Hero(); var en = new Enemy("e", E, "wolf", new Vec2(1.4f, 0)); en.Provoke();
            var log = new List<EnemyEventKind>();
            for (float t = 0; t < 6 && !log.Contains(EnemyEventKind.Struck) && !log.Contains(EnemyEventKind.Dodged); t += Dt)
            {
                foreach (var ev in en.Tick(Dt, hero, 0.3f)) log.Add(ev.Kind);
                if (en.State == EnemyState.Windup) hero.Position = new Vec2(hero.Position.X - 2.5f * Dt, 0);  // 2.5 m/s, well under a run
            }
            Assert.Contains(EnemyEventKind.Dodged, log);
        }

        // ---------------------------------------------------------------- 7. readable windup
        [Fact]
        public void The_windup_ring_is_big_dark_red_and_opaque_by_the_end()
        {
            Assert.True(F.WindupRing.DiameterFactor >= 1.5f);
            Assert.True(F.WindupRing.AlphaAtEnd >= 0.6f);
            Assert.True(F.WindupRing.Color[0] < 0.6f && F.WindupRing.Color[1] < 0.15f && F.WindupRing.Color[2] < 0.15f, "dark red");
            Assert.True(F.WindupRing.AlphaAt(1f) >= 0.6f);
            Assert.True(F.WindupRing.AlphaAt(0.5f) < F.WindupRing.AlphaAt(1f));
            Assert.True(F.WindupRing.AlphaAt(0f) >= 0f);
        }

        // ---------------------------------------------------------------- 8. threat from the side
        [Fact]
        public void The_camera_widens_by_a_fifth_over_half_a_second_and_the_hero_stays_in_a_eleventh_of_the_frame()
        {
            Assert.Equal(1.2f, F.Camera.WidenFactor, 3);
            Assert.Equal(0.5f, F.Camera.WidenSeconds, 3);
            Assert.True(F.Camera.MinHeroFrameShare >= 1f / 11f - 1e-4f);
            float size = 1f;
            for (int i = 0; i < 25; i++) size = F.Camera.Step(size, 1.2f, 0.01f);   // 0.25 s: halfway
            Assert.Equal(1.1f, size, 3);
            for (int i = 0; i < 25; i++) size = F.Camera.Step(size, 1.2f, 0.01f);
            Assert.Equal(1.2f, size, 3);
            for (int i = 0; i < 100; i++) size = F.Camera.Step(size, 1.2f, 0.01f);
            Assert.Equal(1.2f, size, 3);                                            // does not overshoot
            for (int i = 0; i < 100; i++) size = F.Camera.Step(size, 1f, 0.01f);
            Assert.Equal(1f, size, 3);
        }

        [Fact]
        public void Camera_ortho_is_capped_so_that_the_hero_is_not_smaller_than_the_share()
        {
            // hero 1.7 m high, base ortho 6.3: widened 7.56 m → the frame is 15.12 m, the hero is 1/8.9
            Assert.True(F.Camera.HeroShare(1.7f, 6.3f * 1.2f) >= F.Camera.MinHeroFrameShare);
            // a tiny hero would drop under the share: the cap holds the ortho at 1.7 × 11 / 2 = 9.35
            Assert.Equal(9.35f, F.Camera.MaxOrtho(1.7f), 3);
            Assert.Equal(9.35f, F.Camera.Clamp(12f, 1.7f), 3);
            Assert.Equal(7.0f, F.Camera.Clamp(7.0f, 1.7f), 3);
        }

        [Fact]
        public void The_wolf_growl_is_heard_under_fifteen_metres_and_pans_to_its_side()
        {
            Assert.Equal(15f, F.Growl.Meters, 3);
            Assert.Equal(0f, F.Growl.Volume(15.5f));
            Assert.True(F.Growl.Volume(3f) > F.Growl.Volume(12f));
            Assert.True(F.Growl.Volume(12f) > 0f);
            Assert.True(F.Growl.Pan(dxScreen: 6f, distance: 6f) > 0.5f);
            Assert.True(F.Growl.Pan(dxScreen: -6f, distance: 6f) < -0.5f);
            Assert.Equal(0f, F.Growl.Pan(0f, 6f), 3);
            Assert.InRange(F.Growl.Pan(100f, 1f), -1f, 1f);
        }

        [Fact]
        public void Eyes_show_at_the_edge_of_the_light()
        {
            Assert.True(F.Eyes.ShowBeyondLightShare > 0.5f && F.Eyes.ShowBeyondLightShare < 1.0f);
            Assert.True(F.Eyes.IsVisible(distanceFromFire: 7f, lightRadius: 6f, enemyAwake: true));   // just past the edge
            Assert.False(F.Eyes.IsVisible(distanceFromFire: 2f, lightRadius: 6f, enemyAwake: true));  // in the light the wolf itself is seen
            Assert.False(F.Eyes.IsVisible(distanceFromFire: 7f, lightRadius: 6f, enemyAwake: false));
            Assert.False(F.Eyes.IsVisible(distanceFromFire: 40f, lightRadius: 6f, enemyAwake: true)); // far in the dark: nothing
        }

        // ---------------------------------------------------------------- 10. runes
        [Fact]
        public void One_word_gives_the_same_runes_for_every_npc()
        {
            var l = new Comprehension(LS);
            string line = "Добрый день путник";
            Assert.Equal(l.Render(line, "peasant"), l.Render(line, "smith"));
            Assert.Equal(l.Render("огонь", "a"), l.Render("огонь", "b"));
            Assert.Equal(l.Render("Огонь", "a"), l.Render("огонь", "b"));   // case does not matter
            Assert.NotEqual(l.Render("огонь", "a"), l.Render("вода", "a"));
            // stage two reveals the same words for everyone as well
            l.Restore((LS.Stage2At + LS.Stage3At) / 2f);
            Assert.Equal(l.Render(line, "peasant"), l.Render(line, "smith"));
        }

        // ---------------------------------------------------------------- 11. two channels
        [Fact]
        public void Severity_is_the_worse_of_lost_health_and_wound_load_over_the_knockout_load()
        {
            var c = new HeroCondition(WS);
            Assert.Equal(0f, F.Condition.Severity(c, WS), 3);
            c.Damage(50f);
            Assert.Equal(0.5f, F.Condition.Severity(c, WS), 2);
            var d = new HeroCondition(WS);
            d.Wound(WoundType.Cut, 1f);
            Assert.Equal(1f / WS.KnockoutWoundLoad, F.Condition.Severity(d, WS), 3);
        }

        [Fact]
        public void The_world_loses_up_to_a_quarter_of_its_colour_from_severity_point_seven()
        {
            var d = F.Desaturation;
            Assert.Equal(0f, d.Share(0f), 4);
            Assert.Equal(0f, d.Share(d.StartSeverity), 4);
            Assert.Equal(0.25f, d.Share(0.7f), 4);
            Assert.Equal(0.25f, d.Share(1f), 4);
            Assert.True(d.Share(0.5f) > 0f && d.Share(0.5f) < 0.25f);
            Assert.True(d.Share(0.6f) > d.Share(0.5f));
            Assert.Equal(0.7f, d.FullSeverity, 3);
            Assert.Equal(1f - 0.25f, d.Saturation(0.9f), 4);
        }

        [Fact]
        public void Breathing_from_severity_point_five_and_the_stomach_when_hungry()
        {
            Assert.Equal(0.5f, F.Condition.BreathingFromSeverity, 3);
            Assert.False(F.Condition.Breathes(0.49f));
            Assert.True(F.Condition.Breathes(0.5f));
            Assert.False(F.Condition.StomachGrowls(HungerLevel.Peckish));
            Assert.True(F.Condition.StomachGrowls(HungerLevel.Hungry));
            Assert.True(F.Condition.StomachGrowls(HungerLevel.Starving));
        }

        // ---------------------------------------------------------------- 9. hints: text only after 20 s
        [Fact]
        public void Hint_text_appears_only_after_twenty_seconds_of_inaction()
        {
            var o = TestData.Load<ZeldaDaughter.Core.Onboarding.OnboardingSettings>("onboarding.json");
            Assert.Equal(20f, o.TextAfterSeconds, 3);
            Assert.True(o.ShowsText(20f));
            Assert.False(o.ShowsText(19.9f));
        }

        // ---------------------------------------------------------------- 13. the sounds of a blow
        [Fact]
        public void Swing_hit_miss_and_kill_are_separate_sounds_and_the_hit_is_louder_than_the_swing()
        {
            string path = System.IO.Path.Combine(System.IO.Directory.GetParent(TestPaths.CoreRoot)!.FullName, "ZeldaDaughter", "Assets", "Art", "Registries", "sounds.json");
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var snd = doc.RootElement.GetProperty("sounds");
            double Vol(string id) => snd.GetProperty(id).GetProperty("volume").GetDouble();
            string[] Clips(string id) => snd.GetProperty(id).GetProperty("clips").EnumerateArray().Select(c => c.GetString()!).ToArray();

            foreach (var id in new[] { "swing", "hit_miss", "kill", "hit_blade", "hit_fists", "breath_hurt", "stomach", "wolf_growl_near" })
                Assert.True(snd.TryGetProperty(id, out _), id);
            Assert.True(Vol("hit_blade") > Vol("swing") && Vol("hit_fists") > Vol("swing"), "the hit is louder than the swing");
            Assert.True(Vol("kill") >= Vol("hit_fists"));
            Assert.NotEqual(Clips("swing"), Clips("hit_miss"));            // a miss is not the same noise as the swing
            Assert.Empty(Clips("kill").Intersect(Clips("hit_fists")).Union(Clips("kill").Intersect(Clips("hit_blade"))));
            // our own clips are in git: every generated path of the new sounds exists
            string root = System.IO.Path.Combine(System.IO.Directory.GetParent(TestPaths.CoreRoot)!.FullName, "ZeldaDaughter");
            foreach (var id in new[] { "kill", "breath_hurt", "stomach", "wolf_growl_near" })
                foreach (var c in Clips(id).Where(c => c.StartsWith("Assets/Art/Audio/Generated/")))
                    Assert.True(System.IO.File.Exists(System.IO.Path.Combine(root, c)), c);
        }
    }
}
