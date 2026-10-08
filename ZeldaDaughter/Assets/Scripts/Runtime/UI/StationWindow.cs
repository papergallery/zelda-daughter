using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Crafting;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// A crafting station (project-design.md §7, D-15): tap on the smelter or the anvil → the station's own little bag. Put materials from the
    /// hero's bag on it (a tap on an item), then strike (taps on the big plate; the smelter is stoked the same way) — after the last stroke the core
    /// decides (<c>g.Crafting.AtStation</c>): ore → metal, metal + stick → sword, metal + short stick → knife, metal → arrowheads. No list of
    /// recipes: she tries. Weapons exist only at the anvil because the core has them only there. No numbers on the window but the count of
    /// pieces.
    /// </summary>
    public sealed class StationWindow : MonoBehaviour, IWindow
    {
        public const string WindowId = "station";
        public const int MaxInputs = 3;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private ArtAssets _art;
        [SerializeField] private int _strikesNeeded = 3;

        private GameState _g;
        private RectTransform _root;
        private TextMeshProUGUI _title, _hint;
        private RectTransform _inputsRow, _pips, _bag;
        private Button _strike;
        private TextMeshProUGUI _strikeLabel;
        private string _kind, _objectId;
        private Vector3 _stationPos;
        private int _strikes;
        private string _lastItem;
        private int _lastCount;
        private readonly List<string> _inputs = new List<string>();

        public string Id => WindowId;
        public RectTransform Root { get { Build(); return _root; } }

        /// <summary>The station that is open (<c>smelter</c>, <c>anvil</c>), or null.</summary>
        public string Kind => _windows != null && _windows.IsOpen(WindowId) ? _kind : null;
        public IReadOnlyList<string> Inputs => _inputs;
        public int Strikes => _strikes;
        public int StrikesNeeded => _strikesNeeded;
        /// <summary>What the last craft made (item id), or null.</summary>
        public string LastItem => _lastItem;

        public void Configure(GameSession session, SessionUI ui, WindowStack windows, ArtAssets art, int strikesNeeded = 3)
        {
            _session = session;
            _ui = ui;
            _windows = windows;
            _art = art;
            _strikesNeeded = Mathf.Max(1, strikesNeeded);
        }

        private void OnEnable() => _session.Events.StateReady += OnReady;

        private void OnDisable()
        {
            if (_session != null) _session.Events.StateReady -= OnReady;
        }

        private void Start() => _session.OnTap(TapKind.Station, OnTap);

        private void OnReady(GameState g) => _g = g;

        private void OnTap(Tappable t)
        {
            if (_g == null || (_windows != null && _windows.AnyOpen)) return;
            if (!ScreenParts.InReach(_session, _g, t)) { ZdLog.Info("Station", $"{t.Id} too_far"); return; }
            Open(t.Id);
        }

        /// <summary>Opens the station of the scene object (its kind comes from the scene index). False for an object that is no station.</summary>
        public bool Open(string objectId)
        {
            if (_g == null) return false;
            string kind = null;
            Vector3 pos = default;
            foreach (var s in _session.Index.Stations)
                if (s.Id == objectId) { kind = s.Detail; pos = s.Position; break; }
            if (string.IsNullOrEmpty(kind)) return false;
            _kind = kind;
            _objectId = objectId;
            _stationPos = pos;
            _inputs.Clear();
            _strikes = 0;
            _lastItem = null;
            _windows.Open(this);
            return true;
        }

        public void OnOpened()
        {
            _title.text = TitleOf(_kind);
            ZdLog.Info("Station", $"open {_kind} {_objectId}");
            Refresh();
        }

        public void OnClosed() => ZdLog.Info("Station", $"close {_kind}");

        // ------------------------------------------------------------------ what the player does

        /// <summary>Puts one piece of the item from the bag on the station. False if the bag has no more of it or the station is full.</summary>
        public bool Put(string itemId)
        {
            if (_g == null || _inputs.Count >= MaxInputs) return false;
            int onStation = 0;
            foreach (var i in _inputs) if (i == itemId) onStation++;
            if (_g.Bag.Count(itemId) <= onStation) return false;
            _inputs.Add(itemId);
            _strikes = 0;
            _lastItem = null;
            Refresh();
            return true;
        }

        /// <summary>Takes the piece at the place back into the bag (it never left it).</summary>
        public void TakeBack(int index)
        {
            if (index < 0 || index >= _inputs.Count) return;
            _inputs.RemoveAt(index);
            _strikes = 0;
            Refresh();
        }

        /// <summary>One stroke. After the last one the station works. True if it worked now (successfully or not).</summary>
        public bool Strike()
        {
            if (_g == null || _inputs.Count == 0) return false;
            _strikes++;
            _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Craft, _stationPos));
            ZdLog.Info("Station", $"strike {_kind} {_strikes}/{_strikesNeeded}");
            if (_strikes < _strikesNeeded) { Refresh(); return false; }
            Work();
            return true;
        }

        private void Work()
        {
            var r = _g.Crafting.AtStation(_kind, _inputs.ToArray(), _g.Bag);
            _strikes = 0;
            ZdLog.Info("Station", $"{_kind} {string.Join("+", _inputs)} -> {r.Outcome} {r.Item}");
            switch (r.Outcome)
            {
                case CraftOutcome.Done:
                    _lastItem = r.Item;
                    _lastCount = r.Count;
                    _inputs.Clear();
                    _session.BagChanged("station");
                    _session.Say(Topics.CraftOk);
                    break;
                case CraftOutcome.NoRoom:
                    _session.Say(Topics.CraftNoRoom);
                    break;
                case CraftOutcome.MissingIngredients:
                    _inputs.Clear();
                    break;
                default:
                    _session.Say(Topics.CraftFail);
                    break;
            }
            Refresh();
        }

        // ------------------------------------------------------------------ the picture

        private static string TitleOf(string kind)
        {
            switch (kind)
            {
                case "smelter": return "Плавильня";
                case "anvil": return "Наковальня";
                default: return kind ?? "";
            }
        }

        private void Build()
        {
            if (_root != null) return;
            var look = _ui.Look;
            _root = ScreenParts.Frame(_ui.Windows, "StationWindow", look, new Vector2(960f, 1500f), "", out _title);
            _root.gameObject.SetActive(false);
            _inputsRow = UiKit.MakeRect(_root, "Inputs");
            ScreenParts.TopLeft(_inputsRow, 0f, 130f, 960f, 240f);
            _hint = ScreenParts.Label(_root, "Hint", look, "", look != null ? look.TextNormal : 44f, 40f, 385f, 880f, 70f);
            _strike = UiKit.MakeButton(_root, "Strike", look, "", new Vector2(560f, 150f), () => Strike());
            ScreenParts.TopLeft((RectTransform)_strike.transform, 200f, 470f, 560f, 150f);
            _strikeLabel = _strike.GetComponentInChildren<TextMeshProUGUI>();
            if (_strikeLabel != null) _strikeLabel.fontSize = look != null ? look.TextBig : 60f;
            _pips = UiKit.MakeRect(_root, "Pips");
            ScreenParts.TopLeft(_pips, 0f, 635f, 960f, 50f);
            ScreenParts.Label(_root, "BagTitle", look, "Сумка", look != null ? look.TextNormal : 44f, 40f, 705f, 880f, 60f);
            _bag = ScreenParts.Scroll(_root, "Bag", 50f, 770f, 860f, 690f);
        }

        private void Refresh()
        {
            if (_root == null || _g == null) return;
            var look = _ui.Look;
            var icons = _art != null ? _art.ItemIcons : null;
            var cell = new Vector2(240f, 230f);

            ScreenParts.Clear(_inputsRow);
            float x0 = (960f - (MaxInputs * cell.x + (MaxInputs - 1) * 30f)) * 0.5f;
            for (int i = 0; i < MaxInputs; i++)
            {
                RectTransform rt;
                if (i < _inputs.Count)
                {
                    int index = i;
                    string id = _inputs[i];
                    rt = (RectTransform)ScreenParts.Cell(_inputsRow, "Input_" + id, look, icons, id, ScreenParts.ItemName(_g, id), 1, null, cell, () => TakeBack(index)).transform;
                }
                else
                {
                    rt = UiKit.MakePlate(_inputsRow, "Empty_" + i, look, new Color(0.7f, 0.64f, 0.52f, 0.5f)).rectTransform;
                }
                ScreenParts.TopLeft(rt, x0 + i * (cell.x + 30f), 5f, cell.x, cell.y);
            }

            string action = _kind == "smelter" ? "Раздувать" : "Ударить";
            if (_strikeLabel != null) _strikeLabel.text = action;
            _strike.interactable = _inputs.Count > 0;
            _strike.targetGraphic.color = _inputs.Count > 0 ? (look != null ? look.PaperColor : UiKit.DefaultPaper) : new Color(0.62f, 0.58f, 0.5f, 0.8f);

            if (_lastItem != null)
                _hint.text = (_lastCount > 1 ? ScreenParts.ItemName(_g, _lastItem) + " x" + _lastCount : ScreenParts.ItemName(_g, _lastItem)) + " - готово";
            else
                _hint.text = _inputs.Count == 0 ? "Положи сюда, что есть" : "";

            ScreenParts.Clear(_pips);
            float pip = 36f, gap = 22f;
            float px0 = (960f - (_strikesNeeded * pip + (_strikesNeeded - 1) * gap)) * 0.5f;
            for (int i = 0; i < _strikesNeeded; i++)
            {
                var plate = UiKit.MakePlate(_pips, "Pip_" + i, look, i < _strikes ? (look != null ? look.Ink : UiKit.DefaultInk) : new Color(0.7f, 0.64f, 0.52f, 0.6f));
                ScreenParts.TopLeft(plate.rectTransform, px0 + i * (pip + gap), 5f, pip, pip);
            }

            // the bag: what is in it and not yet on the station
            ScreenParts.Clear(_bag);
            var ids = new List<string>();
            foreach (var s in _g.Bag.Stacks) if (!ids.Contains(s.ItemId)) ids.Add(s.ItemId);
            var cs = new Vector2(200f, 230f);
            int shown = 0;
            foreach (var id in ids)
            {
                int onStation = 0;
                foreach (var i in _inputs) if (i == id) onStation++;
                int left = _g.Bag.Count(id) - onStation;
                if (left <= 0) continue;
                string itemId = id;
                var rt = (RectTransform)ScreenParts.Cell(_bag, "Item_" + id, look, icons, id, ScreenParts.ItemName(_g, id), left, null, cs, () => Put(itemId)).transform;
                ScreenParts.TopLeft(rt, (shown % 4) * (cs.x + 20f), (shown / 4) * (cs.y + 15f), cs.x, cs.y);
                shown++;
            }
            ScreenParts.SetContentHeight(_bag, ((shown + 3) / 4) * (cs.y + 15f));
        }
    }
}
