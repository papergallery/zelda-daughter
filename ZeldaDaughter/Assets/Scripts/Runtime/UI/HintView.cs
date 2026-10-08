using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// D-18: the hand of the onboarding hints (project-design.md §6; the core's <c>g.Hints</c>, C-14). A translucent hand: for "swipe" it slides
    /// along the lower edge, for "tap" it presses at the nearest thing to touch, for "long_press" it presses and pulses on the hero. It fades out
    /// as soon as the core marks the hint done and never takes a touch (<c>raycastTarget = false</c>, no raycaster). The hint's text stays in
    /// <see cref="SessionUI"/> (its facade <c>CurrentHint</c> belongs to T-10). The picture is the registry's <c>hint_hand</c>, or a hand drawn in code.
    /// </summary>
    public sealed class HintView : MonoBehaviour
    {
        private const float HandWidth = 150f, HandHeight = 200f;   // canvas units
        private const float MaxAlpha = 0.72f, FadeSeconds = 0.25f;
        private const float SwipeAmplitude = 230f, SwipeSpeed = 2.2f, SwipeHeight = 0.2f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private IconRegistry _icons;

        private GameState _g;
        private RectTransform _hand;
        private Image _image;
        private CanvasGroup _group;
        private string _shown;          // the hint the hand is for, null while it fades out / hidden
        private Tappable _tapTarget;
        private float _clock;
        private static Sprite _drawn, _ringSprite, _dotSprite;
        // D-26: what the hand does, without a word: a ring that fills under a held finger / spreads from a poke, and a trail behind a slide
        private Vector3 _handTip;
        private float _trailAlpha;
        private bool _trailSet;
        private int _trailW;
        private Image _ring;
        private Image[] _trail;
        private const int TrailDots = 5;
        private const float PokeSeconds = 1.4f, HoldSeconds = 1.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _drawn = null; _ringSprite = null; _dotSprite = null; }

        public void Configure(GameSession session, SessionUI ui, IconRegistry icons)
        {
            _session = session;
            _ui = ui;
            _icons = icons;
        }

        /// <summary>The hint the hand shows now (swipe, tap, long_press), or null.</summary>
        public string ShownHint => _shown;
        /// <summary>The hand is on screen (also while it fades out).</summary>
        public bool HandActive => _hand != null && _hand.gameObject.activeSelf;
        public float HandAlpha => _group != null ? _group.alpha : 0f;
        /// <summary>Where the fingertip is, in screen pixels.</summary>
        public Vector2 HandScreenPoint => _hand != null ? (Vector2)_hand.position : Vector2.zero;
        /// <summary>The thing the tap hint points at, or null.</summary>
        public Tappable TapTarget => _tapTarget;
        public Image HandImage => _image;

        private void Start()
        {
            Build();
            _session.Events.StateReady += OnReady;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Events.StateReady -= OnReady;
        }

        private void OnReady(GameState g) => _g = g;

        private void Build()
        {
            _hand = UiKit.MakeRect(_ui.World, "HintHand");
            _hand.sizeDelta = new Vector2(HandWidth, HandHeight);
            _hand.pivot = new Vector2(HandSprites.TipX, HandSprites.TipY);   // the fingertip is the anchor point
            _hand.anchorMin = _hand.anchorMax = Vector2.zero;
            _image = _hand.gameObject.AddComponent<Image>();
            _image.sprite = PickSprite();
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            _group = _hand.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _hand.gameObject.SetActive(false);
            BuildExtras();
        }

        private void BuildExtras()
        {
            if (_ringSprite == null) _ringSprite = HandSprites.Ring();
            if (_dotSprite == null) _dotSprite = HandSprites.Dot();
            var ring = UiKit.MakeRect(_ui.World, "HintRing");
            ring.sizeDelta = new Vector2(190f, 190f);
            ring.anchorMin = ring.anchorMax = Vector2.zero;
            ring.pivot = new Vector2(0.5f, 0.5f);
            _ring = ring.gameObject.AddComponent<Image>();
            _ring.sprite = _ringSprite;
            _ring.raycastTarget = false;
            ring.gameObject.SetActive(false);
            _trail = new Image[TrailDots];
            for (int i = 0; i < TrailDots; i++)
            {
                var d = UiKit.MakeRect(_ui.World, "HintTrail" + i);
                d.sizeDelta = new Vector2(46f, 46f);
                d.anchorMin = d.anchorMax = Vector2.zero;
                d.pivot = new Vector2(0.5f, 0.5f);
                var im = d.gameObject.AddComponent<Image>();
                im.sprite = _dotSprite;
                im.raycastTarget = false;
                d.gameObject.SetActive(false);
                _trail[i] = im;
            }
        }

        /// <summary>The ring and the trail follow the hand: shown with it, hidden when it is hidden or when this hint does not use them.</summary>
        private void UpdateExtras(bool on, string id, Vector3 handPos, float alpha)
        {
            bool ring = on && (id == "tap" || id == "long_press");
            if (_ring != null && _ring.gameObject.activeSelf != ring) _ring.gameObject.SetActive(ring);
            if (ring)
            {
                var inkRed = new Color(0.55f, 0.22f, 0.12f);
                if (id == "long_press")
                {
                    // the finger is held: the ring on the hero grows over the long press, holds, then starts again
                    float t = (_clock % HoldSeconds) / HoldSeconds;
                    // (a plain ring that grows to its full size over the long press — a filled image would read as a bar)
                    _ring.rectTransform.position = handPos;
                    _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.35f, Mathf.Clamp01(t / 0.55f));
                    _ring.color = new Color(inkRed.r, inkRed.g, inkRed.b, alpha * (t > 0.85f ? (1f - t) / 0.15f : 1f));
                }
                else
                {
                    // a poke: a ring spreads from the fingertip at the moment it lands, at the thing to touch
                    float t = (_clock % PokeSeconds) / PokeSeconds;
                    float land = Mathf.Clamp01((t - 0.35f) / 0.65f);
                    _ring.rectTransform.position = handPos;
                    _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, land);
                    _ring.color = new Color(inkRed.r, inkRed.g, inkRed.b, t < 0.35f ? 0f : alpha * (1f - land));
                }
            }
            // the swipe's track: dots along the path of the slide, laid once (a per-frame change rebuilds the canvas and allocates)
            bool trail = on && id == "swipe";
            for (int i = 0; i < _trail.Length; i++)
            {
                var d = _trail[i];
                if (d.gameObject.activeSelf != trail) d.gameObject.SetActive(trail);
                if (!trail) continue;
                if (Mathf.Abs(_trailAlpha - alpha) > 0.004f || !_trailSet || _trailW != Screen.width)
                {
                    float u = (i + 0.5f) / _trail.Length * 2f - 1f;                    // -1 … 1 across the slide
                    float x = Screen.width * 0.5f + u * SwipeAmplitude * _ui.Canvas.scaleFactor;
                    d.rectTransform.position = new Vector3(x, Screen.height * SwipeHeight - 70f * _ui.Canvas.scaleFactor, 0f);
                    d.rectTransform.localScale = Vector3.one * 0.6f;
                    d.color = new Color(0.97f, 0.92f, 0.8f, alpha * 0.6f);
                    if (i == _trail.Length - 1) { _trailAlpha = alpha; _trailSet = true; _trailW = Screen.width; }
                }
            }
        }

        private Sprite PickSprite()
        {
            if (_icons != null)
            {
                foreach (var id in _icons.Ids)
                {
                    if (id != "hint_hand") continue;
                    var s = _icons.Get(id);
                    if (s != null) return s;
                }
            }
            if (_drawn == null) _drawn = HandSprites.Hand();
            return _drawn;
        }

        private void LateUpdate()
        {
            if (_g == null || _hand == null) return;
            string id = _g.Hints.Visible;
            if (id != _shown)
            {
                _shown = id;
                _clock = 0f;
                if (id != null) ZdLog.Info("Hint", "show " + id);
                else ZdLog.Info("Hint", "hide");
            }

            float target = 0f;
            if (_shown != null && Place(_shown)) target = MaxAlpha;
            else if (_shown != null) { /* nothing to point at yet */ }

            float step = Time.unscaledDeltaTime / FadeSeconds * MaxAlpha;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, step);
            bool on = _group.alpha > 0.01f || target > 0f;
            if (_hand.gameObject.activeSelf != on) _hand.gameObject.SetActive(on);
            UpdateExtras(on && target > 0f || _group.alpha > 0.01f, _shown, _handTip, _group.alpha);
            _clock += Time.unscaledDeltaTime;
        }

        /// <summary>Puts the hand where the hint wants it; false if there is nothing to point at.</summary>
        private bool Place(string id)
        {
            var cam = _ui.Camera;
            float scale = _ui.Canvas.scaleFactor;
            switch (id)
            {
                case "swipe":
                {
                    float x = Screen.width * 0.5f + Mathf.Sin(_clock * SwipeSpeed) * SwipeAmplitude * scale;
                    _hand.position = new Vector3(x, Screen.height * SwipeHeight, 0f);
                    _hand.localScale = Vector3.one;
                    _handTip = _hand.position;
                    return true;
                }
                case "tap":
                {
                    _tapTarget = NearestTappable();
                    if (_tapTarget == null || cam == null) return false;
                    var p = cam.WorldToScreenPoint(_tapTarget.AimPoint);
                    if (p.z <= 0f) return false;
                    // a poke at the thing itself: the hand comes up to it from below, touches, and goes back (the ring spreads as it lands)
                    float t = (_clock % PokeSeconds) / PokeSeconds;
                    float reach = t < 0.35f ? Mathf.SmoothStep(0f, 1f, t / 0.35f) : t < 0.5f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
                    float below = (1f - reach) * 90f * _ui.Canvas.scaleFactor;
                    _hand.position = new Vector3(p.x, p.y - below, 0f);
                    _hand.localScale = Vector3.one * (1f - 0.1f * Mathf.Clamp01((t - 0.3f) / 0.1f) * (t < 0.5f ? 1f : 0f));
                    _handTip = new Vector3(p.x, p.y, 0f);
                    return true;
                }
                case "long_press":
                {
                    var hero = _session.Hero != null ? _session.Hero.transform : null;
                    if (hero == null || cam == null) return false;
                    var p = cam.WorldToScreenPoint(hero.position + Vector3.up * 0.9f);
                    if (p.z <= 0f) return false;
                    _hand.position = new Vector3(p.x, p.y, 0f);      // held still: a long press does not move
                    _hand.localScale = Vector3.one;
                    _handTip = _hand.position;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The nearest NPC or pick-up in the hint radius — the thing that made "tappable_nearby" true.</summary>
        private Tappable NearestTappable()
        {
            var hero = _session.Hero;
            if (hero == null) return null;
            var pos = hero.transform.position;
            var near = _g.Data.Session;
            Tappable best = null;
            float bestD = float.MaxValue;
            var list = _session.Index.Tappables;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.Enabled || (t.Kind != TapKind.Npc && t.Kind != TapKind.Pickup) || !t.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(t.transform.position, pos);
                if (!near.IsNear(d) || d >= bestD) continue;
                best = t;
                bestD = d;
            }
            return best;
        }
    }

    /// <summary>The hand drawn in code: a translucent silhouette with an ink outline, one finger pointing up (signed distance fields of capsules).</summary>
    internal static class HandSprites
    {
        public const int W = 96, H = 128;
        /// <summary>The fingertip as a fraction of the picture (pivot).</summary>
        public const float TipX = 44f / W, TipY = 118f / H;

        private static float Capsule(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        /// <summary>A thin ring (ink), for the held finger and the poke.</summary>
        public static Sprite Ring()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "hint_ring", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.07f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A soft dot, for the trail of a slide.</summary>
        public static Sprite Dot()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "hint_dot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite Hand()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "hint_hand_drawn", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var fill = new Color(0.97f, 0.92f, 0.80f);
            var ink = new Color(0.20f, 0.14f, 0.09f);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = Capsule(fx, fy, 44, 60, 44, 109, 9f);          // the finger
                    d = Mathf.Min(d, Capsule(fx, fy, 48, 28, 48, 46, 26f));  // the palm
                    d = Mathf.Min(d, Capsule(fx, fy, 24, 46, 14, 66, 8f));   // the thumb
                    d = Mathf.Min(d, Capsule(fx, fy, 62, 64, 62, 66, 9f));   // curled fingers
                    d = Mathf.Min(d, Capsule(fx, fy, 74, 56, 74, 58, 8f));
                    float inside = Mathf.Clamp01(0.5f - d);                  // anti-aliased edge
                    if (inside <= 0f) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    float edge = Mathf.Clamp01((d + 3f) / 1.5f);             // ink in the outer 3 px
                    var c = Color.Lerp(fill, ink, edge);
                    c.a = inside * Mathf.Lerp(0.9f, 1f, edge);
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(TipX, TipY), 100f);
        }
    }
}
