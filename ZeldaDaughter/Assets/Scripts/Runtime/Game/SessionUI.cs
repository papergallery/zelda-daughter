using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// Placeholder look for T-10 (plain UGUI text and buttons, built in code): hint line, the hero's speech bubble, an NPC
    /// bubble, reply icons. Its look is Р2's job after GК; the session only needs somewhere to show what the core decided.
    /// </summary>
    public sealed class SessionUI : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        private Text _hint;
        private Text _heroBubble;
        private Text _npcBubble;
        private RectTransform _replies;
        private Transform _hero;
        private Transform _npc;
        private float _heroUntil;
        private float _npcUntil;
        private readonly List<Button> _buttons = new List<Button>();

        public string CurrentHint => _hint != null && _hint.enabled ? _hint.text : null;
        public string HeroBubbleText => _heroBubble != null && _heroBubble.enabled ? _heroBubble.text : null;
        public string NpcBubbleText => _npcBubble != null && _npcBubble.enabled ? _npcBubble.text : null;
        public IReadOnlyList<Button> ReplyButtons => _buttons;

        public void Configure(Camera cam) => _camera = cam;

        private void Awake()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            _hint = MakeText("Hint", font, 44, new Vector2(0.5f, 0.06f));
            _heroBubble = MakeText("HeroBubble", font, 40, new Vector2(0.5f, 0.6f));
            _npcBubble = MakeText("NpcBubble", font, 40, new Vector2(0.5f, 0.7f));
            _hint.enabled = _heroBubble.enabled = _npcBubble.enabled = false;

            var row = new GameObject("Replies", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _replies = (RectTransform)row.transform;
            _replies.SetParent(transform, false);
            _replies.anchorMin = _replies.anchorMax = new Vector2(0.5f, 0.16f);
            _replies.sizeDelta = new Vector2(1000, 140);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
        }

        private Text MakeText(string name, Font font, int size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(1000, 200);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        public void ShowHint(string text)
        {
            _hint.enabled = !string.IsNullOrEmpty(text);
            if (_hint.enabled) _hint.text = text;
        }

        public void HeroSay(Transform hero, string text, float seconds)
        {
            _hero = hero;
            _heroBubble.text = text;
            _heroBubble.enabled = true;
            _heroUntil = Time.time + seconds;
        }

        public void NpcSay(Transform npc, string text, float seconds)
        {
            _npc = npc;
            _npcBubble.text = text;
            _npcBubble.enabled = true;
            _npcUntil = Time.time + seconds;
        }

        public void ShowReplies(IReadOnlyList<string> icons, Action<string> onPick)
        {
            HideReplies();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var icon in icons)
            {
                var go = new GameObject("Reply_" + icon, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_replies, false);
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth = 220;
                le.preferredHeight = 120;
                var label = MakeText("Label", font, 36, new Vector2(0.5f, 0.5f));
                label.transform.SetParent(go.transform, false);
                ((RectTransform)label.transform).sizeDelta = new Vector2(220, 120);
                label.text = "[" + icon + "]";
                label.enabled = true;
                string captured = icon;
                var button = go.GetComponent<Button>();
                button.onClick.AddListener(() => onPick(captured));
                _buttons.Add(button);
            }
        }

        public void HideReplies()
        {
            foreach (var b in _buttons) if (b != null) Destroy(b.gameObject);
            _buttons.Clear();
        }

        private void LateUpdate()
        {
            if (_heroBubble.enabled && Time.time > _heroUntil) _heroBubble.enabled = false;
            if (_npcBubble.enabled && Time.time > _npcUntil) _npcBubble.enabled = false;
            Follow(_heroBubble, _hero, 2.6f);
            Follow(_npcBubble, _npc, 2.6f);
        }

        private void Follow(Text text, Transform target, float height)
        {
            if (!text.enabled || target == null || _camera == null) return;
            var p = _camera.WorldToScreenPoint(target.position + Vector3.up * height);
            text.rectTransform.position = p;
        }
    }
}
