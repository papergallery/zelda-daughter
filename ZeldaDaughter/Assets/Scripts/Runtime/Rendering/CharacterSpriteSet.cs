using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    public enum Facing { Front, Back, Side }

    /// <summary>The drawn frames of one pose («attack» seen from the side: wind-up, thrust): key = <c>&lt;action&gt;_&lt;view&gt;</c>.</summary>
    [System.Serializable]
    public sealed class PoseFrames
    {
        [SerializeField] private string _key;
        [SerializeField] private Sprite[] _frames = new Sprite[0];
        public string Key => _key;
        public Sprite[] Frames => _frames;
        public PoseFrames(string key, Sprite[] frames) { _key = key; _frames = frames; }
    }

    /// <summary>
    /// The drawn frames of one character (docs/demo/unity-architecture.md §2.5): walking cycles for the three views (the side one faces
    /// right — the sprite is mirrored for left), an optional «lying» frame, and the two numbers that tie the picture to the world.
    /// A set with <see cref="Placeholder"/> and no sprites is drawn as a coloured silhouette with a nose (<see cref="PlaceholderSprites"/>).
    /// </summary>
    public sealed class CharacterSpriteSet : ScriptableObject
    {
        [SerializeField] private Sprite[] _front = new Sprite[0];
        [SerializeField] private Sprite[] _back = new Sprite[0];
        [SerializeField] private Sprite[] _side = new Sprite[0];
        [SerializeField] private Sprite _down;
        [SerializeField] private PoseFrames[] _poses = new PoseFrames[0];
        [SerializeField] private float _pixelsPerMeter = 75f;
        [SerializeField] private float _strideMeters = 0.8f;
        [SerializeField] private bool _placeholder;
        [SerializeField] private Color _color = new Color(0.8f, 0.65f, 0.4f, 1f);

        /// <summary>Pixels of the picture per metre of the figure's height.</summary>
        public float PixelsPerMeter => _pixelsPerMeter;
        /// <summary>Metres walked per full cycle of frames.</summary>
        public float StrideMeters => _strideMeters;
        public bool Placeholder => _placeholder;
        public Color Color => _color;
        public Sprite Down => _down;

        public bool HasSprites => _front.Length > 0 || _back.Length > 0 || _side.Length > 0;

        /// <summary>
        /// The frames of an action (D-09 poses: attack, pickup, hurt, eat, point, strike…) for the way the figure faces. A pose drawn for one
        /// view serves the others: the exact view first, then the side one (mirrored by the card), then the front one. Null when the set has none.
        /// </summary>
        public Sprite[] Pose(string action, Facing facing, out Facing view)
        {
            view = facing;
            if (string.IsNullOrEmpty(action) || _poses.Length == 0) return null;
            // the exact view, then the side one, then the front one (no array: this runs every frame while an action shows)
            for (int i = 0; i < 3; i++)
            {
                var f = i == 0 ? facing : i == 1 ? Facing.Side : Facing.Front;
                for (int k = 0; k < _poses.Length; k++)
                {
                    var p = _poses[k];
                    if (p.Frames.Length > 0 && KeyIs(p.Key, action, f)) { view = f; return p.Frames; }
                }
            }
            return null;
        }

        /// <summary>The key «action_view» without building the string (this runs every frame while an action shows).</summary>
        private static bool KeyIs(string key, string action, Facing f)
        {
            string v = f == Facing.Front ? "front" : f == Facing.Back ? "back" : "side";
            return key != null && key.Length == action.Length + 1 + v.Length && key[action.Length] == '_'
                && string.CompareOrdinal(key, 0, action, 0, action.Length) == 0 && string.CompareOrdinal(key, action.Length + 1, v, 0, v.Length) == 0;
        }

        // D-25: the drawn frames between the walking ones. Keys of «poses» in the registry (docs/demo/sprites/README.md): the view is the one the
        // figure faces (Facing: Front, Back, Side — in this order), the strings are made once so that nothing is built in a frame.
        private static readonly string[] StartKeys = { "start_front", "start_back", "start_side" };
        private static readonly string[] StopKeys = { "stop_front", "stop_back", "stop_side" };
        private static readonly string[] IdleKeys = { "idle_front", "idle_back", "idle_side" };
        private static readonly string[,] TurnKeys = MakeTurnKeys();
        private static string[,] MakeTurnKeys()
        {
            string[] n = { "front", "back", "side" };
            var k = new string[3, 3];
            for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) k[a, b] = "turn_" + n[a] + "_" + n[b];
            return k;
        }

        private Sprite[] Find(string key)
        {
            for (int i = 0; i < _poses.Length; i++)
            {
                var p = _poses[i];
                if (p.Frames.Length > 0 && string.Equals(p.Key, key)) return p.Frames;
            }
            return null;
        }

        /// <summary>Frames of the first steps (<c>start_&lt;view&gt;</c>), played before the walking cycle. Null when the set has none.</summary>
        public Sprite[] StartFrames(Facing view) => _poses.Length == 0 ? null : Find(StartKeys[(int)view]);
        /// <summary>Frames of the stop (<c>stop_&lt;view&gt;</c>). Null when none.</summary>
        public Sprite[] StopFrames(Facing view) => _poses.Length == 0 ? null : Find(StopKeys[(int)view]);
        /// <summary>Frames of standing (<c>idle_&lt;view&gt;</c>, a drawn breath), played in a loop. Null when none.</summary>
        public Sprite[] IdleFrames(Facing view) => _poses.Length == 0 ? null : Find(IdleKeys[(int)view]);

        /// <summary>
        /// The frames of a turn from one view to another (<c>turn_&lt;from&gt;_&lt;to&gt;</c>; <c>turn_side_side</c> is the turn from the right side to the
        /// left one). If only the opposite turn is drawn it is played backwards (<paramref name="reversed"/>). Null when there is none.
        /// </summary>
        public Sprite[] TurnFrames(Facing from, Facing to, out bool reversed)
        {
            reversed = false;
            if (_poses.Length == 0) return null;
            var f = Find(TurnKeys[(int)from, (int)to]);
            if (f != null) return f;
            f = Find(TurnKeys[(int)to, (int)from]);
            reversed = f != null;
            return f;
        }

        public bool HasPose(string action) => Pose(action, Facing.Front, out _) != null;

        /// <summary>
        /// The walking frame for a path walked, by the length of the cycle in the registry (D-25): a list of any length N ≥ 2. A short set (the
        /// first sprites of D-09: frame 0 stands, 1 and 2 are the steps) walks 1 → 0 → 2 → 0 (2 frames: 1 → 0), so the legs pass each other
        /// instead of scissoring; a long one (4 and more) has the standing frame first and walks 1 … N-1 (frame 0 is only for standing).
        /// One cycle lasts <see cref="StrideMeters"/> (two steps); a single picture is always 0.
        /// </summary>
        public static int StepFrame(int count, float strideMeters, float path)
        {
            if (count <= 1 || strideMeters <= 0f || path <= 0f) return 0;
            float u = path / strideMeters;
            u -= Mathf.Floor(u);
            if (count == 2) return u < 0.5f ? 1 : 0;
            if (count == 3) { int q = Mathf.Min(3, (int)(u * 4f)); return q == 0 ? 1 : q == 2 ? 2 : 0; }
            return 1 + Mathf.Min(count - 2, (int)(u * (count - 1)));
        }

        public Sprite[] Frames(Facing facing)
        {
            switch (facing)
            {
                case Facing.Back: return _back.Length > 0 ? _back : _front;
                case Facing.Side: return _side.Length > 0 ? _side : _front;
                default: return _front;
            }
        }

        public void Configure(Sprite[] front, Sprite[] back, Sprite[] side, Sprite down, float pixelsPerMeter, float strideMeters, bool placeholder, Color color, PoseFrames[] poses = null)
        {
            _poses = poses ?? new PoseFrames[0];
            _front = front ?? new Sprite[0];
            _back = back ?? new Sprite[0];
            _side = side ?? new Sprite[0];
            _down = down;
            _pixelsPerMeter = pixelsPerMeter;
            _strideMeters = strideMeters;
            _placeholder = placeholder;
            _color = color;
        }
    }
}
