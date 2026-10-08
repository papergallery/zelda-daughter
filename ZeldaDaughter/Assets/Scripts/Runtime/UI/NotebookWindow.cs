using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// The notebook (project-design.md §5, D-15): a page of the hero's own handwriting — who asked, what, roughly where — the newest on top. No ticks,
    /// no "done", no markers, no reminders: it is a diary (the core's entries carry no status at all). Opens from the radial menu
    /// (<c>RadialChosen("notebook")</c>) and adds a note to the open page when one is written.
    /// </summary>
    public sealed class NotebookWindow : MonoBehaviour, IWindow
    {
        public const string WindowId = "notebook";
        private const float PageWidth = 960f, PageHeight = 1900f, Margin = 56f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WindowStack _windows;

        private GameState _g;
        private RectTransform _root, _content;
        private TextMeshProUGUI _title;
        private readonly List<string> _shown = new List<string>();

        public string Id => WindowId;
        public RectTransform Root { get { Build(); return _root; } }
        public bool IsOpen => _windows != null && _windows.IsOpen(WindowId);

        /// <summary>The ids of the notes on the page, from the top (the newest) down.</summary>
        public IReadOnlyList<string> ShownIds => _shown;

        /// <summary>Every text on the page, joined (tests: no status words, no digits).</summary>
        public string PageText
        {
            get
            {
                if (_content == null) return "";
                var sb = new System.Text.StringBuilder();
                foreach (var t in _content.GetComponentsInChildren<TextMeshProUGUI>(false)) sb.AppendLine(t.text);
                return sb.ToString();
            }
        }

        public void Configure(GameSession session, SessionUI ui, WindowStack windows)
        {
            _session = session;
            _ui = ui;
            _windows = windows;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.RadialChosen += OnRadialChosen;
            _session.Events.NoteAdded += OnNoteAdded;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.RadialChosen -= OnRadialChosen;
            _session.Events.NoteAdded -= OnNoteAdded;
        }

        private void OnReady(GameState g) => _g = g;
        private void OnRadialChosen(string id) { if (id == WindowId) Open(); }
        private void OnNoteAdded(NotebookEntry e) { if (IsOpen) Refresh(); }

        public bool Open()
        {
            if (_g == null) return false;
            _windows.Open(this);
            return true;
        }

        public void OnOpened()
        {
            ZdLog.Info("Notebook", $"open notes={_g.Notebook.Entries.Count}");
            Refresh();
        }

        public void OnClosed() { }

        private void Build()
        {
            if (_root != null) return;
            var look = _ui.Look;
            _root = ScreenParts.Frame(_ui.Windows, "NotebookWindow", look, new Vector2(PageWidth, PageHeight), "Блокнот", out _title);
            _root.gameObject.SetActive(false);
            _content = ScreenParts.Scroll(_root, "Notes", Margin, 150f, PageWidth - 2f * Margin, PageHeight - 150f - Margin);
        }

        private void Refresh()
        {
            if (_root == null || _g == null) return;
            var look = _ui.Look;
            ScreenParts.Clear(_content);
            _shown.Clear();
            float width = PageWidth - 2f * Margin - 20f;
            float normal = look != null ? look.TextNormal : 44f, small = look != null ? look.TextSmall : 34f;
            var entries = _g.Notebook.Entries;
            float y = 0f;
            if (entries.Count == 0)
            {
                var empty = ScreenParts.Label(_content, "Empty", look, "Пока ничего не записано.", normal, 0f, 20f, width, normal * 1.4f);
                y = 20f + empty.rectTransform.sizeDelta.y;
            }
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                _shown.Add(e.Id);
                string who = WhoOf(e);
                var text = ScreenParts.Label(_content, "Note_" + e.Id, look, (who.Length > 0 ? who + ": " : "") + e.Text, normal, 0f, y, width, 10f, TextAlignmentOptions.TopLeft);
                float h = text.GetPreferredValues(text.text, width, 0f).y;
                text.rectTransform.sizeDelta = new Vector2(width, h);
                y += h + 6f;
                if (!string.IsNullOrEmpty(e.Where))
                {
                    var place = ScreenParts.Label(_content, "Where_" + e.Id, look, "(" + e.Where + ")", small, 40f, y, width - 40f, 10f, TextAlignmentOptions.TopLeft);
                    float wh = place.GetPreferredValues(place.text, width - 40f, 0f).y;
                    place.rectTransform.sizeDelta = new Vector2(width - 40f, wh);
                    y += wh + 6f;
                }
                // a pen line between the notes
                var rule = UiKit.MakeRect(_content, "Rule_" + e.Id);
                var ruleImage = rule.gameObject.AddComponent<UnityEngine.UI.Image>();
                ruleImage.color = new Color(0.2f, 0.14f, 0.09f, 0.35f);
                ruleImage.raycastTarget = false;
                ScreenParts.TopLeft(rule, 0f, y + 10f, width, 2f);
                y += 34f;
            }
            ScreenParts.SetContentHeight(_content, y);
        }

        private string WhoOf(NotebookEntry e)
        {
            if (string.IsNullOrEmpty(e.Who)) return "";
            return _g.Data.Npcs.Npcs.TryGetValue(e.Who, out var d) ? d.Name : "";
        }
    }
}
