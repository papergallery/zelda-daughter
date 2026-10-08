#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Cutout;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>
    /// D-27 (docs/done-criteria/D-27.md, items 2–3): the cut-out run and stand. The checks are the joint-trace checks of ref2game
    /// (MIT, github.com/studioigor/ref2game, animation.md §14): a planted foot does not slide, bones do not stretch, the legs alternate, the
    /// pelvis rides the stance leg, the arms swing against the legs, the cycle has no jump at its seam, standing breathes by the bones.
    /// A synthetic rig (proportions of the heroine D, 320 px/m) and the real one (when the art exists) with data/gait.json.
    /// </summary>
    public class CutoutGaitTests
    {
        const float Fps = 60f, Speed = 2.5f; // data/movement.json: the heroine runs 2.5 m/s
        const float Px = 1f / 320f;          // one pixel at 320 px/m

        static GaitSettings Gait => GaitSettings.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot, "gait.json")));

        static string RealRigPath => Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName,
            "ZeldaDaughter", "Assets", "Art", "Sprites", "heroine", "rig", "heroine_side.rig.json");

        /// <summary>A rig in pixels like the heroine's side view: 538 px tall, hips at 250, knee 125, ankle 30.</summary>
        internal static string SyntheticRigJson()
        {
            var parts = new StringBuilder();
            int k = 0;
            foreach (var name in CutoutRig.Layers)
            {
                if (k++ > 0) parts.Append(',');
                parts.Append('"').Append(name).Append("\":{\"rect\":[0,0,40,120],\"pivot\":[20,5],\"rest\":0}");
            }
            return "{\"id\":\"synthetic\",\"view\":\"side\",\"ppu\":320,\"atlas\":{\"file\":\"x.png\",\"w\":1024,\"h\":1024}," +
                   "\"joints\":{\"pelvis\":[0,262],\"hipNear\":[-12,250],\"hipFar\":[10,252],\"knee\":[-12,138],\"ankle\":[-12,30]," +
                   "\"toe\":[40,2],\"heel\":[-30,2],\"shoulderNear\":[2,420],\"shoulderFar\":[14,424],\"elbow\":[2,330],\"hand\":[2,250]," +
                   "\"head\":[20,470],\"top\":[10,538]},\"parts\":{" + parts + "}}";
        }

        static CutoutRig Synthetic => CutoutRig.Parse(SyntheticRigJson());

        sealed class Frame
        {
            public float T, BodyX, Phase, Gait;
            public bool NearPlanted, FarPlanted, Overreached;
            public Vec2[] J = new Vec2[CutoutGait.JointCount];
        }

        /// <summary>Stand <paramref name="standBefore"/> s, run <paramref name="run"/> s at 2.5 m/s, stand <paramref name="standAfter"/> s; 60 fps.</summary>
        static List<Frame> Play(CutoutRig rig, GaitSettings s, float standBefore, float run, float standAfter, float speed = Speed)
        {
            var g = new CutoutGait(rig, s);
            var list = new List<Frame>();
            float dt = 1f / Fps, x = 0f, t = 0f;
            int n0 = (int)Math.Round(standBefore * Fps), n1 = (int)Math.Round(run * Fps), n2 = (int)Math.Round(standAfter * Fps);
            for (int i = 0; i < n0 + n1 + n2; i++)
            {
                float m = i >= n0 && i < n0 + n1 ? speed * dt : 0f;
                x += m; t += dt;
                g.Step(dt, m);
                var f = new Frame { T = t, BodyX = x, Phase = g.Phase, Gait = g.Gait, NearPlanted = g.NearPlanted, FarPlanted = g.FarPlanted, Overreached = g.Overreached };
                for (int j = 0; j < CutoutGait.JointCount; j++) f.J[j] = g[(Joint)j];
                list.Add(f);
            }
            return list;
        }

        static IEnumerable<Frame> Steady(List<Frame> fr) => fr.Where(f => f.Gait >= 1f);
        static Vec2 World(Frame f, Joint j) => new Vec2(f.BodyX + f.J[(int)j].X, f.J[(int)j].Y);

        // ------------------------------------------------------------------ feet

        [Fact]
        public void A_planted_foot_does_not_slide_in_the_world()
        {
            foreach (var rig in Rigs())
            {
                var fr = Play(rig, Gait, 0.5f, 3f, 0f);
                float worst = 0f;
                for (int i = 1; i < fr.Count; i++)
                {
                    var a = fr[i - 1]; var b = fr[i];
                    if (a.Gait < 1f) continue;
                    foreach (var (planted, ankle) in new[] { (a.NearPlanted && b.NearPlanted, Joint.AnkleNear), (a.FarPlanted && b.FarPlanted, Joint.AnkleFar) })
                    {
                        if (!planted) continue;
                        var d = World(b, ankle) - World(a, ankle);
                        worst = Math.Max(worst, d.Length);
                    }
                }
                Assert.True(worst <= 2f * Px, $"{rig.Id}: planted foot slides {worst / Px:0.00} px a frame (≤ 2 at 320 px/m)");
                Assert.True(worst <= 1e-4f, $"{rig.Id}: by construction the stance foot is fixed, slides {worst / Px:0.000} px");
            }
        }

        [Fact]
        public void The_legs_alternate_one_touchdown_each_per_cycle_half_a_cycle_apart()
        {
            var fr = Steady(Play(Synthetic, Gait, 0.5f, 3f, 0f)).ToList();
            var downs = new List<(float t, bool near)>();
            for (int i = 1; i < fr.Count; i++)
            {
                if (fr[i].NearPlanted && !fr[i - 1].NearPlanted) downs.Add((fr[i].T, true));
                if (fr[i].FarPlanted && !fr[i - 1].FarPlanted) downs.Add((fr[i].T, false));
            }
            Assert.True(downs.Count >= 6, "touchdowns: " + downs.Count);
            for (int i = 1; i < downs.Count; i++) Assert.NotEqual(downs[i - 1].near, downs[i].near);
            float step = Gait.Run.StrideMeters / 2f / Speed;
            for (int i = 1; i < downs.Count; i++) Assert.InRange(downs[i].t - downs[i - 1].t, step - 1.5f / Fps, step + 1.5f / Fps);
            Assert.DoesNotContain(fr, f => f.NearPlanted && f.FarPlanted && Gait.Run.Duty < 0.5f);
        }

        [Fact]
        public void The_swing_foot_leaves_the_ground_and_moves_forward_while_the_stance_foot_sweeps_back()
        {
            var fr = Steady(Play(Synthetic, Gait, 0.5f, 2f, 0f)).ToList();
            float ankleH = Synthetic.AnkleHeight;
            Assert.Contains(fr, f => !f.NearPlanted && f.J[(int)Joint.AnkleNear].Y > ankleH + 0.08f);
            for (int i = 1; i < fr.Count; i++)
                if (fr[i].NearPlanted && fr[i - 1].NearPlanted)
                    Assert.True(fr[i].J[(int)Joint.AnkleNear].X < fr[i - 1].J[(int)Joint.AnkleNear].X, "stance sweeps backward (no moonwalk)");
        }

        // ------------------------------------------------------------------ bones

        [Fact]
        public void Bones_keep_their_length_and_the_legs_reach_their_feet()
        {
            foreach (var rig in Rigs())
            {
                var fr = Play(rig, Gait, 1f, 3f, 1.5f);
                float worst = 0f;
                foreach (var f in fr)
                {
                    Assert.False(f.Overreached && f.Gait >= 1f, $"{rig.Id}: a running leg cannot reach its foot at t={f.T:0.00} phase {f.Phase:0.000}");
                    foreach (var (a, b, len) in Segments(rig))
                        worst = Math.Max(worst, Math.Abs((f.J[(int)b] - f.J[(int)a]).Length - len));
                }
                Assert.True(worst < 0.25f * Px, $"{rig.Id}: a bone stretches {worst / Px:0.000} px");
            }
        }

        static IEnumerable<(Joint, Joint, float)> Segments(CutoutRig r) => new[]
        {
            (Joint.HipNear, Joint.KneeNear, r.Thigh), (Joint.KneeNear, Joint.AnkleNear, r.Shin),
            (Joint.HipFar, Joint.KneeFar, r.Thigh), (Joint.KneeFar, Joint.AnkleFar, r.Shin),
            (Joint.ShoulderNear, Joint.ElbowNear, r.UpperArm), (Joint.ElbowNear, Joint.HandNear, r.Forearm),
            (Joint.ShoulderFar, Joint.ElbowFar, r.UpperArm), (Joint.ElbowFar, Joint.HandFar, r.Forearm),
        };

        [Fact]
        public void Knees_bend_forward_never_backward()
        {
            foreach (var f in Play(Synthetic, Gait, 0.5f, 2f, 1f))
                foreach (var (hip, knee, ankle) in new[] { (Joint.HipNear, Joint.KneeNear, Joint.AnkleNear), (Joint.HipFar, Joint.KneeFar, Joint.AnkleFar) })
                {
                    var h = f.J[(int)hip]; var k = f.J[(int)knee]; var a = f.J[(int)ankle];
                    float cross = (a.X - h.X) * (k.Y - h.Y) - (a.Y - h.Y) * (k.X - h.X); // > 0: the knee is on the +X (facing) side of hip→ankle
                    Assert.True(cross >= -1e-6f, $"knee bends backward at t={f.T:0.00}");
                }
        }

        // ------------------------------------------------------------------ pelvis, arms

        [Fact]
        public void The_pelvis_rides_the_stance_leg_lowest_at_mid_stance_with_equal_dips()
        {
            var fr = Steady(Play(Synthetic, Gait, 0.5f, 3f, 0f)).ToList();
            float duty = Gait.Run.Duty;
            // in stance the pelvis height is the stance leg's: the hip–ankle distance is the shortened leg, not a sine on its own
            foreach (var f in fr.Where(f => f.NearPlanted))
            {
                float u = f.Phase;
                float k = Gait.Run.Reach - Gait.Run.Compression * (float)Math.Sin(Math.PI * u / duty);
                float len = (f.J[(int)Joint.AnkleNear] - f.J[(int)Joint.HipNear]).Length;
                // within 1 % of the leg: the lean turns the hips about the pelvis by a pixel or two, the same for both steps
                float leg = Synthetic.Thigh + Synthetic.Shin;
                Assert.InRange(len, leg * k - 0.01f * leg, leg * k + 0.01f * leg);
            }
            // a run is lowest at mid-stance and higher in the flight
            float mid = fr.Where(f => Math.Abs(f.Phase - duty / 2) < 0.02f).Average(f => f.J[(int)Joint.Pelvis].Y);
            float fly = fr.Where(f => !f.NearPlanted && !f.FarPlanted).Max(f => f.J[(int)Joint.Pelvis].Y);
            Assert.True(fly > mid + 0.005f, $"flight {fly:0.000} m over mid-stance {mid:0.000} m");
            // no limp: the two dips of a cycle are equal
            float dipN = fr.Where(f => f.NearPlanted).Min(f => f.J[(int)Joint.Head].Y);
            float dipF = fr.Where(f => f.FarPlanted).Min(f => f.J[(int)Joint.Head].Y);
            Assert.True(Math.Abs(dipN - dipF) < 2f * Px, $"limp: head dips {dipN:0.0000} vs {dipF:0.0000} m");
        }

        [Fact]
        public void Each_arm_swings_against_its_own_leg()
        {
            var fr = Steady(Play(Synthetic, Gait, 0.5f, 3f, 0f)).ToList();
            foreach (var (hand, ankle) in new[] { (Joint.HandNear, Joint.AnkleNear), (Joint.HandFar, Joint.AnkleFar) })
            {
                var hx = fr.Select(f => f.J[(int)hand].X).ToArray();
                var ax = fr.Select(f => f.J[(int)ankle].X).ToArray();
                Assert.True(Correlation(hx, ax) < -0.6f, $"{hand} vs {ankle}: correlation {Correlation(hx, ax):0.00}");
            }
        }

        static float Correlation(float[] a, float[] b)
        {
            double ma = a.Average(), mb = b.Average(), sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < a.Length; i++) { sab += (a[i] - ma) * (b[i] - mb); saa += (a[i] - ma) * (a[i] - ma); sbb += (b[i] - mb) * (b[i] - mb); }
            return (float)(sab / Math.Sqrt(saa * sbb + 1e-12));
        }

        // ------------------------------------------------------------------ cycle and stand

        [Fact]
        public void The_phase_follows_the_path_not_the_clock()
        {
            var g = new CutoutGait(Synthetic, Gait);
            g.Step(0.5f, 0f);
            Assert.Equal(0f, g.Phase);
            g.Step(1f / 60, Gait.Run.StrideMeters * 0.25f);
            Assert.InRange(g.Phase, 0.2499f, 0.2501f);
            g.Step(5f, Gait.Run.StrideMeters * 0.25f);
            Assert.InRange(g.Phase, 0.4999f, 0.5001f);
        }

        [Fact]
        public void The_cycle_has_no_jump_at_its_seam_nor_anywhere()
        {
            foreach (var rig in Rigs())
            {
                var fr = Steady(Play(rig, Gait, 0.5f, 3f, 0f)).ToList();
                float seam = 0f, rest = 0f;
                string where = "";
                for (int i = 2; i < fr.Count; i++)
                {
                    bool atSeam = fr[i].Phase < fr[i - 1].Phase || fr[i - 1].Phase < fr[i - 2].Phase;
                    for (int j = 0; j < CutoutGait.JointCount; j++)
                    {
                        // in the world: the body's own steady travel adds nothing to a second difference
                        float acc = (fr[i].J[j] - fr[i - 1].J[j] * 2f + fr[i - 2].J[j]).Length;
                        if (atSeam) seam = Math.Max(seam, acc);
                        else if (acc > rest) { rest = acc; where = $"{(Joint)j} at phase {fr[i].Phase:0.000}"; }
                    }
                }
                Assert.True(seam <= rest * 1.05f + 1e-5f, $"{rig.Id}: the seam jerks {seam / Px:0.00} px/frame² against {rest / Px:0.00} elsewhere");
                Assert.True(rest < 12f * Px, $"{rig.Id}: {where} jerks {rest / Px:0.00} px/frame²");
            }
        }

        [Fact]
        public void Standing_breathes_by_the_bones_the_feet_stay()
        {
            var g = new CutoutGait(Synthetic, Gait);
            float minY = 9f, maxY = -9f, minA = 9f, maxA = -9f;
            Vec2 ankle0 = default; bool first = true;
            for (int i = 0; i < (int)(Gait.Stand.BreathSeconds * Fps) + 2; i++)
            {
                g.Step(1f / Fps, 0f);
                minY = Math.Min(minY, g[Joint.Pelvis].Y); maxY = Math.Max(maxY, g[Joint.Pelvis].Y);
                float arm = CutoutGait.FromDown(g[Joint.ElbowNear] - g[Joint.ShoulderNear]);
                minA = Math.Min(minA, arm); maxA = Math.Max(maxA, arm);
                if (first) { ankle0 = g[Joint.AnkleNear]; first = false; }
                Assert.True((g[Joint.AnkleNear] - ankle0).Length < 1e-5f, "a standing foot moved");
                Assert.False(g.Overreached, "a standing leg cannot reach");
            }
            Assert.True(maxY - minY >= Gait.Stand.BreathHipMeters * 0.9f, $"the hips breathe {(maxY - minY) / Px:0.0} px");
            Assert.True(maxA - minA > 0.5f * (float)Math.PI / 180f, "the arms move with the breath");
            // the knees are soft, not locked: the leg is shorter than straight
            Assert.True((g[Joint.AnkleNear] - g[Joint.HipNear]).Length < Synthetic.Thigh + Synthetic.Shin - 0.001f);
        }

        [Fact]
        public void Gait_settings_and_the_real_rig_load()
        {
            var s = Gait;
            Assert.InRange(s.Run.Duty, 0.2f, 0.5f);
            Assert.InRange(s.Run.StrideMeters / 2f / Speed * 60f, 0f, 1000f);
            float spm = Speed / (s.Run.StrideMeters / 2f) * 60f;
            Assert.InRange(spm, 90f, 190f); // the same pace rule as D25MotionTests: a human run, not a mincing
            if (!File.Exists(RealRigPath)) return;
            var rig = CutoutRig.Parse(File.ReadAllText(RealRigPath));
            Assert.Equal("heroine", rig.Id);
            Assert.Equal("side", rig.View);
            Assert.InRange(rig.Top.Y - Math.Min(rig.Toe.Y, rig.Heel.Y), 1.5f, 1.8f); // 1.65 m (tools/art/sprites.json)
        }

        static IEnumerable<CutoutRig> Rigs()
        {
            yield return Synthetic;
            if (File.Exists(RealRigPath)) yield return CutoutRig.Parse(File.ReadAllText(RealRigPath));
        }

        // ------------------------------------------------------------------ the joint trace for tools/art/d27_metrics.py

        /// <summary>
        /// With ZD_TRACE_OUT set: writes the joint and part trace of stand 1 s → run 3 s → stand 1.5 s at 60 fps (the real rig) for the metrics
        /// and the frame sheets on the server. Without it — nothing.
        /// </summary>
        [Fact]
        public void Writes_the_trace_when_asked()
        {
            var path = Environment.GetEnvironmentVariable("ZD_TRACE_OUT");
            if (string.IsNullOrEmpty(path)) return;
            var rig = File.Exists(RealRigPath) ? CutoutRig.Parse(File.ReadAllText(RealRigPath)) : Synthetic;
            File.WriteAllText(path, Trace(rig, Gait, 1f, 3f, 1.5f, Speed, Fps));
        }

        internal static string Trace(CutoutRig rig, GaitSettings s, float standBefore, float run, float standAfter, float speed, float fps)
        {
            var g = new CutoutGait(rig, s);
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.Append("{\"rig\":\"").Append(rig.Id).Append('_').Append(rig.View).Append("\",\"ppu\":").Append(rig.PixelsPerMeter.ToString(ci))
              .Append(",\"fps\":").Append(fps.ToString(ci)).Append(",\"speed\":").Append(speed.ToString(ci))
              .Append(",\"stride\":").Append(s.Run.StrideMeters.ToString(ci)).Append(",\"source\":\"core\",\"joints\":[");
            for (int j = 0; j < CutoutGait.JointCount; j++) sb.Append(j > 0 ? "," : "").Append('"').Append(((Joint)j).ToString()).Append('"');
            sb.Append("],\"layers\":[");
            for (int l = 0; l < CutoutRig.Layers.Length; l++) sb.Append(l > 0 ? "," : "").Append('"').Append(CutoutRig.Layers[l]).Append('"');
            sb.Append("],\"frames\":[");
            float dt = 1f / fps, x = 0f, t = 0f;
            int n0 = (int)Math.Round(standBefore * fps), n1 = (int)Math.Round(run * fps), n2 = (int)Math.Round(standAfter * fps);
            for (int i = 0; i < n0 + n1 + n2; i++)
            {
                float m = i >= n0 && i < n0 + n1 ? speed * dt : 0f;
                x += m; t += dt;
                g.Step(dt, m);
                if (i > 0) sb.Append(',');
                sb.Append("{\"t\":").Append(t.ToString("0.#####", ci)).Append(",\"x\":").Append(x.ToString("0.#####", ci))
                  .Append(",\"phase\":").Append(g.Phase.ToString("0.#####", ci)).Append(",\"gait\":").Append(g.Gait.ToString("0.###", ci))
                  .Append(",\"planted\":[").Append(g.NearPlanted ? 1 : 0).Append(',').Append(g.FarPlanted ? 1 : 0).Append("],\"j\":[");
                for (int j = 0; j < CutoutGait.JointCount; j++)
                {
                    var v = g[(Joint)j];
                    sb.Append(j > 0 ? "," : "").Append('[').Append(v.X.ToString("0.#####", ci)).Append(',').Append(v.Y.ToString("0.#####", ci)).Append(']');
                }
                sb.Append("],\"p\":[");
                for (int l = 0; l < CutoutRig.Layers.Length; l++)
                {
                    var v = g.Pivot(l);
                    sb.Append(l > 0 ? "," : "").Append('[').Append(v.X.ToString("0.#####", ci)).Append(',').Append(v.Y.ToString("0.#####", ci))
                      .Append(',').Append(g.Rotation(l).ToString("0.#####", ci)).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
