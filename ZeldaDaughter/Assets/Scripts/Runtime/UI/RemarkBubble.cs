using UnityEngine;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// D-18: the hero's lines (<c>SessionEvents.HeroSaid</c>) in a paper cloud over her head, in the hand-written font. It fades out after
    /// <c>RemarkBubbleSeconds</c> of the data (read at each line); how often she speaks is the core's business (the pauses of Remarks), not this
    /// view's. Takes the hero's bubble from <see cref="SessionUI"/> (<c>ShowHeroBubble = false</c>) and answers its facade <c>HeroBubbleText</c>.
    /// </summary>
    public sealed class RemarkBubble : MonoBehaviour
    {
        private const float FadeSeconds = 0.5f, Margin = 24f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private float _height = 2.3f;

        private BubbleWidget _bubble;
        private CanvasGroup _group;
        private GameState _g;
        private float _until, _seconds;

        public void Configure(GameSession session, SessionUI ui)
        {
            _session = session;
            _ui = ui;
        }

        /// <summary>The line on screen, or null.</summary>
        public string Text => _bubble != null ? _bubble.Text : null;
        public float Alpha => _group != null ? _group.alpha : 0f;
        public RectTransform Root => _bubble?.Root;

        private void Start()
        {
            _bubble = UiKit.MakeBubble(_ui.World, "RemarkBubble", _ui.Look, 900f);
            _group = _bubble.Root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _ui.ShowHeroBubble = false;
            _ui.HeroTextSource = () => Text;
            _session.Events.StateReady += OnReady;
            _session.Events.HeroSaid += OnSaid;
        }

        private void OnDestroy()
        {
            if (_session != null) { _session.Events.StateReady -= OnReady; _session.Events.HeroSaid -= OnSaid; }
            if (_ui != null) { _ui.ShowHeroBubble = true; _ui.HeroTextSource = null; }
        }

        private void OnReady(GameState g) => _g = g;

        private void OnSaid(string topic, string line)
        {
            _seconds = _g != null ? _g.Data.Session.RemarkBubbleSeconds : 3.5f;
            _until = Time.time + _seconds;
            _group.alpha = 1f;
            _bubble.Show(line);
            Follow();
        }

        private void LateUpdate()
        {
            if (_bubble == null || !_bubble.Visible) return;
            float left = _until - Time.time;
            if (left <= 0f) { _bubble.Hide(); _group.alpha = 0f; return; }
            _group.alpha = Mathf.Clamp01(left / FadeSeconds);
            Follow();
        }

        /// <summary>Over the hero's head, kept inside the screen.</summary>
        private void Follow()
        {
            var cam = _ui.Camera;
            var hero = _session.Hero;
            if (cam == null || hero == null) return;
            var p = cam.WorldToScreenPoint(hero.transform.position + Vector3.up * _height);
            if (p.z <= 0f) return;
            float scale = _ui.Canvas.scaleFactor;
            float half = _bubble.Root.sizeDelta.x * 0.5f * scale + Margin * scale;
            float x = Mathf.Clamp(p.x, half, Mathf.Max(half, Screen.width - half));
            float y = Mathf.Min(p.y, Screen.height - _bubble.Root.sizeDelta.y * scale - Margin * scale);
            _bubble.Root.position = new Vector3(x, y, 0f);
        }
    }
}
