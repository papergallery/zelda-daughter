using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-25 (docs/done-criteria/D-25.md, item 4): the billboards play whatever drawn frames the registry has — the walking cycle of any length,
    /// the turn between views, the first steps, the stop, the loop of standing, poses of any number of frames — and allocate nothing.
    /// Nothing is made by code on top (the author rejected procedural motion). The figures are built from synthetic sets (tiny sprites),
    /// so the tests do not depend on the art.
    /// </summary>
    public class D25MotionTests
    {
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            TestSaves.UseCleanFolder();
            // a camera of our own looking along +Z (its right is +X): the views of FaceDirection do not depend on what the runner has
            var cam = new GameObject("d25cam", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(0, 1, -10), Quaternion.identity);
            _made.Add(cam.gameObject);
            _cam = cam;
        }

        Camera _cam;

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            TestSaves.Clear();
        }

        Sprite[] Frames(int n, string tag)
        {
            var list = new Sprite[n];
            var tex = new Texture2D(8, 16, TextureFormat.RGBA32, false) { name = tag };
            var px = new Color32[8 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(200, 160, 100, 255);
            tex.SetPixels32(px); tex.Apply();
            _made.Add(tex);
            for (int i = 0; i < n; i++)
            {
                list[i] = Sprite.Create(tex, new Rect(0, 0, 8, 16), new Vector2(0.5f, 0f), 16f);
                list[i].name = tag + "_" + i;
                _made.Add(list[i]);
            }
            return list;
        }

        /// <summary>A figure with N frames per view (and a drawn pose «attack»), the cycle 1 m long.</summary>
        BillboardSprite Figure(string id, int frames, Vector3 at, bool withPose = false, params (string key, int n)[] extra)
        {
            var set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            var list = new List<PoseFrames>();
            if (withPose) list.Add(new PoseFrames("attack_side", Frames(2, id + "_attack")));
            foreach (var (key, n) in extra) list.Add(new PoseFrames(key, Frames(n, key)));
            var poses = list.ToArray();
            set.Configure(Frames(frames, id + "_f"), Frames(frames, id + "_b"), Frames(frames, id + "_s"), null, 16f, 1f, false, Color.white, poses);
            var reg = ScriptableObject.CreateInstance<CharacterRegistry>();
            reg.Configure(new[] { id }, new[] { set });
            _made.Add(set); _made.Add(reg);
            var go = new GameObject("fig_" + id);
            _made.Add(go);
            go.transform.position = at;
            var sprite = go.AddComponent<BillboardSprite>();
            sprite.Configure(reg, null, _cam, id);
            return sprite;
        }

        string Name(BillboardSprite a) => a.CurrentSprite != null ? a.CurrentSprite.name : "";

        IEnumerator Settle(float seconds) { for (float t0 = Time.time; Time.time - t0 < seconds;) yield return null; }

        // ------------------------------------------------------------------ the cycle comes from the registry

        [UnityTest] public IEnumerator The_walking_cycle_is_as_long_as_the_registrys_list_3() { yield return Cycle(3); }
        [UnityTest] public IEnumerator The_walking_cycle_is_as_long_as_the_registrys_list_8() { yield return Cycle(8); }
        [UnityTest] public IEnumerator The_walking_cycle_is_as_long_as_the_registrys_list_2() { yield return Cycle(2); }

        IEnumerator Cycle(int n)
        {
            var a = Figure("cycle" + n, n, Vector3.zero);
            a.FaceDirection(Vector3.right);
            yield return null;
            Assert.AreEqual(n, a.FrameCount);
            Assert.AreEqual(0, a.FrameIndex, "standing: frame 0");
            var seen = new List<int>();
            for (int i = 0; i < 195; i++) // 1.95 m in steps of 1 cm: two cycles of 1 m (the end is not on a boundary)
            {
                a.Advance(0.01f);
                yield return null;
                if (seen.Count == 0 || seen[seen.Count - 1] != a.FrameIndex) seen.Add(a.FrameIndex);
            }
            var expected = new List<int>();
            for (int cycle = 0; cycle < 2; cycle++)
            {
                if (n == 3) expected.AddRange(new[] { 1, 0, 2, 0 });
                else if (n == 2) expected.AddRange(new[] { 1, 0 });
                else for (int f = 1; f < n; f++) expected.Add(f);
            }
            CollectionAssert.AreEqual(expected, seen, n >= 4 ? "frame 0 is only for standing, the cycle is 1 … N-1" : "a short set walks 1 → 0 → 2 → 0 (the legs pass, no scissors)");
            a.Stop();
            yield return null;
            Assert.AreEqual(0, a.FrameIndex, "stopped: frame 0");
        }

        [Test]
        public void Step_frame_follows_the_path_not_the_clock_for_any_length()
        {
            Assert.AreEqual(0, CharacterSpriteSet.StepFrame(1, 0.8f, 5f), "one picture");
            Assert.AreEqual(0, CharacterSpriteSet.StepFrame(8, 0.8f, 0f), "no path — standing");
            for (int n = 2; n <= 12; n++)
                for (float p = 0.001f; p < 3f; p += 0.013f)
                    Assert.That(CharacterSpriteSet.StepFrame(n, 0.8f, p), Is.InRange(n >= 4 ? 1 : 0, n - 1), "n=" + n + " p=" + p);
            Assert.AreEqual(CharacterSpriteSet.StepFrame(8, 0.8f, 0.3f), CharacterSpriteSet.StepFrame(8, 0.8f, 0.3f + 0.8f), "periodic");
        }

        // ------------------------------------------------------------------ the pace of the legs

        /// <summary>The real registry (Characters.asset): steps a minute at the pace the figure walks must be a human one, not a mincing.</summary>
        [Test]
        public void The_pace_of_the_legs_in_the_registry_is_a_walk_not_a_mincing()
        {
            var reg = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterRegistry>("Assets/Art/Registries/Characters.asset");
            Assume.That(reg, Is.Not.Null, "the registry is built");
            // (id, speed m/s): the residents walk 1.4 (data/npcs.json walkSpeed), the heroine 2.5 (data/movement.json)
            var people = new[] { ("peasant", 1.4f), ("guard", 1.4f), ("merchant", 1.4f), ("barkeep", 1.4f), ("smith", 1.4f), ("herbalist", 1.4f), ("townswoman", 1.4f), ("old_man", 1.4f), ("weaver", 1.4f), ("heroine", 2.5f) };
            foreach (var (id, speed) in people)
            {
                float stride = reg.Get(id).StrideMeters;                 // metres of a cycle = two steps
                float stepsPerMinute = speed / stride * 2f * 60f;
                Assert.That(stepsPerMinute, Is.InRange(90f, 190f), id + ": " + stepsPerMinute + " steps a minute (stride " + stride + " m)");
            }
            foreach (var id in new[] { "boar", "wolf" })
                Assert.That(reg.Get(id).StrideMeters, Is.InRange(1f, 2.5f), id);
        }

        [UnityTest]
        public IEnumerator A_set_with_nothing_between_changes_the_picture_at_once_as_before()
        {
            var a = Figure("plain", 3, Vector3.zero);
            a.FaceDirection(Vector3.right);
            yield return Settle(0.2f);
            a.FaceDirection(Vector3.left);
            yield return null;
            Assert.AreEqual(ShownKind.Stand, a.Shown);
            Assert.IsTrue(a.Mirrored);
            Assert.Less(a.Card.localScale.x, 0f, "mirrored at once");
        }

        // ------------------------------------------------------------------ turn

        [UnityTest]
        public IEnumerator A_turn_plays_the_drawn_frames_between_the_views_then_the_new_view()
        {
            var a = Figure("turn", 3, Vector3.zero, false, ("turn_front_side", 4));
            yield return Settle(0.2f);
            Assert.AreEqual(Facing.Front, a.Facing);
            a.FaceDirection(Vector3.left);                 // front → the left side: the frames of front → side, mirrored
            var seen = new List<string>();
            float t0 = Time.time, tEnd = -1f;
            while (Time.time - t0 < 0.5f)
            {
                yield return null;
                if (a.Shown == ShownKind.Turn)
                {
                    if (seen.Count == 0 || seen[seen.Count - 1] != Name(a)) seen.Add(Name(a));
                    Assert.Less(a.Card.localScale.x, 0f, "the left side: the turn frames are mirrored");
                }
                else if (seen.Count > 0 && tEnd < 0f) tEnd = Time.time;
            }
            CollectionAssert.AreEqual(new[] { "turn_front_side_0", "turn_front_side_1", "turn_front_side_2", "turn_front_side_3" }, seen);
            Assert.LessOrEqual(tEnd - t0, a.Look.TurnSeconds + 0.06f, "a turn lasts about a tenth of a second");
            Assert.AreEqual(ShownKind.Stand, a.Shown);
            Assert.AreEqual(Facing.Side, a.Facing);

            a.FaceDirection(Vector3.forward);              // side → back: only back → side is drawn? no — nothing: at once
            yield return null;
            Assert.AreNotEqual(ShownKind.Turn, a.Shown);
        }

        [UnityTest]
        public IEnumerator Only_the_opposite_turn_drawn_is_played_backwards()
        {
            var a = Figure("turnrev", 3, Vector3.zero, false, ("turn_front_side", 3));
            a.FaceDirection(Vector3.right);
            yield return Settle(0.4f);                     // front → right side played
            a.FaceDirection(Vector3.back);                 // side → front: the same frames backwards
            var seen = new List<string>();
            for (float t0 = Time.time; Time.time - t0 < 0.4f;)
            {
                yield return null;
                if (a.Shown == ShownKind.Turn && (seen.Count == 0 || seen[seen.Count - 1] != Name(a))) seen.Add(Name(a));
            }
            CollectionAssert.AreEqual(new[] { "turn_front_side_2", "turn_front_side_1", "turn_front_side_0" }, seen);
        }

        [UnityTest]
        public IEnumerator A_turn_from_side_to_side_goes_through_the_drawn_frames_and_back()
        {
            var a = Figure("turnss", 3, Vector3.zero, false, ("turn_side_side", 5));
            a.FaceDirection(Vector3.right);
            yield return Settle(0.4f);
            a.FaceDirection(Vector3.left);
            var seen = new List<string>();
            for (float t0 = Time.time; Time.time - t0 < 0.4f;)
            {
                yield return null;
                if (a.Shown == ShownKind.Turn && (seen.Count == 0 || seen[seen.Count - 1] != Name(a))) seen.Add(Name(a));
            }
            Assert.AreEqual("turn_side_side_0", seen[0]);
            Assert.AreEqual("turn_side_side_4", seen[seen.Count - 1]);
            seen.Clear();
            a.FaceDirection(Vector3.right);                // left → right: backwards
            for (float t0 = Time.time; Time.time - t0 < 0.4f;)
            {
                yield return null;
                if (a.Shown == ShownKind.Turn && (seen.Count == 0 || seen[seen.Count - 1] != Name(a))) seen.Add(Name(a));
            }
            Assert.AreEqual("turn_side_side_4", seen[0]);
            Assert.AreEqual("turn_side_side_0", seen[seen.Count - 1]);
        }

        // ------------------------------------------------------------------ start, stop, idle

        [UnityTest]
        public IEnumerator The_first_steps_and_the_stop_are_drawn_frames_when_the_registry_has_them()
        {
            var a = Figure("startstop", 8, Vector3.zero, false, ("start_side", 3), ("stop_side", 2));
            a.FaceDirection(Vector3.right);
            yield return Settle(0.4f);
            a.Advance(0.01f);
            yield return null;
            Assert.AreEqual(ShownKind.Start, a.Shown);
            StringAssert.StartsWith("start_side", Name(a));
            yield return Settle(0.05f);
            for (int i = 0; i < 40; i++) { a.Advance(0.005f); yield return null; }  // 0.2 m further and past the start frames
            yield return Settle(0.12f);
            a.Advance(0.005f);
            yield return null;
            Assert.AreEqual(ShownKind.Walk, a.Shown);
            a.Stop();
            yield return null;
            Assert.AreEqual(ShownKind.Stop, a.Shown);
            StringAssert.StartsWith("stop_side", Name(a));
            yield return Settle(a.Look.StopSeconds + 0.1f);
            Assert.AreEqual(ShownKind.Stand, a.Shown);
            Assert.AreEqual(0, a.FrameIndex);
        }

        [UnityTest]
        public IEnumerator Standing_loops_the_idle_frames_each_figure_from_its_own_place()
        {
            var a = Figure("idle", 3, Vector3.zero, false, ("idle_front", 4));
            yield return Settle(0.1f);
            Assert.AreEqual(ShownKind.Idle, a.Shown);
            var seen = new HashSet<string>();
            for (float t0 = Time.time; Time.time - t0 < 1.6f;) { yield return null; seen.Add(Name(a)); }
            Assert.AreEqual(4, seen.Count, "all four drawn frames of the breath were shown");
            a.Advance(0.01f);
            yield return null;
            Assert.AreNotEqual(ShownKind.Idle, a.Shown, "walking: not the breath");
            var h = a.Card.localScale.y;
            yield return Settle(0.3f);
            Assert.AreEqual(h, a.Card.localScale.y, 1e-5f, "nothing is made by code on top: the size does not breathe");
        }

        [UnityTest]
        public IEnumerator Without_idle_frames_a_stand_is_one_still_frame_and_the_card_does_not_change_size()
        {
            var a = Figure("still", 3, Vector3.zero);
            yield return Settle(0.2f);
            var h = a.Card.localScale; var n = Name(a);
            for (float t0 = Time.time; Time.time - t0 < 3f;) { yield return null; Assert.AreEqual(n, Name(a)); Assert.AreEqual(h, a.Card.localScale); }
        }

        // ------------------------------------------------------------------ poses of any number of frames

        [UnityTest]
        public IEnumerator A_pose_of_five_frames_plays_all_five_over_its_phase_and_a_fall_is_a_pose_too()
        {
            var a = Figure("pose5", 3, Vector3.zero, false, ("attack_side", 5), ("fall_side", 4));
            a.FaceDirection(Vector3.right);
            yield return null;
            var seen = new List<string>();
            for (int i = 0; i <= 20; i++)
            {
                a.SetPose(new BillboardPose { Action = "attack", ActionPhase = i / 20f });
                yield return null;
                if (seen.Count == 0 || seen[seen.Count - 1] != Name(a)) seen.Add(Name(a));
            }
            CollectionAssert.AreEqual(new[] { "attack_side_0", "attack_side_1", "attack_side_2", "attack_side_3", "attack_side_4" }, seen);
            Assert.AreEqual(ShownKind.Pose, a.Shown);
            seen.Clear();
            for (int i = 0; i <= 12; i++)
            {
                a.SetPose(new BillboardPose { Action = "fall", ActionPhase = i / 13f });
                yield return null;
                if (seen.Count == 0 || seen[seen.Count - 1] != Name(a)) seen.Add(Name(a));
            }
            Assert.AreEqual(4, seen.Count);
        }

        // ------------------------------------------------------------------ no allocations

        static IEnumerator Allocated(int frames, System.Action<long> total, System.Action each = null)
        {
            long sum = 0;
            using (var rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                for (int i = 0; i < frames; i++)
                {
                    each?.Invoke();
                    yield return null;
                    sum += rec.LastValue;
                }
            }
            total(sum);
        }

        IEnumerator AllocRun(string tag, System.Action<long, long> result)
        {
            var figs = new List<BillboardSprite>();
            for (int i = 0; i < 12; i++)
                figs.Add(Figure(tag + i, i % 2 == 0 ? 3 : 8, new Vector3(i, 0, 0), true,
                    ("turn_front_side", 4), ("turn_side_side", 5), ("start_side", 3), ("stop_side", 2), ("idle_front", 4), ("idle_side", 4), ("fall_side", 4)));
            int tick = 0;
            void Drive()
            {
                tick++;
                for (int i = 0; i < figs.Count; i++)
                {
                    var f = figs[i];
                    switch ((i + tick / 40) % 5)
                    {
                        case 0: f.FaceDirection((tick / 20) % 2 == 0 ? Vector3.right : Vector3.left); f.Advance(0.02f); break;
                        case 1: f.Stop(); break;
                        case 2: f.FaceDirection((tick / 25) % 2 == 0 ? Vector3.back : Vector3.right); f.Advance(0.02f); break;
                        case 3: f.SetPose(new BillboardPose { Action = (tick / 7) % 2 == 0 ? "attack" : "fall", ActionPhase = (tick % 30) / 30f }); break;
                        default: f.SetPose(BillboardPose.Stand); break;
                    }
                }
            }
            for (int i = 0; i < 90; i++) { Drive(); yield return null; } // warm-up
            long with = 0, bare = 0;
            yield return Allocated(240, t => with = t, Drive);
            foreach (var f in figs) f.enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
            yield return Allocated(240, t => bare = t, () => { tick++; });
            result(with, bare);
        }

        [UnityTest]
        public IEnumerator Playing_the_drawn_frames_allocates_nothing_in_a_frame()
        {
            // the least of three runs: first-use allocations (a mesh, a material) and the editor's own noise are not the steady state
            long with = 0, bare = 0, best = long.MaxValue;
            for (int k = 0; k < 3; k++)
                yield return AllocRun("alloc" + k + "_", (w, b) => { if (w - b < best) { best = w - b; with = w; bare = b; } });
            Debug.Log($"[ZD:Test] GC allocated in 240 frames of 12 figures (turns, start, stop, idle, poses): {with} B, without the sprites {bare} B");
            Assert.LessOrEqual(best, 1024, "GC bytes of the sprites over 240 frames (over the editor's own)");
        }
    }
}
