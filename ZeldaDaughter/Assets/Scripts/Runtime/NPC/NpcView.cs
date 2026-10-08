using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// One resident in the scene (docs/demo/unity-architecture.md §1, D-12): the object <c>npc_&lt;id&gt;</c> with a drawn figure (child «Figure»,
    /// <see cref="BillboardSprite"/>) and no collider. It walks the waypoints <see cref="NpcPresenter"/> gives it at the speed of the core
    /// (<c>npcs.json walkSpeed</c>), the frame follows the path walked; it can be put somewhere at once, hidden (asleep indoors: not drawn,
    /// not tappable), stopped while the hero talks to it, turned toward a point, and made to point. It decides nothing about where to go.
    /// </summary>
    public sealed class NpcView : MonoBehaviour
    {
        /// <summary>How long the lean of a pointing gesture lasts, s.</summary>
        public const float PointSeconds = 2.5f;
        private const float PointTiltDegrees = 9f;
        private const float MaxFrameSeconds = 0.1f;

        [SerializeField] private string _id;
        [SerializeField] private BillboardSprite _figure;
        [SerializeField] private Tappable _tappable;

        private readonly List<Vector3> _path = new List<Vector3>(16);
        private int _next;
        private float _speed;
        private bool _hideOnArrival;
        private bool _hidden;
        private bool _talking;
        private float _holdUntil;
        private float _poseUntil;
        private Vector3 _heading = Vector3.back;

        public string Id => _id;
        public BillboardSprite Figure => _figure;
        public bool Hidden => _hidden;
        public bool IsWalking => _next < _path.Count;
        /// <summary>Stopped to talk to the hero: does not walk until <see cref="EndTalk"/>.</summary>
        public bool Talking => _talking;
        /// <summary>The way she faces now (ground plane, unit).</summary>
        public Vector3 Heading => _heading;
        /// <summary>The waypoints of the walk in progress (copy-free view; empty when standing).</summary>
        public IReadOnlyList<Vector3> Waypoints => _path;
        public int NextWaypoint => _next;
        public bool Pointing => Time.time < _poseUntil;
        /// <summary>The height of her head above her feet, m (bubbles sit above it).</summary>
        public float HeadHeight => _figure != null ? Mathf.Clamp(_figure.Card.localScale.y, 1.2f, 3f) : 1.8f;

        public event Action<NpcView> Arrived;

        public void Configure(string id, BillboardSprite figure, Tappable tappable)
        {
            _id = id;
            _figure = figure;
            _tappable = tappable;
        }

        /// <summary>Stand here now, hidden or not; any walk in progress is forgotten.</summary>
        public void Place(Vector3 position, bool hidden)
        {
            _path.Clear();
            _next = 0;
            _hideOnArrival = false;
            transform.position = position;
            if (_figure != null) _figure.Stop();
            SetHidden(hidden);
        }

        /// <summary>
        /// Walk the waypoints at <paramref name="speed"/> m/s (the first one is where she stands now and is skipped). Called while she is already
        /// walking, the new waypoints follow the old ones — she finishes her road first.
        /// </summary>
        public void Walk(IReadOnlyList<Vector3> waypoints, float speed, bool hideOnArrival)
        {
            if (_hidden) SetHidden(false);
            _speed = speed;
            _hideOnArrival = hideOnArrival;
            if (!IsWalking) { _path.Clear(); _next = 0; }
            for (int i = 1; i < waypoints.Count; i++) _path.Add(waypoints[i]);
            if (_path.Count == 0) Arrive();
        }

        /// <summary>Asleep indoors, or out again: the figure is drawn and tappable only while not hidden.</summary>
        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            if (_figure != null) _figure.gameObject.SetActive(!hidden);
            if (_tappable != null) _tappable.Enabled = !hidden;
        }

        /// <summary>The hero is talking to her: she stops and keeps still.</summary>
        public void BeginTalk() => _talking = true;

        public void EndTalk() => _talking = false;

        /// <summary>Stays where she is for this long (the end of a gesture, a pause) before she walks on.</summary>
        public void Hold(float seconds) => _holdUntil = Mathf.Max(_holdUntil, Time.time + seconds);

        /// <summary>Turns to look at a point of the world (the hero, a thing she talks about).</summary>
        public void Face(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            _heading = d.normalized;
            if (_figure != null) _figure.FaceDirection(d);
        }

        /// <summary>«Over there»: turns to the point and leans toward it for <see cref="PointSeconds"/>.</summary>
        public void Point(Vector3 target)
        {
            Face(target);
            _poseUntil = Time.time + PointSeconds;
            if (_figure == null) return;
            var cam = _figure.Camera != null ? _figure.Camera.transform : null;
            float side = cam != null ? Vector3.Dot(_heading, cam.right) : _heading.x;
            _figure.SetPose(new BillboardPose { TiltDegrees = side >= 0f ? PointTiltDegrees : -PointTiltDegrees });
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxFrameSeconds);
            if (_poseUntil > 0f && Time.time >= _poseUntil)
            {
                _poseUntil = 0f;
                if (_figure != null) _figure.SetPose(BillboardPose.Stand);
            }
            if (_hidden || _talking || !IsWalking || Time.time < _holdUntil) return;

            float budget = _speed * dt;
            while (budget > 0f && _next < _path.Count)
            {
                var to = _path[_next];
                var delta = to - transform.position;
                float len = delta.magnitude;
                if (len <= budget)
                {
                    transform.position = to;
                    budget -= len;
                    Moved(delta, len);
                    _next++;
                }
                else
                {
                    var step = delta / len * budget;
                    transform.position += step;
                    Moved(delta, budget);
                    budget = 0f;
                }
            }
            if (_next >= _path.Count) Arrive();
        }

        private void Moved(Vector3 delta, float meters)
        {
            delta.y = 0f;
            if (delta.sqrMagnitude > 1e-6f) _heading = delta.normalized;
            if (_figure == null) return;
            _figure.FaceDirection(delta);
            _figure.Advance(meters);
        }

        private void Arrive()
        {
            _path.Clear();
            _next = 0;
            if (_figure != null) _figure.Stop();
            if (_hideOnArrival) SetHidden(true);
            Arrived?.Invoke(this);
        }
    }
}
