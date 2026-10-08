using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Feel;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// D-26, the threat from the side (docs/demo/best-practices-feel.md п. 8; Don't Starve: the dark itself is the danger and fire is the safety): at night an
    /// awake wolf that stands at the edge of the light — or, with no fire, in the dark near her — shows two glowing points where its eyes are. The camera sees
    /// only ±3.5 m to the sides, a wolf is quicker than that: the eyes are seen before the body is. The rule (<see cref="EyesSettings.IsVisible"/>, the numbers in
    /// data/combat-feel.json) is in the core; here the light sources are collected (lit campfires, the burning torch) and the wolves' <see cref="WolfEyes"/> told.
    /// </summary>
    public sealed class NightEyes : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private CombatPresenter _combat;

        private GameState _g;

        public void Configure(GameSession session, CombatPresenter combat)
        {
            _session = session;
            _combat = combat;
        }

        private void OnEnable() => _session.Events.StateReady += OnReady;
        private void OnDisable() { if (_session != null) _session.Events.StateReady -= OnReady; }
        private void OnReady(GameState g) => _g = g;

        private void Update()
        {
            if (_g == null || _combat == null) return;
            var s = _g.Data.Feel.Eyes;
            bool night = _g.Data.Session.IsNight(_g.Clock.Daylight);
            var views = _combat.EnemyViews;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (v == null || v.Enemy == null || v.Enemy.DefId != "wolf") continue;
                var eyes = v.GetComponent<WolfEyes>();
                if (eyes == null) eyes = v.gameObject.AddComponent<WolfEyes>();
                eyes.Configure(v, s);
                eyes.Wanted = night && Shown(v.Enemy, s);
            }
        }

        private bool Shown(Enemy e, EyesSettings s)
        {
            var st = e.State;
            bool awake = st == EnemyState.Alert || st == EnemyState.Chase || st == EnemyState.Windup || st == EnemyState.Recover || st == EnemyState.Fleeing || st == EnemyState.Wander || st == EnemyState.Idle;
            float d, r;
            NearestLight(e.Position, out d, out r);
            return s.IsVisible(d, r, awake);
        }

        /// <summary>The nearest light that reaches the wolf's neighbourhood: a lit campfire (camp.json lightRadius × its light), the hero's torch; with none — the hero herself, radius 0.</summary>
        private void NearestLight(Vec2 at, out float distance, out float radius)
        {
            distance = (at - _g.HeroPosition).Length;
            radius = 0f;
            var fear = _g.Data.Enemies.Fire;
            if (_g.Torch.IsLit)
            {
                radius = fear.TorchRadius * _g.Torch.Light;
            }
            var fires = _g.Camp.Campfires;
            for (int i = 0; i < fires.Count; i++)
            {
                var f = fires[i];
                if (!f.IsLit) continue;
                float d = (at - f.Position).Length;
                if (d < distance) { distance = d; radius = _g.Data.Camp.LightRadius * f.Light; }   // the nearest light of all
            }
        }
    }

    /// <summary>The two points of a wolf's eyes: soft yellow, turning on and off over 0.4 s, a blink now and then. Faces the camera.</summary>
    public sealed class WolfEyes : MonoBehaviour
    {
        private static Mesh _quad;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Material _dotMaterial;
        private static Texture2D _dot;
        private const float FadeSeconds = 0.4f, EyeSize = 0.1f, BlinkLength = 0.16f;

        private EnemyView _view;
        private EyesSettings _s;
        private Renderer[] _eyes;
        private MaterialPropertyBlock _block;
        private float _phase;

        /// <summary>The wolf should show its eyes now (night, at the light's edge, awake).</summary>
        public bool Wanted { get; set; }
        /// <summary>Opacity now, 0…1 (0.4 s to turn on or off).</summary>
        public float Alpha { get; private set; }
        public bool Visible => Alpha > 0.01f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _quad = null; _dotMaterial = null; _dot = null; }

        /// <summary>D-26b: the eyes are light, not a shadow: a lit soft dot (Sprites/Default, always in a build), not the dark shadow material that swallowed the colour on the night frame.</summary>
        private static Material DotMaterial()
        {
            if (_dotMaterial != null) return _dotMaterial;
            _dot = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "ZdEyeDot", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f)) / 8f;
                    _dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1.6f * (1f - d))));
                }
            _dot.Apply();
            _dotMaterial = new Material(Shader.Find("Sprites/Default")) { name = "ZdEyeDot", hideFlags = HideFlags.HideAndDontSave };
            return _dotMaterial;
        }

        public void Configure(EnemyView view, EyesSettings s)
        {
            _view = view;
            _s = s;
        }

        private void Build()
        {
            if (_eyes != null) return;
            if (_quad == null) _quad = MakeQuad();
            var material = DotMaterial();
            _block = new MaterialPropertyBlock();
            _eyes = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Eye" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = _quad;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                _eyes[i] = r;
            }
        }

        private void LateUpdate()
        {
            if (_s == null || _view == null) return;
            Build();
            float dt = Time.deltaTime;
            Alpha = Mathf.MoveTowards(Alpha, Wanted ? 1f : 0f, dt / FadeSeconds);
            bool on = Alpha > 0.005f;
            for (int i = 0; i < _eyes.Length; i++) if (_eyes[i].enabled != on) _eyes[i].enabled = on;
            if (!on) return;

            _phase += dt;
            float blink = _s.BlinkSeconds > 0f && (_phase % _s.BlinkSeconds) < BlinkLength ? 0.1f : 1f;
            var cam = _view.Sprite != null && _view.Sprite.Camera != null ? _view.Sprite.Camera : Camera.main;
            if (cam == null) return;
            var ct = cam.transform;
            float head = Mathf.Max(0.35f, _view.Sprite.Card.localScale.y * 0.62f) + 0.15f;
            var right = ct.right; right.y = 0f; right.Normalize();
            // in front of the card (toward the camera) so the points are not hidden by it; on the side the wolf looks toward the hero
            var centre = transform.position + Vector3.up * head - ct.forward * 0.3f;
            var color = new Color(1f, 0.95f, 0.5f, Alpha * blink);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -0.5f : 0.5f;
                var t = _eyes[i].transform;
                t.position = centre + right * (side * _s.PairGapMeters);
                t.rotation = ct.rotation;
                t.localScale = Vector3.one * EyeSize;
                _eyes[i].GetPropertyBlock(_block);
                _block.SetTexture(MainTex, _dot);
                _block.SetColor(ColorId, color);
                _eyes[i].SetPropertyBlock(_block);
            }
        }

        private static Mesh MakeQuad()
        {
            var m = new Mesh { name = "ZdEyeQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
