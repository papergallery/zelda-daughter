using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Core.Npcs;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.NPC
{
    /// <summary>The road between two anchors, found by the core's <c>RouteGraph</c> when the scene was built (the graph is not Unity-serialisable).</summary>
    [Serializable]
    public sealed class NpcRouteData
    {
        [SerializeField] private string _from;
        [SerializeField] private string _to;
        [SerializeField] private Vector3[] _points = new Vector3[0];

        public string From => _from;
        public string To => _to;
        public Vector3[] Points => _points;

        public NpcRouteData() { }

        public NpcRouteData(string from, string to, Vector3[] points)
        {
            _from = from;
            _to = to;
            _points = points;
        }
    }

    /// <summary>
    /// The town lives by the clock (project-design.md §2, docs/demo/unity-architecture.md §1, D-12). The session calls <c>g.Npcs.Sync()</c>; for each
    /// <see cref="NpcChange"/> this puts the resident at her anchor at once (the first sync, after sleep, no road known) or walks her there along the
    /// roads of the scene (<see cref="NpcRouteData"/>, not through houses); sleepers are hidden when they arrive. It also turns a resident to the
    /// hero while they talk and to the thing she points at, and says whether her shop is open (she stands at it, selling).
    /// </summary>
    public sealed class NpcPresenter : MonoBehaviour
    {
        /// <summary>Residents who meet in the tavern stand on a ring of this radius around its anchor, m (otherwise they would stand on one spot).</summary>
        public const float TavernRingMeters = 1.1f;
        /// <summary>Residents who stroll stand off their anchor by this much, each on her own side (two of them at the fountain must not be one figure), m.</summary>
        public const float StrollRingMeters = 0.9f;

        [SerializeField] private GameSession _session;
        [SerializeField] private NpcView[] _views = new NpcView[0];
        [SerializeField] private NpcRouteData[] _routes = new NpcRouteData[0];

        private GameState _g;
        private readonly Dictionary<string, NpcView> _byId = new Dictionary<string, NpcView>(StringComparer.Ordinal);
        private readonly Dictionary<string, NpcRouteData> _routeByKey = new Dictionary<string, NpcRouteData>(StringComparer.Ordinal);
        private readonly List<string> _tavernOrder = new List<string>();
        private readonly List<string> _allOrder = new List<string>();
        private readonly HashSet<string> _warned = new HashSet<string>();

        public void Configure(GameSession session, NpcView[] views, NpcRouteData[] routes)
        {
            _session = session;
            _views = views ?? new NpcView[0];
            _routes = routes ?? new NpcRouteData[0];
        }

        public IReadOnlyList<NpcView> Views => _views;

        private void Awake()
        {
            foreach (var v in _views) if (v != null) _byId[v.Id] = v;
            foreach (var r in _routes) _routeByKey[Key(r.From, r.To)] = r;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.NpcMoved += OnMoved;
            _session.Events.TalkStarted += OnTalkStarted;
            _session.Events.TalkEnded += OnTalkEnded;
            _session.Events.NpcGesture += OnGesture;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.NpcMoved -= OnMoved;
            _session.Events.TalkStarted -= OnTalkStarted;
            _session.Events.TalkEnded -= OnTalkEnded;
            _session.Events.NpcGesture -= OnGesture;
        }

        private static string Key(string from, string to) => from + ">" + to;

        private void OnReady(GameState g)
        {
            _g = g;
            _tavernOrder.Clear();
            foreach (var id in g.Npcs.Ids)
                foreach (var e in g.Data.Npcs.Npcs[id].Schedule)
                    if (e.ParsedActivity == NpcActivity.Tavern) { _tavernOrder.Add(id); break; }
            _tavernOrder.Sort(StringComparer.Ordinal);
            _allOrder.Clear();
            _allOrder.AddRange(g.Npcs.Ids);
            _allOrder.Sort(StringComparer.Ordinal);
        }

        // ------------------------------------------------------------------ what other parts ask

        /// <summary>The resident's view, or null (the scene has no such resident).</summary>
        public NpcView View(string npcId) => npcId != null && _byId.TryGetValue(npcId, out var v) ? v : null;

        /// <summary>The shop of this resident is open: the core says she is selling now (<c>IsShopOpen</c>) and she is there, awake, not on the road.</summary>
        public bool ShopOpen(string npcId)
        {
            var v = View(npcId);
            return _g != null && v != null && !v.Hidden && !v.IsWalking && _g.Data.Npcs.Npcs.ContainsKey(npcId) && _g.Npcs.IsShopOpen(npcId);
        }

        /// <summary>The waypoints of the road between two anchors (as built from the scene's roads), or null if none joins them.</summary>
        public IReadOnlyList<Vector3> RouteBetween(string from, string to) => _routeByKey.TryGetValue(Key(from, to), out var r) ? r.Points : null;

        /// <summary>Where this resident should stand in the slot she has now (the anchor; off it a little when she strolls or sits in the tavern); false if the scene lacks the anchor.</summary>
        public bool TryPlaceOf(string npcId, out Vector3 position)
        {
            position = default;
            if (_g == null) return false;
            var slot = _g.Npcs.Slot(npcId);
            return TryPlace(npcId, slot.Anchor, slot.Activity, out position);
        }

        // ------------------------------------------------------------------ the clock moved

        private bool TryPlace(string npcId, string anchor, NpcActivity activity, out Vector3 position)
        {
            if (!_session.Index.TryGetAnchor(anchor, out position)) return false;
            List<string> order = activity == NpcActivity.Tavern ? _tavernOrder : activity == NpcActivity.Stroll ? _allOrder : null;
            int i = order != null ? order.IndexOf(npcId) : -1;
            if (i >= 0)
            {
                float angle = (i + 0.5f) / order.Count * Mathf.PI * 2f;
                float radius = activity == NpcActivity.Tavern ? TavernRingMeters : StrollRingMeters;
                position += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            return true;
        }

        private void OnMoved(NpcChange c, bool snap)
        {
            var v = View(c.NpcId);
            if (v == null) return;
            bool sleep = c.Activity == NpcActivity.Sleep;
            if (!TryPlace(c.NpcId, c.ToAnchor, c.Activity, out var dest))
            {
                if (_warned.Add(c.ToAnchor)) ZdLog.Warn("Npc", $"no anchor '{c.ToAnchor}' in this scene: {c.NpcId} stays where she is");
                v.Place(v.transform.position, sleep);
                return;
            }

            if (snap || !c.Walks || v.Talking)
            {
                v.Place(dest, sleep);
                ZdLog.Info("Npc", $"{c.NpcId} at {c.ToAnchor} ({c.Activity}){(sleep ? " hidden" : "")}");
                return;
            }

            var road = RouteBetween(c.FromAnchor, c.ToAnchor);
            if (road == null || road.Count < 2)
            {
                if (_warned.Add(Key(c.FromAnchor, c.ToAnchor))) ZdLog.Warn("Npc", $"no road {c.FromAnchor} → {c.ToAnchor}: {c.NpcId} is put there");
                v.Place(dest, sleep);
                return;
            }
            var way = new List<Vector3>(road.Count + 1);
            for (int i = 0; i < road.Count; i++) way.Add(road[i]);
            way[way.Count - 1] = dest; // the last point of the road is the anchor; the tavern ring moves it a little
            if (v.Hidden) v.Place(road[0], false);
            v.Walk(way, _g.Npcs.WalkSpeed(c.NpcId), sleep);
            ZdLog.Info("Npc", $"{c.NpcId} walks {c.FromAnchor} → {c.ToAnchor} ({c.Activity}) {way.Count} points");
        }

        // ------------------------------------------------------------------ talk

        private void OnTalkStarted(string npcId)
        {
            var v = View(npcId);
            if (v == null) return;
            v.BeginTalk();
            v.Face(_session.Hero.transform.position);
        }

        private void OnTalkEnded(string npcId)
        {
            var v = View(npcId);
            if (v == null) return;
            v.EndTalk();
            v.Hold(0.8f);
        }

        private void OnGesture(string npcId, string type, string targetId)
        {
            var v = View(npcId);
            var target = _session.Index.Find(targetId);
            if (v == null || target == null) return;
            if (type == "point") v.Point(target.transform.position);
            else v.Face(target.transform.position);
            v.Hold(NpcView.PointSeconds);
        }
    }
}
