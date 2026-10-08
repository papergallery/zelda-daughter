using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-16 (docs/demo/unity-architecture.md §7): rain and wet ground, mud that slows, fire on dry grass that spreads with the wind and leaves
    /// burnt ground, the hero's burn, the dark with wolves coming and going, the light of a campfire and of a torch — in scenes/test-demo.json
    /// (six cells of grass in a row at z = −16, a mud zone at (10; −20), wolf zones at ±26), the fire pool in the region.
    /// The rules are the core's; what is checked here is that the scene shows and reacts to them. Numbers of time are shortened in the shared
    /// data set and put back in TearDown.
    /// </summary>
    public class D16NatureTests
    {
        GameSession _s;
        HeroController _hero;
        NatureFx _fx;
        float _wetAfter, _mudRise, _burn, _spawnEvery, _minHero;

        IEnumerator Load(string scene)
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            _hero = UnityEngine.Object.FindFirstObjectByType<HeroController>();
            _fx = UnityEngine.Object.FindFirstObjectByType<NatureFx>();
            Assert.NotNull(_s, "a GameSession (SceneBuilder)");
            Assert.NotNull(_fx, "a NatureFx (SceneBuilder.Nature)");
            _hero.UseDpi(160f);
            var d = _s.State.Data;
            _wetAfter = d.Elements.Rain.WetAfterSeconds;
            _mudRise = d.Elements.Mud.RiseSeconds;
            _burn = d.Elements.Grass.BurnSeconds;
            _spawnEvery = d.Night.SpawnIntervalSeconds;
            _minHero = d.Night.MinHeroDistance;
            yield return new WaitForSeconds(0.3f);
        }

        [UnitySetUp]
        public IEnumerator SetUp() { yield return Load("Assets/Scenes/test-demo.unity"); }

        [TearDown]
        public void TearDown()
        {
            if (_s != null)
            {
                var d = _s.State.Data; // the data set is shared by all tests
                d.Elements.Rain.WetAfterSeconds = _wetAfter;
                d.Elements.Mud.RiseSeconds = _mudRise;
                d.Elements.Grass.BurnSeconds = _burn;
                d.Night.SpawnIntervalSeconds = _spawnEvery;
                d.Night.MinHeroDistance = _minHero;
            }
            TestSaves.Clear();
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator StandAt(float x, float z)
        {
            _hero.Teleport(new Vector3(x, 1f, z), 0f);
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ rain, wet ground, mud

        [UnityTest]
        public IEnumerator Rain_falls_and_the_ground_gets_wet_and_darker()
        {
            var ground = GameObject.Find("Ground").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            Assert.IsFalse(_fx.IsRaining);
            Assert.AreEqual(0f, _fx.Wetness, 1e-4f);
            bool? rainChanged = null;
            _s.Events.RainChanged += r => rainChanged = r;
            var before = ground.sharedMaterial.GetColor("_BaseColor");

            _s.State.Nature.Weather.StartRain(120f);
            yield return Until(() => _fx.RainParticleCount > 0 && _fx.Wetness > 0.1f, 5f);

            Assert.IsTrue(_fx.IsRaining);
            Assert.Greater(_fx.RainParticleCount, 0, "streaks fall");
            Assert.Greater(_fx.Wetness, 0.1f, "wetness grows while it rains");
            Assert.Greater(Shader.GetGlobalFloat("_ZD_Wetness"), 0.05f, "the global parameter is set for the shaders");
            Assert.AreEqual(true, rainChanged, "the bus says it rains");
            ground.GetPropertyBlock(block);
            Assert.Less(block.GetColor("_BaseColor").g, before.g, "wet ground is darker");

            _s.State.Nature.Weather.StopRain();
            yield return Until(() => _fx.RainLevel < 0.01f, 6f);
            Assert.AreEqual(false, rainChanged, "…and that it stopped");
            Assert.Less(_fx.RainLevel, 0.01f);
        }

        [UnityTest]
        public IEnumerator Mud_forms_in_the_rain_and_slows_the_hero()
        {
            _s.State.Data.Elements.Mud.RiseSeconds = 2f;
            yield return StandAt(10f, -20f);
            float dry = _s.State.SpeedMultiplier;
            Assert.AreEqual(1, _fx.MudZones, "the mud zone of the scene has a puddle to show");

            _s.State.Nature.Weather.StartRain(60f);
            yield return Until(() => _s.State.Nature.Mud.Level("mud_test") > 0.95f, 6f);

            Assert.Greater(_s.State.Nature.Mud.Level("mud_test"), 0.9f);
            Assert.Less(_s.State.SpeedMultiplier, dry * 0.7f, "walking in mud is slower (movement.json terrain.mud)");
            var puddle = _fx.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "Mud_mud_test");
            Assert.IsTrue(puddle.enabled, "the puddle is drawn");

            yield return StandAt(0f, 0f);
            Assert.AreEqual(dry, _s.State.SpeedMultiplier, 1e-3f, "out of the mud she is as fast as before");
        }

        // ------------------------------------------------------------------ fire

        [UnityTest]
        public IEnumerator A_torch_sets_dry_grass_alight_the_fire_spreads_downwind_and_leaves_burnt_ground()
        {
            var g = _s.State;
            g.Data.Elements.Grass.BurnSeconds = 4f;
            g.Nature.Wind.Set(new Vec2(1f, 0f), 1f);
            yield return StandAt(-4f, -16.8f);
            float stubbleFrom = _s.Index.Find("grass_cell_01").transform.localScale.y;
            var said = new List<string>();
            _s.Events.HeroSaid += (topic, line) => said.Add(topic);

            _s.Tap("grass_cell_01");
            Assert.AreEqual(GrassState.Dry, g.Nature.Grass.StateOf("grass_cell_01"), "no torch in the bag: nothing happens");

            g.Bag.Add("torch");
            _s.BagChanged("test");
            _s.Tap("grass_cell_01");
            Assert.AreEqual(GrassState.Burning, g.Nature.Grass.StateOf("grass_cell_01"));
            yield return Until(() => _fx.BurningCells >= 3, 6f);
            int burning = _fx.BurningCells;
            Assert.GreaterOrEqual(burning, 3, "the fire grew");
            Assert.AreEqual(Mathf.Min(burning, NatureFx.MaxFires), _fx.ActiveFires, "one fire effect per burning cell");
            Assert.AreEqual(NatureFx.MaxFires, _fx.FirePoolSize);

            yield return Until(() => _fx.BurntPatches > 0, 8f);
            Assert.Greater(_fx.BurntPatches, 0, "burnt ground is left");
            var cell1 = _s.Index.Find("grass_cell_01").transform;
            Assert.Less(cell1.localScale.y, stubbleFrom * 0.5f, "the burnt tuft is stubble");
            Assert.Contains("wound_burn", said, "…and she says so");
            Assert.GreaterOrEqual(_fx.Scorched, 1, "standing in the fire she is burnt");
            Assert.IsFalse(g.IgniteGrass("grass_cell_01"), "burnt ground does not burn again");
        }

        [UnityTest]
        public IEnumerator Wet_grass_does_not_burn()
        {
            var g = _s.State;
            g.Data.Elements.Rain.WetAfterSeconds = 0.5f;
            yield return StandAt(-4f, -16.8f);
            g.Bag.Add("torch");
            _s.BagChanged("test");
            g.Nature.Weather.StartRain(60f);
            yield return Until(() => g.Nature.Grass.StateOf("grass_cell_01") == GrassState.Wet, 5f);
            Assert.AreEqual(GrassState.Wet, g.Nature.Grass.StateOf("grass_cell_01"));
            _s.Tap("grass_cell_01");
            Assert.AreEqual(GrassState.Wet, g.Nature.Grass.StateOf("grass_cell_01"), "a torch does not light wet grass");
            Assert.AreEqual(0, _fx.BurningCells);
        }

        [UnityTest]
        public IEnumerator No_more_than_twelve_fires_at_once_in_a_big_field()
        {
            yield return Load("Assets/Scenes/region.unity");
            var g = _s.State;
            Assume.That(g.Nature.Grass.Count, Is.GreaterThan(30), "the region has a field");
            g.Data.Elements.Grass.BurnSeconds = 30f;
            for (int i = 0; i < 40; i++) g.Nature.Grass.Ignite($"grass_cell_{i:000}");
            yield return Until(() => _fx.BurningCells >= 40, 3f);
            Assert.GreaterOrEqual(_fx.BurningCells, 40);
            Assert.AreEqual(NatureFx.MaxFires, _fx.ActiveFires, "…but only the pool of twelve is drawn");
            Assert.AreEqual(NatureFx.MaxFires, _fx.FirePoolSize, "the pool never grows");
        }

        // ------------------------------------------------------------------ the dark

        [UnityTest]
        public IEnumerator At_night_a_wolf_is_called_far_from_her_and_goes_in_the_morning()
        {
            var g = _s.State;
            g.Data.Night.SpawnIntervalSeconds = 0.3f;
            yield return StandAt(0f, 0f);
            var spawned = new List<WorldEvent>();
            _s.Events.World += e => { if (e.Kind == WorldEventKind.PredatorSpawned) spawned.Add(e); };
            g.Clock.SetTime(1, 0.0);
            Assume.That(g.Clock.Daylight, Is.EqualTo(0f).Within(1e-3f), "midnight is dark");

            yield return Until(() => _fx.WolvesCalled >= 1, 8f);
            Assert.GreaterOrEqual(_fx.WolvesCalled, 1, "the dark calls a wolf");
            Assert.GreaterOrEqual(spawned.Count, 1);
            var at = spawned[0].Position;
            Assert.GreaterOrEqual((at - new Vec2(0f, 0f)).Length, g.Data.Night.MinHeroDistance - 0.01f, "…not near her");
            Assert.NotNull(g.Enemies.Get(spawned[0].Id), "the wolf is an enemy of the roster (D-13 shows it)");

            g.Clock.SetTime(1, 0.5);
            yield return Until(() => _fx.WolvesSent >= 1, 6f);
            Assert.GreaterOrEqual(_fx.WolvesSent, 1, "in the morning the far wolves go");
            Assert.IsNull(g.Enemies.Get(spawned[0].Id));
        }

        [UnityTest]
        public IEnumerator At_night_a_campfire_lights_the_ground_and_a_torch_lights_the_hero()
        {
            var g = _s.State;
            Assume.That(UnityEngine.Object.FindFirstObjectByType<ZeldaDaughter.World.CampPresenter>() != null, "the scene has D-14's CampPresenter");
            g.Clock.SetTime(1, 0.0);
            yield return StandAt(3f, -4f);
            var torchLight = UnityEngine.Object.FindFirstObjectByType<HeroTorchLight>();
            Assert.NotNull(torchLight, "a HeroTorchLight (SceneBuilder.Nature)");
            Assert.IsFalse(torchLight.IsOn, "no torch — no light");

            g.Bag.Add("firewood");
            g.Bag.Add("flint");
            var placed = g.Camp.Place("firewood", new Vec2(3f, -5f));
            Assert.AreEqual(PlaceOutcome.Placed, placed.Outcome);
            var used = g.Camp.Use(placed.Object.Id, "flint");
            _s.Events.RaisePlaced(placed.Object); // what ItemDrag tells the bus after a drop and a use: CampPresenter (D-14) draws the fire from it
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            _s.BagChanged("test");
            Assert.AreEqual(1, g.Camp.Campfires.Count);
            yield return new WaitForSeconds(0.6f);

            var fire = g.Camp.Campfires[0];
            var near = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.enabled && l.type == LightType.Point && l.intensity > 0.5f
                    && Vector2.Distance(new Vector2(l.transform.position.x, l.transform.position.z), new Vector2(fire.Position.X, fire.Position.Y)) < 1f)
                .ToList();
            Assert.GreaterOrEqual(near.Count, 1, "a warm light stands at the campfire");
            Assert.GreaterOrEqual(near.Max(l => l.range), g.Data.Camp.LightRadius - 0.01f, "…as far as data/camp.json says");

            g.Bag.Add("torch");
            _s.BagChanged("test");
            yield return null;
            Assert.IsTrue(torchLight.IsOn, "a torch in the bag — the hero is a light");
            Assert.Greater(torchLight.Intensity, 0.5f);
            Assert.Greater(torchLight.Range, 3f);
            Assert.IsTrue(torchLight.Light.transform.IsChildOf(_hero.transform), "the light goes with her");

            g.Bag.Remove("torch");
            _s.BagChanged("test");
            yield return null;
            Assert.IsFalse(torchLight.IsOn);
            Assert.AreEqual(0f, torchLight.Intensity, 1e-4f);
        }

        // ------------------------------------------------------------------ idle frames: what NatureFx and HeroTorchLight allocate

        static IEnumerator Allocated(int frames, Action<long> total)
        {
            long sum = 0;
            using (var rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
                for (int i = 0; i < frames; i++) { yield return null; sum += rec.LastValue; }
            total(sum);
        }

        [UnityTest]
        public IEnumerator Idle_frames_of_the_elements_do_not_allocate()
        {
            _s.State.Data.Session.RemarkCheckSeconds = 1000f;
            yield return new WaitForSeconds(1.0f);
            for (int i = 0; i < 60; i++) yield return null;
            var mine = new System.Collections.Generic.HashSet<MonoBehaviour>
                { _fx, UnityEngine.Object.FindFirstObjectByType<HeroTorchLight>() };
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m.enabled && m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("ZeldaDaughter") && m != _s).ToList();
            foreach (var m in all) m.enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
            // The whole-frame counter carries the editor's own allocations (~300 KB a frame, swinging by hundreds of KB between windows, D-26b): the assertion is on
            // the bytes allocated inside the scripts' frame phases (ScriptPhaseAlloc), the whole-frame numbers are only logged.
            long without = long.MaxValue, with = long.MaxValue, got = 0, sWithout = long.MaxValue, sWith = long.MaxValue;
            ScriptPhaseAlloc.Start();
            try
            {
                for (int k = 0; k < 3; k++)
                {
                    long s0 = ScriptPhaseAlloc.Total;
                    yield return Allocated(120, t => got = t);
                    without = Math.Min(without, got); sWithout = Math.Min(sWithout, ScriptPhaseAlloc.Total - s0);
                }
                foreach (var m in mine) m.enabled = true;
                for (int i = 0; i < 10; i++) yield return null;
                for (int k = 0; k < 3; k++)
                {
                    long s0 = ScriptPhaseAlloc.Total;
                    yield return Allocated(120, t => got = t);
                    with = Math.Min(with, got); sWith = Math.Min(sWith, ScriptPhaseAlloc.Total - s0);
                }
            }
            finally { ScriptPhaseAlloc.Stop(); }
            foreach (var m in all) m.enabled = true;
            ZdLog.Info("Test", $"D-16 idle GC over 120 frames: scripts' phases {sWithout} B without, {sWith} B with the elements; whole frame (editor included) {without} B / {with} B");
            Assert.LessOrEqual(sWith - sWithout, 512, "NatureFx + HeroTorchLight in idle frames");
        }

        // ------------------------------------------------------------------ frames (criteria D-16 p. 5): docs/demo/frames/D-16-*.png

        IEnumerator Frame(string name, float ortho)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var iso = cam.GetComponent<IsoCamera>();
            if (iso != null) iso.SnapToTarget();
            float size = cam.orthographicSize, aspect = cam.aspect;
            var prev = cam.targetTexture;
            var rt = new RenderTexture(1080, 2340, 24);
            cam.orthographicSize = ortho;
            cam.targetTexture = rt;
            cam.aspect = 1080f / 2340f;
            cam.Render(); // the first render after a load can come out empty
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            tex.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "demo", "frames"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.orthographicSize = size;
            cam.aspect = aspect;
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            ZdLog.Info("Frame", "D-16 " + name);
        }

        [UnityTest]
        public IEnumerator Frame_rain()
        {
            yield return Load("Assets/Scenes/region.unity");
            _s.State.Data.Elements.Rain.WetAfterSeconds = 1f;
            yield return StandAt(-131f, 12f);
            _s.State.Nature.Weather.StartRain(300f);
            yield return new WaitForSeconds(9f);
            var rr = _fx.GetComponentInChildren<ParticleSystemRenderer>(true);
            var rps = rr.GetComponent<ParticleSystem>();
            ZdLog.Info("Test", $"D-16 rain probe: shader={(rr.sharedMaterial != null ? rr.sharedMaterial.shader.name : "null")} count={rps.particleCount} visible={rr.isVisible} bounds={rr.bounds} pos={rr.transform.position} hero={_hero.transform.position} mode={rr.renderMode} queue={rr.sharedMaterial.renderQueue}");
            string shaderName = rr.sharedMaterial != null ? rr.sharedMaterial.shader.name : "null";
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, "rain-probe.txt"), $"shader={shaderName} count={rps.particleCount} visible={rr.isVisible} bounds={rr.bounds} pos={rr.transform.position} hero={_hero.transform.position} mode={rr.renderMode} queue={rr.sharedMaterial.renderQueue} rate={rps.emission.rateOverTime.constant} playing={rps.isPlaying} rainLevel={_fx.RainLevel}");
            yield return Frame("D-16-rain.png", 9f);
        }

        [UnityTest]
        public IEnumerator Frame_grass_fire()
        {
            yield return Load("Assets/Scenes/region.unity");
            var g = _s.State;
            g.Data.Elements.Grass.BurnSeconds = 14f;
            var at = _s.Index.Find("grass_cell_020").transform.position;
            yield return StandAt(at.x - 3.5f, at.z - 3.5f);
            g.Bag.Add("torch");
            _s.BagChanged("frame");
            foreach (var c in _s.Index.GrassCells)
                if (Vector3.Distance(c.Position, at) < 2.2f) g.Nature.Grass.Ignite(c.Id);
            yield return new WaitForSeconds(4f);
            yield return Frame("D-16-grass-fire.png", 8f);
            yield return new WaitForSeconds(14f);
            yield return Frame("D-16-grass-burnt.png", 8f);
        }

        [UnityTest]
        public IEnumerator Frame_night_campfire_and_wolf()
        {
            yield return Load("Assets/Scenes/region.unity");
            var g = _s.State;
            g.Data.Night.SpawnIntervalSeconds = 0.3f;
            g.Data.Night.MinHeroDistance = 4f;
            var zone = _s.Index.Find("zone_wolves_forest").transform.position;
            yield return StandAt(zone.x + 9f, zone.z + 2f);
            g.Clock.SetTime(1, 0.0);
            g.Bag.Add("firewood");
            g.Bag.Add("flint");
            var placed = g.Camp.Place("firewood", new Vec2(zone.x + 10.5f, zone.z + 0.5f));
            var used = g.Camp.Use(placed.Object.Id, "flint");
            _s.Events.RaisePlaced(placed.Object);
            _s.Events.RaiseUsedOnWorld(placed.Object.Id, used);
            g.Bag.Add("torch");
            _s.BagChanged("frame");
            yield return Until(() => _fx.WolvesCalled >= 1, 8f);
            yield return new WaitForSeconds(2.5f);
            yield return Frame("D-16-night-campfire-wolf.png", 9f);
        }
    }
}
