#nullable enable
using System;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Condition;

namespace ZeldaDaughter.Core.Feel
{
    /// <summary>
    /// data/combat-feel.json (D-26): the numbers of the game's response — frame rate, hit-stop, camera shake, haptics, the camera that widens,
    /// the wolf's growl and eyes, the windup ring, the world that loses colour. The views read them; the rules that need testing are here.
    /// </summary>
    public sealed class FeelSettings
    {
        public int TargetFrameRate { get; set; } = 60;
        public HitStopSettings HitStop { get; set; } = new HitStopSettings();
        public ShakeSettings Shake { get; set; } = new ShakeSettings();
        public HapticSettings Haptics { get; set; } = new HapticSettings();
        public WindupRingSettings WindupRing { get; set; } = new WindupRingSettings();
        public CameraFeelSettings Camera { get; set; } = new CameraFeelSettings();
        public GrowlSettings Growl { get; set; } = new GrowlSettings();
        public EyesSettings Eyes { get; set; } = new EyesSettings();
        public DesaturationSettings Desaturation { get; set; } = new DesaturationSettings();
        public ConditionFeelSettings Condition { get; set; } = new ConditionFeelSettings();

        /// <summary>Hit-stop seconds for one blow of the hero: a miss freezes nothing.</summary>
        public float HitStopFor(StrikeOutcome outcome, bool killed) =>
            outcome == StrikeOutcome.Hit ? (killed ? HitStop.Kill : HitStop.Hit) : HitStop.Miss;

        /// <summary>Haptic pulse length in ms for one blow of the hero; 0 — none (a miss, nothing happened).</summary>
        public int HapticMsFor(StrikeOutcome outcome, bool killed) =>
            outcome == StrikeOutcome.Hit ? (killed ? Haptics.KillMs : Haptics.HitMs) : 0;
    }

    public sealed class HitStopSettings
    {
        public float Hit { get; set; }
        public float HeroStruck { get; set; }
        public float Kill { get; set; }
        public float Miss { get; set; }
        /// <summary>The hero's blow freezes this long after it starts — at the moment of the impact, not of the wind-up.</summary>
        public float ImpactDelay { get; set; }
        /// <summary>A blow to the hero freezes this long after it lands — her own pose of the blow (the shudder) is set first, then held.</summary>
        public float StruckDelay { get; set; }
    }

    public sealed class ShakeSettings
    {
        /// <summary>Largest shift of the camera (m) at trauma 1.</summary>
        public float MaxMeters { get; set; }
        /// <summary>Trauma falls from 1 to 0 in this many seconds.</summary>
        public float DecaySeconds { get; set; }
        /// <summary>How fast the noise changes (smooth, not random per frame).</summary>
        public float NoiseHz { get; set; }
        public float Hit { get; set; }
        public float Kill { get; set; }
        public float HeroStruck { get; set; }
        public float Knockout { get; set; }
    }

    public sealed class HapticSettings
    {
        public int HitMs { get; set; }
        public float HitAmplitude { get; set; }
        public int KillMs { get; set; }
        public float KillAmplitude { get; set; }
        public int HeroStruckMs { get; set; }
        public float HeroStruckAmplitude { get; set; }
        public int KnockoutMs { get; set; }
        public float KnockoutAmplitude { get; set; }
    }

    public sealed class WindupRingSettings
    {
        /// <summary>Ring diameter as a multiple of the enemy's own diameter.</summary>
        public float DiameterFactor { get; set; }
        public float AlphaAtStart { get; set; }
        public float AlphaAtEnd { get; set; }
        /// <summary>RGB 0..1, dark red.</summary>
        public float[] Color { get; set; } = { 0.45f, 0.06f, 0.06f };

        public float AlphaAt(float progress)
        {
            float p = progress < 0 ? 0 : progress > 1 ? 1 : progress;
            return AlphaAtStart + (AlphaAtEnd - AlphaAtStart) * p;
        }
    }

