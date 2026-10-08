#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Combat
{
    /// <summary>data/weapons.json (C-16). Weapon id is an item id from items.json, or <see cref="Fists"/>.</summary>
    public sealed class WeaponSettings
    {
        public const string Fists = "fists";

        public float AttackCooldown { get; set; }
        /// <summary>Share of the damage a miss still deals (a glancing blow, April CombatController: 10 %).</summary>
        public float MissDamageShare { get; set; }
        public Dictionary<string, WeaponDef> Weapons { get; set; } = new Dictionary<string, WeaponDef>();
    }

    public sealed class WeaponDef
    {
        /// <summary>blade | bow | blunt | fists — the family that trains (skills.json weapons).</summary>
        public string Class { get; set; } = "";
        public float Damage { get; set; }
        public float Range { get; set; }
        /// <summary>cut | fracture | burn | poison, or empty for no wound.</summary>
        public string Wound { get; set; } = "";
        public float Severity { get; set; }
        /// <summary>Seconds a hit holds the enemy staggered (hammer).</summary>
        public float Stun { get; set; }

        public WeaponClass? ParsedClass => Enum.TryParse<WeaponClass>(Class, true, out var c) ? c : (WeaponClass?)null;
        public WoundType? ParsedWound => WoundNames.Parse(Wound);
    }

    /// <summary>data/enemies.json (C-17).</summary>
    public sealed class EnemySettings
    {
        /// <summary>No windup is ever shorter: on a phone a 0.3 s tell cannot be dodged (April wolf).</summary>
        public float MinWindup { get; set; }
        public float AlertSeconds { get; set; }
        public float IdleSeconds { get; set; }
        public float WanderSeconds { get; set; }
        public float WanderSpeed { get; set; }
        public float LeaveSpeed { get; set; }
        /// <summary>Aggro is dropped when the hero is farther than this × the enemy's aggroRange.</summary>
        public float LoseInterestFactor { get; set; }
        public Dictionary<string, EnemyDef> Enemies { get; set; } = new Dictionary<string, EnemyDef>();
    }

    public sealed class EnemyDef
    {
        public string Name { get; set; } = "";
        public float Hp { get; set; }
        public float Damage { get; set; }
        public string Wound { get; set; } = "";
        public float Severity { get; set; }
        /// <summary>Reach of the blow (m): the windup starts here, and the blow lands only if the hero is still inside.</summary>
        public float Range { get; set; }
        public float Cooldown { get; set; }
        public float Windup { get; set; }
        /// <summary>A single hit of at least this share of max HP staggers.</summary>
        public float StaggerShare { get; set; }
        public float StaggerSeconds { get; set; }
        public float ChaseSpeed { get; set; }
        public float AggroRange { get; set; }
        public bool AggroOnSight { get; set; }
        public bool AggroOnDamage { get; set; }

        public WoundType? ParsedWound => WoundNames.Parse(Wound);
    }

    static class WoundNames
    {
        public static WoundType? Parse(string name) =>
            string.IsNullOrEmpty(name) ? (WoundType?)null : Enum.TryParse<WoundType>(name, true, out var w) ? w : (WoundType?)null;
    }
}
