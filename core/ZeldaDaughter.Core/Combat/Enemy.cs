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
    public enum EnemyState { Idle, Wander, Alert, Chase, Windup, Recover, Staggered, Leaving, Dead, Fleeing }

    /// <summary>Is there a fire (a campfire, the hero's torch) that scares at this point? <paramref name="source"/> and <paramref name="radius"/> — where it burns and how far its fear reaches (D-23).</summary>
    public delegate bool FireQuery(Vec2 at, out Vec2 source, out float radius);

    public enum EnemyEventKind { Alerted, WindupStarted, Struck, Dodged, Staggered, LostInterest, Died, HeroKnockedOut, Frightened }

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
        bool _stalking;
        bool _ignoreHero;
        Vec2 _calmAt;
        Vec2 _dir = new Vec2(1, 0);
        Vec2 _fireAt;
        float _fireRadius;

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

        /// <summary>Seconds in the current state (always counts up, also in a stagger); starts over on every state change. For the view's animation (C6).</summary>
        public float StateSeconds { get; private set; }

        /// <summary>0..1 through the windup (the readable swing), 0 in any other state. The blow lands when it reaches 1. For the view (C6).</summary>
        public float WindupProgress => State != EnemyState.Windup ? 0f : Math.Min(1f, StateSeconds / WindupSeconds);

        /// <summary>Windup length actually used: data value, never below the minimum.</summary>
        public float WindupSeconds => Math.Max(Def.Windup, _s.MinWindup);

        /// <summary>The blow lands at this distance or closer: the range less the dodge forgiveness (D-26).</summary>
        public float ReachMeters => Math.Max(0.1f, Def.Range - _s.DodgeForgiveness);

        /// <summary>Wakes the enemy as if it had been hurt (a thrown stone, a noise) — without damage.</summary>
        public IReadOnlyList<EnemyEvent> Provoke()
        {
            var ev = new List<EnemyEvent>(1);
            if (!IsCarcass) StartAggro(ev);
            return ev;
        }

        /// <summary>
        /// A night predator (D-23): it goes for the hero from wherever it was called, and does not give up on the way — until it is within its
        /// aggro range, or she is farther than <c>stalkMeters</c>. After that it behaves as any enemy.
        /// </summary>
        public IReadOnlyList<EnemyEvent> Hunt()
        {
            var ev = new List<EnemyEvent>(1);
            if (IsCarcass) return ev;
            StartAggro(ev);
            _stalking = true;
            return ev;
        }

        /// <summary>The morning (D-23): the enemy loses interest and walks away from the hero; it comes back to her only after she has moved on.</summary>
        public IReadOnlyList<EnemyEvent> Dismiss()
        {
            var ev = new List<EnemyEvent>(1);
            if (IsCarcass || State == EnemyState.Leaving) return ev;
            Disengage(ev, leave: true);
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
                if (State != EnemyState.Staggered) StateSeconds = 0f;
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
                if (_stalking && (dist <= Def.AggroRange || dist > _s.StalkMeters)) _stalking = false;   // arrived, or too far to follow
                if (down) { Disengage(ev, leave: true); }                       // April: endless knockout loop
                else if (!_stalking && dist > _s.LoseInterestFactor * Def.AggroRange) Disengage(ev, leave: false);
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

            if (Def.FearsFire && Fire != null && State != EnemyState.Staggered && State != EnemyState.Fleeing && Fire(Position, out var src, out var rad))
            {
                // the fire reaches it: the windup is dropped, it runs from the fire (D-23)
                _fireAt = src; _fireRadius = rad;
                Enter(EnemyState.Fleeing);
                ev.Add(new EnemyEvent(EnemyEventKind.Frightened));
            }

            StateSeconds += dt;
            _timer += State == EnemyState.Staggered ? -dt : dt;
            switch (State)
            {
                case EnemyState.Fleeing:
                    {
                        if (Fire != null && Fire(Position, out var s2, out var r2)) { _fireAt = s2; _fireRadius = r2; }
                        var away = Position - _fireAt;
                        float len = away.Length;
                        Move(len > 1e-4f ? away * (1f / len) : _dir * -1f, _s.Fire.FleeSpeedFactor * Def.ChaseSpeed * SpeedScale * dt, false);
                        if (len >= _fireRadius + _s.Fire.FleeMargin || StateSeconds >= _s.Fire.MaxFleeSeconds) Enter(_aggro ? EnemyState.Chase : EnemyState.Idle);
                    }
                    break;
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
                    if (dist <= Def.Range && dist > ReachMeters && _s.WindupCreepFactor > 0f)
                    {
                        // the hero stands inside the old range but outside the reach: the enemy steps in, so standing still is still a hit
                        float step = Math.Min(Def.ChaseSpeed * _s.WindupCreepFactor * SpeedScale * dt, dist - ReachMeters);
                        Move(toHero * (1f / dist), step);
                        dist = (hero.Position - Position).Length;
                    }
                    if (_timer >= WindupSeconds)
                    {
                        if (dist <= ReachMeters + 1e-3f) Land(hero, ev);
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
            _stalking = false;
            _ignoreHero = leave;
            Enter(leave ? EnemyState.Leaving : EnemyState.Idle);
            ev.Add(new EnemyEvent(EnemyEventKind.LostInterest));
        }

        void Enter(EnemyState s) { State = s; _timer = 0; StateSeconds = 0; }

        /// <summary>
        /// The view's verdict on a spot (a wall, a house, deep water): true — the enemy cannot stand there. A step into such a spot is
        /// tried again along one axis (sliding), else the enemy stays. Set by <see cref="EnemyRoster"/>.
        /// </summary>
        public Func<Vec2, bool>? Blocked { get; set; }

        /// <summary>Where fire scares (set by <see cref="EnemyRoster"/>); null — nothing does. Only an enemy whose definition <c>fearsFire</c> cares.</summary>
        public FireQuery? Fire { get; set; }

        internal void SetPosition(Vec2 p) => Position = p;

        bool Closed(Vec2 p, bool avoidFire)
        {
            var blocked = Blocked;
            if (blocked != null && blocked(p)) return true;
            return avoidFire && Def.FearsFire && Fire != null && Fire(p, out _, out _);   // a scared animal does not step into the fire's reach
        }

        void Move(Vec2 dir, float meters, bool avoidFire = true)
        {
            var to = Position + dir * meters;
            if (!Closed(to, avoidFire)) { Position = to; return; }
            var alongX = new Vec2(to.X, Position.Y);
            if (!Closed(alongX, avoidFire)) { Position = alongX; return; }
            var alongY = new Vec2(Position.X, to.Y);
            if (!Closed(alongY, avoidFire)) Position = alongY;
        }
    }
}
