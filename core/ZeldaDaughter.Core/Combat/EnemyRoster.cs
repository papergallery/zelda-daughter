#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Combat
{
    /// <summary>Something an enemy did (<see cref="EnemyEvent"/>) and who did it, for the view's one event stream.</summary>
    public readonly struct EnemyNotice
    {
        public readonly string EnemyId;
        public readonly EnemyEvent Event;
        /// <summary>The same as <see cref="Event"/> (the name the architecture document uses).</summary>
        public EnemyEvent EnemyEvent => Event;

        public EnemyNotice(string enemyId, EnemyEvent ev) { EnemyId = enemyId; Event = ev; }
        public override string ToString() => $"{EnemyId}: {Event}";
    }

    /// <summary>
    /// The living enemies of the game (D-13). The view spawns them (scene markers, night wolves called by <c>TickWorld</c>), ticks them with a
    /// fixed step and taps one to strike. A killed enemy leaves the roster, is remembered in <c>GameState.Killed</c> and leaves a carcass;
    /// living enemies are not saved (D-01). Created by <c>GameState</c> (<c>g.Enemies</c>).
    /// </summary>
    public sealed class EnemyRoster
    {
        readonly EnemySettings _s;
        readonly HeroCombat _hero;
        readonly Func<string> _weaponInHand;
        readonly Func<string, bool> _isKilled;
        readonly Action<Enemy> _died;
        readonly List<Enemy> _active = new List<Enemy>();
        readonly List<EnemyEvent> _scratch = new List<EnemyEvent>(4);
        readonly Func<Vec2, bool> _blockedProxy;

        /// <param name="isKilled">Is this enemy id in the list of kills (no respawn).</param>
        /// <param name="died">Called once when an enemy dies, after it left the roster (remember the kill, leave a carcass).</param>
        public EnemyRoster(EnemySettings settings, HeroCombat hero, Func<string> weaponInHand, Func<string, bool> isKilled, Action<Enemy> died)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _weaponInHand = weaponInHand ?? throw new ArgumentNullException(nameof(weaponInHand));
            _isKilled = isKilled ?? throw new ArgumentNullException(nameof(isKilled));
            _died = died ?? throw new ArgumentNullException(nameof(died));
            _blockedProxy = p => Blocked != null && Blocked(p);
        }

        /// <summary>The living enemies, in the order they were spawned.</summary>
        public IReadOnlyList<Enemy> Active => _active;

        /// <summary>The view's verdict on a spot: true — no enemy may stand there (walls, houses, water). Null — nothing is forbidden.</summary>
        public Func<Vec2, bool>? Blocked { get; set; }

        public Enemy? Get(string id)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].Id == id) return _active[i];
            return null;
        }

        /// <summary>
        /// A new enemy of the kind <paramref name="defId"/> (enemies.json) at a point. Null if this id was killed before. An id already
        /// alive returns that enemy unchanged.
        /// </summary>
        public Enemy? Spawn(string id, string defId, Vec2 position)
        {
            if (_isKilled(id)) return null;
            var have = Get(id);
            if (have != null) return have;
            var e = new Enemy(id, _s, defId, position) { Blocked = _blockedProxy };
            _active.Add(e);
            return e;
        }

        /// <summary>The enemy leaves without dying (the morning, a scene change). False if it was not there.</summary>
        public bool Remove(string id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Id == id) { _active.RemoveAt(i); return true; }
            return false;
        }

        /// <summary>
        /// One step of every living enemy (a fixed step, 0.05 s, from the view). <paramref name="into"/> is cleared and filled with what
        /// they did. <paramref name="roll"/> — one random number 0..1; each enemy derives its own. An enemy that died (bled out) is removed.
        /// </summary>
        public void Tick(float dt, double roll, List<EnemyNotice> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                var e = _active[i];
                _scratch.Clear();
                e.Tick(dt, _hero, (float)Rolls.At(roll, StableHash(e.Id), 7), _scratch);
                for (int k = 0; k < _scratch.Count; k++) into.Add(new EnemyNotice(e.Id, _scratch[k]));
                if (e.IsCarcass)
                {
                    _active.RemoveAt(i--);
                    _died(e);
                }
            }
        }

        /// <summary>
        /// The hero's blow at an enemy with the weapon in hand (<c>GameState.WeaponInHand</c>). One roll 0..1. A killing blow removes the
        /// enemy and fires the death rules (kill remembered, carcass left). Unknown id — <see cref="StrikeOutcome.Unavailable"/>.
        /// </summary>
        public StrikeResult Strike(string enemyId, double roll)
        {
            var e = Get(enemyId);
            if (e == null) return new StrikeResult(StrikeOutcome.Unavailable);
            var r = _hero.Strike(_weaponInHand(), e, (float)roll);
            if (e.IsCarcass)
            {
                Remove(enemyId);
                _died(e);
            }
            return r;
        }

        // string.GetHashCode differs between runs: the same save must give the same wander on every machine
        static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
                return h;
            }
        }
    }
}
