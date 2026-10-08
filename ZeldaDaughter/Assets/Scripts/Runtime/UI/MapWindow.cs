using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Game;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// The map (project-design.md §4, D-15). It is bought: no map in the bag — no screen (<c>g.Map.HasMap</c>). The sheet is the region's contours
    /// drawn in ink from the scene config by the editor (<c>MapBaker</c>: river, roads, forest, houses; no relief, no details); on it only the
    /// marks that talk has opened (<c>g.Map.VisibleMarks</c>), each a small ink cross with the name beside it in the hero's hand. There is no "you are
    /// here" — she finds herself by the land — and no quest markers. The sheet is turned a quarter so the long road runs up the page: east is
    /// up, north is to the left (the letter «С» by the compass).
    /// </summary>
    public sealed class MapWindow : MonoBehaviour, IWindow
    {
        public const string WindowId = "map";
        private const float MaxWidth = 1040f, MaxHeight = 2060f;
        /// <summary>The compass rose is drawn by MapBaker at this pixel of the sheet (same constants there).</summary>
        public static readonly Vector2 CompassPixel = new Vector2(170f, 130f);

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private Sprite _sheet;
        [SerializeField] private Vector2 _groundSize = new Vector2(360f, 200f); // x, z metres, centred on the origin

        private GameState _g;
        private RectTransform _root, _marks;
        private readonly List<string> _shown = new List<string>();

        public string Id => WindowId;
        public RectTransform Root { get { Build(); return _root; } }
        public bool IsOpen => _windows != null && _windows.IsOpen(WindowId);

        /// <summary>The marks on the sheet now, in the order they were learned.</summary>
        public IReadOnlyList<string> ShownMarks => _shown;
        public bool HasSheet => _sheet != null;

        public void Configure(GameSession session, SessionUI ui, WindowStack windows, Sprite sheet, Vector2 groundSize)
        {
            _session = session;
            _ui = ui;
            _windows = windows;
            _sheet = sheet;
            _groundSize = groundSize;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.RadialChosen += OnRadialChosen;
            _session.Events.MarkOpened += OnMarkOpened;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.RadialChosen -= OnRadialChosen;
            _session.Events.MarkOpened -= OnMarkOpened;
        }

        private void OnReady(GameState g) => _g = g;
        private void OnRadialChosen(string id) { if (id == WindowId) Open(); }
        private void OnMarkOpened(string id) { if (IsOpen) Refresh(); }

        /// <summary>Opens the map. False when the hero has none.</summary>
        public bool Open()
        {
            if (_g == null) return false;
            if (!_g.Map.HasMap) { ZdLog.Info("Map", "no_map"); return false; }
            _windows.Open(this);
            return true;
        }

        public void OnOpened()
        {
            ZdLog.Info("Map", $"open marks={_g.Map.VisibleMarks.Count}");
            Refresh();
        }

        public void OnClosed() { }

        /// <summary>World point → the sheet's normalised position (0..1, origin bottom-left); the sheet is turned so that +x is up and +z is to the left (MapBaker draws it so).</summary>
        public Vector2 ToSheet(float x, float z)
        {
            return new Vector2(Mathf.Clamp01((_groundSize.y * 0.5f - z) / _groundSize.y), Mathf.Clamp01((x + _groundSize.x * 0.5f) / _groundSize.x));
        }

        private void Build()
        {
            if (_root != null) return;
            var look = _ui.Look;
            float w = MaxWidth, h = MaxHeight;
            if (_sheet != null)
            {
                float k = Mathf.Min(MaxWidth / _sheet.rect.width, MaxHeight / _sheet.rect.height);
                w = _sheet.rect.width * k;
                h = _sheet.rect.height * k;
            }
            var image = UiKit.MakePlate(_ui.Windows, "MapWindow", look, Color.white, true);
            if (_sheet != null)
            {
                image.sprite = _sheet;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            _root = image.rectTransform;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = new Vector2(w, h);
            _root.gameObject.SetActive(false);

            _marks = UiKit.MakeRect(_root, "Marks");
            UiKit.Stretch(_marks);

            var north = UiKit.MakeText(_root, "North", look, "С", look != null ? look.TextBig : 60f);
            north.rectTransform.anchorMin = north.rectTransform.anchorMax = Vector2.zero;
            float scale = _sheet != null ? w / _sheet.rect.width : 1f;
            north.rectTransform.anchoredPosition = new Vector2((CompassPixel.x - 100f) * scale, CompassPixel.y * scale);
            north.rectTransform.sizeDelta = new Vector2(70f, 80f);
        }

        private void Refresh()
        {
            if (_root == null || _g == null) return;
            var look = _ui.Look;
            ScreenParts.Clear(_marks);
            _shown.Clear();
            var ink = look != null ? look.Ink : UiKit.DefaultInk;
            foreach (var id in _g.Map.VisibleMarks)
            {
                var def = _g.Map.Def(id);
                var place = _session.Index.Find(def.Object);
                if (place == null) { ZdLog.Warn("Map", $"mark {id}: no object '{def.Object}' in the scene"); continue; }
                var p = place.transform.position;
                var at = ToSheet(p.x, p.z);
                _shown.Add(id);

                var mark = UiKit.MakeRect(_marks, "Mark_" + id);
                mark.anchorMin = mark.anchorMax = at;
                mark.sizeDelta = new Vector2(40f, 40f);
                mark.anchoredPosition = Vector2.zero;
                for (int i = 0; i < 2; i++)
                {
                    var bar = UiKit.MakeRect(mark, "Bar" + i);
                    bar.sizeDelta = new Vector2(8f, 44f);
                    bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                    var img = bar.gameObject.AddComponent<Image>();
                    img.color = ink;
                    img.raycastTarget = false;
                }

                var plate = UiKit.MakePlate(mark, "Label", look, new Color(0.93f, 0.88f, 0.76f, 0.85f));
                var text = UiKit.MakeText(plate.transform, "Text", look, def.Name, look != null ? look.TextSmall : 34f);
                var size = text.GetPreferredValues(def.Name, 360f, 0f);
                bool toLeft = at.x > 0.55f;
                plate.rectTransform.sizeDelta = new Vector2(size.x + 28f, size.y + 14f);
                plate.rectTransform.pivot = new Vector2(toLeft ? 1f : 0f, 0.5f);
                plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                plate.rectTransform.anchoredPosition = new Vector2(toLeft ? -30f : 30f, 0f);
                UiKit.Stretch(text.rectTransform, 4f);
            }
        }
    }
}
