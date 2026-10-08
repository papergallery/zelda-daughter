using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Loot;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// D-13, the fight in the scene (docs/demo/unity-architecture.md §1, §3): brings the core's enemies (<c>g.Enemies</c>: the spawn points of the
    /// scene, the night wolves that <c>TickWorld</c> calls) and carcasses (<c>g.Carcasses</c>) into the world as views, and turns the player's taps
    /// into the core's calls — a tap on an enemy is one <c>Strike</c>, a tap on a carcass is <c>Carcasses.Tap</c>. The rules are in the core; here
    /// are the calls, the log lines and the events for the other views. Enemies think in the session's tick (<c>GameSession.StepEnemies</c>).
    /// </summary>
    public sealed class CombatPresenter : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private ArtAssets _art;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _enemiesRoot;
        [SerializeField] private Transform _carcassesRoot;
        [SerializeField] private float _carcassReachMeters = 2.5f;
        [SerializeField] private float _blockHeight = 0.6f;
        [SerializeField] private float _blockRadius = 0.3f;

        private GameState _g;
        private int _blockMask;
        private readonly List<EnemyView> _enemies = new List<EnemyView>();
        private readonly List<CarcassView> _carcasses = new List<CarcassView>();

        public IReadOnlyList<EnemyView> EnemyViews => _enemies;
        public IReadOnlyList<CarcassView> CarcassViews => _carcasses;
        public EnemyView FindEnemy(string id) { for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].EnemyId == id) return _enemies[i]; return null; }
        public CarcassView FindCarcass(string id) { for (int i = 0; i < _carcasses.Count; i++) if (_carcasses[i].CarcassId == id) return _carcasses[i]; return null; }

        public void Configure(GameSession session, ArtAssets art, Camera cam, Transform enemiesRoot, Transform carcassesRoot)
        {
            _session = session;
            _art = art;
            _camera = cam;
            _enemiesRoot = enemiesRoot;
            _carcassesRoot = carcassesRoot;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.Enemy += OnEnemy;
            _session.Events.CarcassGone += OnCarcassGone;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.Enemy -= OnEnemy;
            _session.Events.CarcassGone -= OnCarcassGone;
        }

        private void Start()
        {
            _session.OnTap(TapKind.Enemy, Strike);
            _session.OnTap(TapKind.Carcass, TapCarcass);
        }

        // ------------------------------------------------------------------ the world's enemies

        private void OnReady(GameState g)
        {
            _g = g;
            _blockMask = LayerMask.GetMask(ProjectLayers.Blocking);
            g.Enemies.Blocked = IsBlocked;
            int spawned = 0;
            foreach (var point in _session.Index.EnemySpawns)
            {
                if (string.IsNullOrEmpty(point.Detail) || !g.Data.Enemies.Enemies.ContainsKey(point.Detail))
                {
                    ZdLog.Warn("Combat", $"spawn {point.Id}: unknown enemy '{point.Detail}'");
                    continue;
                }
                var at = FreeNear(point.Position);
                if (g.Enemies.Spawn(point.Id, point.Detail, new Vec2(at.x, at.z)) != null) spawned++;
            }
            ZdLog.Info("Combat", $"ready spawned={spawned} killed={g.Killed.Count} carcasses={g.Carcasses.Active.Count}");
            SyncViews();
        }

        /// <summary>No enemy stands in a wall, a tree or a house: a spot is blocked if a collider of the Blocking layer is there.</summary>
        private bool IsBlocked(Vec2 p) => Physics.CheckSphere(new Vector3(p.X, _blockHeight, p.Y), _blockRadius, _blockMask, QueryTriggerInteraction.Ignore);

        /// <summary>The marker may sit in a tree: take the nearest free spot around it.</summary>
        private Vector3 FreeNear(Vector3 p)
        {
            if (!IsBlocked(new Vec2(p.x, p.z))) return p;
            for (int ring = 1; ring <= 8; ring++)
            {
                float r = ring * 0.5f;
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    var q = new Vector3(p.x + Mathf.Cos(a) * r, p.y, p.z + Mathf.Sin(a) * r);
                    if (!IsBlocked(new Vec2(q.x, q.z))) return q;
                }
            }
            return p;
        }

        private void Update()
        {
            if (_g != null) SyncViews();
        }

        /// <summary>Views follow the core's lists: a living enemy has a view, a dead one has a carcass instead, a carcass has one until it is butchered.</summary>
        private void SyncViews()
        {
            var live = _g.Enemies.Active;
            for (int i = 0; i < live.Count; i++)
                if (FindEnemy(live[i].Id) == null) MakeEnemyView(live[i]);
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var v = _enemies[i];
                if (_g.Enemies.Get(v.EnemyId) != null) continue;
                _enemies.RemoveAt(i);
                v.Dispose(_session.Index);
            }

            var dead = _g.Carcasses.Active;
            for (int i = 0; i < dead.Count; i++)
                if (FindCarcass(dead[i].Id) == null) MakeCarcassView(dead[i]);
            for (int i = _carcasses.Count - 1; i >= 0; i--)
            {
                var v = _carcasses[i];
                bool alive = false;
                for (int k = 0; k < dead.Count && !alive; k++) alive = dead[k].Id == v.CarcassId;
                if (alive) continue;
                _carcasses.RemoveAt(i);
                v.Dispose(_session.Index);
            }
        }

        private void MakeEnemyView(Enemy e)
        {
            var go = new GameObject("enemy_" + e.Id);
            go.SetActive(false); // the sprite builds itself in Awake: configure first
            go.transform.SetParent(_enemiesRoot, false);
            go.transform.position = new Vector3(e.Position.X, 0f, e.Position.Y);
            var sprite = go.AddComponent<BillboardSprite>();
            sprite.Configure(_art.Characters, _art.Sprites, _camera, e.DefId);
            var tap = go.AddComponent<Tappable>();
            tap.Configure(e.Id, TapKind.Enemy, -1f, 0.45f);
            var view = go.AddComponent<EnemyView>();
            view.Configure(_session, e, sprite, tap, _art.Sprites);
            go.SetActive(true);
            _session.Index.RegisterDynamic(tap);
            _enemies.Add(view);
            ZdLog.Info("Combat", $"enemy {e.Id} ({e.DefId}) at {e.Position.X:0.0},{e.Position.Y:0.0}");
        }

        private void MakeCarcassView(Carcass c)
        {
            var go = new GameObject("carcass_" + c.Id);
            go.SetActive(false);
            go.transform.SetParent(_carcassesRoot, false);
            go.transform.position = new Vector3(c.Position.X, 0f, c.Position.Y);
            var sprite = go.AddComponent<BillboardSprite>();
            sprite.Configure(_art.Characters, _art.Sprites, _camera, c.DefId);
            var tap = go.AddComponent<Tappable>();
            tap.Configure(c.Id, TapKind.Carcass, -1f, 0.2f);
            var view = go.AddComponent<CarcassView>();
            view.Configure(_session, c, sprite, tap);
            go.SetActive(true);
            _session.Index.RegisterDynamic(tap);
            _carcasses.Add(view);
            ZdLog.Info("Combat", $"carcass {c.Id} ({c.DefId}) at {c.Position.X:0.0},{c.Position.Y:0.0}");
        }

        private void OnCarcassGone(string id) { if (_g != null) SyncViews(); }

        // ------------------------------------------------------------------ what the enemies did

        private void OnEnemy(EnemyNotice n)
        {
            var ev = n.Event;
            for (int i = 0; i < ev.SkillChanges.Count; i++) _session.Events.RaiseSkill(ev.SkillChanges[i]); // toughness on a blow taken, agility on a dodge
            if (ev.Kind != EnemyEventKind.HeroKnockedOut) { ZdLog.Info("Combat", $"{n.EnemyId} {ev.Kind}"); return; }
            // the core's blow reports its knockout only here; the hero's view hears it on the usual condition stream
            ZdLog.Info("Combat", $"{n.EnemyId} HeroKnockedOut");
            _session.Events.RaiseCondition(new ConditionEvent(ConditionEventKind.KnockedOut));
        }

        // ------------------------------------------------------------------ the hero's taps

        /// <summary>A tap on an enemy: one blow with the weapon in hand. Out of reach — the hero turns to it and that is all.</summary>
        public void Strike(Tappable t)
        {
            if (_g == null) return;
            var e = _g.Enemies.Get(t.Id);
            if (e == null) return;
            var toward = new Vector3(e.Position.X, _session.Hero.transform.position.y, e.Position.Y);
            var events = _session.Events;
            string weapon = _g.WeaponInHand;
            var r = _g.Enemies.Strike(t.Id, _session.Rolls.Combat.Next());
            switch (r.Outcome)
            {
                case StrikeOutcome.Hit:
                case StrikeOutcome.Miss:
                    TurnHero(toward);
                    ZdLog.Info("Combat", $"{(r.Outcome == StrikeOutcome.Hit ? "hit" : "miss")} {t.Id} weapon={weapon} killed={r.Killed}");
                    events.RaiseHeroActed(new HeroAct(HeroActKind.Strike, toward, weapon));
                    for (int i = 0; i < r.SkillChanges.Count; i++) events.RaiseSkill(r.SkillChanges[i]);
                    events.RaiseHeroStruck(t.Id, r);
                    for (int i = 0; i < r.EnemyEvents.Count; i++) events.RaiseEnemy(new EnemyNotice(t.Id, r.EnemyEvents[i]));
                    if (r.Killed) SyncViews(); // the carcass is already lying where it fell
                    break;
                case StrikeOutcome.OutOfRange:
                    TurnHero(toward);
                    ZdLog.Info("Combat", $"out_of_range {t.Id}");
                    events.RaiseEnemyOutOfRange(t.Id);
                    break;
                case StrikeOutcome.Cooldown:
                    TurnHero(toward);
                    ZdLog.Info("Combat", $"cooldown {t.Id}");
                    break;
            }
        }

        /// <summary>A tap on a carcass: bare hands take the minimum once, the knife in the bag takes everything and the carcass is gone.</summary>
        public void TapCarcass(Tappable t)
        {
            if (_g == null) return;
            var c = _g.Carcasses.Get(t.Id);
            if (c == null) return;
            var hero = _session.Hero.transform.position;
            var toward = new Vector3(c.Position.X, hero.y, c.Position.Y);
            TurnHero(toward);
            var dx = new Vector2(c.Position.X - hero.x, c.Position.Y - hero.z);
            if (dx.magnitude > _carcassReachMeters)
            {
                ZdLog.Info("Loot", $"out_of_range {t.Id}");
                return;
            }
            var r = _g.Carcasses.Tap(t.Id);
            ZdLog.Info("Loot", $"{t.Id} {r}");
            var events = _session.Events;
            events.RaiseLooted(t.Id, r);
            switch (r.Outcome)
            {
                case LootOutcome.Butchered:
                    events.RaiseHeroActed(new HeroAct(HeroActKind.Butcher, toward, _g.Data.Enemies.ButcherTool));
                    _session.BagChanged("loot");
                    break;
                case LootOutcome.Minimal:
                    events.RaiseHeroActed(new HeroAct(HeroActKind.Pickup, toward, r.Items.Count > 0 ? r.Items[0].Item : null));
                    _session.BagChanged("loot");
                    _session.Say(Topics.ButcherNoKnife);
                    break;
                case LootOutcome.Nothing:
                    _session.Say(Topics.ButcherNoKnife);
                    break;
                case LootOutcome.NoRoom:
                    _session.Say(Topics.CraftNoRoom);
                    break;
            }
        }

        /// <summary>The hero faces a point on the ground (she is not walking: the controller turns her only when she moves).</summary>
        private void TurnHero(Vector3 toward)
        {
            var hero = _session.Hero.transform;
            var d = toward - hero.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            hero.rotation = Quaternion.LookRotation(d);
        }
    }

    /// <summary>The names of the physics layers (ProjectSetup makes them).</summary>
    internal static class ProjectLayers
    {
        public const string Ground = "Ground";
        public const string Blocking = "Blocking";
    }
}
