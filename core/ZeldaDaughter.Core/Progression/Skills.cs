#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Progression
{
    public enum Stat { Strength, Toughness, Agility, Accuracy, Endurance, CarryCapacity }

    /// <summary>Weapon families of §6: improvised blunt (stick, stone, hammer), blades, bows, bare hands.</summary>
    public enum WeaponClass { Blade, Bow, Blunt, Fists }

    public enum SkillEventKind { Attack, Damaged, Walked, Victory, Dodged }

    /// <summary>Something the hero did or suffered — the only way skills grow (§6: no experience points).</summary>
    public readonly struct SkillEvent
    {
        public readonly SkillEventKind Kind;
        public readonly WeaponClass Weapon;
        public readonly bool Success;
        public readonly float Amount;

        SkillEvent(SkillEventKind kind, WeaponClass weapon, bool success, float amount) { Kind = kind; Weapon = weapon; Success = success; Amount = amount; }

        public static SkillEvent Attack(WeaponClass weapon, bool hit) => new SkillEvent(SkillEventKind.Attack, weapon, hit, 0);
        public static SkillEvent Damaged(float amount) => new SkillEvent(SkillEventKind.Damaged, default, true, amount);
        public static SkillEvent Walked(float meters, bool overloaded) => new SkillEvent(SkillEventKind.Walked, default, overloaded, meters);
        public static SkillEvent Victory(WeaponClass weapon) => new SkillEvent(SkillEventKind.Victory, weapon, true, 0);
        public static SkillEvent Dodged() => new SkillEvent(SkillEventKind.Dodged, default, true, 0);
    }

    /// <summary>A stat (or a weapon proficiency, when <see cref="Weapon"/> is set) moved; tiers drive the hero's remarks.</summary>
    public readonly struct SkillChange
    {
        public readonly Stat Stat;
        public readonly WeaponClass? Weapon;
        public readonly float Old;
        public readonly float New;
        public readonly int OldTier;
        public readonly int NewTier;

        public SkillChange(Stat stat, WeaponClass? weapon, float old, float @new, int oldTier, int newTier)
        {
            Stat = stat; Weapon = weapon; Old = old; New = @new; OldTier = oldTier; NewTier = newTier;
        }
    }

    /// <summary>How well the hero handles a weapon family: damage and speed multipliers, hit-chance bonus.</summary>
    public readonly struct WeaponHandling
    {
        public readonly float DamageMultiplier;
        public readonly float SpeedMultiplier;
        public readonly float HitBonus;

        public WeaponHandling(float damage, float speed, float hit) { DamageMultiplier = damage; SpeedMultiplier = speed; HitBonus = hit; }
    }

    /// <summary>Kenshi-style progression (project-design.md §6): everything grows by use, failures included, with diminishing returns.</summary>
    public sealed class Skills
    {
        readonly SkillSettings _s;
        readonly float[] _stats = new float[6];
        readonly float[] _weapons = new float[4];
        float _walkMeters;
        float _overloadMeters;

        public Skills(SkillSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            for (int i = 0; i < _stats.Length; i++) _stats[i] = Curve((Stat)i).Start;
            foreach (WeaponClass w in Enum.GetValues(typeof(WeaponClass))) WeaponCurveOf(w); // fail early on missing data
        }

        public float Get(Stat stat) => _stats[(int)stat];
        public float Weapon(WeaponClass weapon) => _weapons[(int)weapon];
        public int Tier(float value)
        {
            int tier = 0;
            for (int i = 0; i < _s.Tiers.Count; i++) if (value >= _s.Tiers[i]) tier = i;
            return tier;
        }

        /// <summary>gain = raw · rate · (1 − v/max)^decay, never past max (April StatGrowthCurve.cs:26).</summary>
        public static float Gain(float raw, float rate, float decay, float value, float max)
        {
            if (raw <= 0 || value >= max) return 0f;
            float g = raw * rate * (float)Math.Pow(1.0 - value / max, decay);
            return Math.Min(g, max - value);
        }

        public IReadOnlyList<SkillChange> Apply(SkillEvent e)
        {
            var changes = new List<SkillChange>(3);
            var xp = _s.Experience;
            switch (e.Kind)
            {
                case SkillEventKind.Attack:
                    Grow(Stat.Strength, xp.Attack, changes);
                    Grow(Stat.Accuracy, e.Success ? xp.Hit : xp.Hit * Curve(Stat.Accuracy).Failure, changes);
                    var wc = WeaponCurveOf(e.Weapon);
                    GrowWeapon(e.Weapon, e.Success ? 1f : wc.Failure, changes);
                    break;
                case SkillEventKind.Damaged:
                    Grow(Stat.Toughness, e.Amount * xp.DamageToToughness, changes);
                    break;
                case SkillEventKind.Dodged:
                    Grow(Stat.Agility, xp.Dodge, changes);
                    break;
                case SkillEventKind.Walked:
                    Grow(Stat.Endurance, Units(ref _walkMeters, e.Amount), changes);
                    if (e.Success) Grow(Stat.CarryCapacity, Units(ref _overloadMeters, e.Amount), changes);
                    break;
                case SkillEventKind.Victory:
                    foreach (Stat s in Enum.GetValues(typeof(Stat))) Grow(s, Curve(s).Victory, changes);
                    break;
            }
            return changes;
        }

        float Units(ref float acc, float meters)
        {
            acc += Math.Max(0f, meters);
            float units = (float)Math.Floor(acc / _s.Experience.MetersPerUnit);
            acc -= units * _s.Experience.MetersPerUnit;
            return units;
        }

        void Grow(Stat stat, float raw, List<SkillChange> changes)
        {
            var c = Curve(stat);
            float old = _stats[(int)stat];
            float gain = Gain(raw, c.Rate, c.Decay, old, _s.Max);
            if (gain <= 0) return;
            _stats[(int)stat] = old + gain;
            changes.Add(new SkillChange(stat, null, old, old + gain, Tier(old), Tier(old + gain)));
        }

        void GrowWeapon(WeaponClass weapon, float raw, List<SkillChange> changes)
        {
            var c = WeaponCurveOf(weapon);
            float old = _weapons[(int)weapon];
            float gain = Gain(raw, c.Rate, c.Decay, old, _s.Max);
            if (gain <= 0) return;
            _weapons[(int)weapon] = old + gain;
            changes.Add(new SkillChange(default, weapon, old, old + gain, Tier(old), Tier(old + gain)));
        }

        float N(Stat s) => _stats[(int)s] / _s.Max;

        public float DamageMultiplier() => 1f + N(Stat.Strength) * _s.Effects.MaxDamageBonus;
        public float DamageReduction() => N(Stat.Toughness) * _s.Effects.MaxDamageReduction;
        public float AttackSpeedMultiplier() => 1f + N(Stat.Agility) * _s.Effects.MaxAttackSpeedBonus;
        public float HitChance() => _s.Effects.BaseHitChance + N(Stat.Accuracy) * (1f - _s.Effects.BaseHitChance);
        public float HealMultiplier() => 1f + N(Stat.Endurance) * _s.Effects.MaxHealBonus;
        public float CapacityMultiplier() => 1f + N(Stat.CarryCapacity) * _s.Effects.MaxCapacityBonus;

        /// <summary>§6 «обучение не мгновенное»: a new weapon is weak, slow and misses until practised.</summary>
        public WeaponHandling WeaponHandling(WeaponClass weapon)
        {
            float n = _weapons[(int)weapon] / _s.Max;
            var nw = _s.NewWeapon;
            return new WeaponHandling(Lerp(nw.DamageFrom, 1f, n), Lerp(nw.SpeedFrom, 1f, n), Lerp(nw.HitFrom, 0f, n));
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));

        StatCurve Curve(Stat s) => _s.Stats.TryGetValue(Key(s), out var c) ? c : throw new InvalidOperationException($"skills.json: no stat '{Key(s)}'");
        WeaponCurve WeaponCurveOf(WeaponClass w) => _s.Weapons.TryGetValue(Key(w), out var c) ? c : throw new InvalidOperationException($"skills.json: no weapon '{Key(w)}'");

        public static string Key(Stat s) => s == Stat.CarryCapacity ? "carry_capacity" : s.ToString().ToLowerInvariant();
        public static string Key(WeaponClass w) => w.ToString().ToLowerInvariant();

        /// <summary>For save/load (C-13).</summary>
        public void Restore(IReadOnlyDictionary<string, float> stats, IReadOnlyDictionary<string, float> weapons)
        {
            foreach (Stat s in Enum.GetValues(typeof(Stat))) if (stats.TryGetValue(Key(s), out float v)) _stats[(int)s] = Math.Min(v, _s.Max);
            foreach (WeaponClass w in Enum.GetValues(typeof(WeaponClass))) if (weapons.TryGetValue(Key(w), out float v)) _weapons[(int)w] = Math.Min(v, _s.Max);
        }
    }
}
