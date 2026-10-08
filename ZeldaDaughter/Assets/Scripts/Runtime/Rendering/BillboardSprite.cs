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

        public static BillboardPose Stand => default;
    }

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
        private float _path;
        private bool _moving;
        private Sprite _shown;
        private bool _dirty = true;

        public string CharacterId => _characterId;
        public Facing Facing => _facing;
        /// <summary>The side view is drawn flipped (the figure looks to the left of the screen).</summary>
        public bool Mirrored => _mirrored;
        public Sprite CurrentSprite => _shown;
        /// <summary>Index of the walking frame shown; 0 while standing.</summary>
        public int FrameIndex { get; private set; }
        public BillboardPose Pose => _pose;
        public Color Tint => _tint;
        /// <summary>The card, for tests and effects (its rotation faces the camera, its scale is the figure's size).</summary>
        public Transform Card { get { EnsureBuilt(); return _card; } }
        public Camera Camera => _camera;

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
            Facing facing;
            bool mirrored = _mirrored;
            if (Mathf.Abs(x) > Mathf.Abs(y)) { facing = Facing.Side; mirrored = x < 0f; }
            else facing = y > 0f ? Facing.Back : Facing.Front; // away from the camera shows the back
            if (facing != _facing || mirrored != _mirrored) _dirty = true;
            _facing = facing;
            _mirrored = mirrored;
        }

        /// <summary>The figure walked <paramref name="meters"/>: the frame follows the path (stride from the set), not the clock.</summary>
        public void Advance(float meters)
        {
            if (meters <= 0f) return;
            _path += meters;
            _moving = true;
            _dirty = true;
        }

        /// <summary>Stands: the first frame.</summary>
        public void Stop()
        {
            if (!_moving && _path == 0f) return;
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

        private void LateUpdate()
        {
            EnsureBuilt();
            var set = Set();
            if (_camera == null) _camera = Camera.main;

            var frames = set.Frames(_facing);
            int count = Mathf.Max(1, frames.Length);
            int frame = _moving && count > 1 && set.StrideMeters > 0f ? (int)(_path / set.StrideMeters * count) % count : 0;
            Sprite sprite = _pose.Lying && set.Down != null ? set.Down : (frames.Length > 0 ? frames[frame] : null);
            if (sprite != _shown) { _shown = sprite; _dirty = true; }
            FrameIndex = frame;

            ApplyPose(set, sprite);
            if (_dirty) Paint(sprite);
        }

        private void ApplyPose(CharacterSpriteSet set, Sprite sprite)
        {
            if (sprite == null) { _card.gameObject.SetActive(false); return; }
            _card.gameObject.SetActive(true);
            var rect = sprite.rect;
            float ppm = Mathf.Max(1f, set.PixelsPerMeter);
            float w = rect.width / ppm, h = rect.height / ppm * (1f - 0.3f * Mathf.Clamp01(_pose.Crouch));
            var pivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);

            var face = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            float shake = _pose.Shake > 0f ? Mathf.Sin(Time.time * 70f) * _pose.Shake : 0f;
            var tilt = Quaternion.Euler(0f, 0f, -_pose.TiltDegrees - (_pose.Lying && set.Down == null ? 90f : 0f));
            _card.rotation = face * tilt;
            _card.localScale = new Vector3(_mirrored ? -w : w, h, 1f);
            // the quad's origin is bottom-centre; move it so the sprite's pivot (the feet) sits on this object's origin
            var offset = _card.rotation * new Vector3((_mirrored ? -1f : 1f) * (0.5f - pivot.x) * w + shake, -pivot.y * h + (_pose.Lying ? 0.12f : 0f), 0f);
            _card.position = transform.position + offset;

            var look = LookOrDefault();
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
            var r = sprite.textureRect;
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
