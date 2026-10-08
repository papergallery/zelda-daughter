using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>A pose made by code on top of the drawn frame: lean, crouch, tremble, lying down (HeroView / EnemyView set it).</summary>
    public struct BillboardPose
    {
        /// <summary>Lean in degrees, + to the right of the screen.</summary>
        public float TiltDegrees;
        /// <summary>0 standing … 1 crouched (the figure is squashed down to 70 %).</summary>
        public float Crouch;
        /// <summary>Tremble amplitude in metres (a hit, a shiver).</summary>
        public float Shake;
        /// <summary>Lies on the ground (the «down» frame if the set has one, else the figure turned on its side).</summary>
        public bool Lying;
        /// <summary>The whole figure is raised off the ground, metres (the bounce of a step, a hop of a blow).</summary>
        public float Lift;
        /// <summary>
        /// A drawn pose of the set (D-09: «attack», «pickup», «hurt», «eat», «point», «strike»). Shown instead of the walking frame when the
        /// set has it; then the lean, crouch and lift (made by code for a silhouette with no such picture) are not applied — the drawing says it.
        /// </summary>
        public string Action;
        /// <summary>0 … 1 along the pose's frames (the first frame at 0, the last at 1).</summary>
        public float ActionPhase;

        public static BillboardPose Stand => default;
    }

    /// <summary>Which kind of drawn frame is on the card (D-25).</summary>
    public enum ShownKind { Walk, Stand, Idle, Start, Stop, Turn, Pose }

    /// <summary>
    /// One drawn figure standing in the 3D world (hero, NPC, beast): a card facing the camera, one of three views by the way the figure
    /// faces (the side view is mirrored for the other side), the frame by the path walked, a pose by code, a tint, a blob of shadow
    /// (docs/demo/unity-architecture.md §1). W0 gives the API and the silhouette stand-in; D-11 fills the look.
    /// The figure is the child «Card» of this object: the object itself is placed by whoever moves it.
    /// </summary>
    public sealed class BillboardSprite : MonoBehaviour
    {
        private static Mesh _quad;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CharacterRegistry _registry;
        [SerializeField] private SpriteLook _look;
        [SerializeField] private Camera _camera;
        [SerializeField] private string _characterId;

        private CharacterSpriteSet _set;
        private Transform _card;
        private MeshRenderer _cardRenderer;
        private Transform _shadow;
        private MeshRenderer _shadowRenderer;
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _shadowBlock;
        private Color _tint = Color.white;
        private BillboardPose _pose;
        private Facing _facing = Facing.Front;
        private bool _mirrored;
        private bool _sideLeft;       // the last sideways move was to the left of the screen (side poses are drawn facing right)
        private bool _flip;           // the card is drawn flipped now
        private bool _drawnPose;      // the frame shown is a drawn pose (action / lying): no lean or squash on top
        private float _path;
        private bool _moving;
        private Sprite _shown;
        private bool _dirty = true;

        // D-25: drawn frames between the walking ones (turn, first steps, stop, idle): all from the registry, none made by code.
        private Facing _lastFacing = Facing.Front;
        private bool _lastMirrored, _haveLast;
        private Sprite[] _turnFrames;
        private float _turnT;
        private bool _turnReversed, _turnFlip;
        private float _startT = -1f, _stopT = -1f;
        private Facing _stopView;
        private bool _stopFlip;
        private float _idlePhase;
        private bool _idleSeeded;

        public string CharacterId => _characterId;
        public Facing Facing => _facing;
        /// <summary>The side view is drawn flipped (the figure looks to the left of the screen).</summary>
        public bool Mirrored => _mirrored;
        public Sprite CurrentSprite => _shown;
        /// <summary>Index of the walking frame shown; 0 while standing.</summary>
        public int FrameIndex { get; private set; }
        /// <summary>What the card shows now (for tests and the frame sheets).</summary>
        public ShownKind Shown { get; private set; }
        public BillboardPose Pose => _pose;
        public Color Tint => _tint;
        /// <summary>The card, for tests and effects (its rotation faces the camera, its scale is the figure's size).</summary>
        public Transform Card { get { EnsureBuilt(); return _card; } }
        public Camera Camera => _camera;
        /// <summary>The look asset in use (the durations of the drawn frames between the walking ones).</summary>
        public SpriteLook Look => LookOrDefault();
        /// <summary>The set has a drawn pose of this action (for any view).</summary>
        public bool HasPose(string action) => Set().HasPose(action);
        /// <summary>A drawn pose is on the card now (an action frame or the lying one).</summary>
        public bool ShowsDrawnPose => _drawnPose;
        /// <summary>Metres walked per full cycle of frames (of the current set).</summary>
        public float StrideMeters => Set().StrideMeters;
        /// <summary>Number of frames in the walking cycle of the facing now (1 when the set is a single picture).</summary>
        public int FrameCount => Mathf.Max(1, Set().Frames(_facing).Length);

        public void Configure(CharacterRegistry registry, SpriteLook look, Camera cam, string characterId)
        {
            _registry = registry;
            _look = look;
            _camera = cam;
            _characterId = characterId;
            _set = null;
            _dirty = true;
        }

        public void SetCharacter(string id)
        {
            _characterId = id;
            _set = null;
            _dirty = true;
        }

        /// <summary>Turn the figure toward a world direction (ground plane; y is ignored). A near-zero direction changes nothing.</summary>
        public void FaceDirection(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 1e-6f) return;
            var cam = _camera != null ? _camera.transform : null;
            Vector3 right = cam != null ? cam.right : Vector3.right;
            Vector3 forward = cam != null ? cam.forward : Vector3.forward;
            right.y = 0f; forward.y = 0f;
            right.Normalize(); forward.Normalize();
            float x = Vector3.Dot(worldDirection, right), y = Vector3.Dot(worldDirection, forward);
            // Hysteresis (SpriteLook.FacingHysteresis): the side view is left only when the other axis wins clearly, and entered only when
            // the sideways axis wins clearly — so a walk along a diagonal does not flicker between the views.
            float h = Mathf.Max(1f, LookOrDefault().FacingHysteresis);
            if (Mathf.Abs(x) > 1e-4f) _sideLeft = x < 0f;
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            Facing facing;
            bool mirrored = _mirrored;
            bool side = _facing == Facing.Side ? ax * h >= ay : ax > ay * h;
            if (side) { facing = Facing.Side; mirrored = x < 0f; }
            else facing = y > 0f ? Facing.Back : Facing.Front; // away from the camera shows the back
            if (facing != _facing || mirrored != _mirrored) _dirty = true;
            _facing = facing;
            _mirrored = mirrored;
        }

        /// <summary>The figure walked <paramref name="meters"/>: the frame follows the path (stride from the set), not the clock.</summary>
        public void Advance(float meters)
        {
            if (meters <= 0f) return;
            if (!_moving)
            {
                _stopT = -1f;
                _startT = Set().StartFrames(_facing) != null ? 0f : -1f;
            }
            _path += meters;
            _moving = true;
            _dirty = true;
        }

        /// <summary>Stands: the first frame.</summary>
        public void Stop()
        {
            if (!_moving && _path == 0f) return;
            if (_moving && Set().StopFrames(_facing) != null) { _stopT = 0f; _stopView = _facing; _stopFlip = _facing == Facing.Side && _mirrored; }
            _startT = -1f;
            _moving = false;
            _path = 0f;
            _dirty = true;
        }

        public void SetPose(BillboardPose pose) { _pose = pose; _dirty = true; }

        public void SetTint(Color tint)
        {
            if (tint == _tint) return;
            _tint = tint;
            _dirty = true;
        }

        private void EnsureBuilt()
        {
            if (_card != null) return;
            if (_quad == null) _quad = MakeQuad();
            var card = new GameObject("Card", typeof(MeshFilter), typeof(MeshRenderer));
            card.transform.SetParent(transform, false);
            card.GetComponent<MeshFilter>().sharedMesh = _quad;
            _card = card.transform;
            _cardRenderer = card.GetComponent<MeshRenderer>();
            _cardRenderer.sharedMaterial = LookOrDefault().SpriteMaterial;
            _cardRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cardRenderer.receiveShadows = false;
            _block = new MaterialPropertyBlock();

            var shadow = new GameObject("Shadow", typeof(MeshFilter), typeof(MeshRenderer));
            shadow.transform.SetParent(transform, false);
            shadow.GetComponent<MeshFilter>().sharedMesh = _quad;
            _shadow = shadow.transform;
            _shadowRenderer = shadow.GetComponent<MeshRenderer>();
            _shadowRenderer.sharedMaterial = LookOrDefault().ShadowMaterial;
            _shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shadowRenderer.receiveShadows = false;
            _shadowBlock = new MaterialPropertyBlock();
        }

        private SpriteLook _fallbackLook;
        private SpriteLook LookOrDefault()
        {
            if (_look != null) return _look;
            if (_fallbackLook == null)
            {
                _fallbackLook = ScriptableObject.CreateInstance<SpriteLook>();
                _fallbackLook.hideFlags = HideFlags.HideAndDontSave;
            }
            return _fallbackLook;
        }

        private CharacterSpriteSet Set()
        {
            if (_set != null) return _set;
            var set = _registry != null ? _registry.Get(_characterId) : null;
            if (set == null) set = PlaceholderSprites.SetFor(_characterId, Color.gray);
            if (!set.HasSprites) set = PlaceholderSprites.SetFor(_characterId, set.Color, set.PixelsPerMeter, set.StrideMeters);
            _set = set;
            return _set;
        }

        private void Awake() => EnsureBuilt();

        /// <summary>
        /// D-26 hit-stop: while true the card keeps the picture, the pose and the place it has (it is a child of this object, whose own movement is held
        /// by the owner); nothing here advances. Not <c>Time.timeScale</c> — the rest of the world goes on.
        /// </summary>
        public bool Frozen { get; set; }

        private void LateUpdate()
        {
            if (Frozen) return;
            EnsureBuilt();
            var set = Set();
            if (_camera == null) _camera = Camera.main;

            var frames = set.Frames(_facing);
            int count = Mathf.Max(1, frames.Length);
            int frame = _moving ? CharacterSpriteSet.StepFrame(count, set.StrideMeters, _path) : 0; // the length of the cycle is the registry's (D-25)
            float dt = Time.deltaTime;
            var look = LookOrDefault();
            Sprite sprite;
            _drawnPose = false;
            _flip = _facing == Facing.Side && _mirrored; // the front and back views are drawn as they are
            Sprite[] acted;
            Facing view;
            if (_pose.Lying && set.Down != null)
            {
                sprite = set.Down;                       // lies on the ground, drawn facing right
                _drawnPose = true;
                DropBetween();
                _flip = _sideLeft;
            }
            else if (!_pose.Lying && !string.IsNullOrEmpty(_pose.Action) && (acted = set.Pose(_pose.Action, _facing, out view)) != null)
            {
                int n = acted.Length;
                sprite = acted[Mathf.Clamp((int)(Mathf.Clamp01(_pose.ActionPhase) * n), 0, n - 1)];
                _drawnPose = true;
                DropBetween();
                _flip = view == Facing.Side && _sideLeft;
            }
            else sprite = FrameBetween(set, frames, frame, dt, look);
            if (sprite != _shown) { _shown = sprite; _dirty = true; }
            FrameIndex = frame;

            ApplyPose(set, sprite);
            if (_dirty) Paint(sprite);
        }


        /// <summary>
        /// The walking frame, or a drawn one between the walking ones (D-25): the turn from one view to another, the first steps, the stop,
        /// the loop of standing. Whatever the registry has is played; what it has not — the picture simply changes at once, as before.
        /// </summary>
        private Sprite FrameBetween(CharacterSpriteSet set, Sprite[] frames, int frame, float dt, SpriteLook look)
        {
            Shown = _moving ? ShownKind.Walk : ShownKind.Stand;
            bool changed = _haveLast && (_facing != _lastFacing || (_facing == Facing.Side && _mirrored != _lastMirrored));
            if (changed && !_pose.Lying && string.IsNullOrEmpty(_pose.Action)) BeginTurn(set);
            _lastFacing = _facing; _lastMirrored = _mirrored; _haveLast = true;

            if (_turnFrames != null)
            {
                float u = _turnT / Mathf.Max(0.02f, look.TurnSeconds);
                _turnT += dt;
                if (u < 1f)
                {
                    int n = _turnFrames.Length, k = Mathf.Clamp((int)(u * n), 0, n - 1);
                    _flip = _turnFlip;
                    Shown = ShownKind.Turn;
                    return _turnFrames[_turnReversed ? n - 1 - k : k];
                }
                _turnFrames = null;
            }
            if (_moving && _startT >= 0f)
            {
                var start = set.StartFrames(_facing);
                float at = _startT;
                _startT += dt;
                if (start != null && at < look.StartSeconds)
                {
                    Shown = ShownKind.Start;
                    return start[Mathf.Clamp((int)(at / Mathf.Max(0.02f, look.StartSeconds) * start.Length), 0, start.Length - 1)];
                }
                _startT = -1f;
            }
            if (!_moving && _stopT >= 0f)
            {
                var stop = set.StopFrames(_stopView);
                float at = _stopT;
                _stopT += dt;
                if (stop != null && at < look.StopSeconds)
                {
                    _flip = _stopFlip;
                    Shown = ShownKind.Stop;
                    return stop[Mathf.Clamp((int)(at / Mathf.Max(0.02f, look.StopSeconds) * stop.Length), 0, stop.Length - 1)];
                }
                _stopT = -1f;
            }
            if (!_moving)
            {
                var idle = set.IdleFrames(_facing);
                if (idle != null)
                {
                    if (!_idleSeeded) { _idleSeeded = true; _idlePhase = Seed01(); }
                    float per = Mathf.Max(0.05f, look.IdleFrameSeconds);
                    Shown = ShownKind.Idle;
                    return idle[(int)((Time.time / per + _idlePhase * idle.Length) % idle.Length)];
                }
            }
            return frames.Length > 0 ? frames[frame] : null;
        }

        /// <summary>A drawn pose is on the card: no turn, start or stop frames are waiting behind it.</summary>
        private void DropBetween()
        {
            Shown = ShownKind.Pose;
            _turnFrames = null; _startT = -1f; _stopT = -1f;
            _lastFacing = _facing; _lastMirrored = _mirrored; _haveLast = true;
        }

        private void BeginTurn(CharacterSpriteSet set)
        {
            var from = _lastFacing;
            var frames = set.TurnFrames(from, _facing, out bool reversed);
            if (frames == null) { _turnFrames = null; return; }
            _turnFrames = frames;
            _turnT = 0f;
            if (from == Facing.Side && _facing == Facing.Side)
            {
                _turnFlip = false;              // turn_side_side is drawn from the right side to the left one
                _turnReversed = _lastMirrored;  // from the left side to the right one: backwards
            }
            else
            {
                _turnReversed = reversed;
                // the drawings turn towards the right side: the mirror of the side that takes part
                _turnFlip = _facing == Facing.Side ? _mirrored : from == Facing.Side && _lastMirrored;
            }
        }

        /// <summary>A number 0 … 1 of this figure (its id and place), so that two of one kind do not breathe together.</summary>
        private float Seed01()
        {
            uint h = 2166136261u;
            if (_characterId != null) for (int i = 0; i < _characterId.Length; i++) h = (h ^ _characterId[i]) * 16777619u;
            var p = transform.position;
            h = (h ^ (uint)(Mathf.RoundToInt(p.x * 10f) * 73856093)) * 16777619u;
            h = (h ^ (uint)(Mathf.RoundToInt(p.z * 10f) * 19349663)) * 16777619u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFF) / 65536f;
        }

        private void ApplyPose(CharacterSpriteSet set, Sprite sprite)
        {
            if (sprite == null) { _card.gameObject.SetActive(false); return; }
            _card.gameObject.SetActive(true);
            var rect = sprite.rect;
            float ppm = Mathf.Max(1f, set.PixelsPerMeter);
            float crouch = _drawnPose ? 0f : _pose.Crouch, tiltDeg = _drawnPose ? 0f : _pose.TiltDegrees, lift = _drawnPose ? 0f : _pose.Lift;
            float w = rect.width / ppm, h = rect.height / ppm * (1f - 0.3f * Mathf.Clamp01(crouch));
            var pivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);

            var face = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            float shake = _pose.Shake > 0f ? Mathf.Sin(Time.time * 70f) * _pose.Shake : 0f;
            var tilt = Quaternion.Euler(0f, 0f, -tiltDeg - (_pose.Lying && set.Down == null ? 90f : 0f));
            _card.rotation = face * tilt;
            _card.localScale = new Vector3(_flip ? -w : w, h, 1f);
            // the quad's origin is bottom-centre; move it so the sprite's pivot (the feet) sits on this object's origin
            var look = LookOrDefault();
            var offset = _card.rotation * new Vector3((_flip ? -1f : 1f) * (0.5f - pivot.x) * w + shake, -pivot.y * h + (_pose.Lying && set.Down == null ? look.LyingLift : 0f), 0f);
            _card.position = transform.position + offset + Vector3.up * lift;

            float sw = look.ShadowWidthMeters, depth = sw * 0.6f;
            _shadow.rotation = Quaternion.Euler(90f, 0f, 0f); // lies flat; the quad's long edge then runs along +Z from its origin
            _shadow.localScale = new Vector3(sw, depth, 1f);
            _shadow.position = transform.position + new Vector3(0f, look.ShadowLift, -0.5f * depth); // centred under the feet
        }

        private void Paint(Sprite sprite)
        {
            _dirty = false;
            if (sprite == null) return;
            var tex = sprite.texture;
            var r = sprite.rect; // not textureRect: for a Tight-mesh sprite Unity gives the trimmed box there and the picture would be stretched over the card (D-21 frame)
            _cardRenderer.GetPropertyBlock(_block);
            _block.SetTexture(BaseMap, tex);
            _block.SetVector(BaseMapST, new Vector4(r.width / tex.width, r.height / tex.height, r.x / tex.width, r.y / tex.height));
            _block.SetColor(BaseColor, _tint);
            _cardRenderer.SetPropertyBlock(_block);

            _shadowRenderer.GetPropertyBlock(_shadowBlock);
            _shadowBlock.SetTexture(BaseMap, PlaceholderSprites.ShadowTexture);
            _shadowBlock.SetColor(BaseColor, new Color(0f, 0f, 0f, LookOrDefault().ShadowAlpha));
            _shadowRenderer.SetPropertyBlock(_shadowBlock);
        }

        /// <summary>A unit quad, origin at the bottom-centre, facing -Z (towards a camera looking along +Z).</summary>
        private static Mesh MakeQuad()
        {
            var m = new Mesh { name = "ZdBillboardQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
