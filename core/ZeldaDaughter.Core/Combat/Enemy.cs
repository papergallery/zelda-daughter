#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Combat
{
    /// <summary>The blow itself is a moment (an event), not a state: windup → <see cref="EnemyEventKind.Struck"/> or Dodged → recover.</summary>
    public enum EnemyState { Idle, Wander, Alert, Chase, Windup, Recover, Staggered, Leaving, Dead }

    public enum EnemyEventKind { Alerted, WindupStarted, Struck, Dodged, Staggered, LostInterest, Died }

    public readonly struct EnemyEvent
    {
        public readonly EnemyEventKind Kind;
        /// <summary>Damage dealt to the hero (Struck) or taken (Staggered, Died).</summary>
        public readonly float Amount;

        public EnemyEvent(EnemyEventKind kind, float amount = 0) { Kind = kind; Amount = amount; }
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

        public IReadOnlyList<EnemyEvent> Tick(float dt, HeroCombat hero, float roll)
        {
            var ev = new List<EnemyEvent>(2);
            if (IsCarcass || dt <= 0) return ev;
            bool down = hero.Condition.IsKnockedOut;
            Vec2 toHero = hero.Position - Position;
            float dist = toHero.Length;

            if (_aggro)
            {
                if (down) { Disengage(ev, leave: true); }                       // April: endless knockout loop
                else if (dist > _s.LoseInterestFactor * Def.AggroRange) Disengage(ev, leave: false);
            }
            else if (Def.AggroOnSight && !down && !_ignoreHero && dist <= Def.AggroRange && State != EnemyState.Staggered)
            {
                StartAggro(ev);
            }

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
                    Move(_dir, _s.WanderSpeed * dt);
                    if (_timer >= _s.WanderSeconds) Enter(EnemyState.Idle);
                    break;
                case EnemyState.Leaving:
                    Move(dist > 1e-4f ? toHero * (-1f / dist) : _dir, _s.LeaveSpeed * dt);
                    if (dist > Def.AggroRange) { _ignoreHero = false; Enter(EnemyState.Idle); }
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
                    else Move(toHero * (1f / dist), Math.Min(Def.ChaseSpeed * dt, dist));
                    break;
                case EnemyState.Windup:
                    if (_timer >= WindupSeconds)
                    {
                        if (dist <= Def.Range) ev.Add(Land(hero));
                        else
                        {
                            hero.Skills.Apply(SkillEvent.Dodged());
                            ev.Add(new EnemyEvent(EnemyEventKind.Dodged));
                        }
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
            return ev;
        }

        EnemyEvent Land(HeroCombat hero)
        {
            var type = Def.ParsedWound;
            if (type.HasValue) hero.Condition.Wound(type.Value, Def.Severity);
            float dmg = Def.Damage * (1f - hero.Skills.DamageReduction());
            hero.Condition.Damage(dmg);
            hero.Skills.Apply(SkillEvent.Damaged(dmg));
            return new EnemyEvent(EnemyEventKind.Struck, dmg);
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

        void Move(Vec2 dir, float meters) => Position = Position + dir * meters;
    }
}