    public sealed class CameraFeelSettings
    {
        /// <summary>Ortho size × this at night and in a fight.</summary>
        public float WidenFactor { get; set; }
        public float WidenSeconds { get; set; }
        /// <summary>The hero is never smaller than this share of the frame height.</summary>
        public float MinHeroFrameShare { get; set; }
        /// <summary>Distance (m) to the nearest awake enemy under which the camera counts it as a fight.</summary>
        public float FightMeters { get; set; }

        /// <summary>Moves <paramref name="current"/> (a multiple of the base ortho size) toward <paramref name="target"/> without overshoot.</summary>
        public float Step(float current, float target, float dt)
        {
            float rate = WidenSeconds > 0 ? (WidenFactor - 1f) / WidenSeconds : 1000f;
            float d = rate * dt;
            if (current < target) return Math.Min(target, current + d);
            if (current > target) return Math.Max(target, current - d);
            return current;
        }

        public float HeroShare(float heroHeightMeters, float orthoSize) => heroHeightMeters / (2f * orthoSize);
        public float MaxOrtho(float heroHeightMeters) => heroHeightMeters / (2f * MinHeroFrameShare);
        public float Clamp(float orthoSize, float heroHeightMeters) => Math.Min(orthoSize, MaxOrtho(heroHeightMeters));
    }

    public sealed class GrowlSettings
    {
        /// <summary>A wolf closer than this is heard.</summary>
        public float Meters { get; set; }
        public float MaxVolume { get; set; }
        /// <summary>Stereo pan at the extreme side (0..1).</summary>
        public float MaxPan { get; set; }
        /// <summary>Seconds between the growls of one wolf.</summary>
        public float EverySeconds { get; set; }

        public float Volume(float distance) => distance >= Meters ? 0f : MaxVolume * (1f - Math.Max(0f, distance) / Meters);

        /// <summary>-1..1 from the sideways offset on the screen (m, right positive) and the distance.</summary>
        public float Pan(float dxScreen, float distance)
        {
            float p = dxScreen / Math.Max(distance, 1f);
            return Math.Max(-1f, Math.Min(1f, p)) * MaxPan;
        }
    }

    public sealed class EyesSettings
    {
        /// <summary>Eyes show from this share of the light radius…</summary>
        public float ShowBeyondLightShare { get; set; }
        /// <summary>…to this many metres past it.</summary>
        public float OuterMeters { get; set; }
        public float PairGapMeters { get; set; }
        public float BlinkSeconds { get; set; }

        public bool IsVisible(float distanceFromFire, float lightRadius, bool enemyAwake) =>
            enemyAwake && distanceFromFire >= lightRadius * ShowBeyondLightShare && distanceFromFire <= lightRadius + OuterMeters;
    }

    public sealed class DesaturationSettings
    {
        public float StartSeverity { get; set; }
        public float FullSeverity { get; set; }
        /// <summary>Largest loss of colour (0.25 = −25 %).</summary>
        public float MaxShare { get; set; }

        public float Share(float severity)
        {
            if (FullSeverity <= StartSeverity) return severity >= FullSeverity ? MaxShare : 0f;
            float t = (severity - StartSeverity) / (FullSeverity - StartSeverity);
            return MaxShare * (t < 0 ? 0 : t > 1 ? 1 : t);
        }

        /// <summary>Colour multiplier of the world, 1 = untouched.</summary>
        public float Saturation(float severity) => 1f - Share(severity);
    }

    public sealed class ConditionFeelSettings
    {
        /// <summary>The hero's breath is heard from this severity.</summary>
        public float BreathingFromSeverity { get; set; }
        public float StomachEverySeconds { get; set; }

        public bool Breathes(float severity) => severity >= BreathingFromSeverity;
        public bool StomachGrowls(HungerLevel level) => level >= HungerLevel.Hungry;

        /// <summary>How bad she is, 0..1: the worse of the lost health and the wound load over the knockout load.</summary>
        public float Severity(HeroCondition c, WoundSettings ws)
        {
            float lost = 1f - c.HpFraction;
            float load = ws.KnockoutWoundLoad > 0 ? c.WoundLoad / ws.KnockoutWoundLoad : 0f;
            float s = Math.Max(lost, load);
            return s < 0 ? 0 : s > 1 ? 1 : s;
        }
    }
}
