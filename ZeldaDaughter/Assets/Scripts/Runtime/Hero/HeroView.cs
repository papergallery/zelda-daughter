using System;
using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Hero
{
    /// <summary>
    /// D-11: the hero as a drawn figure (project-design.md §1, §6): no bars — the state is read from the figure. Takes the way she goes from
    /// how she actually moved (so a wall that stops her stops the picture), turns the <see cref="BillboardSprite"/> to it, advances its frame
    /// by the path walked (a limp makes the steps uneven), bounces a little, plays the hands' actions as poses made by code (a lunge, a
    /// crouch, a chew), tints the clothes by the wounds (reddish — a cut, dark — a burn, greenish — poison), holds the side when hurt,
    /// and lays her down in a knockout. Hunger has no picture (only remarks). The sound reads <see cref="SessionEvents.HeroStep"/>.
    /// </summary>
    [DefaultExecutionOrder(-10)] // before the sprite's LateUpdate: the pose is set, then drawn
    public sealed class HeroView : MonoBehaviour
    {
        // Look numbers (not balance): how long each action shows, how strong the poses are.
        const float StrikeSeconds = 0.40f, PickupSeconds = 0.45f, EatSeconds = 0.70f, TreatSeconds = 0.80f, ButcherSeconds = 1.00f, PlaceSeconds = 0.45f, CraftSeconds = 0.50f;
        const float FallSeconds = 0.30f, GetUpSeconds = 0.45f, HitShakeSeconds = 0.25f;
        const float StopGraceSeconds = 0.10f, TeleportMeters = 2f;

        [SerializeField] private GameSession _session;
        [SerializeField] private HeroController _hero;
        [SerializeField] private BillboardSprite _sprite;
        [SerializeField] private SpriteLook _look;

        private GameState _g;
        private Vector3 _last;
        private bool _hasLast;
        private float _idle;
        private bool _walking;
        private float _s, _w, _wPrev;       // half-strides walked: raw, warped by the limp, and the warped one of the last frame
        private int _stepsDone;
        private Color _tint = Color.white;

        private bool _hasAct;
        private HeroActKind _act;
        private float _actT, _actDur;
        private Vector3 _actToward;
        private float _hitShake;

        private bool _down, _coreDown;
        private float _downT, _upT = GetUpSeconds;

        /// <summary>An action of the hands is showing now.</summary>
        public bool Acting => _hasAct;
        public HeroActKind CurrentAct => _act;
        /// <summary>Lies knocked out (the fall has begun).</summary>
        public bool IsDown => _down;
        public bool Walking => _walking;
        public bool Limping { get; private set; }
        /// <summary>Foot-falls shown since the hero last stood still.</summary>
        public int Steps => _stepsDone;
        public Color Tint => _tint;
        public BillboardSprite Sprite => _sprite;

        public void Configure(GameSession session, HeroController hero, BillboardSprite sprite, SpriteLook look)
        {
            _session = session;
            _hero = hero;
            _sprite = sprite;
            _look = look;
        }

        private void OnEnable()
        {
            var e = _session.Events;
            e.StateReady += OnStateReady;
            e.Condition += OnCondition;
            e.HeroActed += OnActed;
            e.Enemy += OnEnemy;
            e.HeroStruck += OnHeroStruck;
            e.World += OnWorld;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            var e = _session.Events;
            e.StateReady -= OnStateReady;
            e.Condition -= OnCondition;
            e.HeroActed -= OnActed;
            e.Enemy -= OnEnemy;
            e.HeroStruck -= OnHeroStruck;
            e.World -= OnWorld;
        }

        // ------------------------------------------------------------------ what the core tells

        private void OnStateReady(GameState g)
        {
            _g = g;
            _coreDown = g.Condition.IsKnockedOut;
            if (_coreDown) SetDown(true, true); // a save made mid-knockout: she is already on the ground
        }

        private void OnCondition(ConditionEvent e)
        {
            if (e.Kind == ConditionEventKind.KnockedOut) SetDown(true, false);
            else if (e.Kind == ConditionEventKind.Revived) SetDown(false, false);
        }

        private void OnEnemy(EnemyNotice n)
        {
            if (n.Event.Kind == EnemyEventKind.Struck) _hitShake = HitShakeSeconds;
            else if (n.Event.Kind == EnemyEventKind.HeroKnockedOut) SetDown(true, false); // the core hands this one over only here
        }

        private void OnHeroStruck(string enemyId, StrikeResult r)
        {
            // the blow has its own HeroActed from the combat presenter; a lone result still shows a lunge
            if (!(_hasAct && _act == HeroActKind.Strike)) Begin(new HeroAct(HeroActKind.Strike));
        }

        private void OnWorld(ZeldaDaughter.Core.World.WorldEvent e)
        {
            if (e.Kind == ZeldaDaughter.Core.World.WorldEventKind.HeroScorched) _hitShake = HitShakeSeconds;
        }

        private void OnActed(HeroAct act) => Begin(act);

        // ------------------------------------------------------------------ actions and knockout

        private void Begin(HeroAct act)
        {
            if (_down) return;
            _hasAct = true;
            _act = act.Kind;
            _actT = 0f;
            _actToward = act.Toward;
            switch (act.Kind)
            {
                case HeroActKind.Strike: _actDur = StrikeSeconds; break;
                case HeroActKind.Pickup: _actDur = PickupSeconds; break;
                case HeroActKind.Eat: _actDur = EatSeconds; break;
                case HeroActKind.Treat: _actDur = TreatSeconds; break;
                case HeroActKind.Butcher: _actDur = ButcherSeconds; break;
                case HeroActKind.Place: _actDur = PlaceSeconds; break;
                default: _actDur = CraftSeconds; break;
            }
            if (act.Toward != Vector3.zero)
            {
                var to = act.Toward - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) _sprite.FaceDirection(to);
            }
            ZdLog.Info("Hero", "act " + act);
        }

        private void SetDown(bool down, bool instant)
        {
            if (down == _down) return;
            _down = down;
            _hasAct = false;
            if (down)
            {
                _downT = instant ? FallSeconds : 0f;
                StopWalking();
                if (_hero != null) _hero.Locked = true;
            }
            else
            {
                _upT = 0f;
                if (_hero != null) _hero.Locked = false;
            }
            ZdLog.Info("Hero", down ? "knocked_out" : "revived");
            _session.Events.RaiseHeroDown(down);
        }

        // ------------------------------------------------------------------ the frame

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            var p = transform.position;
            if (!_hasLast) { _last = p; _hasLast = true; }
            var d = p - _last;
            d.y = 0f;
            _last = p;
            float dist = d.magnitude;
            if (dist > TeleportMeters) dist = 0f; // a teleport (a load) is not a walk

            if (_g != null && _g.Condition.IsKnockedOut != _coreDown)
            {
                _coreDown = _g.Condition.IsKnockedOut;
                SetDown(_coreDown, false);
            }
            if (_down && _hero != null && !_hero.Locked) _hero.Locked = true; // a closing window must not stand her up

            Limping = _g != null && (_g.Condition.Flags & VisibleState.Limping) != 0;

            if (!_down && dist > 1e-4f && dt > 0f)
            {
                _idle = 0f;
                _sprite.FaceDirection(d);
                WalkBy(dist);
            }
            else
            {
                _idle += dt;
                if (_idle > StopGraceSeconds) StopWalking();
            }

            _sprite.SetPose(ComposePose(dt));
            UpdateTint(dt);
        }

        private void StopWalking()
        {
            if (_walking || _s > 0f) _sprite.Stop();
            _walking = false;
            _s = _w = _wPrev = 0f;
            _stepsDone = 0;
        }

        /// <summary>The step: the path of the sprite goes by half-strides; a limp makes the first of each pair long and the second short.</summary>
        private void WalkBy(float meters)
        {
            _walking = true;
            float half = Mathf.Max(0.05f, _sprite.StrideMeters * 0.5f);
            _s += meters / half;
            _w = Warp(_s, LimpSkew());
            _sprite.Advance((_w - _wPrev) * half);
            _wPrev = _w;
            int steps = (int)Mathf.Floor(_w);
            while (_stepsDone < steps)
            {
                _stepsDone++;
                _session.Events.RaiseHeroStep(transform.position, Limping);
            }
        }

        /// <summary>How uneven the pair of steps is: 0 healthy … 0.45 a full fracture (the long step lasts 1.45 of a half-stride, the short one 0.55).</summary>
        private float LimpSkew()
        {
            if (_g == null || (_g.Condition.Flags & VisibleState.Limping) == 0) return 0f;
            return Mathf.Lerp(0.2f, 0.45f, Mathf.Clamp01(_g.Condition.Severity(WoundType.Fracture)));
        }

        /// <summary>Time of a pair of steps (2 units) with the first one lasting 1+d and the second 1-d; d = 0 returns <paramref name="s"/>.</summary>
        public static float Warp(float s, float d)
        {
            if (d <= 0f) return s;
            float k = Mathf.Floor(s * 0.5f) * 2f, r = s - k, first = 1f + d;
            return k + (r < first ? r / first : 1f + (r - first) / (1f - d));
        }

        private BillboardPose ComposePose(float dt)
        {
            var pose = BillboardPose.Stand;
            float bob = _look != null ? _look.BobMeters : 0.035f;

            if (_down) return DownPose(dt, pose);
            if (_upT < GetUpSeconds)
            {
                _upT += dt;
                float k = 1f - Mathf.Clamp01(_upT / GetUpSeconds);
                pose.Crouch = 0.55f * k;
                pose.TiltDegrees = 25f * k;
            }

            if (_walking)
            {
                float phase = _w - Mathf.Floor(_w);
                float hump = Mathf.Sin(Mathf.PI * phase);
                pose.Lift = bob * hump;
                if (Limping && ((int)Mathf.Floor(_w) & 1) == 0) // the long step: she drags the bad leg and dips on it
                {
                    pose.Crouch += 0.08f * hump;
                    pose.TiltDegrees -= 5f * hump;
                }
            }

            bool hurtSide = _g != null && (_g.Condition.Flags & (VisibleState.HoldingSide | VisibleState.Bleeding)) != 0;
            if (hurtSide) // a hand at the side: she leans on it
            {
                pose.TiltDegrees -= 4f;
                pose.Crouch += 0.05f;
            }

            if (_hasAct)
            {
                _actT += dt;
                float u = Mathf.Clamp01(_actT / _actDur), hump = Mathf.Sin(Mathf.PI * u);
                switch (_act)
                {
                    case HeroActKind.Strike:
                        pose.TiltDegrees += Side(_actToward) * 16f * hump;   // lunges at the target
                        pose.Lift += 0.06f * hump;
                        break;
                    case HeroActKind.Pickup:
                    case HeroActKind.Place:
                        pose.Crouch += 0.65f * hump;
                        break;
                    case HeroActKind.Eat:
                    case HeroActKind.Treat:
                        pose.Crouch += 0.15f * hump;
                        pose.TiltDegrees -= 4f * hump;
                        pose.Shake = 0.012f * hump;                          // chews
                        break;
                    case HeroActKind.Butcher:
                        pose.Crouch += 0.6f * Mathf.Min(1f, hump * 2f);
                        pose.Shake = 0.02f * hump;
                        break;
                    default: // Craft
                        pose.Crouch += 0.3f * hump;
                        pose.Shake = 0.01f * hump;
                        break;
                }
                if (u >= 1f) _hasAct = false;
            }

            if (_hitShake > 0f)
            {
                _hitShake -= dt;
                pose.Shake = Mathf.Max(pose.Shake, 0.05f * Mathf.Clamp01(_hitShake / HitShakeSeconds));
            }
            return pose;
        }

        private BillboardPose DownPose(float dt, BillboardPose pose)
        {
            _downT += dt;
            float u = Mathf.Clamp01(_downT / FallSeconds);
            if (u < 1f) { pose.TiltDegrees = 70f * u; pose.Crouch = 0.4f * u; }
            else pose.Lying = true;
            return pose;
        }

        /// <summary>-1 … 1: the target is to the left … right of the screen (the lunge leans toward it); with no target — the way she faces.</summary>
        private float Side(Vector3 toward)
        {
            var cam = _sprite.Camera != null ? _sprite.Camera.transform : null;
            var right = cam != null ? cam.right : Vector3.right;
            right.y = 0f;
            right.Normalize();
            var to = toward == Vector3.zero ? transform.forward : toward - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return _sprite.Mirrored ? -1f : 1f;
            return Mathf.Clamp(Vector3.Dot(to.normalized, right) * 2f, -1f, 1f);
        }

        /// <summary>Wounds as the colour of the clothes (project-design §6): a cut — reddish, a burn — dark, poison — greenish. Hunger has none.</summary>
        private void UpdateTint(float dt)
        {
            var target = Color.white;
            if (_g != null)
            {
                var c = _g.Condition;
                target = Mul(target, Color.Lerp(Color.white, new Color(1f, 0.55f, 0.5f), Strength(c.Severity(WoundType.Cut))));
                target = Mul(target, Color.Lerp(Color.white, new Color(0.5f, 0.46f, 0.44f), Strength(c.Severity(WoundType.Burn))));
                target = Mul(target, Color.Lerp(Color.white, new Color(0.72f, 1f, 0.66f), Strength(c.Severity(WoundType.Poison))));
            }
            _tint = Color.Lerp(_tint, target, 1f - Mathf.Exp(-6f * dt));
            if (Mathf.Abs(_tint.r - target.r) + Mathf.Abs(_tint.g - target.g) + Mathf.Abs(_tint.b - target.b) < 0.004f) _tint = target;
            _sprite.SetTint(_tint);
        }

        private static float Strength(float severity) => severity <= 0f ? 0f : 0.45f + 0.55f * Mathf.Clamp01(severity);
        private static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);
    }
}
