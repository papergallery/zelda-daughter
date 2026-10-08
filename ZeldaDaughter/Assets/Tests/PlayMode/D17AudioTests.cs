using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-17 (docs/demo/unity-architecture.md §7, docs/done-criteria/D-17.md): footsteps by surface, ambience by place and time, the bard in the
    /// evening, actions, fire, a constant number of AudioSources. Scene test-demo; the sounds are synthetic clips in a registry of the test's own,
    /// so the tests pass with or without the purchased files in Assets/ThirdParty.
    /// </summary>
    public class D17AudioTests
    {
        static readonly string[] Silent = { "hit_wolf", "windup_wolf", "bard_tavern" }; // the library has no such sound (docs/demo/backlog.md)

        GameSession _s;
        HeroController _hero;
        AudioDirector _dir;
        BardSource _bard;
        SoundRegistry _reg;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _dir = Object.FindFirstObjectByType<AudioDirector>();
            _bard = Object.FindFirstObjectByType<BardSource>();
            Assert.NotNull(_s, "test-demo has a GameSession");
            Assert.NotNull(_dir, "SceneBuilder.Audio puts an AudioDirector on Game");
            Assert.NotNull(_bard, "SceneBuilder.Audio puts a BardSource at the tavern's bar");
            _hero.UseDpi(160f);
            _reg = SyntheticRegistry(Silent);
            _dir.Configure(_s, _hero, _reg, new SoundZone[0]);
            _bard.Configure(_s, _reg, 17f, 23f);
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown() => TestSaves.Clear();

        // ---------------------------------------------------------------- helpers

        /// <summary>A registry with every id of the real one (so no "missing sound"), two short noise clips each, none for the ids in <paramref name="silent"/>.</summary>
        SoundRegistry SyntheticRegistry(params string[] silent)
        {
            var real = Object.FindFirstObjectByType<ArtAssets>().Sounds;
            Assert.NotNull(real, "the scene has a sound registry");
            var defs = new List<SoundDef>();
            foreach (var d in real.Sounds)
            {
                bool none = silent.Contains(d.Id);
                defs.Add(new SoundDef
                {
                    Id = d.Id, Volume = d.Volume <= 0f ? 0.5f : d.Volume, PitchJitter = d.PitchJitter, Spatial = d.Spatial,
                    Clips = none ? new AudioClip[0] : new[] { Clip(d.Id + "_a"), Clip(d.Id + "_b") },
                });
            }
            var reg = ScriptableObject.CreateInstance<SoundRegistry>();
            reg.Configure(defs.ToArray());
            return reg;
        }

        static AudioClip Clip(string name)
        {
            var c = AudioClip.Create(name, 22050, 1, 22050, false);
            var data = new float[22050];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < data.Length; i++) data[i] = (float)(rnd.NextDouble() * 2 - 1) * 0.2f;
            c.SetData(data, 0);
            return c;
        }

        static SoundZone Circle(string kind, Vector3 c, float radius, float fade) =>
            new SoundZone { Kind = kind, AreaJson = new Area { Shape = "circle", Center = new Pt(c.x, c.z), Radius = radius }.ToJson(), Fade = fade };

        Vector3 HeroAt => _hero.transform.position;
        bool InDef(AudioClip clip, string id) => clip != null && _reg.Sounds.First(d => d.Id == id).Clips.Contains(clip);

        // ---------------------------------------------------------------- footsteps

        [Test]
        public void The_surface_of_a_step_is_a_zone_or_the_terrain()
        {
            var p = new Vector3(5f, 0f, 5f);
            Assert.AreEqual("grass", _dir.SurfaceAt(p, "grass"));
            Assert.AreEqual("road", _dir.SurfaceAt(p, "road"));
            Assert.AreEqual("mud", _dir.SurfaceAt(p, "mud"));
            Assert.AreEqual("water", _dir.SurfaceAt(p, "water"));
            Assert.AreEqual("ground", _dir.SurfaceAt(p, "something_else"), "an unknown terrain is bare ground");
            _dir.SetZones(new[] { Circle("stone", p, 3f, 0f), Circle("wood", new Vector3(20f, 0f, 5f), 3f, 0f) });
            Assert.AreEqual("stone", _dir.SurfaceAt(p, "road"), "the square's stone beats the road under it");
            Assert.AreEqual("wood", _dir.SurfaceAt(new Vector3(20f, 0f, 5f), "road"), "the bridge");
            Assert.AreEqual("road", _dir.SurfaceAt(new Vector3(40f, 0f, 5f), "road"), "outside the zones the terrain stays");
        }

        [Test]
        public void Steps_on_the_road_and_on_the_grass_play_different_clips()
        {
            string terrain = "road";
            _dir.TerrainSource = _ => terrain;
            _s.Events.RaiseHeroStep(HeroAt, false);
            var roadClip = _dir.LastStepClip;
            Assert.AreEqual("road", _dir.LastStepSurface);
            Assert.IsTrue(InDef(roadClip, "step_road"), "a clip of step_road");
            terrain = "grass";
            _s.Events.RaiseHeroStep(HeroAt, false);
            Assert.AreEqual("grass", _dir.LastStepSurface);
            Assert.IsTrue(InDef(_dir.LastStepClip, "step_grass"), "a clip of step_grass");
            Assert.AreNotSame(roadClip, _dir.LastStepClip);
            Assert.AreEqual(1, _dir.PlayedCount("step_road"));
            Assert.AreEqual(1, _dir.PlayedCount("step_grass"));
        }

        [Test]
        public void Steps_on_stone_and_wood_and_water_have_their_own_sounds()
        {
            _dir.TerrainSource = _ => "road";
            _dir.SetZones(new[] { Circle("stone", HeroAt, 4f, 0f) });
            _s.Events.RaiseHeroStep(HeroAt, false);
            Assert.AreEqual("stone", _dir.LastStepSurface);
            Assert.IsTrue(InDef(_dir.LastStepClip, "step_stone"));
            _dir.SetZones(new[] { Circle("wood", HeroAt, 4f, 0f) });
            _s.Events.RaiseHeroStep(HeroAt, false);
            Assert.AreEqual("wood", _dir.LastStepSurface);
            Assert.IsTrue(InDef(_dir.LastStepClip, "step_wood"));
            _dir.SetZones(new SoundZone[0]);
            _dir.TerrainSource = _ => "water";
            _s.Events.RaiseHeroStep(HeroAt, false);
            Assert.AreEqual("water", _dir.LastStepSurface);
            Assert.IsTrue(InDef(_dir.LastStepClip, "step_water"));
        }

        [Test]
        public void The_same_clip_does_not_play_twice_in_a_row()
        {
            _dir.TerrainSource = _ => "grass";
            AudioClip prev = null;
            for (int i = 0; i < 12; i++)
            {
                _s.Events.RaiseHeroStep(HeroAt, i % 5 == 0);
                if (prev != null) Assert.AreNotSame(prev, _dir.LastStepClip, $"step {i}");
                prev = _dir.LastStepClip;
            }
        }

        // ---------------------------------------------------------------- ambience

        [Test]
        public void The_mix_is_forest_in_the_wild_and_town_in_the_town_and_crickets_at_night()
        {
            var t = new float[AmbientMix.Count];
            AmbientMix.Targets(0f, 0f, 0f, 1f, false, t);
            Assert.Greater(t[AmbientMix.Forest], 0.9f);
            Assert.AreEqual(0f, t[AmbientMix.Town]);
            Assert.AreEqual(0f, t[AmbientMix.Night], 0.001f);
            AmbientMix.Targets(1f, 0f, 0f, 1f, false, t);
            Assert.Greater(t[AmbientMix.Town], 0.9f);
            Assert.AreEqual(0f, t[AmbientMix.Forest], 0.001f);
            AmbientMix.Targets(0f, 0f, 0f, 0f, false, t);
            Assert.Greater(t[AmbientMix.Night], 0.9f);
            Assert.AreEqual(0f, t[AmbientMix.Forest], 0.001f);
            AmbientMix.Targets(0f, 0f, 1f, 1f, true, t);
            Assert.Greater(t[AmbientMix.River], 0.9f);
            Assert.AreEqual(1f, t[AmbientMix.Rain]);
        }

        [UnityTest]
        public IEnumerator Ambience_changes_smoothly_between_the_forest_and_the_town_and_at_night()
        {
            _s.State.Clock.SetTime(1, 0.5);
            yield return new WaitForSeconds(3.6f);
            Assert.Greater(_dir.LayerLevel(AmbientMix.Forest), 0.9f, "forest by day in the wild");
            Assert.AreEqual(0f, _dir.LayerLevel(AmbientMix.Town), 0.01f);

            _dir.SetZones(new[] { Circle("town", HeroAt, 8f, 20f) });
            yield return new WaitForSeconds(1.2f);
            float mid = _dir.LayerLevel(AmbientMix.Town);
            Assert.That(mid, Is.GreaterThan(0.1f).And.LessThan(0.9f), "the crossfade is gradual, not a cut");
            yield return new WaitForSeconds(2.6f);
            Assert.Greater(_dir.LayerLevel(AmbientMix.Town), 0.9f, "the town");
            Assert.AreEqual(0f, _dir.LayerLevel(AmbientMix.Forest), 0.01f, "no birds in the square");

            _dir.SetZones(new SoundZone[0]);
            _s.State.Clock.SetTime(1, 0.95);
            yield return new WaitForSeconds(3.6f);
            Assert.Greater(_dir.LayerLevel(AmbientMix.Night), 0.8f, "night");
            Assert.AreEqual(0f, _dir.LayerLevel(AmbientMix.Forest), 0.02f);
        }

        // ---------------------------------------------------------------- the bard

        [UnityTest]
        public IEnumerator The_bard_plays_in_the_evening_and_is_silent_by_day()
        {
            _reg = SyntheticRegistry("hit_wolf", "windup_wolf"); // this one has a lute
            _bard.Configure(_s, _reg, 17f, 23f);
            _s.State.Clock.SetTime(1, 0.5); // noon
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_bard.Evening);
            Assert.IsFalse(_bard.Playing, "silent by day");
            Assert.AreEqual(0f, _bard.Level);

            _s.State.Clock.SetTime(1, 19.0 / 24.0); // evening
            yield return new WaitForSeconds(2.6f);
            Assert.IsTrue(_bard.Evening);
            Assert.IsTrue(_bard.Playing, "the bard plays in the evening");
            Assert.Greater(_bard.Level, 0.9f);
            Assert.AreEqual(1f, _bard.Source.spatialBlend, "heard near the tavern only");

            _s.State.Clock.SetTime(1, 0.5);
            yield return new WaitForSeconds(2.6f);
            Assert.IsFalse(_bard.Playing, "and stops with the day");
        }

        [UnityTest]
        public IEnumerator Without_a_lute_the_bard_is_silence_and_nothing_breaks()
        {
            _s.State.Clock.SetTime(1, 19.0 / 24.0);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(_bard.Evening);
            Assert.IsFalse(_bard.Playing);
        }

        // ---------------------------------------------------------------- actions, fire

        [Test]
        public void Actions_make_their_sounds_and_a_missing_clip_is_silence()
        {
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Pickup));
            Assert.AreEqual(1, _dir.PlayedCount("act_pickup"));
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Eat));
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Craft));
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike));
            Assert.AreEqual(1, _dir.PlayedCount("act_eat"));
            Assert.AreEqual(1, _dir.PlayedCount("act_craft"));
            Assert.AreEqual(1, _dir.PlayedCount("hit_miss"), "the swing");

            _s.Events.RaiseHeroStruck("spawn_boar", new StrikeResult(StrikeOutcome.Hit, 1f));
            Assert.AreEqual(1, _dir.PlayedCount("hit_fists"), "bare hands");
            Assert.AreEqual(1, _dir.PlayedCount("hit_boar"), "the boar squeals");
            _s.Events.RaiseHeroStruck("spawn_boar", new StrikeResult(StrikeOutcome.Miss));
            Assert.AreEqual(1, _dir.PlayedCount("hit_fists"), "a miss is not a hit");

            _s.Events.RaiseEnemy(new EnemyNotice("spawn_boar", new EnemyEvent(EnemyEventKind.WindupStarted)));
            Assert.AreEqual(1, _dir.PlayedCount("windup_boar"));
            _s.Events.RaiseEnemy(new EnemyNotice("wolf_1", new EnemyEvent(EnemyEventKind.WindupStarted)));
            Assert.AreEqual(0, _dir.PlayedCount("windup_wolf"), "the library has no wolf: silence, no error");
            _s.Events.RaiseEnemy(new EnemyNotice("spawn_boar", new EnemyEvent(EnemyEventKind.Struck, 2f)));
            Assert.AreEqual(1, _dir.PlayedCount("hit_hero"));

            _s.Events.RaiseWindowOpened("inventory");
            Assert.AreEqual(1, _dir.PlayedCount("ui_paper"));
            _s.Events.RaiseTimeJumped(8.0);
            Assert.AreEqual(1, _dir.PlayedCount("act_sleep"));
        }

        [UnityTest]
        public IEnumerator A_lit_campfire_crackles_near_the_hero_and_not_when_far_or_burnt_out()
        {
            var near = HeroAt + new Vector3(4f, 0f, 0f);
            _s.State.Camp.Restore(1, null, new[] { new Campfire("fire_audio", new Vec2(near.x, near.z), 200f, _s.State.Data.Camp.FadeSeconds) });
            yield return new WaitForSeconds(0.6f);
            var fire = _dir.GetComponentsInChildren<AudioSource>().Where(a => a.name.StartsWith("Fire_") && a.isPlaying).ToList();
            Assert.AreEqual(1, fire.Count, "one fire, one voice");
            var at = fire[0].transform.position;
            Assert.Less(new Vector2(at.x - near.x, at.z - near.z).magnitude, 0.1f, "at the fire");
            Assert.Greater(fire[0].volume, 0.1f);

            _s.State.Camp.Restore(1, null, new[] { new Campfire("fire_far", new Vec2(near.x + 300f, near.z), 200f, _s.State.Data.Camp.FadeSeconds) });
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, _dir.GetComponentsInChildren<AudioSource>().Count(a => a.name.StartsWith("Fire_") && a.isPlaying), "too far to hear");
        }

        // ---------------------------------------------------------------- the pool

        [UnityTest]
        public IEnumerator The_number_of_audio_sources_never_changes()
        {
            int before = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(AmbientMix.Count + AudioDirector.OneShotVoices + AudioDirector.FireVoices + 1, _dir.SourceCount, "layers + voices + fires + torch");
            Assert.AreEqual(AmbientMix.Count + AudioDirector.OneShotVoices + AudioDirector.FireVoices + 1, _dir.GetComponentsInChildren<AudioSource>(true).Length, "all of them live under the director");
            _dir.TerrainSource = _ => "grass";
            for (int i = 0; i < 60; i++)
            {
                _s.Events.RaiseHeroStep(HeroAt, false);
                _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Craft));
                _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike));
                _s.Events.RaiseHeroStruck("spawn_boar", new StrikeResult(StrikeOutcome.Hit, 1f));
                if (i % 10 == 0) yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(before, Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length, "a pool, not a source per sound");
            Assert.LessOrEqual(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.isPlaying), before);
        }

        [Test]
        public void Without_any_clips_the_game_is_silent_and_works()
        {
            var empty = ScriptableObject.CreateInstance<SoundRegistry>();
            empty.Configure(_reg.Sounds.Select(d => new SoundDef { Id = d.Id, Volume = d.Volume, Spatial = d.Spatial, Clips = new AudioClip[0] }).ToArray());
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[ZD:Audio\] no clips"));
            _dir.Configure(_s, _hero, empty, new SoundZone[0]);
            _s.Events.RaiseHeroStep(HeroAt, false);
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Pickup));
            Assert.AreEqual(0, _dir.PlayedCount("act_pickup"));
            Assert.IsNull(_dir.Play("step_grass"));
        }
    }
}
