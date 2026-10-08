#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Cutout
{
    /// <summary>
    /// D-27 (ADR-0010): one view of a cut-out figure — painted parts in one atlas and the joints measured on the drawing. Written by
    /// <c>tools/art/d27_rig.py</c> as <c>&lt;id&gt;_&lt;view&gt;.rig.json</c> next to the atlas. Rig space: metres, the origin is the ground point
    /// between the feet (the sprite pivot), +X is the way the figure faces, +Y is up. The idea of a rig of painted parts with two-bone limbs, the
    /// far limb as the same drawing behind and darker, round joint caps — ref2game (MIT, github.com/studioigor/ref2game, animation.md §1–3, §15.5).
    /// </summary>
    public sealed class CutoutRig
    {
        /// <summary>The parts in drawing order (far to near). The far limbs are darker copies of the near drawings in the atlas.</summary>
        public static readonly string[] Layers =
        {
            "forearmFar", "upperArmFar", "elbowCapFar",
            "footFar", "shinFar", "thighFar", "kneeCapFar",
            "footNear", "shinNear", "thighNear", "kneeCapNear",
            "body",
            "forearmNear", "upperArmNear", "elbowCapNear",
        };

        public string Id { get; private set; } = "";
        public string View { get; private set; } = "";
        public float PixelsPerMeter { get; private set; } = 320f;
        public string AtlasFile { get; private set; } = "";
        public int AtlasWidth { get; private set; }
        public int AtlasHeight { get; private set; }

        // joints at rest, rig metres
        public Vec2 Pelvis, HipNear, HipFar, Knee, Ankle, Toe, Heel, ShoulderNear, ShoulderFar, Elbow, Hand, Head, Top;

        public float Thigh => (Knee - HipNear).Length;
        public float Shin => (Ankle - Knee).Length;
        public float UpperArm => (Elbow - ShoulderNear).Length;
        public float Forearm => (Hand - Elbow).Length;
        /// <summary>Ankle height above the sole when the foot stands flat.</summary>
        public float AnkleHeight => Ankle.Y - Math.Min(Toe.Y, Heel.Y);
        /// <summary>Toe and heel from the ankle at rest (the foot flat).</summary>
        public Vec2 ToeFromAnkle => Toe - Ankle;
        public Vec2 HeelFromAnkle => Heel - Ankle;

        public IReadOnlyDictionary<string, CutoutPart> Parts => _parts;
        readonly Dictionary<string, CutoutPart> _parts = new Dictionary<string, CutoutPart>(StringComparer.Ordinal);

        /// <summary>Reads a rig file; every layer must have a part and every joint must be given (the message names what is missing).</summary>
        public static CutoutRig Parse(string json)
        {
            var o = JObject.Parse(json);
            var r = new CutoutRig
            {
                Id = (string?)o["id"] ?? "",
                View = (string?)o["view"] ?? "",
                PixelsPerMeter = (float?)o["ppu"] ?? 320f,
            };
            var atlas = o["atlas"] as JObject ?? throw new FormatException("rig: no atlas");
            r.AtlasFile = (string?)atlas["file"] ?? "";
            r.AtlasWidth = (int?)atlas["w"] ?? 0;
            r.AtlasHeight = (int?)atlas["h"] ?? 0;
            var j = o["joints"] as JObject ?? throw new FormatException("rig: no joints");
            float k = 1f / r.PixelsPerMeter;
            Vec2 J(string name)
            {
                if (!(j[name] is JArray a) || a.Count != 2) throw new FormatException("rig: joint '" + name + "' missing");
                return new Vec2((float)a[0] * k, (float)a[1] * k);
            }
            r.Pelvis = J("pelvis"); r.HipNear = J("hipNear"); r.HipFar = J("hipFar");
            r.Knee = J("knee"); r.Ankle = J("ankle"); r.Toe = J("toe"); r.Heel = J("heel");
            r.ShoulderNear = J("shoulderNear"); r.ShoulderFar = J("shoulderFar"); r.Elbow = J("elbow"); r.Hand = J("hand");
            r.Head = J("head"); r.Top = J("top");
            var parts = o["parts"] as JObject ?? throw new FormatException("rig: no parts");
            foreach (var name in Layers)
            {
                if (!(parts[name] is JObject p)) throw new FormatException("rig: part '" + name + "' missing");
                var rect = (JArray?)p["rect"] ?? throw new FormatException("rig: part '" + name + "' has no rect");
                var piv = (JArray?)p["pivot"] ?? throw new FormatException("rig: part '" + name + "' has no pivot");
                r._parts[name] = new CutoutPart(name, (float)rect[0], (float)rect[1], (float)rect[2], (float)rect[3],
                    (float)piv[0], (float)piv[1], (float?)p["rest"] ?? 0f);
            }
            if (r.Thigh < 1e-3f || r.Shin < 1e-3f || r.UpperArm < 1e-3f || r.Forearm < 1e-3f) throw new FormatException("rig: a limb of zero length");
            return r;
        }
    }

    /// <summary>
    /// A painted part: its rectangle in the atlas and the pivot (the joint it turns about), both in atlas pixels with the origin top-left
    /// (as the PNG is stored). <see cref="Rest"/> — the angle of the bone as drawn, radians from straight down, + toward the facing.
    /// </summary>
    public sealed class CutoutPart
    {
        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public float W { get; }
        public float H { get; }
        public float PivotX { get; }
        public float PivotY { get; }
        public float Rest { get; }

        public CutoutPart(string name, float x, float y, float w, float h, float pivotX, float pivotY, float rest)
        {
            Name = name; X = x; Y = y; W = w; H = h; PivotX = pivotX; PivotY = pivotY; Rest = rest;
        }
    }

    /// <summary>data/gait.json (D-27): the numbers of the cut-out run and stand. Look numbers, not balance; sources in <c>_source</c>.</summary>
    public sealed class GaitSettings
    {
        public RunGait Run { get; set; } = new RunGait();
        public StandGait Stand { get; set; } = new StandGait();
        /// <summary>Seconds of the blend between standing and running (both ways).</summary>
        public float BlendSeconds { get; set; } = 0.15f;

        public static GaitSettings Parse(string json) =>
            JsonConvert.DeserializeObject<GaitSettings>(json) ?? throw new FormatException("gait.json: empty");
    }

    public sealed class RunGait
    {
        /// <summary>Metres per cycle (two steps). The same as the registry's strideMeters of the figure.</summary>
        public float StrideMeters { get; set; } = 1.7f;
        /// <summary>Part of the cycle a foot is on the ground (below 0.5 — a run with flight).</summary>
        public float Duty { get; set; } = 0.34f;
        /// <summary>Middle of the stance sweep from the hip, metres (negative — the runner lands under the hips and pushes off behind).</summary>
        public float StanceCenterMeters { get; set; } = -0.03f;
        public float LiftMeters { get; set; } = 0.14f;
        /// <summary>Where in the swing (0..1) the foot is highest: early — the heel kicks up behind.</summary>
        public float LiftPeak { get; set; } = 0.4f;
        public float ToePitchDegrees { get; set; } = 25f;
        /// <summary>Stance leg length (hip to ankle) as a part of the full leg at touchdown and push-off.</summary>
        public float Reach { get; set; } = 0.97f;
        /// <summary>How much the stance leg shortens at mid-stance (a part of the full leg) — the run is lowest there.</summary>
        public float Compression { get; set; } = 0.14f;
        public float LeanDegrees { get; set; } = 8f;
        public float ArmSwingDegrees { get; set; } = 35f;
        public float ArmBiasDegrees { get; set; } = 5f;
        public float ElbowDegrees { get; set; } = 70f;
        public float ElbowSwingDegrees { get; set; } = 25f;
    }

    public sealed class StandGait
    {
        public float BreathSeconds { get; set; } = 3.6f;
        public float BreathHipMeters { get; set; } = 0.006f;
        public float BreathTorsoDegrees { get; set; } = 1.2f;
        public float BreathArmDegrees { get; set; } = 2.5f;
        /// <summary>Feet from their hips along the facing, metres: the near foot a little back, the far one a little forward.</summary>
        public float NearFootMeters { get; set; } = -0.03f;
        public float FarFootMeters { get; set; } = 0.04f;
        /// <summary>The hips stand this much lower than the straight legs: soft knees, not «at attention».</summary>
        public float KneeSoftMeters { get; set; } = 0.008f;
        public float ArmOutDegrees { get; set; } = 3f;
        public float ElbowDegrees { get; set; } = 10f;
    }
}
