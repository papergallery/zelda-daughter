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
        private static Sprite _drawn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _drawn = null;

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
                    return true;
                }
                case "tap":
                {
                    _tapTarget = NearestTappable();
                    if (_tapTarget == null || cam == null) return false;
                    var p = cam.WorldToScreenPoint(_tapTarget.AimPoint);
                    if (p.z <= 0f) return false;
                    float press = 1f - 0.12f * Mathf.Max(0f, Mathf.Sin(_clock * 4f));
                    _hand.position = new Vector3(p.x, p.y, 0f);
                    _hand.localScale = Vector3.one * press;
                    return true;
                }
                case "long_press":
                {
                    var hero = _session.Hero != null ? _session.Hero.transform : null;
                    if (hero == null || cam == null) return false;
                    var p = cam.WorldToScreenPoint(hero.position + Vector3.up * 0.9f);
                    if (p.z <= 0f) return false;
                    float pulse = 1f + 0.1f * Mathf.Sin(_clock * 3f);
                    _hand.position = new Vector3(p.x, p.y, 0f);
                    _hand.localScale = Vector3.one * pulse;
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
