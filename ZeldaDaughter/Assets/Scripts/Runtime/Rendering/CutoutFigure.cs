using UnityEngine;
using ZeldaDaughter.Core.Cutout;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// D-27 pilot (ADR-0010): the figure drawn as a cut-out rig — painted parts on bones — instead of drawn frames, in the views it has a rig
    /// for (the heroine: side). Lives next to <see cref="BillboardSprite"/> and takes over its card only while the card would show walking or
    /// standing in that view; drawn poses (attack, pickup, lying), turns and the other views stay the card's. The motion is
    /// <see cref="CutoutGait"/> in the core (phase from the path walked, IK legs, pelvis on the stance leg, arms against the legs, breathing by
    /// the bones); here it is only drawn: one mesh of all the parts in drawing order (far limbs first, darker in the atlas), one draw call, the
    /// sprite material, the card's tint. The path is measured from this object's own movement, as <c>HeroView</c> does.
    /// </summary>
    [DefaultExecutionOrder(10)] // after BillboardSprite.LateUpdate: it has chosen what the card shows
    public sealed class CutoutFigure : MonoBehaviour
    {
        const float TeleportMeters = 2f;
        const float LayerDepth = 0.002f; // metres toward the camera per layer: the order of the parts does not hang on equal depths

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static GaitSettings _defaultGait;

        [SerializeField] private BillboardSprite _sprite;
        [SerializeField] private TextAsset _rig;
        [SerializeField] private Texture2D _atlas;
        [SerializeField] private Facing _view = Facing.Side;

        private CutoutGait _gait;
        private CutoutRig _def;
        private Transform _root;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;
        private Vector3[] _verts;
        private Vector2[] _uv;
        private Vector3 _last;
        private bool _hasLast;
        private MeshRenderer _card;

        public CutoutGait Gait => _gait;
        /// <summary>The rig is on screen now (the card is hidden).</summary>
        public bool Showing { get; private set; }
        public Transform Root => _root;

        public void Configure(BillboardSprite sprite, TextAsset rig, Texture2D atlas, Facing view = Facing.Side)
        {
            _sprite = sprite;
            _rig = rig;
            _atlas = atlas;
            _view = view;
            _gait = null;
        }

        /// <summary>The run and stand numbers: data/gait.json (the editor copies data/ into Resources/Data).</summary>
        public static GaitSettings DefaultGait()
        {
            if (_defaultGait != null) return _defaultGait;
            var asset = Resources.Load<TextAsset>("Data/gait");
            _defaultGait = asset != null ? GaitSettings.Parse(asset.text) : new GaitSettings();
            return _defaultGait;
        }

        private bool Ready()
        {
            if (_gait != null) return true;
            if (_sprite == null || _rig == null || _atlas == null) return false;
            _def = CutoutRig.Parse(_rig.text);
            _gait = new CutoutGait(_def, DefaultGait());
            Build();
            return true;
        }

        private void Build()
        {
            var go = new GameObject("Cutout", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            _root = go.transform;
            int n = CutoutRig.Layers.Length;
            _verts = new Vector3[n * 4];
            _uv = new Vector2[n * 4];
            var tris = new int[n * 6];
            var normals = new Vector3[n * 4];
            for (int l = 0; l < n; l++)
            {
                var p = _def.Parts[CutoutRig.Layers[l]];
                float u0 = p.X / _def.AtlasWidth, u1 = (p.X + p.W) / _def.AtlasWidth;
                float v1 = 1f - p.Y / _def.AtlasHeight, v0 = 1f - (p.Y + p.H) / _def.AtlasHeight;
                int v = l * 4;
                _uv[v] = new Vector2(u0, v0); _uv[v + 1] = new Vector2(u1, v0); _uv[v + 2] = new Vector2(u0, v1); _uv[v + 3] = new Vector2(u1, v1);
                for (int k = 0; k < 4; k++) normals[v + k] = Vector3.back;
                int t = l * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v + 2; tris[t + 4] = v + 3; tris[t + 5] = v + 1;
            }
            _mesh = new Mesh { name = "ZdCutout_" + _def.Id };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = _uv;
            _mesh.normals = normals;
            _mesh.triangles = tris;
            _mesh.bounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(3f, 3f, 1f));
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = _sprite.Look.SpriteMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _block = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (!Ready()) return;
            if (_sprite.Frozen) return; // D-26 hit-stop: the pose holds
            float dt = Time.deltaTime;
            var p = transform.position;
            if (!_hasLast) { _last = p; _hasLast = true; }
            var d = p - _last;
            d.y = 0f;
            _last = p;
            float dist = d.magnitude;
            if (dist > TeleportMeters) dist = 0f;

            bool show = _sprite.Facing == _view && !_sprite.Pose.Lying && string.IsNullOrEmpty(_sprite.Pose.Action) &&
                        (_sprite.Shown == ShownKind.Walk || _sprite.Shown == ShownKind.Stand || _sprite.Shown == ShownKind.Idle ||
                         _sprite.Shown == ShownKind.Start || _sprite.Shown == ShownKind.Stop);
            _gait.Step(dt, show ? dist : 0f);
            Show(show);
            if (!show) return;

            var cam = _sprite.Camera;
            _root.SetPositionAndRotation(p, cam != null ? cam.transform.rotation : Quaternion.identity);
            _root.localScale = new Vector3(_sprite.Mirrored ? -1f : 1f, 1f, 1f);
            Fill();
            _renderer.GetPropertyBlock(_block);
            _block.SetTexture(BaseMap, _atlas);
            _block.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, 0f));
            _block.SetColor(BaseColor, _sprite.Tint);
            _renderer.SetPropertyBlock(_block);
        }

        private void Show(bool on)
        {
            if (_card == null && _sprite.Card != null) _card = _sprite.Card.GetComponent<MeshRenderer>();
            if (_card != null) _card.enabled = !on;
            _renderer.enabled = on;
            Showing = on;
        }

        /// <summary>The quads of the parts where the solve put them: each about its pivot, turned from how it is drawn.</summary>
        private void Fill()
        {
            float k = 1f / _def.PixelsPerMeter;
            for (int l = 0; l < CutoutRig.Layers.Length; l++)
            {
                var part = _def.Parts[CutoutRig.Layers[l]];
                var at = _gait.Pivot(l);
                float a = _gait.Rotation(l), c = Mathf.Cos(a), s = Mathf.Sin(a), z = -l * LayerDepth;
                int v = l * 4;
                Corner(v, part.X, part.Y + part.H, part, k, c, s, at, z);         // bottom-left
                Corner(v + 1, part.X + part.W, part.Y + part.H, part, k, c, s, at, z);
                Corner(v + 2, part.X, part.Y, part, k, c, s, at, z);              // top-left
                Corner(v + 3, part.X + part.W, part.Y, part, k, c, s, at, z);
            }
            _mesh.vertices = _verts;
        }

        private void Corner(int i, float px, float py, CutoutPart part, float k, float c, float s, ZeldaDaughter.Core.Common.Vec2 at, float z)
        {
            float x = (px - part.PivotX) * k, y = (part.PivotY - py) * k; // atlas y grows down, rig y up
            _verts[i] = new Vector3(at.X + x * c - y * s, at.Y + x * s + y * c, z);
        }

        private void OnDisable()
        {
            if (_card != null) _card.enabled = true;
            if (_renderer != null) _renderer.enabled = false;
            Showing = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
