using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZeldaDaughter.Core.Cutout;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Tests
{
    /// <summary>
    /// D-27 pilot (docs/done-criteria/D-27.md, item 2): in the game the heroine's side view is a cut-out rig — painted parts on bones — that
    /// takes over the card only for walking and standing in that view; drawn poses, lying and the other views stay the card's. The run in the
    /// game keeps a planted foot in place and the bones whole (the joint trace goes to docs/demo/d27/unity-trace.json for
    /// tools/art/d27_metrics.py). Needs the rig files (tools/art/d27_rig.py); without them the scene has no rig and the tests are inconclusive.
    /// </summary>
    public class D27CutoutTests
    {
        GameSession _s;
        HeroController _hero;
        HeroView _view;
        BillboardSprite _sprite;
        CutoutFigure _cut;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestSaves.UseCleanFolder();
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/test-demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _s = Object.FindFirstObjectByType<GameSession>();
            _hero = Object.FindFirstObjectByType<HeroController>();
            _view = _hero.GetComponent<HeroView>();
            _sprite = _view.Sprite;
            _cut = _sprite.GetComponent<CutoutFigure>();
            Assume.That(_cut, Is.Not.Null, "SceneBuilder.AddHeroView put the rig on the hero (the rig files exist)");
            _hero.UseDpi(160f);
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            TestSaves.Clear();
        }

        static Vector3 CamForward() { var f = Camera.main.transform.forward; f.y = 0f; return f.normalized; }
        static Vector3 CamRight() { var r = Camera.main.transform.right; r.y = 0f; return r.normalized; }

        IEnumerator Walk(Vector3 dir, float meters, float speed, System.Action each = null)
        {
            var cc = _hero.GetComponent<CharacterController>();
            cc.minMoveDistance = 0f;
            dir.y = 0f; dir.Normalize();
            float left = meters;
            while (left > 0f)
            {
                float step = Mathf.Min(left, speed * Time.deltaTime);
                cc.Move(dir * step);
                left -= step;
                yield return null;
                each?.Invoke();
            }
        }

        MeshRenderer Card => _sprite.Card.GetComponent<MeshRenderer>();
        MeshRenderer Rig => _cut.Root.GetComponent<MeshRenderer>();

        [UnityTest]
        public IEnumerator The_side_run_is_the_rig_the_front_and_back_are_the_drawn_frames()
        {
            yield return Walk(CamRight(), 1.0f, 2.5f);
            Assert.AreEqual(Facing.Side, _sprite.Facing);
            Assert.IsTrue(_cut.Showing, "running sideways: the rig");
            Assert.IsTrue(Rig.enabled); Assert.IsFalse(Card.enabled, "the card hides under the rig");
            Assert.Less(Quaternion.Angle(_cut.Root.rotation, Camera.main.transform.rotation), 0.5f, "the rig faces the camera like the card");
            Assert.Greater(_cut.Root.localScale.x, 0f, "to the right: drawn as is");
            yield return Walk(-CamRight(), 1.0f, 2.5f);
            Assert.IsTrue(_cut.Showing);
            Assert.Less(_cut.Root.localScale.x, 0f, "to the left: mirrored");
            yield return Walk(-CamForward(), 0.8f, 2.5f);
            Assert.AreEqual(Facing.Front, _sprite.Facing);
            Assert.IsFalse(_cut.Showing, "toward the camera: the drawn frames");
            Assert.IsTrue(Card.enabled); Assert.IsFalse(Rig.enabled);
        }

        [UnityTest]
        public IEnumerator A_drawn_pose_shows_the_card_even_sideways()
        {
            yield return Walk(CamRight(), 0.6f, 2.5f);
            Assert.IsTrue(_cut.Showing);
            _s.Events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, _hero.transform.position + CamRight() * 2f, "fists"));
            yield return null; yield return null;
            Assert.IsFalse(_cut.Showing, "the attack is a drawn pose");
            Assert.IsTrue(Card.enabled);
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(_cut.Showing, "back to standing sideways: the rig");
        }

        [UnityTest]
        public IEnumerator Standing_breathes_by_the_bones_not_by_the_picture()
        {
            yield return Walk(CamRight(), 0.5f, 2.5f);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_cut.Showing);
            Assert.AreEqual(0f, _cut.Gait.Gait, 1e-3f, "standing");
            float lo = 9f, hi = -9f;
            var scale = _cut.Root.localScale;
            var ankle = _cut.Gait[Joint.AnkleNear];
            for (float t = 0f; t < _cut.Gait.Settings.Stand.BreathSeconds + 0.2f; t += Time.deltaTime)
            {
                yield return null;
                lo = Mathf.Min(lo, _cut.Gait[Joint.Pelvis].Y); hi = Mathf.Max(hi, _cut.Gait[Joint.Pelvis].Y);
                Assert.AreEqual(scale, _cut.Root.localScale, "the picture is never scaled");
                Assert.Less((_cut.Gait[Joint.AnkleNear] - ankle).Length, 1e-4f, "the feet stay");
            }
            Assert.Greater(hi - lo, 0.004f, "the hips sink and rise with the breath (soft knees)");
        }

        /// <summary>The run in the game, 2.5 m/s at 30 frames a second: the planted foot stays in the world, the bones keep their length.</summary>
        [UnityTest]
        public IEnumerator The_run_in_the_game_keeps_a_planted_foot_and_whole_bones()
        {
            Time.captureFramerate = 30;
            yield return Walk(CamRight(), 0.5f, 2.5f);
            var g = _cut.Gait;
            var rig = g.Rig;
            var right = CamRight();
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.Append("{\"rig\":\"heroine_side\",\"ppu\":").Append(rig.PixelsPerMeter.ToString(ci)).Append(",\"fps\":30,\"speed\":2.5,\"stride\":")
              .Append(g.Settings.Run.StrideMeters.ToString(ci)).Append(",\"source\":\"unity\",\"joints\":[");
            for (int j = 0; j < CutoutGait.JointCount; j++) sb.Append(j > 0 ? "," : "").Append('"').Append(((Joint)j).ToString()).Append('"');
            sb.Append("],\"frames\":[");
            float slip = 0f, stretch = 0f;
            Vector3 prevN = default, prevF = default; bool prevPN = false, prevPF = false;
            int n = 0;
            var start = _cut.transform.position;
            yield return Walk(right, 6.8f, 2.5f, () =>
            {
                var root = _cut.transform.position;
                float x = Vector3.Dot(root - start, right);
                var an = World(root, right, g[Joint.AnkleNear]); var af = World(root, right, g[Joint.AnkleFar]);
                if (g.Gait >= 1f)
                {
                    if (g.NearPlanted && prevPN) slip = Mathf.Max(slip, (an - prevN).magnitude);
                    if (g.FarPlanted && prevPF) slip = Mathf.Max(slip, (af - prevF).magnitude);
                    stretch = Mathf.Max(stretch, Mathf.Abs((g[Joint.KneeNear] - g[Joint.HipNear]).Length - rig.Thigh));
                    stretch = Mathf.Max(stretch, Mathf.Abs((g[Joint.AnkleNear] - g[Joint.KneeNear]).Length - rig.Shin));
                }
                prevN = an; prevF = af; prevPN = g.NearPlanted; prevPF = g.FarPlanted;
                if (n++ > 0) sb.Append(',');
                sb.Append("{\"t\":").Append(Time.time.ToString("0.#####", ci)).Append(",\"x\":").Append(x.ToString("0.#####", ci))
                  .Append(",\"phase\":").Append(g.Phase.ToString("0.#####", ci)).Append(",\"gait\":").Append(g.Gait.ToString("0.###", ci))
                  .Append(",\"planted\":[").Append(g.NearPlanted ? 1 : 0).Append(',').Append(g.FarPlanted ? 1 : 0).Append("],\"j\":[");
                for (int j = 0; j < CutoutGait.JointCount; j++)
                {
                    var v = g[(Joint)j];
                    sb.Append(j > 0 ? "," : "").Append('[').Append(v.X.ToString("0.#####", ci)).Append(',').Append(v.Y.ToString("0.#####", ci)).Append(']');
                }
                sb.Append("]}");
            });
            sb.Append("]}");
            Time.captureFramerate = 0;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "demo", "d27", "unity-trace.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            ZdLog.Info("Cutout", $"trace {n} frames → {path}; slip {slip * rig.PixelsPerMeter:0.00} px/frame, stretch {stretch * rig.PixelsPerMeter:0.000} px");
            Assert.Greater(n, 60);
            Assert.LessOrEqual(slip * rig.PixelsPerMeter, 2f, "a planted foot slides ≤ 2 px a frame at 320 px/m");
            Assert.Less(stretch * rig.PixelsPerMeter, 0.25f, "bones keep their length");
        }

        /// <summary>A rig point in the world along the run: the rig's +X is the way she runs (the camera's right here).</summary>
        static Vector3 World(Vector3 root, Vector3 right, ZeldaDaughter.Core.Common.Vec2 p) => root + right * p.X + Vector3.up * p.Y;
    }
}
