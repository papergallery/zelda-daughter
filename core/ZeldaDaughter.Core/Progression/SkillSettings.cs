#nullable enable
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Progression
{
    /// <summary>data/skills.json (C-06).</summary>
    public sealed class SkillSettings
    {
        public float Max { get; set; }
        public List<float> Tiers { get; set; } = new List<float>();
        public Dictionary<string, StatCurve> Stats { get; set; } = new Dictionary<string, StatCurve>();
        public Dictionary<string, WeaponCurve> Weapons { get; set; } = new Dictionary<string, WeaponCurve>();
        public ExperienceSettings Experience { get; set; } = new ExperienceSettings();
        public EffectSettings Effects { get; set; } = new EffectSettings();
        public NewWeaponSettings NewWeapon { get; set; } = new NewWeaponSettings();
    }

    public sealed class StatCurve
    {
        public float Rate { get; set; }
        public float Decay { get; set; }
        /// <summary>Raw experience of a failed attempt (a miss), relative to a success.</summary>
        public float Failure { get; set; }
        public float Victory { get; set; }
        public float Start { get; set; }
    }

    public sealed class WeaponCurve
    {
        public float Rate { get; set; }
        public float Decay { get; set; }
        public float Failure { get; set; }
    }

    public sealed class ExperienceSettings
    {
        public float Attack { get; set; }
        public float Hit { get; set; }
        public float Dodge { get; set; }
        public float DamageToToughness { get; set; }
        public float MetersPerUnit { get; set; }
    }

    public sealed class EffectSettings
    {
        public float MaxDamageBonus { get; set; }
        public float MaxDamageReduction { get; set; }
        public float MaxAttackSpeedBonus { get; set; }
        public float BaseHitChance { get; set; }
        public float MaxHealBonus { get; set; }
        public float MaxCapacityBonus { get; set; }
    }

    public sealed class NewWeaponSettings
    {
        public float DamageFrom { get; set; }
        public float SpeedFrom { get; set; }
        public float HitFrom { get; set; }
    }
}
