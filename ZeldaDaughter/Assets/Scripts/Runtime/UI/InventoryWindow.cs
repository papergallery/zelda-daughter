using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// The bag (docs/demo/unity-architecture.md §3, project-design.md §7): a sheet of paper with <c>inventory.json slots</c> cells, a stack to a cell, its
    /// icon (a paper plate with the item's name while the picture is not drawn yet) and the count as a digit — the only place numbers are allowed (the
    /// «no numbers» rule is about the hero's state). A finger on a cell starts <see cref="ItemDrag"/>. Built on first open, refreshed when the bag changes.
    /// </summary>
    public sealed class InventoryWindow : MonoBehaviour, IWindow
    {
        public const string WindowId = "bag";
        private const int Columns = 4;
        private const float Cell = 200f, Gap = 16f, Margin = 44f, TitleHeight = 96f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private ItemDrag _drag;
        [SerializeField] private IconRegistry _icons;

        private sealed class CellView
        {
            public RectTransform Root;
            public Image Plate;
            public RectTransform Icon;      // rebuilt only when the item in the cell changes
            public TextMeshProUGUI Name;    // while the picture is missing
            public TextMeshProUGUI Count;
            public string Item;
            public int Shown = -1;
            public bool Dimmed;
        }

        private GameState _g;
        private RectTransform _root;
        private readonly List<CellView> _cells = new List<CellView>();
        private Color _plateColor, _emptyColor;
        private bool _open;
        private BubbleWidget _info;
        private float _infoUntil;
        private int _infoCell = -1;

        public string Id => WindowId;
        public RectTransform Root { get { Build(); return _root; } }
        public int CellCount { get { Build(); return _cells.Count; } }
        public bool IsOpen => _open;

        public void Configure(GameSession session, SessionUI ui, WindowStack windows, ItemDrag drag, IconRegistry icons)
        {
            _session = session;
            _ui = ui;
            _windows = windows;
            _drag = drag;
            _icons = icons;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.BagChanged += OnBagChanged;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.BagChanged -= OnBagChanged;
        }

        private void OnReady(GameState g) => _g = g;

        private void OnBagChanged(string why)
        {
            if (_root != null) Refresh();
        }

        /// <summary>Opens the bag through the window stack.</summary>
        public void Open()
        {
            Build();
            _windows.Open(this);
        }

        public void OnOpened()
        {
            _open = true;
            Refresh();
            ZdLog.Info("Items", $"bag open cells={_cells.Count} stacks={_g.Bag.UsedSlots}");
        }

        // ------------------------------------------------------------------ the description (D-23)

        /// <summary>The description cloud on screen (the text from data/items.json), or null.</summary>
        public string InfoText => _info != null ? _info.Text : null;
        public int InfoCell => _info != null && _info.Visible ? _infoCell : -1;
        public RectTransform InfoRoot => _info?.Root;

        /// <summary>
        /// A long press on a cell: a paper cloud above the cell tells in a few words what the thing is (<c>description</c> in items.json) — no numbers.
        /// It goes after <c>itemInfoSeconds</c>, on the next press, or when the bag closes. False for an empty cell.
        /// </summary>
        public bool ShowInfo(int index)
        {
            Build();
            string item = ItemIn(index);
            if (item == null || _g == null || !_g.Data.Items.TryGetValue(item, out var def)) return false;
            var look = _ui.Look;
            if (_info == null)
            {
                _info = UiKit.MakeBubble(_root, "ItemInfo", look, 700f, look != null ? look.TextSmall : 34f);
                var rt = _info.Root;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0f);
                _info.Background.raycastTarget = false;
            }
            _info.Show(def.Name + ". " + def.Description);
            _info.Root.SetAsLastSibling();
            _infoCell = index;
            var cell = _cells[index].Root;
            float width = _root.sizeDelta.x, half = _info.Root.sizeDelta.x * 0.5f;
            float x = Mathf.Clamp(cell.anchoredPosition.x, half + 8f, Mathf.Max(half + 8f, width - half - 8f));
            _info.Root.anchoredPosition = new Vector2(x, cell.anchoredPosition.y + Cell * 0.5f + 8f);
            _infoUntil = Time.unscaledTime + _g.Data.Session.ItemInfoSeconds;
            ZdLog.Info("Items", $"describe {item}");
            return true;
        }

        public void HideInfo()
        {
            if (_info != null) _info.Hide();
            _infoCell = -1;
        }

        private void Update()
        {
            if (_info != null && _info.Visible && Time.unscaledTime >= _infoUntil) HideInfo();
        }

        public void OnClosed()
        {
            HideInfo();
            _open = false;
            _drag.Cancel();
        }

        // ------------------------------------------------------------------ the cells

        /// <summary>The cell under a screen point, or -1.</summary>
        public int CellAt(Vector2 screen)
        {
            Build();
            for (int i = 0; i < _cells.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_cells[i].Root, screen, null)) return i;
            return -1;
        }

        public bool ContainsScreenPoint(Vector2 screen)
        {
            Build();
            return RectTransformUtility.RectangleContainsScreenPoint(_root, screen, null);
        }

        /// <summary>Where cell <paramref name="index"/> is on the screen, in pixels (the canvas is Overlay: a transform position is a pixel).</summary>
        public Vector2 CellScreenPosition(int index)
        {
            Build();
            return _cells[index].Root.position;
        }

        /// <summary>The item in a cell, or null when the cell is empty.</summary>
        public string ItemIn(int index)
        {
            Build();
            return index >= 0 && index < _cells.Count ? _cells[index].Item : null;
        }

        public void SetDimmed(int index, bool dimmed)
        {
            Build();
            if (index < 0 || index >= _cells.Count) return;
            _cells[index].Dimmed = dimmed;
            Paint(_cells[index]);
        }

        public void Refresh()
        {
            if (_root == null || _g == null) return;
            var stacks = _g.Bag.Stacks;
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                string item = i < stacks.Count ? stacks[i].ItemId : null;
                int count = i < stacks.Count ? stacks[i].Count : 0;
                if (item != c.Item) SetItem(c, item);
                if (count != c.Shown)
                {
                    c.Shown = count;
                    bool show = count > 1;
                    c.Count.gameObject.SetActive(show);
                    if (show) c.Count.text = count.ToString();
                }
                Paint(c);
            }
        }

        private void SetItem(CellView c, string item)
        {
            c.Item = item;
            if (c.Icon != null) Destroy(c.Icon.gameObject);
            c.Icon = null;
            c.Name.gameObject.SetActive(false);
            if (item == null) return;
            var look = _ui.Look;
            var sprite = _icons != null ? _icons.Get(item) : null;
            if (sprite != null)
            {
                c.Icon = UiKit.MakeIcon(c.Root, "Icon", _icons, item, look, Cell - 56f);
                c.Icon.anchorMin = c.Icon.anchorMax = new Vector2(0.5f, 0.5f);
                c.Icon.anchoredPosition = Vector2.zero;
                c.Icon.SetSiblingIndex(0);
            }
            else
            {
                c.Name.text = _g.Data.Items.TryGetValue(item, out var def) ? def.Name : item;
                c.Name.gameObject.SetActive(true);
            }
        }

        private void Paint(CellView c)
        {
            var color = c.Item == null ? _emptyColor : _plateColor;
            if (c.Dimmed) color.a *= 0.45f;
            c.Plate.color = color;
            if (c.Icon != null) c.Icon.gameObject.SetActive(!c.Dimmed);
            c.Name.alpha = c.Dimmed ? 0.3f : 1f;
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            if (_root != null) return;
            var look = _ui.Look;
            _plateColor = look != null ? look.PaperColor : UiKit.DefaultPaper;
            _emptyColor = Color.Lerp(_plateColor, new Color(0.45f, 0.38f, 0.28f, _plateColor.a), 0.35f);

            int slots = GameData.Current.Inventory.Slots;
            int rows = (slots + Columns - 1) / Columns;
            float width = Columns * Cell + (Columns - 1) * Gap + 2f * Margin;
            float height = rows * Cell + (rows - 1) * Gap + 2f * Margin + TitleHeight;

            var sheet = UiKit.MakePlate(_ui.Windows, "BagWindow", look, null, true);
            _root = sheet.rectTransform;
            UiKit.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(width, height), new Vector2(0.5f, 0.5f));
            _root.gameObject.SetActive(false);

            var title = UiKit.MakeText(_root, "Title", look, "Сумка", look != null ? look.TextBig : 60f);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(width - 2f * Margin, TitleHeight), new Vector2(0.5f, 1f));
            title.rectTransform.anchoredPosition = new Vector2(0f, -Margin * 0.5f);

            for (int i = 0; i < slots; i++)
            {
                int col = i % Columns, row = i / Columns;
                var plate = UiKit.MakeSlot(_root, "Cell_" + i, look, Cell);
                var rt = plate.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(Margin + Cell * 0.5f + col * (Cell + Gap), -(Margin + TitleHeight + Cell * 0.5f + row * (Cell + Gap)));
                plate.gameObject.AddComponent<CellHandler>().Configure(_drag, i);

                var name = UiKit.MakeText(rt, "Name", look, "", look != null ? look.TextSmall : 34f);
                UiKit.Stretch(name.rectTransform, 10f);
                name.gameObject.SetActive(false);
                var count = UiKit.MakeText(rt, "Count", look, "", look != null ? look.TextNormal : 44f, TextAlignmentOptions.BottomRight);
                UiKit.Stretch(count.rectTransform, 12f);
                count.gameObject.SetActive(false);
                _cells.Add(new CellView { Root = rt, Plate = plate, Name = name, Count = count });
            }
        }
    }
}
