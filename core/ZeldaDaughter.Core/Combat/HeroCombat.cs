#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Combat
{
    public enum StrikeOutcome { Hit, Miss, OutOfRange, Cooldown, Unavailable }

    public readonly struct StrikeResult
    {
        public readonly StrikeOutcome Outcome;
        public readonly float Damage;
        public readonly WoundType? Wound;
        public readonly float Severity;
        public readonly float StunSeconds;
        public readonly bool Killed;
        public readonly IReadOnlyList<SkillChange> SkillChanges;
        /// <summary>What the blow did to the enemy (Alerted, Staggered, Died) — for the view.</summary>
        public readonly IReadOnlyList<EnemyEvent> EnemyEvents;

        public StrikeResult(StrikeOutcome outcome, float damage = 0, WoundType? wound = null, float severity = 0, float stun = 0, bool killed = false, IReadOnlyList<SkillChange>? changes = null, IReadOnlyList<EnemyEvent>? enemyEvents = null)
        {
            Outcome = outcome; Damage = damage; Wound = wound; Severity = severity; StunSeconds = stun; Killed = killed;
            SkillChanges = changes ?? Array.Empty<SkillChange>();
            EnemyEvents = enemyEvents ?? Array.Empty<EnemyEvent>();
        }
    }

    /// <summary>
    /// The hero's side of a fight (project-design.md §6): a tap on an enemy is one <see cref="Strike"/> — one hit roll, one damage
    /// calculation, one skill event. The roll (0..1) comes from outside. Enemy blows on the hero are in <see cref="Enemy"/>.
    /// </summary>
    public sealed class HeroCombat
    {
        readonly WeaponSettings _s;

        /// <param name="hunger">Hunger weakens and slows blows past the threshold (D-01); null — ignored.</param>
        /// <param name="bag">Overload slows blows with the same curve as walking (§7); null — ignored.</param>
        public HeroCombat(WeaponSettings settings, Skills skills, HeroCondition condition, Hunger? hunger = null, Bag? bag = null)
        {
            Hunger = hunger;
            Bag = bag;
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public Skills Skills { get; }
        public HeroCondition Condition { get; }
        public Hunger? Hunger { get; }
        public Bag? Bag { get; }
        /// <summary>Ground position (x, z), set by the view each frame.</summary>
        public Vec2 Position { get; set; }
        public float CooldownLeft { get; private set; }

        /// <summary>Hunger multiplier on damage and speed (1 until the hunger threshold).</summary>
        public float HungerMultiplier => Hunger?.Multiplier ?? 1f;

        /// <summary>Overload slows the swing: the bag's speed curve, 1 without a bag or a load.</summary>
        public float LoadSpeedMultiplier => Bag?.SpeedMultiplier(Skills.CapacityMultiplier()) ?? 1f;

        public void Tick(float dt) { if (dt > 0) CooldownLeft = Math.Max(0f, CooldownLeft - dt); }

        /// <summary>Skill + weapon handling + wounds: <c>clamp01((accuracy + new-weapon penalty) × wound accuracy)</c>.</summary>
        public float HitChance(string weaponId)
        {
            var w = Weapon(weaponId, out var cls);
            var h = Skills.WeaponHandling(cls);
            float c = (Skills.HitChance() + h.HitBonus) * Condition.AccuracyMultiplier;
            return c < 0 ? 0 : c > 1 ? 1 : c;
        }

        /// <summary>
        /// One tap = one blow. Hit when <c>roll &lt; HitChance</c>; a miss still deals <c>missDamageShare</c> of the damage and no wound.
        /// Out of reach or inside the cooldown nothing happens (no roll is consumed, nothing trains).
        /// </summary>
        public StrikeResult Strike(string weaponId, Enemy target, float roll)
        {
            var w = Weapon(weaponId, out var cls);
            if (Condition.IsKnockedOut || target.IsCarcass) return new StrikeResult(StrikeOutcome.Unavailable);
            if (CooldownLeft > 0) return new StrikeResult(StrikeOutcome.Cooldown);
            if ((target.Position - Position).Length > w.Range) return new StrikeResult(StrikeOutcome.OutOfRange);

            var handling = Skills.WeaponHandling(cls);
            bool hit = roll < HitChance(weaponId);
            float damage = w.Damage * Skills.DamageMultiplier() * handling.DamageMultiplier * Condition.AttackMultiplier * HungerMultiplier;
            if (!hit) damage *= _s.MissDamageShare;
            WoundType? wound = hit && w.Severity > 0 ? w.ParsedWound : null;
            float severity = wound.HasValue ? w.Severity : 0f;
            float stun = hit ? w.Stun : 0f;
            CooldownLeft = _s.AttackCooldown / Math.Max(0.01f, handling.SpeedMultiplier * Skills.AttackSpeedMultiplier() * HungerMultiplier * LoadSpeedMultiplier);

            var enemyEvents = target.Receive(damage, wound, severity, stun);
            bool killed = target.IsCarcass;
            var changes = new List<SkillChange>(Skills.Apply(SkillEvent.Attack(cls, hit)));
            if (killed) changes.AddRange(Skills.Apply(SkillEvent.Victory(cls)));
            return new StrikeResult(hit ? StrikeOutcome.Hit : StrikeOutcome.Miss, damage, wound, severity, stun, killed, changes, enemyEvents);
        }

        WeaponDef Weapon(string id, out WeaponClass cls)
        {
            if (!_s.Weapons.TryGetValue(id, out var w)) throw new ArgumentException($"weapons.json: no weapon '{id}'", nameof(id));
            cls = w.ParsedClass ?? throw new InvalidOperationException($"weapons.json: '{id}' — unknown class '{w.Class}'");
            return w;
        }
    }
}
