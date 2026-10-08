#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
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

        public StrikeResult(StrikeOutcome outcome, float damage = 0, WoundType? wound = null, float severity = 0, float stun = 0, bool killed = false, IReadOnlyList<SkillChange>? changes = null)
        {
            Outcome = outcome; Damage = damage; Wound = wound; Severity = severity; StunSeconds = stun; Killed = killed;
            SkillChanges = changes ?? Array.Empty<SkillChange>();
        }
    }

    /// <summary>
    /// The hero's side of a fight (project-design.md §6): a tap on an enemy is one <see cref="Strike"/> — one hit roll, one damage
    /// calculation, one skill event. The roll (0..1) comes from outside. Enemy blows on the hero are in <see cref="Enemy"/>.
    /// </summary>
    public sealed class HeroCombat
    {
        readonly WeaponSettings _s;

        public HeroCombat(WeaponSettings settings, Skills skills, HeroCondition condition)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public Skills Skills { get; }
        public HeroCondition Condition { get; }
        /// <summary>Ground position (x, z), set by the view each frame.</summary>
        public Vec2 Position { get; set; }
        public float CooldownLeft { get; private set; }

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
            float damage = w.Damage * Skills.DamageMultiplier() * handling.DamageMultiplier * Condition.AttackMultiplier;
            if (!hit) damage *= _s.MissDamageShare;
            WoundType? wound = hit && w.Severity > 0 ? w.ParsedWound : null;
            float severity = wound.HasValue ? w.Severity : 0f;
            float stun = hit ? w.Stun : 0f;
            CooldownLeft = _s.AttackCooldown / Math.Max(0.01f, handling.SpeedMultiplier * Skills.AttackSpeedMultiplier());

            target.Receive(damage, wound, severity, stun);
            bool killed = target.IsCarcass;
            var changes = new List<SkillChange>(Skills.Apply(SkillEvent.Attack(cls, hit)));
            if (killed) changes.AddRange(Skills.Apply(SkillEvent.Victory(cls)));
            return new StrikeResult(hit ? StrikeOutcome.Hit : StrikeOutcome.Miss, damage, wound, severity, stun, killed, changes);
        }

        WeaponDef Weapon(string id, out WeaponClass cls)
        {
            if (!_s.Weapons.TryGetValue(id, out var w)) throw new ArgumentException($"weapons.json: no weapon '{id}'", nameof(id));
            cls = w.ParsedClass ?? throw new InvalidOperationException($"weapons.json: '{id}' — unknown class '{w.Class}'");
            return w;
        }
    }
}
