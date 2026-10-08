using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Game;
using ZeldaDaughter.NPC;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// What a talk looks like (D-12, project-design.md §3): a paper bubble above the resident — her words as the hero understands them (runes,
    /// a mix, the text; <c>Comprehension.Render</c> does that in the core) with icons under them; the hero's answers as a row of icon buttons
    /// above her head (<c>Reply_&lt;icon&gt;</c>); a «?» at the resident when she does not understand; the <c>trade</c> icon in the row while
    /// her shop is open. Built in code in the World layer of <see cref="SessionUI"/>; nothing here takes a touch except the buttons; the bubble
    /// is moved up so that it never covers the hero or her answers.
    /// </summary>
    public sealed class TalkBubbleView : MonoBehaviour
    {
        private const float MaxTextWidth = 760f;
        private const float IconSize = 96f, IconGap = 12f;
        private const float ReplySize = 150f, ReplyGap = 18f, TradeGap = 48f, TradeSize = 130f;
        private const float QuestionSize = 110f;
        private const float Margin = 24f;
        private const float AboveHeadPx = 36f;
        private const float QuestionSeconds = 1.6f;
        // The hero's capsule is 2 m tall around her transform; her head is a little above that.
        private const float HeroFeet = -1f, HeroHead = 1.25f;

        [SerializeField] private SessionUI _ui;
        [SerializeField] private IconRegistry _icons;
        [SerializeField] private UiLook _look;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _hero;

        private bool _built;
        private RectTransform _bubble, _iconRow, _replyRow, _question;
        private TextMeshProUGUI _label;
        private Transform _npc;
        private float _npcHead = 1.8f;
        private float _until = float.PositiveInfinity;
        private float _questionUntil;
        private readonly List<Button> _replyButtons = new List<Button>();
        private readonly List<string> _iconIds = new List<string>();
        private Button _trade;
        private Rect _heroRect, _replyRect, _questionRect, _bubbleRect;

        public void Configure(SessionUI ui, IconRegistry icons, UiLook look, Camera cam, Transform hero)
        {
            _ui = ui;
            _icons = icons;
            _look = look;
            _camera = cam;
            _hero = hero;
        }

        // ------------------------------------------------------------------ what the tests read

        /// <summary>The words in the bubble, or null while it is hidden.</summary>
        public string Text { get { return _built && _bubble.gameObject.activeSelf ? _label.text : null; } }
        public bool BubbleVisible => _built && _bubble.gameObject.activeSelf;
        /// <summary>Ids of the icons under the words (empty once the hero understands enough to need none).</summary>
        public IReadOnlyList<string> IconIds => _iconIds;
        public IReadOnlyList<Button> ReplyButtons => _replyButtons;
        public bool QuestionVisible => _built && _question.gameObject.activeSelf;
        public bool TradeVisible => _trade != null;
        public Button TradeButton => _trade;
        /// <summary>Screen rectangles (pixels) of what is drawn now — the bubble must not overlap the hero's.</summary>
        public Rect BubbleRect => _bubbleRect;
        public Rect HeroRect => _heroRect;
        public Rect ReplyRowRect => _replyRect;

        // ------------------------------------------------------------------ the bubble

        /// <summary>
        /// The resident says something. <paramref name="icons"/> go under the words (pass none when the hero understands without them). The bubble
        /// stays until <see cref="Linger"/> or <see cref="Hide"/>: a talk in progress does not time out.
        /// </summary>
        public void ShowNpc(Transform npc, string text, IReadOnlyList<string> icons)
        {
            Build();
            _npc = npc;
            var view = npc != null ? npc.GetComponent<NpcView>() : null;
            _npcHead = view != null ? view.HeadHeight : 1.8f;
            _until = float.PositiveInfinity;

            float pad = _look != null ? _look.Padding : 24f;
            _iconIds.Clear();
            if (icons != null) for (int i = 0; i < icons.Count; i++) _iconIds.Add(icons[i]);

            for (int i = _iconRow.childCount - 1; i >= 0; i--) Destroy(_iconRow.GetChild(i).gameObject);
            float iconsW = _iconIds.Count > 0 ? _iconIds.Count * IconSize + (_iconIds.Count - 1) * IconGap : 0f;
            for (int i = 0; i < _iconIds.Count; i++)
            {
                var rt = UiKit.MakeIcon(_iconRow, "Icon_" + _iconIds[i], _icons, _iconIds[i], _look, IconSize);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-iconsW * 0.5f + IconSize * 0.5f + i * (IconSize + IconGap), 0f);
            }

            _label.text = text;
            var pref = _label.GetPreferredValues(text, MaxTextWidth, 0f);
            float textW = Mathf.Ceil(pref.x) + 2f;
            float contentW = Mathf.Max(textW, iconsW);
            float iconsH = _iconIds.Count > 0 ? IconSize + pad * 0.5f : 0f;
            _label.rectTransform.sizeDelta = new Vector2(contentW, pref.y);
            _label.rectTransform.anchoredPosition = new Vector2(0f, -pad);
            _iconRow.sizeDelta = new Vector2(contentW, IconSize);
            _iconRow.anchoredPosition = new Vector2(0f, pad);
            _iconRow.gameObject.SetActive(_iconIds.Count > 0);
            _bubble.sizeDelta = new Vector2(contentW + 2f * pad, pref.y + iconsH + 2f * pad);
            _bubble.gameObject.SetActive(true);
        }

        /// <summary>The talk is over: the bubble (and the trade icon) stay this long, then go.</summary>
        public void Linger(float seconds) => _until = Time.time + seconds;

        /// <summary>Everything off: bubble, answers, «?», trade.</summary>
        public void Hide()
        {
            if (!_built) return;
            _bubble.gameObject.SetActive(false);
            _question.gameObject.SetActive(false);
            _iconIds.Clear();
            HideReplies();
            _npc = null;
        }

        /// <summary>The resident did not get the hero's icon: a «?» over her head for a moment.</summary>
        public void ShowQuestion(Transform npc)
        {
            Build();
            if (npc != null && npc != _npc)
            {
                _npc = npc;
                var view = npc.GetComponent<NpcView>();
                _npcHead = view != null ? view.HeadHeight : 1.8f;
            }
            _question.gameObject.SetActive(true);
            _questionUntil = Time.time + QuestionSeconds;
        }

        // ------------------------------------------------------------------ the hero's answers

        /// <summary>The hero's answers as icon buttons above her head. <paramref name="onTrade"/> adds the trade icon at the end of the row (null — no shop to show).</summary>
        public void ShowReplies(IReadOnlyList<string> icons, Action<string> onPick, Action onTrade)
        {
            Build();
            HideReplies();
            int n = icons.Count;
            float width = n * ReplySize + Mathf.Max(0, n - 1) * ReplyGap;
            if (onTrade != null) width += (n > 0 ? TradeGap : 0f) + TradeSize;
            _replyRow.sizeDelta = new Vector2(width, ReplySize);
            float x = -width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                string icon = icons[i];
                var b = MakeIconButton("Reply_" + icon, icon, ReplySize, x + ReplySize * 0.5f, () => onPick(icon));
                _replyButtons.Add(b);
                x += ReplySize + ReplyGap;
            }
            if (onTrade != null)
            {
                x += n > 0 ? TradeGap - ReplyGap : 0f;
                _trade = MakeIconButton("Trade", "trade", TradeSize, x + TradeSize * 0.5f, () => onTrade());
            }
            _replyRow.gameObject.SetActive(n > 0 || onTrade != null);
        }

        public void HideReplies()
        {
            if (!_built) return;
            for (int i = _replyRow.childCount - 1; i >= 0; i--) Destroy(_replyRow.GetChild(i).gameObject);
            _replyButtons.Clear();
            _trade = null;
            _replyRow.gameObject.SetActive(false);
        }

        private Button MakeIconButton(string name, string icon, float size, float x, Action click)
        {
            var plate = UiKit.MakePlate(_replyRow, name, _look, null, true);
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(x, 0f);
            UiKit.MakeIcon(rt, "Icon", _icons, icon, _look, size * 0.72f).anchoredPosition = Vector2.zero;
            var b = plate.gameObject.AddComponent<Button>();
            b.targetGraphic = plate;
            b.onClick.AddListener(() => click());
            return b;
        }

        // ------------------------------------------------------------------ build and place

        private void Build()
        {
            if (_built) return;
            _built = true;
            var world = _ui.World;
            var root = UiKit.MakeRect(world, "TalkBubble");
            UiKit.Stretch(root);

            var plate = UiKit.MakePlate(root, "NpcBubble", _look);
            _bubble = plate.rectTransform;
            _bubble.anchorMin = _bubble.anchorMax = Vector2.zero;
            _bubble.pivot = new Vector2(0.5f, 0f);
            _label = UiKit.MakeText(_bubble, "Text", _look, "", _look != null ? _look.TextNormal : 44f);
            var lrt = _label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            _iconRow = UiKit.MakeRect(_bubble, "Icons");
            _iconRow.anchorMin = _iconRow.anchorMax = new Vector2(0.5f, 0f);
            _iconRow.pivot = new Vector2(0.5f, 0f);
            _bubble.gameObject.SetActive(false);

            _replyRow = UiKit.MakeRect(root, "Replies");
            _replyRow.anchorMin = _replyRow.anchorMax = Vector2.zero;
            _replyRow.pivot = new Vector2(0.5f, 0f);
            _replyRow.gameObject.SetActive(false);

            var q = UiKit.MakePlate(root, "NpcQuestion", _look);
            _question = q.rectTransform;
            _question.anchorMin = _question.anchorMax = Vector2.zero;
            _question.pivot = new Vector2(0.5f, 0f);
            _question.sizeDelta = new Vector2(QuestionSize, QuestionSize);
            UiKit.MakeIcon(_question, "Icon", _icons, "question", _look, QuestionSize * 0.72f).anchoredPosition = Vector2.zero;
            _question.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_built) return;
            float now = Time.time;
            if (_question.gameObject.activeSelf && now > _questionUntil) _question.gameObject.SetActive(false);
            if (_bubble.gameObject.activeSelf && now > _until) Hide();
            bool bubble = _bubble.gameObject.activeSelf, replies = _replyRow.gameObject.activeSelf, question = _question.gameObject.activeSelf;
            if (!bubble && !replies && !question) return;
            if (_camera == null) return;
            Place(bubble, replies, question);
        }

        /// <summary>Everything in screen pixels: the hero's box, the answers above her head, the «?» at the resident, the bubble above that and clear of all of them.</summary>
        private void Place(bool bubble, bool replies, bool question)
        {
            float scale = _ui.Canvas.scaleFactor;
            float sw = Screen.width, sh = Screen.height;

            var feet = _camera.WorldToScreenPoint(_hero.position + Vector3.up * HeroFeet);
            var head = _camera.WorldToScreenPoint(_hero.position + Vector3.up * HeroHead);
            float hh = Mathf.Abs(head.y - feet.y);
            _heroRect = new Rect(head.x - hh * 0.3f, Mathf.Min(feet.y, head.y), hh * 0.6f, hh);

            _replyRect = default;
            if (replies)
            {
                var size = _replyRow.sizeDelta * scale;
                var p = ClampBottomCenter(new Vector2(head.x, head.y + AboveHeadPx * scale), size, sw, sh);
                _replyRow.position = p;
                _replyRect = new Rect(p.x - size.x * 0.5f, p.y, size.x, size.y);
            }

            _questionRect = default;
            Vector3 npcHead = default;
            bool haveNpc = _npc != null && _camera != null;
            if (haveNpc) npcHead = _camera.WorldToScreenPoint(_npc.position + Vector3.up * (_npcHead + 0.1f));
            if (question && haveNpc)
            {
                var size = _question.sizeDelta * scale;
                var p = ClampBottomCenter(new Vector2(npcHead.x + size.x * 0.7f, npcHead.y), size, sw, sh);
                _question.position = p;
                _questionRect = new Rect(p.x - size.x * 0.5f, p.y, size.x, size.y);
            }

            _bubbleRect = default;
            if (bubble && haveNpc)
            {
                var size = _bubble.sizeDelta * scale;
                var p = new Vector2(npcHead.x, npcHead.y + AboveHeadPx * scale);
                for (int pass = 0; pass < 4; pass++)
                {
                    p = ClampBottomCenter(p, size, sw, sh);
                    var r = new Rect(p.x - size.x * 0.5f, p.y, size.x, size.y);
                    float lift = float.NegativeInfinity;
                    Push(r, _heroRect, ref lift);
                    if (replies) Push(r, _replyRect, ref lift);
                    if (question) Push(r, _questionRect, ref lift);
                    if (lift == float.NegativeInfinity) break;
                    p.y = lift + 8f * scale;
                }
                _bubble.position = p;
                _bubbleRect = new Rect(p.x - size.x * 0.5f, p.y, size.x, size.y);
            }
        }

        private static void Push(Rect bubble, Rect obstacle, ref float lift)
        {
            if (obstacle.width <= 0f || !bubble.Overlaps(obstacle)) return;
            lift = Mathf.Max(lift, obstacle.yMax);
        }

        private static Vector2 ClampBottomCenter(Vector2 p, Vector2 size, float sw, float sh)
        {
            float m = Margin;
            p.x = size.x >= sw - 2f * m ? sw * 0.5f : Mathf.Clamp(p.x, m + size.x * 0.5f, sw - m - size.x * 0.5f);
            p.y = Mathf.Clamp(p.y, m, Mathf.Max(m, sh - m - size.y));
            return p;
        }
    }
}
