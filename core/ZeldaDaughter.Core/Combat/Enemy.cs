#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Combat
{
    /// <summary>The blow itself is a moment (an event), not a state: windup → <see cref="EnemyEventKind.Struck"/> or Dodged → recover.</summary>
    public enum EnemyState { Idle, Wander, Alert, Chase, Windup, Recover, Staggered, Leaving, Dead }

    public enum EnemyEventKind { Alerted, WindupStarted, Struck, Dodged, Staggered, LostInterest, Died, HeroKnockedOut }

    public readonly struct EnemyEvent
    {
        public readonly EnemyEventKind Kind;
        /// <summary>Damage dealt to the hero (Struck) or taken (Staggered, Died).</summary>
        public readonly float Amount;
        /// <summary>Skills the hero's side of this event moved (Struck → toughness, Dodged → agility): the view announces tiers.</summary>
        public readonly IReadOnlyList<SkillChange> SkillChanges;

        public EnemyEvent(EnemyEventKind kind, float amount = 0, IReadOnlyList<SkillChange>? changes = null)
        {
            Kind = kind; Amount = amount;
            SkillChanges = changes ?? Array.Empty<SkillChange>();
        }
        public override string ToString() => Kind.ToString();
    }

    /// <summary>
    /// A boar or a wolf (project-design.md §6): rest → wander → alert → chase → windup → blow → recover. The blow lands only if the
    /// hero is still in reach when the windup ends (the dodge); a big blow staggers and breaks the windup; death leaves a carcass.
    /// Time and the one random number per step (<c>roll</c>, 0..1, used to pick a wander direction) come from outside.
    /// </summary>
    public sealed class Enemy
    {
        readonly EnemySettings _s;
        readonly float[] _wounds = new float[4];
        float _timer;
        bool _aggro;
        bool _ignoreHero;
        Vec2 _calmAt;
        Vec2 _dir = new Vec2(1, 0);

        public Enemy(string id, EnemySettings settings, string defId, Vec2 position)
        {
            Id = id;
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            Def = settings.Enemies.TryGetValue(defId, out var d) ? d : throw new ArgumentException($"enemies.json: no enemy '{defId}'", nameof(defId));
            DefId = defId;
            Position = position;
            Hp = d.Hp;
        }

        public string Id { get; }
        public string DefId { get; }
        public EnemyDef Def { get; }
        public Vec2 Position { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp => Def.Hp;
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public bool IsCarcass => State == EnemyState.Dead;
        public float WoundSeverity(WoundType t) => _wounds[(int)t];

        /// <summary>Windup length actually used: data value, never below the minimum.</summary>
        public float WindupSeconds => Math.Max(Def.Windup, _s.MinWindup);

        /// <summary>Wakes the enemy as if it had been hurt (a thrown stone, a noise) — without damage.</summary>
        public IReadOnlyList<EnemyEvent> Provoke()
        {
            var ev = new List<EnemyEvent>(1);
            if (!IsCarcass) StartAggro(ev);
            return ev;
        }

        /// <summary>One blow taken: damage, the weapon's wound and stun (seconds, 0 = none). Staggers on a big hit; dies at zero.</summary>
        public IReadOnlyList<EnemyEvent> Receive(float damage, WoundType? wound, float severity, float stunSeconds)
        {
            var ev = new List<EnemyEvent>(2);
            if (IsCarcass) return ev;
            Hp = Math.Max(0f, Hp - Math.Max(0f, damage));
            if (wound.HasValue && severity > _wounds[(int)wound.Value]) _wounds[(int)wound.Value] = Math.Min(1f, severity);
            if (Hp <= 0f)
            {
                State = EnemyState.Dead;
                ev.Add(new EnemyEvent(EnemyEventKind.Died, damage));
                return ev;
            }
            if (Def.AggroOnDamage) StartAggro(ev);
            float stun = Math.Max(stunSeconds, damage >= Def.StaggerShare * Def.Hp ? Def.StaggerSeconds : 0f);
            if (stun > 0f)
            {
                _timer = State == EnemyState.Staggered ? Math.Max(_timer, stun) : stun;
                State = EnemyState.Staggered;
                ev.Add(new EnemyEvent(EnemyEventKind.Staggered, damage));
            }
            return ev;
        }

        static readonly EnemyEvent[] NoEvents = new EnemyEvent[0];
        readonly List<EnemyEvent> _scratch = new List<EnemyEvent>(2);

        /// <remarks>Nothing happened — a shared empty list (no allocation in an ordinary frame, C8).</remarks>
        public IReadOnlyList<EnemyEvent> Tick(float dt, HeroCombat hero, float roll)
        {
            _scratch.Clear();
            Tick(dt, hero, roll, _scratch);
            return _scratch.Count == 0 ? NoEvents : _scratch.ToArray();
        }

        /// <summary>Same, appending the events to <paramref name="ev"/> (not cleared).</summary>
        public void Tick(float dt, HeroCombat hero, float roll, List<EnemyEvent> ev)
        {
            if (IsCarcass || dt <= 0) return;
            bool down = hero.Condition.IsKnockedOut;
            Vec2 toHero = hero.Position - Position;
            float dist = toHero.Length;

            if (_aggro)
            {
                if (down) { Disengage(ev, leave: true); }                       // April: endless knockout loop
                else if (dist > _s.LoseInterestFactor * Def.AggroRange) Disengage(ev, leave: false);
            }
            else
            {
                // after walking away from a downed hero: calm until he comes up by himself (D-01, enemies.json reapproachMeters)
                if (_ignoreHero && State != EnemyState.Leaving && !down && dist <= Def.AggroRange && (hero.Position - _calmAt).Length > _s.ReapproachMeters)
                    _ignoreHero = false;
                if (Def.AggroOnSight && !down && !_ignoreHero && dist <= Def.AggroRange && State != EnemyState.Staggered) StartAggro(ev);
            }

            Bleed(dt, ev);
            if (IsCarcass) return;

            _timer += State == EnemyState.Staggered ? -dt : dt;
            switch (State)
            {
                case EnemyState.Idle:
                    if (_timer >= _s.IdleSeconds)
                    {
                        double a = roll * 2 * Math.PI;
                        _dir = new Vec2((float)Math.Cos(a), (float)Math.Sin(a));
                        Enter(EnemyState.Wander);
                    }
                    break;
                case EnemyState.Wander:
                    Move(_dir, _s.WanderSpeed * SpeedScale * dt);
                    if (_timer >= _s.WanderSeconds) Enter(EnemyState.Idle);
                    break;
                case EnemyState.Leaving:
                    Move(dist > 1e-4f ? toHero * (-1f / dist) : _dir, _s.LeaveSpeed * SpeedScale * dt);
                    if (dist > Def.AggroRange * _s.LoseInterestFactor) { _calmAt = hero.Position; Enter(EnemyState.Idle); } // _ignoreHero stays
                    break;
                case EnemyState.Alert:
                    if (_timer >= _s.AlertSeconds) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Chase:
                    if (dist <= Def.Range)
                    {
                        Enter(EnemyState.Windup);
                        ev.Add(new EnemyEvent(EnemyEventKind.WindupStarted));
                    }
                    else Move(toHero * (1f / dist), Math.Min(Def.ChaseSpeed * SpeedScale * dt, dist));
                    break;
                case EnemyState.Windup:
                    if (_timer >= WindupSeconds)
                    {
                        if (dist <= Def.Range) Land(hero, ev);
                        else ev.Add(new EnemyEvent(EnemyEventKind.Dodged, 0, hero.Skills.Apply(SkillEvent.Dodged())));
                        Enter(EnemyState.Recover);
                    }
                    break;
                case EnemyState.Recover:
                    if (_timer >= Def.Cooldown) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Staggered:
                    if (_timer <= 0) Enter(_aggro ? EnemyState.Chase : EnemyState.Idle);
                    break;
            }
            return;
        }

        void Land(HeroCombat hero, List<EnemyEvent> ev)
        {
            bool knocked = false;
            var type = Def.ParsedWound;
            if (type.HasValue) knocked |= hero.Condition.Wound(type.Value, Def.Severity).Any(IsKnockOut);
            float dmg = Def.Damage * (1f - hero.Skills.DamageReduction());
            knocked |= hero.Condition.Damage(dmg).Any(IsKnockOut);
            ev.Add(new EnemyEvent(EnemyEventKind.Struck, dmg, hero.Skills.Apply(SkillEvent.Damaged(dmg))));
            if (knocked) ev.Add(new EnemyEvent(EnemyEventKind.HeroKnockedOut));
        }

        static bool IsKnockOut(ConditionEvent e) => e.Kind == ConditionEventKind.KnockedOut;

        /// <summary>Product over wounds of 1 + (speedAtFull − 1) × severity (data: enemies.json woundEffects).</summary>
        float SpeedScale
        {
            get
            {
                float m = 1f;
                foreach (var kv in _s.WoundEffects)
                {
                    var t = EnumNames.Parse<WoundType>(kv.Key);
                    if (t.HasValue) m *= 1f + (kv.Value.SpeedAtFull - 1f) * _wounds[(int)t.Value];
                }
                return Math.Max(m, 0.05f);
            }
        }

        void Bleed(float dt, List<EnemyEvent> ev)
        {
            float drain = 0f;
            foreach (var kv in _s.WoundEffects)
            {
                var t = EnumNames.Parse<WoundType>(kv.Key);
                if (t.HasValue) drain += kv.Value.HpDrainPerSecond * _wounds[(int)t.Value];
            }
            if (drain <= 0f) return;
            Hp = Math.Max(0f, Hp - drain * dt);
            if (Hp <= 0f)
            {
                State = EnemyState.Dead;
                ev.Add(new EnemyEvent(EnemyEventKind.Died, 0));
            }
        }

        void StartAggro(List<EnemyEvent> ev)
        {
            if (_aggro) return;
            _aggro = true;
            _ignoreHero = false;
            Enter(EnemyState.Alert);
            ev.Add(new EnemyEvent(EnemyEventKind.Alerted));
        }

        void Disengage(List<EnemyEvent> ev, bool leave)
        {
            _aggro = false;
            _ignoreHero = leave;
            Enter(leave ? EnemyState.Leaving : EnemyState.Idle);
            ev.Add(new EnemyEvent(EnemyEventKind.LostInterest));
        }

        void Enter(EnemyState s) { State = s; _timer = 0; }

        /// <summary>
        /// The view's verdict on a spot (a wall, a house, deep water): true — the enemy cannot stand there. A step into such a spot is
        /// tried again along one axis (sliding), else the enemy stays. Set by <see cref="EnemyRoster"/>.
        /// </summary>
        public Func<Vec2, bool>? Blocked { get; set; }

        internal void SetPosition(Vec2 p) => Position = p;

        void Move(Vec2 dir, float meters)
        {
            var to = Position + dir * meters;
            var blocked = Blocked;
            if (blocked == null || !blocked(to)) { Position = to; return; }
            var alongX = new Vec2(to.X, Position.Y);
            if (!blocked(alongX)) { Position = alongX; return; }
            var alongY = new Vec2(Position.X, to.Y);
            if (!blocked(alongY)) Position = alongY;
        }
    }
}
