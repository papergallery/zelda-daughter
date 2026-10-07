using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Condition
{
    /// <summary>data/wounds.json (C-07).</summary>
    public sealed class WoundSettings
    {
        public float MaxHp { get; set; }
        public float NaturalHpRegenPerSecond { get; set; }
        public float RestMultiplier { get; set; }
        /// <summary>Sum of wound severities that knocks the hero out (§6 «при критическом накоплении ран»).</summary>
        public float KnockoutWoundLoad { get; set; }
        public float KnockoutSeconds { get; set; }
        public float ReviveHpFraction { get; set; }
        public float SleepHpFraction { get; set; }
        public float SleepWoundSeverity { get; set; }
        public Dictionary<string, WoundTypeSettings> Types { get; set; } = new Dictionary<string, WoundTypeSettings>();
        public FlagSettings Flags { get; set; } = new FlagSettings();
    }

    public sealed class WoundTypeSettings
    {
        /// <summary>Real seconds for a full-severity wound to close by itself.</summary>
        public float NaturalHealSeconds { get; set; }
        public float HpDrainPerSecond { get; set; }
        public float SpeedAtFull { get; set; } = 1f;
        public float AccuracyAtFull { get; set; } = 1f;
        public float AttackAtFull { get; set; } = 1f;
        public string Medicine { get; set; } = "";
    }

    public sealed class FlagSettings
    {
        public float LimpFromFracture { get; set; }
        public float HoldSideBelowHp { get; set; }
    }

    /// <summary>§6: cut/stab (bleeding), fracture (limp), burn (accuracy), poison (nausea).</summary>
    public enum WoundType { Cut, Fracture, Burn, Poison }

    public enum RestKind { None, Campfire, Tavern }

    /// <summary>What the view shows instead of a health bar (§1 «состояние считывается визуально»).</summary>
    [Flags]
    public enum VisibleState
    {
        None = 0,
        Limping = 1,
        HoldingSide = 2,
        Bleeding = 4,
        Burned = 8,
        Nauseous = 16,
        KnockedOut = 32,
    }

    public enum ConditionEventKind { KnockedOut, Revived, WoundHealed }

    public readonly struct ConditionEvent
    {
        public readonly ConditionEventKind Kind;
        public readonly WoundType Wound;

        public ConditionEvent(ConditionEventKind kind, WoundType wound = default) { Kind = kind; Wound = wound; }
        public override string ToString() => Kind == ConditionEventKind.WoundHealed ? $"{Kind} {Wound}" : Kind.ToString();
    }

    /// <summary>
    /// The hero's hidden health and wounds (project-design.md §6). Wounds of a type are replaced only by a heavier one;
    /// knockout by health reaching zero (bleeding included) or by a new wound pushing the total over the limit;
    /// after a knockout the hero gets up weak and keeps the wounds. Healing: wait (very slow), rest (×), medicine (now).
    /// </summary>
    public sealed class HeroCondition
    {
        static readonly WoundType[] AllTypes = { WoundType.Cut, WoundType.Fracture, WoundType.Burn, WoundType.Poison };

        readonly WoundSettings _s;
        readonly WoundTypeSettings[] _types = new WoundTypeSettings[4];
        readonly float[] _severity = new float[4];
        float _knockoutLeft;

        public HeroCondition(WoundSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            foreach (var t in AllTypes)
                _types[(int)t] = settings.Types.TryGetValue(Key(t), out var ts) ? ts : throw new InvalidOperationException($"wounds.json: no type '{Key(t)}'");
            Hp = settings.MaxHp;
        }

        public float Hp { get; private set; }
        public float HpFraction => Hp / _s.MaxHp;
        public bool IsKnockedOut => _knockoutLeft > 0;
        public float Severity(WoundType t) => _severity[(int)t];

        public float WoundLoad
        {
            get { float sum = 0; foreach (var v in _severity) sum += v; return sum; }
        }

        public float SpeedMultiplier => Product(t => t.SpeedAtFull);
        public float AccuracyMultiplier => Product(t => t.AccuracyAtFull);
        public float AttackMultiplier => Product(t => t.AttackAtFull);

        public VisibleState Flags
        {
            get
            {
                var f = VisibleState.None;
                if (Severity(WoundType.Fracture) >= _s.Flags.LimpFromFracture && Severity(WoundType.Fracture) > 0) f |= VisibleState.Limping;
                if (HpFraction < _s.Flags.HoldSideBelowHp) f |= VisibleState.HoldingSide;
                if (Severity(WoundType.Cut) > 0) f |= VisibleState.Bleeding;
                if (Severity(WoundType.Burn) > 0) f |= VisibleState.Burned;
                if (Severity(WoundType.Poison) > 0) f |= VisibleState.Nauseous;
                if (IsKnockedOut) f |= VisibleState.KnockedOut;
                return f;
            }
        }

        public IReadOnlyList<ConditionEvent> Wound(WoundType type, float severity)
        {
            var ev = new List<ConditionEvent>(1);
            if (IsKnockedOut) return ev; // April test: damage after a knockout is ignored
            severity = Clamp01(severity);
            if (severity > _severity[(int)type]) _severity[(int)type] = severity;
            if (WoundLoad >= _s.KnockoutWoundLoad) KnockOut(ev);
            return ev;
        }

        public IReadOnlyList<ConditionEvent> Damage(float amount)
        {
            var ev = new List<ConditionEvent>(1);
            if (IsKnockedOut || amount <= 0) return ev;
            Hp -= amount;
            if (Hp <= 0) KnockOut(ev);
            return ev;
        }

        /// <summary>Universal small heal (food, §6) — health only, wounds need their medicine.</summary>
        public void Heal(float amount)
        {
            if (!IsKnockedOut && amount > 0) Hp = Math.Min(_s.MaxHp, Hp + amount);
        }

        public IReadOnlyList<ConditionEvent> Tick(float dt, RestKind rest)
        {
            var ev = new List<ConditionEvent>();
            if (dt <= 0) return ev;
            if (IsKnockedOut)
            {
                _knockoutLeft -= dt;
                if (_knockoutLeft <= 0)
                {
                    _knockoutLeft = 0;
                    Hp = _s.MaxHp * _s.ReviveHpFraction;
                    ev.Add(new ConditionEvent(ConditionEventKind.Revived));
                }
                return ev;
            }

            float restK = rest == RestKind.None ? 1f : _s.RestMultiplier;
            float drain = 0;
            foreach (var t in AllTypes)
            {
                int i = (int)t;
                if (_severity[i] <= 0) continue;
                drain += _types[i].HpDrainPerSecond * _severity[i];
                float heal = _types[i].NaturalHealSeconds > 0 ? dt * restK / _types[i].NaturalHealSeconds : 0;
                _severity[i] = Math.Max(0, _severity[i] - heal);
                if (_severity[i] == 0) ev.Add(new ConditionEvent(ConditionEventKind.WoundHealed, t));
            }
            if (drain > 0) Hp -= drain * dt;
            else Hp = Math.Min(_s.MaxHp, Hp + _s.NaturalHpRegenPerSecond * restK * dt);
            if (Hp <= 0) KnockOut(ev);
            return ev;
        }

        /// <summary>Apply a medicine item (bandage, splint, burn_salve, antidote). False if nothing to treat.</summary>
        public bool Treat(string itemId)
        {
            if (IsKnockedOut) return false;
            foreach (var t in AllTypes)
            {
                if (_types[(int)t].Medicine == itemId && _severity[(int)t] > 0)
                {
                    _severity[(int)t] = 0;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Sleep in a tavern bed (§6): wounds heal down to a threshold, health up to a share.</summary>
        public void Sleep()
        {
            if (IsKnockedOut) return;
            for (int i = 0; i < _severity.Length; i++) _severity[i] = Math.Min(_severity[i], _s.SleepWoundSeverity);
            Hp = Math.Max(Hp, _s.MaxHp * _s.SleepHpFraction);
        }

        /// <summary>For save/load (C-13).</summary>
        public void Restore(float hp, IReadOnlyDictionary<string, float> severities)
        {
            Hp = Math.Max(0.01f, Math.Min(_s.MaxHp, hp));
            foreach (var t in AllTypes) _severity[(int)t] = severities.TryGetValue(Key(t), out float v) ? Clamp01(v) : 0f;
            _knockoutLeft = 0;
        }

        void KnockOut(List<ConditionEvent> ev)
        {
            Hp = Math.Max(Hp, 0);
            _knockoutLeft = _s.KnockoutSeconds;
            ev.Add(new ConditionEvent(ConditionEventKind.KnockedOut));
        }

        float Product(Func<WoundTypeSettings, float> atFull)
        {
            float m = 1f;
            foreach (var t in AllTypes) m *= 1f + (atFull(_types[(int)t]) - 1f) * _severity[(int)t];
            return m;
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        public static string Key(WoundType t) => t.ToString().ToLowerInvariant();
    }
}
