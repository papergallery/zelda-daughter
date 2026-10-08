#nullable enable
using System;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Cutout
{
    /// <summary>Joints of one solved pose, rig metres (origin — the ground point under the body, +X — facing).</summary>
    public enum Joint
    {
        Pelvis, Head, Top,
        HipNear, KneeNear, AnkleNear, ToeNear, HeelNear,
        HipFar, KneeFar, AnkleFar, ToeFar, HeelFar,
        ShoulderNear, ElbowNear, HandNear,
        ShoulderFar, ElbowFar, HandFar,
    }

    /// <summary>
    /// D-27: the cut-out run and stand of a figure (ADR-0010), solved per frame without allocations. Rules from ref2game
    /// (MIT, github.com/studioigor/ref2game, animation.md §5, §12, §15.1, §15.4):
    /// - the phase comes from the path walked, not the clock (one cycle = <see cref="RunGait.StrideMeters"/>, two steps);
    /// - each foot has an explicit path: in stance it moves back at exactly the body's speed (so it stays planted in the world), in the
    ///   swing it eases forward with a lift and a toe tip, and leaves and meets the ground at the ground's speed (no jerk);
    /// - the knee is solved by two-bone IK from the hip to the ankle and bends forward; the foot stays level in stance;
    /// - the pelvis rides the stance leg: its height is the stance leg's reach over the foot (lowest at mid-stance in a run), and in the
    ///   flight it follows a smooth arc from push-off to the next touchdown;
    /// - the arms swing against the legs, the elbows bend more on the forward swing;
    /// - standing breathes by the bones (the hips sink a little through soft knees, the torso and arms move), never by scaling the picture.
    /// The far leg and arm are the near drawings half a cycle later.
    /// </summary>
    public sealed class CutoutGait
    {
        public const int JointCount = 19;
        /// <summary>The part of the ground's speed the swing foot has when it leaves and meets the ground (ground speed matching).</summary>
        const float SwingMatch = 1f;
        /// <summary>Standing legs reach this part of their length at most (soft knees).</summary>
        const float StandReach = 0.996f;
        const float Deg = (float)(Math.PI / 180.0);

        readonly CutoutRig _rig;
        readonly GaitSettings _s;
        readonly Vec2[] _j = new Vec2[JointCount];
        readonly float[] _rot = new float[CutoutRig.Layers.Length];
        readonly Vec2[] _pivot = new Vec2[CutoutRig.Layers.Length];

        public CutoutGait(CutoutRig rig, GaitSettings settings)
        {
            _rig = rig ?? throw new ArgumentNullException(nameof(rig));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            Solve();
        }

        public CutoutRig Rig => _rig;
        public GaitSettings Settings => _s;
        /// <summary>0..1 along the run cycle; 0 — the near foot touches down.</summary>
        public float Phase { get; private set; }
        /// <summary>0 standing … 1 running (eased by <see cref="GaitSettings.BlendSeconds"/>).</summary>
        public float Gait { get; private set; }
        /// <summary>Seconds lived (the breath).</summary>
        public float Time { get; private set; }
        /// <summary>The near foot (or the far one) is planted now: a running stance, or standing.</summary>
        public bool NearPlanted { get; private set; } = true;
        public bool FarPlanted { get; private set; } = true;
        /// <summary>The leg could not reach its foot's target this frame (the foot would slide).</summary>
        public bool Overreached { get; private set; }

        public Vec2 this[Joint j] => _j[(int)j];
        /// <summary>Rotation of the part of layer <paramref name="layer"/> about its pivot from its drawn angle, radians, counter-clockwise.</summary>
        public float Rotation(int layer) => _rot[layer];
        /// <summary>Where the pivot of the part of layer <paramref name="layer"/> is, rig metres.</summary>
        public Vec2 Pivot(int layer) => _pivot[layer];

        /// <summary>A frame: <paramref name="meters"/> walked along the facing in <paramref name="dt"/> seconds (0 — standing).</summary>
        public void Step(float dt, float meters)
        {
            if (dt < 0f) dt = 0f;
            if (meters < 0f) meters = 0f;
            Time += dt;
            float stride = Math.Max(0.05f, _s.Run.StrideMeters);
            Phase = Frac(Phase + meters / stride);
            float target = meters > 1e-5f ? 1f : 0f;
            float rate = _s.BlendSeconds > 1e-4f ? dt / _s.BlendSeconds : 1f;
            Gait = target > Gait ? Math.Min(target, Gait + rate) : Math.Max(target, Gait - rate);
            Solve();
        }

        /// <summary>Puts the figure at a phase and gait at once (tests, frame sheets).</summary>
        public void Set(float phase, float gait, float time)
        {
            Phase = Frac(phase);
            Gait = Clamp01(gait);
            Time = time;
            Solve();
        }

        // ------------------------------------------------------------------ the solve

        void Solve()
        {
            var r = _rig;
            var run = _s.Run;
            var st = _s.Stand;
            float w = Smooth(Gait);
            float p = Phase;
            float legLen = r.Thigh + r.Shin;
            float ankleH = r.AnkleHeight;

            // standing targets
            float b = 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * Time / Math.Max(0.5f, st.BreathSeconds));
            var sNear = new Vec2(r.HipNear.X + st.NearFootMeters, ankleH);
            var sFar = new Vec2(r.HipFar.X + st.FarFootMeters, ankleH);
            float standPelvis = r.Pelvis.Y - st.KneeSoftMeters;
            standPelvis = Math.Min(standPelvis, PelvisOver(sNear, r.HipNear - r.Pelvis, legLen * (StandReach - 0.001f)));
            standPelvis = Math.Min(standPelvis, PelvisOver(sFar, r.HipFar - r.Pelvis, legLen * (StandReach - 0.001f)));
            standPelvis -= st.BreathHipMeters * b;
            float standTorso = st.BreathTorsoDegrees * Deg * b;              // breathing in: the chest rises, a little back
            float standArm = (st.ArmOutDegrees + st.BreathArmDegrees * b) * Deg;
            float standElbow = st.ElbowDegrees * Deg;

            // running targets
            Vec2 rNear = sNear, rFar = sFar;
            float pitchNear = 0f, pitchFar = 0f, runPelvis = standPelvis, runTorso = 0f;
            float armNear = 0f, armFar = 0f, elbowNear = 0f, elbowFar = 0f;
            bool nearStance = true, farStance = true;
            if (w > 0f)
            {
                rNear = FootTarget(p, r.HipNear, ankleH, out pitchNear, out nearStance);
                rFar = FootTarget(p + 0.5f, r.HipFar, ankleH, out pitchFar, out farStance);
                runPelvis = RunPelvis(p);
                runTorso = -run.LeanDegrees * Deg;                           // forward lean: clockwise
                float c = (float)Math.Cos(2.0 * Math.PI * p);
                armNear = (run.ArmBiasDegrees - run.ArmSwingDegrees * c) * Deg;  // the near arm is back when the near foot lands
                armFar = (run.ArmBiasDegrees + run.ArmSwingDegrees * c) * Deg;
                elbowNear = (run.ElbowDegrees + run.ElbowSwingDegrees * (0.5f - 0.5f * c)) * Deg;
                elbowFar = (run.ElbowDegrees + run.ElbowSwingDegrees * (0.5f + 0.5f * c)) * Deg;
            }
            NearPlanted = w <= 0f || (w >= 1f && nearStance);
            FarPlanted = w <= 0f || (w >= 1f && farStance);

            float pelvisY = Lerp(standPelvis, runPelvis, w);
            float torso = Lerp(standTorso, runTorso, w);
            var pelvis = new Vec2(r.Pelvis.X, pelvisY);
            _j[(int)Joint.Pelvis] = pelvis;
            _j[(int)Joint.Head] = pelvis + Rot(r.Head - r.Pelvis, torso);
            _j[(int)Joint.Top] = pelvis + Rot(r.Top - r.Pelvis, torso);
            Overreached = false;
            Leg(Joint.HipNear, r.HipNear, pelvis, torso, Lerp(sNear, rNear, w), Lerp(0f, pitchNear, w), 7, NearPlanted, w);
            Leg(Joint.HipFar, r.HipFar, pelvis, torso, Lerp(sFar, rFar, w), Lerp(0f, pitchFar, w), 3, FarPlanted, w);
            Arm(Joint.ShoulderNear, r.ShoulderNear, pelvis, torso, torso + Lerp(standArm, armNear, w), Lerp(standElbow, elbowNear, w), 12);
            Arm(Joint.ShoulderFar, r.ShoulderFar, pelvis, torso, torso + Lerp(standArm, armFar, w), Lerp(standElbow, elbowFar, w), 0);
            Place(11, pelvis, torso); // body
        }

        /// <summary>The foot of a leg whose cycle is at <paramref name="phase"/>: ankle target (rig metres), toe pitch, on the ground or not.</summary>
        Vec2 FootTarget(float phase, Vec2 hip, float ankleH, out float pitch, out bool stance)
        {
            var run = _s.Run;
            float u = Frac(phase), S = run.StrideMeters, duty = Clamp(run.Duty, 0.1f, 0.9f);
            float D = duty * S, land = run.StanceCenterMeters + D * 0.5f;
            if (u < duty)
            {
                stance = true;
                pitch = 0f;
                return new Vec2(hip.X + land - u * S, ankleH);
            }
            stance = false;
            float s = (u - duty) / (1f - duty);
            // Hermite from push-off to touchdown; the end tangents point back like the ground goes by in the body's frame, so the foot
            // leaves and lands softly in the world (a part of the ground's speed: the full one overshoots past the leg's reach)
            float m = -(1f - duty) * S * SwingMatch, x0 = land - D, x1 = land;
            float s2 = s * s, s3 = s2 * s;
            float x = (2 * s3 - 3 * s2 + 1) * x0 + (s3 - 2 * s2 + s) * m + (-2 * s3 + 3 * s2) * x1 + (s3 - s2) * m;
            float q = (float)(Math.Log(0.5) / Math.Log(Clamp(run.LiftPeak, 0.15f, 0.85f)));
            // sin² — the foot leaves and meets the ground with no vertical jerk; the peak sits at LiftPeak of the swing
            float sl = (float)Math.Sin(Math.PI * Math.Pow(s, q)), sp = (float)Math.Sin(Math.PI * s);
            float lift = run.LiftMeters * sl * sl;
            pitch = -run.ToePitchDegrees * Deg * sp * sp;
            return new Vec2(hip.X + x, ankleH + lift);
        }

        /// <summary>The pelvis over a stance foot at <paramref name="u"/> of its stance: the leg's reach, shortened at mid-stance.</summary>
        float StancePelvis(float u, Vec2 hip)
        {
            var run = _s.Run;
            float duty = Clamp(run.Duty, 0.1f, 0.9f), S = run.StrideMeters;
            float land = run.StanceCenterMeters + duty * S * 0.5f;
            float k = run.Reach - run.Compression * (float)Math.Sin(Math.PI * Clamp01(u / duty));
            var foot = new Vec2(hip.X + land - u * S, _rig.AnkleHeight);
            return PelvisOver(foot, Rot(hip - _rig.Pelvis, -run.LeanDegrees * Deg), (_rig.Thigh + _rig.Shin) * k);
        }

        float RunPelvis(float p)
        {
            float duty = Clamp(_s.Run.Duty, 0.1f, 0.9f);
            float un = Frac(p), uf = Frac(p + 0.5f);
            bool n = un < duty, f = uf < duty;
            if (n && f) return Math.Min(StancePelvis(un, _rig.HipNear), StancePelvis(uf, _rig.HipFar));
            if (n) return StancePelvis(un, _rig.HipNear);
            if (f) return StancePelvis(uf, _rig.HipFar);
            // flight: from the push-off of the leg that left last to the touchdown of the other, a smooth arc matching both ends
            bool nearLeft = un < 0.5f;
            float ua = nearLeft ? un : uf;
            Vec2 hipA = nearLeft ? _rig.HipNear : _rig.HipFar, hipB = nearLeft ? _rig.HipFar : _rig.HipNear;
            float span = 0.5f - duty, t = (ua - duty) / span, h = 1e-3f;
            float y0 = StancePelvis(duty, hipA), y1 = StancePelvis(0f, hipB);
            float d0 = (StancePelvis(duty, hipA) - StancePelvis(duty - h, hipA)) / h * span;
            float d1 = (StancePelvis(h, hipB) - StancePelvis(0f, hipB)) / h * span;
            float t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * y0 + (t3 - 2 * t2 + t) * d0 + (-2 * t3 + 3 * t2) * y1 + (t3 - t2) * d1;
        }

        /// <summary>Pelvis height that puts the hip (<paramref name="hipFromPelvis"/> from the pelvis, turned with the torso) <paramref name="len"/> from the ankle.</summary>
        float PelvisOver(Vec2 ankle, Vec2 hipFromPelvis, float len)
        {
            float dx = ankle.X - (_rig.Pelvis.X + hipFromPelvis.X);
            float up = len > Math.Abs(dx) ? (float)Math.Sqrt(len * len - dx * dx) : 0f;
            return ankle.Y + up - hipFromPelvis.Y;
        }

        void Leg(Joint hipJoint, Vec2 hipRest, Vec2 pelvis, float torso, Vec2 ankle, float pitch, int footLayer, bool planted, float gait)
        {
            var r = _rig;
            var hip = pelvis + Rot(hipRest - r.Pelvis, torso);
            float l1 = r.Thigh, l2 = r.Shin;
            var d = ankle - hip;
            float dist = d.Length, min = Math.Abs(l1 - l2) + 1e-4f;
            // soft IK: past the stance reach the target is eased in, so a straightening knee never snaps (a near-straight two-bone
            // solve turns the knee fast); a planted foot inside the reach is untouched
            float full = (l1 + l2) * 0.9999f, soft = Math.Min(full, (l1 + l2) * Lerp(StandReach, Math.Max(0.5f, _s.Run.Reach), gait));
            if (dist > soft)
            {
                float band = full - soft, eased = band > 1e-6f ? soft + band * (1f - (float)Math.Exp(-(dist - soft) / band)) : soft;
                if (planted && dist - eased > 1e-5f) Overreached = true;
                ankle = hip + d * (eased / dist); dist = eased;
            }
            else if (dist < min) { ankle = hip + d.Normalized * min; dist = min; }
            var v = (ankle - hip) * (1f / dist);
            float cosA = Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            var knee = hip + Rot(v, (float)Math.Acos(cosA)) * l1;   // counter-clockwise from the hip-ankle line: the knee forward
            int i = (int)hipJoint;
            _j[i] = hip; _j[i + 1] = knee; _j[i + 2] = ankle;
            _j[i + 3] = ankle + Rot(r.ToeFromAnkle, pitch);
            _j[i + 4] = ankle + Rot(r.HeelFromAnkle, pitch);
            float thigh = FromDown(knee - hip), shin = FromDown(ankle - knee);
            // layers: foot, shin, thigh, knee cap
            Place(footLayer, ankle, pitch);
            Bone(footLayer + 1, knee, shin);
            Bone(footLayer + 2, hip, thigh);
            Bone(footLayer + 3, knee, thigh, CutoutRig.Layers[footLayer + 2]);
        }

        void Arm(Joint shoulderJoint, Vec2 shoulderRest, Vec2 pelvis, float torso, float upper, float elbowBend, int forearmLayer)
        {
            var r = _rig;
            var sh = pelvis + Rot(shoulderRest - r.Pelvis, torso);
            var elbow = sh + Dir(upper) * r.UpperArm;
            float fore = upper + elbowBend;
            var hand = elbow + Dir(fore) * r.Forearm;
            int i = (int)shoulderJoint;
            _j[i] = sh; _j[i + 1] = elbow; _j[i + 2] = hand;
            // layers: forearm, upper arm, elbow cap
            Bone(forearmLayer, elbow, fore);
            Bone(forearmLayer + 1, sh, upper);
            Bone(forearmLayer + 2, elbow, upper, CutoutRig.Layers[forearmLayer + 1]);
        }

        /// <summary>A bone part at <paramref name="at"/> pointing <paramref name="angle"/> (from down, + forward); turned from how it is drawn.</summary>
        void Bone(int layer, Vec2 at, float angle, string? restOf = null)
        {
            var part = _rig.Parts[restOf ?? CutoutRig.Layers[layer]];
            Place(layer, at, angle - part.Rest);
        }

        void Place(int layer, Vec2 at, float rotation) { _pivot[layer] = at; _rot[layer] = rotation; }

        // ------------------------------------------------------------------ math

        /// <summary>The direction <paramref name="a"/> radians from straight down, + toward the facing (counter-clockwise).</summary>
        public static Vec2 Dir(float a) => new Vec2((float)Math.Sin(a), -(float)Math.Cos(a));
        /// <summary>The angle of <paramref name="d"/> from straight down, + toward the facing.</summary>
        public static float FromDown(Vec2 d) => (float)Math.Atan2(d.X, -d.Y);
        public static Vec2 Rot(Vec2 v, float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
        static float Frac(float x) => x - (float)Math.Floor(x);
        static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;
        static float Clamp01(float x) => Clamp(x, 0f, 1f);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));
        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
