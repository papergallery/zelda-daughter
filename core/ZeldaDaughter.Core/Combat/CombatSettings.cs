#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Loot;
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
        /// <summary>A tap in the last this-many seconds of the cooldown is not lost: the blow goes the moment the cooldown ends (D-26, <see cref="StrikeBuffer"/>).</summary>
        public float TapBufferSeconds { get; set; }
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

        public WeaponClass? ParsedClass => EnumNames.Parse<WeaponClass>(Class);
        public WoundType? ParsedWound => WoundNames.Parse(Wound);
    }

    /// <summary>data/enemies.json (C-17).</summary>
    public sealed class EnemySettings
    {
        /// <summary>No windup is ever shorter: on a phone a 0.3 s tell cannot be dodged (April wolf).</summary>
        public float MinWindup { get; set; }
        /// <summary>The blow counts only if the hero is within <c>range − dodgeForgiveness</c> at its moment (D-26): the dodge has a margin on a phone.</summary>
        public float DodgeForgiveness { get; set; }
        /// <summary>While winding up, an enemy whose hero is within its range but outside the reach steps in at this × chaseSpeed (so a hero who stands still is hit).</summary>
        public float WindupCreepFactor { get; set; }
        public float AlertSeconds { get; set; }
        public float IdleSeconds { get; set; }
        public float WanderSeconds { get; set; }
        public float WanderSpeed { get; set; }
        public float LeaveSpeed { get; set; }
        /// <summary>Aggro is dropped when the hero is farther than this × the enemy's aggroRange.</summary>
        public float LoseInterestFactor { get; set; }
        /// <summary>After walking away from a downed hero the enemy ignores him until he comes into aggroRange having moved this far (m).</summary>
        public float ReapproachMeters { get; set; }
        /// <summary>A hunting night predator (<see cref="Enemy.Hunt"/>) stops following a hero farther than this (m).</summary>
        public float StalkMeters { get; set; }
        /// <summary>The tool whose presence in the bag turns a tap on a carcass into butchering (§6): item id.</summary>
        public string ButcherTool { get; set; } = "";
        /// <summary>Wound name (cut | fracture | burn | poison) → what it does to the enemy.</summary>
        public Dictionary<string, EnemyWoundEffect> WoundEffects { get; set; } = new Dictionary<string, EnemyWoundEffect>();
        public Dictionary<string, EnemyDef> Enemies { get; set; } = new Dictionary<string, EnemyDef>();
        /// <summary>How far fire scares the animals that fear it (D-23).</summary>
        public FireFearSettings Fire { get; set; } = new FireFearSettings();
    }

    /// <summary>enemies.json «fire»: wolves keep out of a campfire's and a torch's reach and run from it.</summary>
    public sealed class FireFearSettings
    {
        /// <summary>A lit campfire scares within this many metres (not less than camp.json lightRadius: no wolf in the light). Shrinks with a dying fire.</summary>
        public float CampfireRadius { get; set; }
        /// <summary>The hero's burning torch scares within this many metres. Shrinks as the torch dies.</summary>
        public float TorchRadius { get; set; }
        /// <summary>Metres past the edge of the reach that a scared animal runs before it turns back.</summary>
        public float FleeMargin { get; set; }
        /// <summary>Running from fire: this × chaseSpeed.</summary>
        public float FleeSpeedFactor { get; set; } = 1f;
        public float MaxFleeSeconds { get; set; } = 5f;
    }

    public sealed class EnemyWoundEffect
    {
        /// <summary>Speed multiplier at full severity (fracture: limping).</summary>
        public float SpeedAtFull { get; set; } = 1f;
        /// <summary>HP lost per second at full severity (cut: bleeding).</summary>
        public float HpDrainPerSecond { get; set; }
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
        /// <summary>Keeps away from a campfire and a torch, runs from them (D-23): wolves — yes, the boar — no.</summary>
        public bool FearsFire { get; set; }
        /// <summary>What the carcass gives: minimum for bare hands, full set with a knife (D-05).</summary>
        public LootTable Loot { get; set; } = new LootTable();

        public WoundType? ParsedWound => WoundNames.Parse(Wound);
    }

    /// <summary>Enum fields in data are names only: a number ("1") or a list ("Cut,Burn") is not a name (D-01).</summary>
    static class EnumNames
    {
        static class Names<T> where T : struct, Enum
        {
            // built once: a lookup per frame (enemy wound effects, schedule slots) must not allocate (C8)
            public static readonly Dictionary<string, T> Map = Build();

            static Dictionary<string, T> Build()
            {
                var m = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in Enum.GetNames(typeof(T))) m[n] = (T)Enum.Parse(typeof(T), n);
                return m;
            }
        }

        public static T? Parse<T>(string name) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Names<T>.Map.TryGetValue(name, out var v) ? v : (T?)null;
        }
    }

    static class WoundNames
    {
        public static WoundType? Parse(string name) => EnumNames.Parse<WoundType>(name);
    }
}
