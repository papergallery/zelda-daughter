using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// The shop (project-design.md §2, D-15): opens on <c>TradeRequested(npc, item?)</c> and only while the trader stands at her counter
    /// (<c>g.Npcs.IsShopOpen</c>). Two columns — the trader's goods and what she bought from the hero (buyback) on the left, the hero's bag on the
    /// right — and the table between them: what the hero gives, what she takes. A tap on a thing puts one piece on the table, a tap on the table
    /// takes it off. Before the deal the trader reacts to the offer (<c>g.Trade.Evaluate</c>) — a gesture, not a number, so it reads in any language.
    /// Barter always; coins and prices only after the hero knows what a coin is. The only digits are prices and counts.
    /// </summary>
    public sealed class TradeWindow : MonoBehaviour, IWindow
    {
        public const string WindowId = "trade";
        private const int MaxPerLine = 99;

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private ArtAssets _art;

        private GameState _g;
        private RectTransform _root;
        private TextMeshProUGUI _title, _leftHead, _rightHead, _giveHead, _takeHead, _reaction;
        private RectTransform _left, _right, _giveRow, _takeRow;
        private Button _deal;
        private string _trader;
        private readonly List<TradeLine> _give = new List<TradeLine>();
        private readonly List<TradeLine> _take = new List<TradeLine>();
        private TradeOutcome _lastOutcome;
        private float _pollLeft;

        public string Id => WindowId;
        public RectTransform Root { get { Build(); return _root; } }

        public bool IsOpen => _windows != null && _windows.IsOpen(WindowId);
        /// <summary>The trader's key (npcs.json) while the window is open, else null.</summary>
        public string Trader => IsOpen ? _trader : null;
        public IReadOnlyList<TradeLine> Give => _give;
        public IReadOnlyList<TradeLine> Take => _take;
        /// <summary>The trader's reaction to the table now (empty while the table is empty).</summary>
        public string ReactionText => _reaction != null ? _reaction.text : "";
        public bool DealEnabled => _deal != null && _deal.interactable;

        public void Configure(GameSession session, SessionUI ui, WindowStack windows, ArtAssets art)
        {
            _session = session;
            _ui = ui;
            _windows = windows;
            _art = art;
        }

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.TradeRequested += OnTradeRequested;
            _session.Events.BagChanged += OnBagChanged;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.TradeRequested -= OnTradeRequested;
            _session.Events.BagChanged -= OnBagChanged;
        }

        private void OnReady(GameState g) => _g = g;
        private void OnBagChanged(string why) { if (IsOpen) Refresh(); }

        private void OnTradeRequested(string npcId, string itemId) => Open(npcId, itemId);

        /// <summary>Opens the shop of the trader; <paramref name="itemId"/> (an item dropped on her) goes onto the table at once. False if she has no shop or it is closed.</summary>
        public bool Open(string npcId, string itemId = null)
        {
            if (_g == null || string.IsNullOrEmpty(npcId)) return false;
            string key = npcId.StartsWith("npc_", StringComparison.Ordinal) ? npcId.Substring(4) : npcId;
            if (!_g.Trade.IsTrader(key)) { ZdLog.Info("Trade", $"{key} no_shop"); return false; }
            if (!_g.Npcs.IsShopOpen(key)) { ZdLog.Info("Trade", $"{key} closed"); return false; }
            _trader = key;
            _give.Clear();
            _take.Clear();
            if (!string.IsNullOrEmpty(itemId) && _g.Bag.Count(itemId) > 0 && _g.Trade.BuyValue(key, itemId) > 0f && Visible(itemId))
                _give.Add(new TradeLine(itemId, 1));
            _windows.Open(this);
            return true;
        }

        public void OnOpened()
        {
            _title.text = _g.Data.Npcs.Npcs.TryGetValue(_trader, out var d) ? d.Name : _trader;
            _pollLeft = 0.5f;
            ZdLog.Info("Trade", $"open {_trader} coins={(_g.Trade.KnowsCoins ? "known" : "unknown")}");
            Refresh();
        }

        public void OnClosed() => ZdLog.Info("Trade", $"close {_trader}");

        private void Update()
        {
            if (!IsOpen || _g == null) return;
            _pollLeft -= Time.deltaTime;
            if (_pollLeft > 0f) return;
            _pollLeft = 0.5f;
            if (_g.Trade.Evaluate(_trader, Offer()).Outcome != _lastOutcome) Refresh(); // the shop closed, the evening came…
        }

        // ------------------------------------------------------------------ the table

        private TradeOffer Offer() => new TradeOffer(_give, _take);

        /// <summary>The coin is a thing the hero does not know yet: it is not shown on either side until she learns it.</summary>
        private bool Visible(string itemId) => itemId != "coin" || _g.Trade.KnowsCoins;

        /// <summary>One more piece of the trader's goods (or of the buyback shelf) onto the table. False when there is no more of it.</summary>
        public bool AddTake(string itemId, bool fromBuyback = false)
        {
            if (_g == null || !Visible(itemId)) return false;
            int have = TakeLimit(itemId, fromBuyback);
            if (CountIn(_take, itemId, fromBuyback) >= have) return false;
            Bump(_take, itemId, fromBuyback, 1);
            Refresh();
            return true;
        }

        /// <summary>One more piece from the hero's bag onto the table.</summary>
        public bool AddGive(string itemId)
        {
            if (_g == null || !Visible(itemId)) return false;
            if (CountIn(_give, itemId, false) >= _g.Bag.Count(itemId)) return false;
            Bump(_give, itemId, false, 1);
            Refresh();
            return true;
        }

        public void RemoveGive(string itemId) { Bump(_give, itemId, false, -1); Refresh(); }
        public void RemoveTake(string itemId, bool fromBuyback = false) { Bump(_take, itemId, fromBuyback, -1); Refresh(); }

        public void ClearTable()
        {
            _give.Clear();
            _take.Clear();
            Refresh();
        }

        /// <summary>What the core says to the table now, without changing anything.</summary>
        public TradeResult Evaluate() => _g.Trade.Evaluate(_trader, Offer());

        /// <summary>The deal: the core exchanges the sets (all or nothing). The table is cleared when it went through.</summary>
        public TradeResult Deal()
        {
            var r = _g.Trade.Execute(_trader, Offer());
            _session.Events.RaiseTraded(r);
            ZdLog.Info("Trade", $"{_trader} {r.Outcome} give={r.GiveValue:0.##} take={r.TakeValue:0.##}");
            if (r.Outcome == TradeOutcome.Done)
            {
                _give.Clear();
                _take.Clear();
                _session.BagChanged("trade");
            }
            else if (r.Outcome == TradeOutcome.NoRoom) _session.Say(Topics.CraftNoRoom);
            Refresh();
            return r;
        }

        private int TakeLimit(string itemId, bool fromBuyback)
        {
            if (itemId == "coin") return MaxPerLine;
            if (fromBuyback)
            {
                foreach (var l in _g.Trade.Buyback(_trader)) if (l.Item == itemId) return l.Count;
                return 0;
            }
            foreach (var s in _g.Trade.Stock(_trader)) if (s.Item == itemId) return s.Count < 0 ? MaxPerLine : s.Count;
            return 0;
        }

        private static int CountIn(List<TradeLine> lines, string item, bool fromBuyback)
        {
            foreach (var l in lines) if (l.Item == item && l.FromBuyback == fromBuyback) return l.Count;
            return 0;
        }

        private static void Bump(List<TradeLine> lines, string item, bool fromBuyback, int by)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.Item != item || l.FromBuyback != fromBuyback) continue;
                int n = l.Count + by;
                if (n <= 0) lines.RemoveAt(i); else lines[i] = new TradeLine(item, n, fromBuyback);
                return;
            }
            if (by > 0) lines.Add(new TradeLine(item, by, fromBuyback));
        }

        /// <summary>The bag, the shelf or the buyback may have changed under the table: cut every line down to what exists.</summary>
        private void Clamp()
        {
            for (int i = _give.Count - 1; i >= 0; i--)
            {
                var l = _give[i];
                int n = Mathf.Min(l.Count, _g.Bag.Count(l.Item));
                if (n <= 0 || !Visible(l.Item)) _give.RemoveAt(i); else if (n != l.Count) _give[i] = new TradeLine(l.Item, n);
            }
            for (int i = _take.Count - 1; i >= 0; i--)
            {
                var l = _take[i];
                int n = Mathf.Min(l.Count, TakeLimit(l.Item, l.FromBuyback));
                if (n <= 0 || !Visible(l.Item)) _take.RemoveAt(i); else if (n != l.Count) _take[i] = new TradeLine(l.Item, n, l.FromBuyback);
            }
        }

        // ------------------------------------------------------------------ the reaction

        /// <summary>How the trader answers the offer: a gesture. How near the sides are decides between kinds of refusal and approval.</summary>
        private static string ReactionOf(TradeResult r, bool empty)
        {
            if (empty) return "";
            switch (r.Outcome)
            {
                case TradeOutcome.Done:
                    return r.TakeValue > 0f && r.GiveValue >= r.TakeValue * 1.5f ? "улыбается и кивает" : "кивает";
                case TradeOutcome.NotEnough:
                    return r.TakeValue > 0f && r.GiveValue >= r.TakeValue * 0.75f ? "мнётся, чешет подбородок" : "качает головой";
                case TradeOutcome.NotWanted: return "морщится и отодвигает вещь";
                case TradeOutcome.NotInStock: return "разводит руками";
                case TradeOutcome.Closed: return "убирает товар под прилавок";
                case TradeOutcome.CoinsUnknown: return "непонимающе смотрит на кружочки";
                case TradeOutcome.NoRoom: return "кивает на твою полную сумку";
                case TradeOutcome.MissingItems: return "ждёт";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ the picture

        private void Build()
        {
            if (_root != null) return;
            var look = _ui.Look;
            float normal = look != null ? look.TextNormal : 44f;
            _root = ScreenParts.Frame(_ui.Windows, "TradeWindow", look, new Vector2(1020f, 1800f), "", out _title);
            _root.gameObject.SetActive(false);
            _leftHead = ScreenParts.Label(_root, "LeftHead", look, "Товар", normal, 30f, 120f, 470f, 56f);
            _rightHead = ScreenParts.Label(_root, "RightHead", look, "Моя сумка", normal, 520f, 120f, 470f, 56f);
            _left = ScreenParts.Scroll(_root, "Left", 30f, 180f, 470f, 760f);
            _right = ScreenParts.Scroll(_root, "Right", 520f, 180f, 470f, 760f);
            _giveHead = ScreenParts.Label(_root, "GiveHead", look, "Отдаю", normal, 30f, 960f, 300f, 50f, TextAlignmentOptions.Left);
            _giveRow = UiKit.MakeRect(_root, "GiveRow");
            ScreenParts.TopLeft(_giveRow, 30f, 1010f, 960f, 170f);
            _takeHead = ScreenParts.Label(_root, "TakeHead", look, "Беру", normal, 30f, 1195f, 300f, 50f, TextAlignmentOptions.Left);
            _takeRow = UiKit.MakeRect(_root, "TakeRow");
            ScreenParts.TopLeft(_takeRow, 30f, 1245f, 960f, 170f);
            _reaction = ScreenParts.Label(_root, "Reaction", look, "", normal, 30f, 1440f, 960f, 90f);
            _deal = UiKit.MakeButton(_root, "Deal", look, "Меняю", new Vector2(460f, 130f), () => Deal());
            ScreenParts.TopLeft((RectTransform)_deal.transform, 280f, 1560f, 460f, 130f);
            var dealLabel = _deal.GetComponentInChildren<TextMeshProUGUI>();
            if (dealLabel != null) dealLabel.fontSize = look != null ? look.TextBig : 60f;
        }

        private void Refresh()
        {
            if (_root == null || _g == null || _trader == null) return;
            Clamp();
            var look = _ui.Look;
            var icons = _art != null ? _art.ItemIcons : null;
            bool coins = _g.Trade.KnowsCoins;
            var cell = new Vector2(144f, 190f);
            const float gap = 11f;

            // left: the trader's goods, then the buyback shelf
            ScreenParts.Clear(_left);
            int n = 0;
            foreach (var s in _g.Trade.Stock(_trader))
            {
                if (s.Count == 0) continue;
                int taken = CountIn(_take, s.Item, false);
                string item = s.Item;
                int shown = s.Count < 0 ? 0 : s.Count - taken;
                string price = coins ? _g.Trade.CoinPrice(_trader, item, 1) + " мон." : null;
                PlaceCell(_left, ScreenParts.Cell(_left, "Stock_" + item, look, icons, item, ScreenParts.ItemName(_g, item), shown, price, cell, () => AddTake(item)), n++, cell, gap);
            }
            if (coins)
                PlaceCell(_left, ScreenParts.Cell(_left, "Stock_coin", look, icons, "coin", ScreenParts.ItemName(_g, "coin"), 0, null, cell, () => AddTake("coin")), n++, cell, gap);
            foreach (var b in _g.Trade.Buyback(_trader))
            {
                int taken = CountIn(_take, b.Item, true);
                string item = b.Item;
                string price = coins ? Mathf.CeilToInt(_g.Trade.BuybackPrice(_trader, item)) + " мон." : null;
                var button = ScreenParts.Cell(_left, "Buyback_" + item, look, icons, item, ScreenParts.ItemName(_g, item), b.Count - taken, price, cell, () => AddTake(item, true));
                PlaceCell(_left, button, n++, cell, gap);
            }
            ScreenParts.SetContentHeight(_left, ((n + 2) / 3) * (cell.y + gap));

            // right: the hero's bag, less what lies on the table
            ScreenParts.Clear(_right);
            var ids = new List<string>();
            foreach (var st in _g.Bag.Stacks) if (!ids.Contains(st.ItemId)) ids.Add(st.ItemId);
            int m = 0;
            foreach (var id in ids)
            {
                if (!Visible(id)) continue;
                int left = _g.Bag.Count(id) - CountIn(_give, id, false);
                if (left <= 0) continue;
                string item = id;
                bool unwanted = _g.Trade.BuyValue(_trader, item) <= 0f;
                PlaceCell(_right, ScreenParts.Cell(_right, "Bag_" + item, look, icons, item, ScreenParts.ItemName(_g, item), left, null, cell, () => AddGive(item), unwanted), m++, cell, gap);
            }
            ScreenParts.SetContentHeight(_right, ((m + 2) / 3) * (cell.y + gap));

            // the table
            FillRow(_giveRow, _give, look, icons, line => RemoveGive(line.Item));
            FillRow(_takeRow, _take, look, icons, line => RemoveTake(line.Item, line.FromBuyback));

            var result = _g.Trade.Evaluate(_trader, Offer());
            _lastOutcome = result.Outcome;
            _reaction.text = ReactionOf(result, _give.Count == 0 && _take.Count == 0);
            if (_reaction.text.Length > 0) _reaction.text = "<i>" + _reaction.text + "</i>";
            bool ok = result.Outcome == TradeOutcome.Done;
            _deal.interactable = ok;
            _deal.targetGraphic.color = ok ? (look != null ? look.PaperColor : UiKit.DefaultPaper) : new Color(0.62f, 0.58f, 0.5f, 0.8f);
        }

        private static void PlaceCell(RectTransform parent, Button b, int index, Vector2 cell, float gap)
        {
            ScreenParts.TopLeft((RectTransform)b.transform, (index % 3) * (cell.x + gap), (index / 3) * (cell.y + gap), cell.x, cell.y);
        }

        private void FillRow(RectTransform row, List<TradeLine> lines, UiLook look, IconRegistry icons, Action<TradeLine> onTap)
        {
            ScreenParts.Clear(row);
            float w = Mathf.Min(130f, (960f - 10f * Mathf.Max(0, lines.Count - 1)) / Mathf.Max(1, lines.Count));
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var b = ScreenParts.Cell(row, "Line_" + line.Item, look, icons, line.Item, ScreenParts.ItemName(_g, line.Item), line.Count, null, new Vector2(w, 170f), () => onTap(line));
                ScreenParts.TopLeft((RectTransform)b.transform, i * (w + 10f), 0f, w, 170f);
            }
        }
    }
}
