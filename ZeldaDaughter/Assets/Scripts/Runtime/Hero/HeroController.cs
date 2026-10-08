using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Movement;
using ZeldaDaughter.World;
using CoreTouchPhase = ZeldaDaughter.Core.Input.TouchPhase;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ZeldaDaughter.Hero
{
    /// <summary>
    /// T-05: one finger moves the hero (project-design.md §1). Touches → the core GestureRecognizer → MoveIntent through
    /// the camera yaw → SpeedModel → CharacterController. Unity only translates input and shows the result.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroController : MonoBehaviour
    {
        [SerializeField] private IsoCamera _iso;
        [SerializeField] private Camera _camera;
        [SerializeField] private string _terrain = "ground";
        [SerializeField] private TerrainZones _zones;
        private string _lastTerrain;

        private CharacterController _cc;
        private GestureRecognizer _gestures;
        private SpeedModel _speed;
        private GestureSettings _input;
        private float _dpi;
        private MoveIntent _intent = MoveIntent.None;
        private float _verticalSpeed;
        private float _moved;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private static readonly IReadOnlyList<float> NoModifiers = new float[0];

        public void Configure(IsoCamera iso, Camera cam, string terrain)
        {
            _iso = iso;
            _camera = cam;
            _terrain = terrain;
        }

        /// <summary>Rivers, roads and terrain zones of the scene (D-10); without them the ground terrain of the scene applies everywhere.</summary>
        public void SetZones(TerrainZones zones) => _zones = zones;

        /// <summary>The terrain id under the hero now.</summary>
        public string CurrentTerrain => _zones != null ? _zones.At(transform.position) : _terrain;

        /// <summary>Tests: a fixed density instead of the device's.</summary>
        public void UseDpi(float dpi)
        {
            _dpi = dpi;
            _gestures = new GestureRecognizer(_input, _dpi);
        }

        public bool IsMoving => _intent.IsMoving;

        /// <summary>Every recognised gesture — the game session listens (T-10).</summary>
        public event Action<GestureEvent> Gesture;

        /// <summary>Move without walking (loading a save).</summary>
        public void Teleport(Vector3 position, float facingDegrees)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>(); // the session may load before this Awake
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facingDegrees, 0f));
            _cc.enabled = true;
            _intent = MoveIntent.None;
            _verticalSpeed = 0f;
            if (_iso != null) _iso.SnapToTarget(); // the camera must not glide from the old place
        }

        /// <summary>A touch over UI (reply buttons) belongs to the UI, not to walking.</summary>
        public bool IsOverUI(Vec2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = new Vector2(screen.X, screen.Y) }, _uiHits);
            return _uiHits.Count > 0;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            var data = GameData.Current;
            _input = data.Input;
            _speed = new SpeedModel(data.Movement);
            _dpi = Screen.dpi > 0 ? Screen.dpi : _input.ReferenceDpi;
            _gestures = new GestureRecognizer(_input, _dpi);
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR || UNITY_STANDALONE
            TouchSimulation.Enable(); // mouse drag = one finger
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            TouchSimulation.Disable();
#endif
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            foreach (var t in Touch.activeTouches)
            {
                var phase = Map(t.phase);
                if (phase == null) continue;
                OnTouch(t.finger.index, phase.Value, t.time, new Vec2(t.screenPosition.x, t.screenPosition.y));
            }
            Handle(_gestures.Tick(Time.realtimeSinceStartupAsDouble));
            Move(Time.deltaTime);
        }

        /// <summary>One touch from the device (Update) or a test: a touch that begins over UI belongs to the UI and is dropped.
        /// Returns whether the hero's gestures got it.</summary>
        public bool OnTouch(int finger, CoreTouchPhase phase, double time, Vec2 pos)
        {
            if (phase == CoreTouchPhase.Began && IsOverUI(pos)) return false;
            Feed(new TouchSample(finger, phase, time, pos, phase == CoreTouchPhase.Began ? HitAt(pos) : default));
            return true;
        }

        /// <summary>One touch sample (already filtered).</summary>
        public void Feed(TouchSample sample) => Handle(_gestures.Feed(sample));

        /// <summary>What a touch at this screen point lands on: the hero by its screen projection, else what the ray hits.</summary>
        public TouchHit HitAt(Vec2 screen)
        {
            var heroOnScreen = _camera.WorldToScreenPoint(transform.position);
            if (_input.IsOnHero(screen, new Vec2(heroOnScreen.x, heroOnScreen.y), _dpi)) return TouchHit.Hero;
            if (Physics.Raycast(_camera.ScreenPointToRay(new Vector3(screen.X, screen.Y)), out var hit, 500f))
            {
                // A composite object (a house) has colliders on its parts: the object is the ancestor directly under "Objects".
                var t = hit.collider.transform;
                while (t.parent != null && t.parent.name != "Objects") t = t.parent;
                if (t.parent != null) return TouchHit.Object(t.name);
            }
            return TouchHit.Ground;
        }

        private void Handle(IReadOnlyList<GestureEvent> events)
        {
            foreach (var e in events)
            {
                Gesture?.Invoke(e);
                switch (e.Kind)
                {
                    case GestureKind.SwipeStarted:
                        _moved = 0f;
                        break;
                    case GestureKind.SwipeUpdated:
                        bool was = _intent.IsMoving;
                        _intent = CameraBasis.FromYawDegrees(_iso != null ? _iso.Yaw : 0f).Intent(e.Direction, e.Strength);
                        if (!was) ZdLog.Info("Move", $"start dir={_intent.Direction} strength={e.Strength:0.00}");
                        break;
                    case GestureKind.SwipeEnded:
                        if (_intent.IsMoving) ZdLog.Info("Move", $"stop dist={_moved:0.00}");
                        _intent = MoveIntent.None;
                        break;
                    case GestureKind.LongPressOnHero:
                        ZdLog.Info("Gesture", "long_press_hero");
                        break;
                    case GestureKind.LongPressReleased:
                        ZdLog.Info("Gesture", "long_press_released");
                        break;
                    case GestureKind.Tap:
                        ZdLog.Info("Gesture", "tap " + e.TargetId);
                        break;
                }
            }
        }

        private void Move(float dt)
        {
            if (dt <= 0f) return;
            Vector3 horizontal = Vector3.zero;
            if (_intent.IsMoving)
            {
                string terrain = CurrentTerrain;
                if (terrain != _lastTerrain)
                {
                    ZdLog.Info("Move", $"terrain {_lastTerrain ?? "-"} -> {terrain}");
                    _lastTerrain = terrain;
                }
                float speed = _speed.Speed(_intent.Strength, terrain, NoModifiers);
                horizontal = new Vector3(_intent.Direction.X, 0f, _intent.Direction.Y) * speed * dt;
                transform.rotation = Quaternion.LookRotation(new Vector3(_intent.Direction.X, 0f, _intent.Direction.Y));
            }
            _verticalSpeed = _cc.isGrounded && _verticalSpeed < 0f ? -1f : _verticalSpeed + Physics.gravity.y * dt;
            var before = transform.position;
            _cc.Move(horizontal + Vector3.up * _verticalSpeed * dt);
            var delta = transform.position - before;
            _moved += new Vector2(delta.x, delta.z).magnitude;
        }

        private static CoreTouchPhase? Map(UnityTouchPhase p)
        {
            switch (p)
            {
                case UnityTouchPhase.Began: return CoreTouchPhase.Began;
                case UnityTouchPhase.Moved: return CoreTouchPhase.Moved;
                case UnityTouchPhase.Stationary: return CoreTouchPhase.Stationary;
                case UnityTouchPhase.Ended: return CoreTouchPhase.Ended;
                case UnityTouchPhase.Canceled: return CoreTouchPhase.Canceled;
                default: return null;
            }
        }
    }
}
