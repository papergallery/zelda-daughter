using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// One living enemy of the core drawn as a figure (D-13): follows the core's position smoothly (the core thinks in steps of 0.05 s), and shows
    /// what it is doing by pose, without numbers. A windup is readable for at least <c>minWindup</c> (0.6 s): the figure crouches and leans back
    /// from the hero, trembles harder as the blow nears, and a dark ring under it grows. A blow taken — a flash of red and a shudder; a stun — swaying.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        // How it looks, not balance.
        private const float FollowRate = 25f;          // smoothing of the 0.05 s steps
        private const float HurtSeconds = 0.35f;
        private const float LungeSeconds = 0.25f;
        private const float WindupLeanDegrees = 14f;
        private const float WindupCrouch = 0.65f;
        private const float WindupShadowGrowth = 1.0f; // the ring is 2× wide at the end
        private static Mesh _flatQuad;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BillboardSprite _sprite;
        [SerializeField] private Tappable _tappable;
        [SerializeField] private SpriteLook _look;
        [SerializeField] private float _groundRayHeight = 4f;

        private GameSession _session;
        private Enemy _enemy;
        private string _id;
        private int _groundMask;
        private float _hurt;
        private float _lunge;
        private MeshRenderer _ring;
        private MaterialPropertyBlock _ringBlock;
        private bool _ringShown;

        public string EnemyId => _id;
        public Enemy Enemy => _enemy;
        public BillboardSprite Sprite => _sprite;
        /// <summary>1 outside a windup, up to 2 at its end: the size of the dark ring under the enemy relative to its resting size.</summary>
        public float RingScale { get; private set; } = 1f;
        public bool RingVisible => _ringShown;
        /// <summary>1 right after a blow taken, falling to 0.</summary>
        public float Hurt => _hurt;

        public void Configure(GameSession session, Enemy enemy, BillboardSprite sprite, Tappable tappable, SpriteLook look)
        {
            _session = session;
            _enemy = enemy;
            _id = enemy.Id;
            _sprite = sprite;
            _tappable = tappable;
            _look = look;
        }

        private void OnEnable()
        {
            _groundMask = LayerMask.GetMask(ProjectLayers.Ground);
            _session.Events.Enemy += OnNotice;
            _session.Events.HeroStruck += OnHeroStruck;
            BuildRing();
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.Enemy -= OnNotice;
            _session.Events.HeroStruck -= OnHeroStruck;
        }

        /// <summary>Leaves the world: no longer tappable, gone.</summary>
        public void Dispose(WorldIndex index)
        {
            if (_tappable != null)
            {
                _tappable.Enabled = false;
                index.UnregisterDynamic(_tappable);
            }
            Destroy(gameObject);
        }

        private void BuildRing()
        {
            if (_ring != null) return;
            if (_flatQuad == null) _flatQuad = MakeFlatQuad();
            var go = new GameObject("WindupRing", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = _flatQuad;
            _ring = go.GetComponent<MeshRenderer>();
            var look = _look != null ? _look : ScriptableObject.CreateInstance<SpriteLook>();
            _ring.sharedMaterial = look.ShadowMaterial;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ringBlock = new MaterialPropertyBlock();
            _ring.enabled = false;
            _ringWidth = look.ShadowWidthMeters;
        }

        private float _ringWidth = 0.9f;

        private void Update()
        {
            if (_enemy == null) return;
            float dt = Time.deltaTime;
            var target = new Vector3(_enemy.Position.X, 0f, _enemy.Position.Y);
            target.y = GroundAt(target);
            var cur = transform.position;
            float k = 1f - Mathf.Exp(-FollowRate * dt);
            var next = (cur - target).sqrMagnitude > 9f ? target : Vector3.Lerp(cur, target, k);
            var step = next - cur;
            float moved = new Vector2(step.x, step.z).magnitude;
            transform.position = next;

            var hero = _session.Hero.transform.position;
            var toHero = hero - next;
            toHero.y = 0f;
            var state = _enemy.State;
            bool engaged = state == EnemyState.Alert || state == EnemyState.Chase || state == EnemyState.Windup || state == EnemyState.Recover || state == EnemyState.Staggered;
            if (engaged) _sprite.FaceDirection(toHero);
            else if (moved > 1e-4f) _sprite.FaceDirection(step);

            if (dt > 0f && moved / dt > 0.3f) _sprite.Advance(moved); else _sprite.Stop();

            _hurt = Mathf.Max(0f, _hurt - dt / HurtSeconds);
            _lunge = Mathf.Max(0f, _lunge - dt / LungeSeconds);
            Pose(toHero, state);
        }

        private void Pose(Vector3 toHero, EnemyState state)
        {
            var pose = BillboardPose.Stand;
            float side = SideOf(toHero);           // +1: the hero is to the right of the enemy on the screen
            float ring = 1f;
            bool showRing = false;
            switch (state)
            {
                case EnemyState.Alert:
                    pose.Crouch = 0.2f;            // head down: it has noticed
                    pose.TiltDegrees = side * 4f;
                    break;
                case EnemyState.Windup:
                {
                    float p = _enemy.WindupProgress;
                    float e = p * p * (3f - 2f * p);
                    pose.Crouch = WindupCrouch * e;
                    pose.TiltDegrees = -side * WindupLeanDegrees * e;     // back, away from the hero
                    pose.Shake = 0.02f + 0.05f * p;                        // never zero in a windup
                    ring = 1f + WindupShadowGrowth * e;
                    showRing = true;
                    break;
                }
                case EnemyState.Staggered:
                    pose.TiltDegrees = Mathf.Sin(_enemy.StateSeconds * 16f) * 9f;   // swaying, stunned
                    pose.Crouch = 0.25f;
                    break;
            }
            if (_lunge > 0f) { pose.TiltDegrees = side * 14f * _lunge; pose.Crouch = 0f; }  // the blow itself: a thrust at the hero
            if (_hurt > 0f) pose.Shake += 0.05f * _hurt;
            _sprite.SetPose(pose);
            _sprite.SetTint(Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), _hurt));
            ShowRing(showRing, ring);
        }

        private void ShowRing(bool show, float scale)
        {
            RingScale = scale;
            if (_ring == null) return;
            _ringShown = show;
            _ring.enabled = show;
            if (!show) return;
            float body = Mathf.Abs(_sprite.Card.localScale.x);                // the figure's own width on the ground
            float w = Mathf.Max(_ringWidth, body * 0.9f) * scale;
            _ring.transform.localScale = new Vector3(w, 1f, w * 0.6f);
            _ring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            float t = Mathf.Clamp01(scale - 1f);
            _ring.GetPropertyBlock(_ringBlock);
            _ringBlock.SetTexture(BaseMap, PlaceholderSprites.ShadowTexture);
            _ringBlock.SetColor(BaseColor, new Color(0.35f * t, 0.04f * t, 0.02f * t, 0.35f + 0.35f * t));
            _ring.SetPropertyBlock(_ringBlock);
        }

        /// <summary>+1 if the hero is to the right of this enemy on the screen, −1 to the left (the camera's right on the ground).</summary>
        private float SideOf(Vector3 toHero)
        {
            var cam = _sprite.Camera != null ? _sprite.Camera.transform : null;
            if (cam == null) return toHero.x >= 0f ? 1f : -1f;
            var right = cam.right;
            right.y = 0f;
            return Vector3.Dot(toHero, right) >= 0f ? 1f : -1f;
        }

        private float GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, _groundRayHeight, p.z), Vector3.down, out var hit, _groundRayHeight * 3f, _groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return p.y;
        }

        private void OnNotice(EnemyNotice n)
        {
            if (n.EnemyId != _id) return;
            var kind = n.Event.Kind;
            if (kind == EnemyEventKind.Struck || kind == EnemyEventKind.Dodged) _lunge = 1f;
        }

        private void OnHeroStruck(string enemyId, StrikeResult r)
        {
            if (enemyId != _id) return;
            if (r.Outcome == StrikeOutcome.Hit) _hurt = 1f;
            else if (r.Outcome == StrikeOutcome.Miss) _hurt = 0.5f; // a miss still scratches
        }

        /// <summary>A unit quad lying on the ground, centred.</summary>
        private static Mesh MakeFlatQuad()
        {
            var m = new Mesh { name = "ZdFlatQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
