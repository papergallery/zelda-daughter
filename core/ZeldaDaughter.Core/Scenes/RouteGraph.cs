#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Scenes
{
    /// <summary>
    /// Where residents walk (C7, decision 19: a graph over the roads, no NavMesh). Nodes are the points of the scene's <c>paths</c> and
    /// <c>walkways</c>; a point of one way that lies within the width of another way (a door path ending on a road, a trail meeting a trail)
    /// joins it, and ways that cross join where they cross. Every object tagged <c>anchor</c> steps onto the nearest way within
    /// <see cref="AttachRadius"/>. <see cref="Route"/> is the shortest walk between two anchors as a list of waypoints.
    /// Pure geometry on the ground plane (x, z), no Unity.
    /// </summary>
    public sealed class RouteGraph
    {
        /// <summary>An anchor farther than this (m) from every way is known but cannot be reached.</summary>
        public const float DefaultAttachRadius = 10f;
        const float Same = 0.02f;

        readonly List<Vec2> _nodes = new List<Vec2>();
        readonly List<List<(int To, float Weight)>> _edges = new List<List<(int, float)>>();
        readonly Dictionary<string, int> _anchorNode = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<string> _anchors = new List<string>();
        readonly Dictionary<string, Vec2> _anchorPos = new Dictionary<string, Vec2>(StringComparer.Ordinal);

        sealed class Segment
        {
            public Vec2 A, B;
            public float HalfWidth;
            public int Strip;
            public readonly List<(float T, int Node)> Joins = new List<(float, int)>();
        }

        RouteGraph() { }

        public float AttachRadius { get; private set; } = DefaultAttachRadius;

        /// <summary>Anchor ids (objects tagged <c>anchor</c>), in scene order.</summary>
        public IReadOnlyList<string> Anchors => _anchors;

        public bool Has(string anchorId) => _anchorPos.ContainsKey(anchorId);

        public Vec2 PositionOf(string anchorId) => _anchorPos.TryGetValue(anchorId, out var p) ? p : throw new ArgumentException($"no anchor '{anchorId}' in the scene", nameof(anchorId));

        public static RouteGraph From(SceneConfig config, float attachRadius = DefaultAttachRadius)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var g = new RouteGraph { AttachRadius = attachRadius };
            g.Build(config);
            return g;
        }

        /// <summary>
        /// The shortest walk from one anchor to another: waypoints starting at the first anchor's place and ending at the second's.
        /// Null if either is unknown or they are not joined by ways. The same anchor twice — one point.
        /// </summary>
        public IReadOnlyList<Vec2>? Route(string fromAnchor, string toAnchor)
        {
            if (!_anchorNode.TryGetValue(fromAnchor, out int from) || !_anchorNode.TryGetValue(toAnchor, out int to))
            {
                // an anchor that could not attach is still "known" but unreachable, except to itself
                if (fromAnchor == toAnchor && _anchorPos.TryGetValue(fromAnchor, out var only)) return new[] { only };
                return null;
            }
            if (from == to) return new[] { _anchorPos[fromAnchor] };

            int n = _nodes.Count;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.PositiveInfinity; prev[i] = -1; }
            dist[from] = 0f;
            for (int step = 0; step < n; step++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || float.IsPositiveInfinity(dist[u])) break;
                if (u == to) break;
                done[u] = true;
                foreach (var (v, w) in _edges[u])
                    if (dist[u] + w < dist[v] - 1e-6f) { dist[v] = dist[u] + w; prev[v] = u; }
            }
            if (float.IsPositiveInfinity(dist[to])) return null;
            var path = new List<Vec2>();
            for (int at = to; at >= 0; at = prev[at]) path.Add(_nodes[at]);
            path.Reverse();
            path[0] = _anchorPos[fromAnchor];
            path[path.Count - 1] = _anchorPos[toAnchor];
            return path;
        }

        /// <summary>Length of a walk, metres.</summary>
        public static float LengthOf(IReadOnlyList<Vec2> route)
        {
            float len = 0f;
            for (int i = 1; i < route.Count; i++) len += (route[i] - route[i - 1]).Length;
            return len;
        }

        // --- construction ---

        void Build(SceneConfig config)
        {
            var strips = new List<StripConfig>(config.Paths);
            strips.AddRange(config.Walkways);

            var segments = new List<Segment>();
            var vertexNode = new List<List<int>>();   // per strip: node of each vertex
            for (int s = 0; s < strips.Count; s++)
            {
                var pts = strips[s].Points;
                var nodes = new List<int>();
                for (int i = 0; i < pts.Count; i++) nodes.Add(NodeAt(new Vec2(pts[i].X, pts[i].Z)));
                vertexNode.Add(nodes);
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    var seg = new Segment { A = new Vec2(pts[i].X, pts[i].Z), B = new Vec2(pts[i + 1].X, pts[i + 1].Z), HalfWidth = strips[s].Width / 2f, Strip = s };
                    seg.Joins.Add((0f, nodes[i]));
                    seg.Joins.Add((1f, nodes[i + 1]));
                    segments.Add(seg);
                }
            }

            // a vertex of one way within reach of another way joins it
            for (int s = 0; s < strips.Count; s++)
                for (int i = 0; i < strips[s].Points.Count; i++)
                {
                    var v = new Vec2(strips[s].Points[i].X, strips[s].Points[i].Z);
                    int vn = vertexNode[s][i];
                    foreach (var seg in segments)
                    {
                        if (seg.Strip == s) continue;
                        var (q, t) = Project(seg, v);
                        if ((q - v).Length > seg.HalfWidth + strips[s].Width / 2f + 1e-3f) continue;
                        Join(seg, t, q, vn);
                    }
                }

            // ways that cross join where they cross
            for (int i = 0; i < segments.Count; i++)
                for (int j = i + 1; j < segments.Count; j++)
                {
                    if (segments[i].Strip == segments[j].Strip) continue;
                    if (!Cross(segments[i], segments[j], out var x, out float ti, out float tj)) continue;
                    int xn = NodeAt(x);
                    segments[i].Joins.Add((ti, xn));
                    segments[j].Joins.Add((tj, xn));
                }

            // anchors step onto the nearest way
            foreach (var o in config.Objects)
            {
                if (!o.Tags.Contains("anchor")) continue;
                var p = new Vec2(o.Position.X, o.Position.Z);
                _anchors.Add(o.Id);
                _anchorPos[o.Id] = p;
                Segment? best = null;
                Vec2 bq = p;
                float bt = 0f, bd = float.PositiveInfinity;
                foreach (var seg in segments)
                {
                    var (q, t) = Project(seg, p);
                    float d = (q - p).Length;
                    if (d < bd - 1e-4f) { bd = d; best = seg; bq = q; bt = t; }
                }
                if (best == null || bd > AttachRadius) continue;
                int an = NodeAt(p);
                _anchorNode[o.Id] = an;
                Join(best, bt, bq, an);
            }

            // chain every segment's joins in order along it
            foreach (var seg in segments)
            {
                seg.Joins.Sort((a, b) => a.T.CompareTo(b.T));
                for (int k = 1; k < seg.Joins.Count; k++) Link(seg.Joins[k - 1].Node, seg.Joins[k].Node);
            }
        }

        // the vertex/anchor `at` joins segment `seg` at the projection `q` (param t): the node at q is part of the segment, `at` is linked to it
        void Join(Segment seg, float t, Vec2 q, int at)
        {
            int qn = NodeAt(q);
            seg.Joins.Add((t, qn));
            Link(at, qn);
        }

        int NodeAt(Vec2 p)
        {
            for (int i = 0; i < _nodes.Count; i++) if ((_nodes[i] - p).Length <= Same) return i;
            _nodes.Add(p);
            _edges.Add(new List<(int, float)>());
            return _nodes.Count - 1;
        }

        void Link(int a, int b)
        {
            if (a == b) return;
            float w = (_nodes[a] - _nodes[b]).Length;
            foreach (var (to, _) in _edges[a]) if (to == b) return;
            _edges[a].Add((b, w));
            _edges[b].Add((a, w));
        }

        static (Vec2 Point, float T) Project(Segment s, Vec2 p)
        {
            var v = s.B - s.A;
            float len2 = v.X * v.X + v.Y * v.Y;
            float t = len2 <= 1e-9f ? 0f : Math.Max(0f, Math.Min(1f, ((p.X - s.A.X) * v.X + (p.Y - s.A.Y) * v.Y) / len2));
            return (s.A + v * t, t);
        }

        static bool Cross(Segment a, Segment b, out Vec2 point, out float ta, out float tb)
        {
            point = default; ta = tb = 0f;
            var r = a.B - a.A;
            var s = b.B - b.A;
            float denom = r.X * s.Y - r.Y * s.X;
            if (Math.Abs(denom) < 1e-9f) return false;   // parallel
            var qp = b.A - a.A;
            ta = (qp.X * s.Y - qp.Y * s.X) / denom;
            tb = (qp.X * r.Y - qp.Y * r.X) / denom;
            if (ta < 0f || ta > 1f || tb < 0f || tb > 1f) return false;
            point = a.A + r * ta;
            return true;
        }
    }
}
