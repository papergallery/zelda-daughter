using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// The one root Canvas of the game (docs/demo/unity-architecture.md §2.6): Screen Space Overlay, 1080×2340 reference, with three nested
    /// layers — <see cref="World"/> (bubbles, hints, reply buttons, the finger's ghost), <see cref="Windows"/> (one window at a time), and
    /// <see cref="Overlay"/> (fades). It is built in code. For T-10 and the tests it keeps the old facade (<see cref="CurrentHint"/>,
    /// <see cref="HeroBubbleText"/>, <see cref="NpcBubbleText"/>, <see cref="ReplyButtons"/>); D-12 and D-18 replace the bubbles with their own views.
    /// Text is changed only when it changes: a re-layout of TextMeshPro every frame is what makes a phone hot.
    /// </summary>
    public sealed class SessionUI : MonoBehaviour
    {
        private const float ReplyWidth = 220f, ReplyHeight = 120f, ReplyGap = 24f;

        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _hero;
        [SerializeField] private UiLook _look;
        [SerializeField] private IconRegistry _talkIcons;
        [SerializeField] private TalkBubbleView _talkView; // D-12: when set, it draws the NPC's bubble and the hero's answers

        private Canvas _canvas;
        private RectTransform _world, _windows, _overlay;
        private BubbleWidget _hint, _heroBubble, _npcBubble;
        private RectTransform _replies;
        private Transform _npc;
        private float _heroUntil, _npcUntil;
        private float _heroSeconds = 3.5f;
        private SessionEvents _events;
        private readonly List<Button> _buttons = new List<Button>();
        private bool _built;

        /// <summary>The root canvas.</summary>
        public Canvas Canvas { get { Build(); return _canvas; } }
        public RectTransform World { get { Build(); return _world; } }
        public RectTransform Windows { get { Build(); return _windows; } }
        public RectTransform Overlay { get { Build(); return _overlay; } }
        public UiLook Look => _look;
        public IconRegistry TalkIcons => _talkIcons;
        public Camera Camera => _camera;

        /// <summary>D-18 turns this off when its own remark bubble takes over the hero's lines.</summary>
        public bool ShowHeroBubble { get; set; } = true;

        public string CurrentHint { get { Build(); return _hint.Text; } }
        public string HeroBubbleText { get { Build(); return _heroBubble.Text; } }
        public string NpcBubbleText { get { if (_talkView != null) return _talkView.Text; Build(); return _npcBubble.Text; } }
        public IReadOnlyList<Button> ReplyButtons => _talkView != null ? _talkView.ReplyButtons : _buttons;

        /// <summary>D-12: the talk view takes over the NPC's bubble and the answers; the facade above reads from it.</summary>
        public void UseTalkView(TalkBubbleView view) => _talkView = view;

        public void Configure(Camera cam, Transform hero, UiLook look, IconRegistry talkIcons)
        {
            _camera = cam;
            _hero = hero;
            _look = look;
            _talkIcons = talkIcons;
        }

        /// <summary>The session's bus: the hero's lines come from <c>HeroSaid</c>, the bubble time from the state's data.</summary>
        public void Bind(SessionEvents events)
        {
            if (_events != null) { _events.HeroSaid -= OnHeroSaid; _events.StateReady -= OnReady; }
            _events = events;
            _events.HeroSaid += OnHeroSaid;
            _events.StateReady += OnReady;
        }

        private void OnReady(GameState g) => _heroSeconds = g.Data.Session.RemarkBubbleSeconds;

        private void OnHeroSaid(string topic, string line)
        {
            if (ShowHeroBubble) HeroSay(_hero, line, _heroSeconds);
        }

        private void OnDestroy()
        {
            if (_events != null) { _events.HeroSaid -= OnHeroSaid; _events.StateReady -= OnReady; }
        }

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            _world = Layer("World", 0);
            _windows = Layer("Windows", 10);
            _overlay = Layer("Overlay", 20);

            _hint = UiKit.MakeBubble(_world, "Hint", _look, 960f);
            UiKit.Place(_hint.Root, new Vector2(0.5f, 0.06f), _hint.Root.sizeDelta, new Vector2(0.5f, 0.5f));
            _heroBubble = UiKit.MakeBubble(_world, "HeroBubble", _look, 900f);
            _npcBubble = UiKit.MakeBubble(_world, "NpcBubble", _look, 900f);
            _replies = UiKit.MakeRect(_world, "Replies");
            UiKit.Place(_replies, new Vector2(0.5f, 0.16f), new Vector2(1000f, ReplyHeight), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A nested canvas over the root: its own mesh (a change in one layer does not rebuild the others) and its own raycaster for buttons.</summary>
        private RectTransform Layer(string name, int order)
        {
            var rt = UiKit.MakeRect(transform, name);
            UiKit.Stretch(rt);
            var c = rt.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = order;
            rt.gameObject.AddComponent<GraphicRaycaster>();
            return rt;
        }

        public void ShowHint(string text)
        {
            Build();
            if (string.IsNullOrEmpty(text)) { _hint.Hide(); return; }
            _hint.Show(text);
        }

        public void HeroSay(Transform hero, string text, float seconds)
        {
            Build();
            if (hero != null) _hero = hero;
            _heroBubble.Show(text);
            _heroUntil = Time.time + seconds;
        }

        public void NpcSay(Transform npc, string text, float seconds)
        {
            Build();
            _npc = npc;
            _npcBubble.Show(text);
            _npcUntil = Time.time + seconds;
        }

        public void ShowReplies(IReadOnlyList<string> icons, Action<string> onPick)
        {
            Build();
            HideReplies();
            for (int i = 0; i < icons.Count; i++)
            {
                string icon = icons[i];
                var b = UiKit.MakeButton(_replies, "Reply_" + icon, _look, "[" + icon + "]", new Vector2(ReplyWidth, ReplyHeight), () => onPick(icon));
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - (icons.Count - 1) * 0.5f) * (ReplyWidth + ReplyGap), 0f);
                _buttons.Add(b);
            }
        }

        public void HideReplies()
        {
            foreach (var b in _buttons) if (b != null) Destroy(b.gameObject);
            _buttons.Clear();
        }

        private void LateUpdate()
        {
            if (!_built) return;
            if (_heroBubble.Visible && Time.time > _heroUntil) _heroBubble.Hide();
            if (_npcBubble.Visible && Time.time > _npcUntil) _npcBubble.Hide();
            Follow(_heroBubble, _hero, 2.6f);
            Follow(_npcBubble, _npc, 2.6f);
        }

        private void Follow(BubbleWidget bubble, Transform target, float height)
        {
            if (!bubble.Visible || target == null || _camera == null) return;
            bubble.Root.position = _camera.WorldToScreenPoint(target.position + Vector3.up * height);
        }
    }
}
