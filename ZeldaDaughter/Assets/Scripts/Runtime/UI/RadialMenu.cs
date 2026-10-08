using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// A long press on the hero opens a fan of plates over her — bag, map (only once a map is bought: <c>g.Map.HasMap</c>), notebook (docs/demo/unity-architecture.md §3).
    /// The finger is still down: the plate nearest its direction lights up (<c>HeroController.FingerPosition</c>); lifted on a plate — the window opens (the bag by itself,
    /// the map and the notebook through <c>SessionEvents.RadialChosen</c> for D-15); lifted over the hero's own spot — the plates stay as buttons; lifted elsewhere, or a tap
    /// beside the plates — the menu goes. The sector is chosen by direction, so a thumb that overshoots still picks.
    /// </summary>
    public sealed class RadialMenu : MonoBehaviour
    {
        public const string Bag = "bag", Map = "map", Notebook = "notebook";
        private const float Radius = 290f, PlateSize = 210f, CenterRadius = 95f, MaxSectorDegrees = 50f, EdgeMargin = 140f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private HeroController _hero;
        [SerializeField] private InventoryWindow _inventory;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private IconRegistry _icons;

        private enum State { Closed, Holding, Resting }

        private sealed class Sector
        {
            public string Id;
            public float Degrees;
            public RectTransform Root;
            public Image Plate;
        }

        private GameState _g;
        private State _state;
        private RectTransform _root;
        private readonly List<Sector> _sectors = new List<Sector>(3);
        private Vector2 _center;
        private Sector _lit;
        private Color _plateColor, _litColor;

        public bool IsOpen => _state != State.Closed;
        public bool IsHolding => _state == State.Holding;
        /// <summary>The sectors now shown, in order (bag, map if bought, notebook).</summary>
        public IReadOnlyList<string> SectorIds
        {
            get
            {
                var ids = new List<string>(_sectors.Count);
                foreach (var s in _sectors) ids.Add(s.Id);
                return ids;
            }
        }
        public string LitSector => _lit?.Id;

        public void Configure(GameSession session, SessionUI ui, HeroController hero, InventoryWindow inventory, WindowStack windows, IconRegistry talkIcons)
        {
            _session = session;
            _ui = ui;
            _hero = hero;
            _inventory = inventory;
            _windows = windows;
            _icons = talkIcons;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.LongPressHero += OnLongPress;
            _session.Events.LongPressReleased += OnReleased;
            _session.Events.WindowOpened += OnWindowOpened;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.LongPressHero -= OnLongPress;
            _session.Events.LongPressReleased -= OnReleased;
            _session.Events.WindowOpened -= OnWindowOpened;
        }

        private void OnReady(GameState g) => _g = g;

        private void OnWindowOpened(string id) { if (_state != State.Closed) Close(); }

        /// <summary>Where a sector's plate is on the screen, in pixels.</summary>
        public Vector2 SectorScreenPosition(string id)
        {
            foreach (var s in _sectors) if (s.Id == id) return s.Root.position;
            return Vector2.zero;
        }

        // ------------------------------------------------------------------ the gesture

        private void OnLongPress()
        {
            if (_g == null || _windows.AnyOpen) return;
            Open();
        }

        private void OnReleased()
        {
            if (_state != State.Holding) return;
            var picked = SectorAt(_hero.FingerPosition);
            if (picked != null) { Choose(picked.Id); return; }
            if (((_hero.FingerPosition - _center).magnitude) <= CenterRadius * _ui.Canvas.scaleFactor)
            {
                _state = State.Resting; // the plates stay as buttons
                Light(null);
                ZdLog.Info("Items", "radial rests");
                return;
            }
            Close();
        }

        private void Update()
        {
            if (_state != State.Holding) return;
            Light(SectorAt(_hero.FingerPosition));
        }

        // ------------------------------------------------------------------ the fan

        private void Open()
        {
            var look = _ui.Look;
            _plateColor = look != null ? look.PaperColor : UiKit.DefaultPaper;
            _litColor = Color.Lerp(_plateColor, new Color(1f, 0.93f, 0.7f, 1f), 0.7f);

            Close();
            _root = UiKit.MakeRect(_ui.Windows, "RadialMenu");
            UiKit.Stretch(_root);
            _root.SetAsLastSibling();

            // beside the plates a tap closes the menu (the backdrop takes it, so it never reaches the hero)
            var back = UiKit.MakeRect(_root, "Backdrop");
            UiKit.Stretch(back);
            var backImage = back.gameObject.AddComponent<Image>();
            backImage.color = new Color(0f, 0f, 0f, 0f);
            backImage.raycastTarget = true;
            var backButton = back.gameObject.AddComponent<Button>();
            backButton.transition = Selectable.Transition.None;
            backButton.onClick.AddListener(Close);

            var ids = new List<string> { Bag };
            if (_g.Map.HasMap) ids.Add(Map);
            ids.Add(Notebook);

            var cam = _ui.Camera;
            var onScreen = cam.WorldToScreenPoint(_hero.transform.position);
            float scale = _ui.Canvas.scaleFactor;
            _center = new Vector2(onScreen.x, onScreen.y);

            _sectors.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                float deg = DegreesFor(i, ids.Count);
                var s = new Sector { Id = ids[i], Degrees = deg };
                var plate = UiKit.MakePlate(_root, "Sector_" + ids[i], look, null, true);
                s.Plate = plate;
                s.Root = plate.rectTransform;
                s.Root.sizeDelta = new Vector2(PlateSize, PlateSize);
                var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                var p = _center + dir * Radius * scale;
                p.x = Mathf.Clamp(p.x, EdgeMargin * scale, Screen.width - EdgeMargin * scale);
                p.y = Mathf.Clamp(p.y, EdgeMargin * scale, Screen.height - EdgeMargin * scale);
                s.Root.position = p;

                var sprite = _icons != null ? _icons.Get("radial_" + ids[i]) : null;
                if (sprite != null)
                {
                    var icon = UiKit.MakeIcon(s.Root, "Icon", _icons, "radial_" + ids[i], look, PlateSize - 60f);
                    icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                    icon.anchoredPosition = new Vector2(0f, 14f);
                }
                var caption = UiKit.MakeText(s.Root, "Caption", look, Caption(ids[i]), look != null ? look.TextNormal : 44f);
                UiKit.Stretch(caption.rectTransform, 8f);

                var button = plate.gameObject.AddComponent<Button>();
                button.targetGraphic = plate;
                button.transition = Selectable.Transition.None;
                string id = ids[i];
                button.onClick.AddListener(() => Choose(id));
                _sectors.Add(s);
            }
            _lit = null;
            _state = State.Holding;
            ZdLog.Info("Items", "radial open " + string.Join(",", ids));
        }

        private static string Caption(string id) => id == Bag ? "Сумка" : id == Map ? "Карта" : "Блокнот";

        /// <summary>A fan above the hero: three plates at 150°, 90°, 30°; two at 135° and 45°; the first plate is the left one.</summary>
        private static float DegreesFor(int index, int count)
        {
            if (count == 2) return index == 0 ? 135f : 45f;
            if (count == 3) return 150f - 60f * index;
            return 90f;
        }

        /// <summary>The plate in the direction of the finger from the centre (outside the dead zone, within the plate's share of the circle).</summary>
        private Sector SectorAt(Vector2 finger)
        {
            var d = finger - _center;
            if (d.magnitude < CenterRadius * _ui.Canvas.scaleFactor) return null;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Sector best = null;
            float bestDiff = MaxSectorDegrees;
            foreach (var s in _sectors)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angle, s.Degrees));
                if (diff <= bestDiff) { bestDiff = diff; best = s; }
            }
            return best;
        }

        private void Light(Sector s)
        {
            if (s == _lit) return;
            if (_lit != null) { _lit.Plate.color = _plateColor; _lit.Root.localScale = Vector3.one; }
            _lit = s;
            if (_lit != null) { _lit.Plate.color = _litColor; _lit.Root.localScale = Vector3.one * 1.18f; }
        }

        /// <summary>A plate chosen (finger lifted on it, or a click on a resting plate): the menu goes, the window comes.</summary>
        public void Choose(string id)
        {
            Close();
            ZdLog.Info("Items", "radial choose " + id);
            if (id == Bag) _inventory.Open();
            _session.Events.RaiseRadialChosen(id);
        }

        public void Close()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _sectors.Clear();
            _lit = null;
            _state = State.Closed;
        }
    }
}
