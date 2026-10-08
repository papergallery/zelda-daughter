using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    public enum Facing { Front, Back, Side }

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

        public Sprite[] Frames(Facing facing)
        {
            switch (facing)
            {
                case Facing.Back: return _back.Length > 0 ? _back : _front;
                case Facing.Side: return _side.Length > 0 ? _side : _front;
                default: return _front;
            }
        }

        public void Configure(Sprite[] front, Sprite[] back, Sprite[] side, Sprite down, float pixelsPerMeter, float strideMeters, bool placeholder, Color color)
        {
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
