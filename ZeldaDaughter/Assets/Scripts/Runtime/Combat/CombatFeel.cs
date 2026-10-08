using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Feel;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// D-26, the response of the fight (docs/demo/best-practices-feel.md; the numbers are data/combat-feel.json, the rules that can be tested are in
    /// the core's <c>Feel</c>): hears <see cref="SessionEvents"/> and answers a blow with three things at once — a freeze of the two who met
    /// (hit-stop: their poses and places are held, <c>Time.timeScale</c> is not touched and the core goes on), a shake of the camera by trauma, and a
    /// short pulse in the hand. Also: the camera that moves back by a fifth at night and in a fight (threat from the side is seen earlier), and the
    /// world that loses up to a quarter of its colour when she is badly hurt (the second channel of the state, after the figure).
    /// Real time everywhere (<c>unscaledDeltaTime</c>).
    /// </summary>
    public sealed class CombatFeel : MonoBehaviour
    {
        private const string ShakePref = "zd_shake";
        private const float FightHoldSeconds = 1.5f, SaturationRate = 1.0f, FallbackHeroHeight = 1.7f;

        [SerializeField] private GameSession _session;
        [SerializeField] private IsoCamera _iso;
        [SerializeField] private CombatPresenter _combat;
        [SerializeField] private HeroView _heroView;

        private GameState _g;
        private FeelSettings _s;
        private readonly HitStop _stop = new HitStop();
        private Trauma _trauma;
        private float _impactIn = -1f, _impactSeconds;
        private string _impactEnemy;
        private EnemyView _frozenEnemy;
        private float _widen = 1f, _fightHold, _saturation = 1f;

        /// <summary>The «no shake» setting (PlayerPrefs «zd_shake», default on); the camera stays still when off.</summary>
        public static bool ShakeEnabled
        {
            // read from PlayerPrefs once: a PlayerPrefs call every frame allocates
            get { if (_shake < 0) _shake = PlayerPrefs.GetInt(ShakePref, 1); return _shake != 0; }
            set { _shake = value ? 1 : 0; PlayerPrefs.SetInt(ShakePref, _shake); PlayerPrefs.Save(); }
        }
        private static int _shake = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _shake = -1;

        public bool HitStopActive => _stop.Active;
        public float HitStopRemaining => _stop.Remaining;
        /// <summary>Freezes begun since the start / the length of the last one — what the tests and the log read.</summary>
        public int StopsStarted { get; private set; }
        public float LastStopSeconds { get; private set; }
        public float TraumaValue => _trauma != null ? _trauma.Value : 0f;
        public float ShakeAmplitude => _trauma != null ? _trauma.Amplitude : 0f;
        /// <summary>Ortho size now / as built (1 … 1.2).</summary>
        public float CameraWiden => _widen;
        /// <summary>The colour of the world now (1 untouched … 0.75).</summary>
        public float WorldSaturation => _saturation;

        public void Configure(GameSession session, IsoCamera iso, CombatPresenter combat, HeroView heroView)
        {
            _session = session;
            _iso = iso;
            _combat = combat;
            _heroView = heroView;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.HeroStruck += OnHeroStruck;
            _session.Events.Enemy += OnEnemy;
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.Events.StateReady -= OnReady;
                _session.Events.HeroStruck -= OnHeroStruck;
                _session.Events.Enemy -= OnEnemy;
            }
            Release();
            WatercolorFeature.StateSaturation = 1f;
            if (_iso != null) { _iso.SetShake(0f, 0f); _iso.SetWiden(1f); }
        }

        private void OnReady(GameState g)
        {
            _g = g;
            _s = g.Data.Feel;
            _trauma = new Trauma(_s.Shake) { Enabled = ShakeEnabled };
        }

        // ------------------------------------------------------------------ what happened

        private void OnHeroStruck(string enemyId, StrikeResult r)
        {
            if (_s == null) return;
            if (r.Outcome != StrikeOutcome.Hit)
            {
                if (r.Outcome == StrikeOutcome.Miss) ZdLog.Info("Feel", $"miss enemy={enemyId} stop=0.000");
                return;
            }
            float stop = _s.HitStopFor(r.Outcome, r.Killed);
            _trauma.Add(r.Killed ? _s.Shake.Kill : _s.Shake.Hit);
            Haptics.Pulse(r.Killed ? HapticKind.Kill : HapticKind.Hit);
            if (stop > 0f)
            {
                _impactIn = _s.HitStop.ImpactDelay;     // the freeze comes at the impact, a moment into the swing
                _impactSeconds = stop;
                _impactEnemy = enemyId;
                if (_impactIn <= 0f) StartImpact();
            }
            ZdLog.Info("Feel", $"hit enemy={enemyId} killed={r.Killed} stop={stop:0.000} trauma={_trauma.Value:0.00} haptic={(r.Killed ? "kill" : "hit")}");
        }

        private void OnEnemy(EnemyNotice n)
        {
            if (_s == null) return;
            var kind = n.Event.Kind;
            if (kind == EnemyEventKind.Struck)
            {
                _trauma.Add(_s.Shake.HeroStruck);
                Haptics.Pulse(HapticKind.HeroStruck);
                _impactIn = _s.HitStop.StruckDelay;      // a frame or two: her shudder pose is composed first, then held
                _impactSeconds = _s.HitStop.HeroStruck;
                _impactEnemy = n.EnemyId;
                if (_impactIn <= 0f) StartImpact();
                ZdLog.Info("Feel", $"hero_struck enemy={n.EnemyId} stop={_s.HitStop.HeroStruck:0.000} trauma={_trauma.Value:0.00} haptic=hero_struck");
            }
            else if (kind == EnemyEventKind.HeroKnockedOut)
            {
                _trauma.Add(_s.Shake.Knockout);
                Haptics.Pulse(HapticKind.Knockout);
                ZdLog.Info("Feel", $"knockout trauma={_trauma.Value:0.00} haptic=knockout");
            }
        }

        private void StartImpact()
        {
            _impactIn = -1f;
            BeginStop(_impactSeconds, _impactEnemy);
        }

        /// <summary>Holds the hero and the one she met: poses, pictures, places. A longer freeze replaces a shorter one; a shorter does not cut it.</summary>
        private void BeginStop(float seconds, string enemyId)
        {
            if (seconds <= 0f) return;
            bool wasActive = _stop.Active;
            _stop.Request(seconds);
            var enemy = _combat != null && !string.IsNullOrEmpty(enemyId) ? _combat.FindEnemy(enemyId) : null;
            if (_frozenEnemy != null && _frozenEnemy != enemy) _frozenEnemy.Frozen = false;
            _frozenEnemy = enemy;
            if (_frozenEnemy != null) _frozenEnemy.Frozen = true;
            if (_session.Hero != null) _session.Hero.Frozen = true;
            if (_heroView != null) _heroView.Frozen = true;
            if (!wasActive) StopsStarted++;
            LastStopSeconds = _stop.Remaining;
            ZdLog.Info("Feel", $"hitstop {_stop.Remaining:0.000} hero+{(enemy != null ? enemyId : "-")}");
        }

        private void Release()
        {
            if (_frozenEnemy != null) _frozenEnemy.Frozen = false;
            _frozenEnemy = null;
            if (_session != null && _session.Hero != null) _session.Hero.Frozen = false;
            if (_heroView != null) _heroView.Frozen = false;
        }

        // ------------------------------------------------------------------ the frame

        private void Update()
        {
            if (_g == null || _s == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (_impactIn >= 0f)
            {
                _impactIn -= dt;
                if (_impactIn <= 0f) StartImpact();
            }
            bool was = _stop.Active;
            _stop.Tick(dt);
            if (was && !_stop.Active) Release();

            _trauma.Enabled = ShakeEnabled;
            _trauma.Tick(dt);
            if (_iso != null)
            {
                _trauma.Offset(Time.unscaledTimeAsDouble, out float x, out float y);
                _iso.SetShake(x, y);
                UpdateWiden(dt);
            }
            UpdateColour(dt);
        }

        /// <summary>The camera backs off by a fifth at night and in a fight, over half a second; never so far that she is smaller than 1/11 of the frame.</summary>
        private void UpdateWiden(float dt)
        {
            bool night = _g.Data.Session.IsNight(_g.Clock.Daylight);
            if (InFight()) _fightHold = FightHoldSeconds; else _fightHold = Mathf.Max(0f, _fightHold - dt);
            float target = night || _fightHold > 0f ? _s.Camera.WidenFactor : 1f;
            _widen = _s.Camera.Step(_widen, target, dt);
            float baseOrtho = _iso.BaseOrtho;
            float applied = _widen;
            if (baseOrtho > 0f) applied = Mathf.Max(1f, Mathf.Min(_widen, _s.Camera.MaxOrtho(HeroHeight()) / baseOrtho));
            _iso.SetWiden(applied);
        }

        private bool InFight()
        {
            var live = _g.Enemies.Active;
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                var st = e.State;
                if (st != EnemyState.Alert && st != EnemyState.Chase && st != EnemyState.Windup && st != EnemyState.Recover && st != EnemyState.Staggered) continue;
                if ((e.Position - _g.HeroPosition).Length <= _s.Camera.FightMeters) return true;
            }
            return false;
        }

        private float HeroHeight()
        {
            if (_heroView != null && _heroView.Sprite != null)
            {
                float h = _heroView.Sprite.Card.localScale.y;
                if (h > 0.3f) return h;
            }
            return FallbackHeroHeight;
        }

        /// <summary>The world loses colour as she gets worse (second channel after the figure's pose): up to −25 % from severity 0.7.</summary>
        private void UpdateColour(float dt)
        {
            float severity = _s.Condition.Severity(_g.Condition, _g.Data.Wounds);
            float target = _s.Desaturation.Saturation(severity);
            _saturation = Mathf.MoveTowards(_saturation, target, SaturationRate * dt);
            WatercolorFeature.StateSaturation = _saturation;
        }
    }
}
