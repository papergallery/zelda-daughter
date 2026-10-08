using UnityEngine;
using ZeldaDaughter.Core.Dialogue;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// A conversation with a resident (D-12, project-design.md §3). A tap on a resident → <c>g.Talk(npc)</c>, so what the lines do — marks on the
    /// map, notes, requests, the coin lesson — happens in the core; the thanks for a handed-over request start by themselves at the node named in
    /// <c>quests.json</c>. <see cref="TalkBubbleView"/> draws it: her words through the hero's understanding with icons, the hero's answers as
    /// icon buttons, «?» when she does not get the icon, the trade icon while her shop is open. A line with a gesture turns her to the object
    /// (<see cref="SessionEvents.NpcGesture"/>). The talk breaks off if she goes to sleep or the hero walks away.
    /// </summary>
    public sealed class TalkPresenter : MonoBehaviour
    {
        /// <summary>The hero who walks this many metres farther from the resident than she was when the talk began (at least the «near» radius, <c>session.json tappableHintRadius</c>) has left it.</summary>
        public const float LeaveMeters = 12f;
        private static readonly string[] NoIcons = new string[0];

        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private TalkBubbleView _bubble;
        [SerializeField] private NpcPresenter _npcs;

        private GameState _g;
        private Conversation _talk;
        private string _npc;
        private Transform _npcTransform;
        private Tappable _npcTappable;
        private float _leaveAt;

        public void Configure(GameSession session, SessionUI ui)
        {
            _session = session;
            _ui = ui;
        }

        /// <summary>D-12's SceneBuilder part gives the presenter its view and the residents' presenter (the shop is open only while she stands at it).</summary>
        public void Wire(TalkBubbleView bubble, NpcPresenter npcs)
        {
            _bubble = bubble;
            _npcs = npcs;
        }

        /// <summary>A talk is going on (the last node has not been reached).</summary>
        public bool Active => _talk != null;
        public string NpcId => _npc;
        public string NodeId => _talk?.NodeId;

        private void OnEnable()
        {
            _session.Events.StateReady += OnReady;
            _session.Events.QuestDone += OnQuestDone;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnReady;
            _session.Events.QuestDone -= OnQuestDone;
        }

        private void Start()
        {
            _session.OnTap(TapKind.Npc, OnTapNpc);
            _session.SetReplyHandler(Reply);
        }

        private void OnReady(GameState g) => _g = g;

        private void Update()
        {
            if (_talk == null) return;
            bool gone = _npcTappable == null || !_npcTappable.Enabled;
            float away = Vector3.Distance(_npcTransform.position, _session.Hero.transform.position);
            if (gone || away > _leaveAt) Break(gone ? "she is gone" : "the hero left");
        }

        // ------------------------------------------------------------------ begin

        private void OnTapNpc(Tappable npc) => Begin(KeyOf(npc.Id), "start", npc);

        private static string KeyOf(string objectId) => objectId.StartsWith("npc_") ? objectId.Substring(4) : objectId;

        /// <summary>The request was handed over (<c>Quests.Give</c> raised <c>QuestDone</c>): the receiver says thanks at her own node.</summary>
        private void OnQuestDone(string questId)
        {
            if (_g == null || !_g.Data.Quests.Quests.TryGetValue(questId, out var q) || string.IsNullOrEmpty(q.Thanks)) return;
            var t = _session.Index.FindTappable("npc_" + q.Receiver);
            if (t == null || !t.Enabled || !t.gameObject.activeInHierarchy) return;
            Begin(q.Receiver, q.Thanks, t);
        }

        private void Begin(string key, string startNode, Tappable npc)
        {
            if (_g == null || !_g.Data.Dialogues.Npcs.ContainsKey(key)) return;
            if (_talk != null) Break("another talk");
            _talk = _g.Talk(key, startNode);
            _npc = key;
            _npcTransform = npc.transform;
            _npcTappable = npc;
            _leaveAt = Mathf.Max(Vector3.Distance(npc.transform.position, _session.Hero.transform.position), _g.Data.Session.TappableHintRadius) + LeaveMeters;
            _session.Events.RaiseTalkStarted(key);
            Show();
        }

        // ------------------------------------------------------------------ the hero answers

        private void Reply(string icon)
        {
            if (_talk == null) return;
            var r = _talk.Reply(icon, _session.Rolls.Talk.Next());
            if (!r.Accepted) return;
            if (r.Misunderstood)
            {
                ZdLog.Info("Talk", $"{_npc} did not understand [{icon}]");
                _bubble.ShowQuestion(_npcTransform);
                return;
            }
            Show();
        }

        // ------------------------------------------------------------------ show

        private void Show()
        {
            var v = _talk.Current;
            var icons = _g.Language.ShowIcons ? (System.Collections.Generic.IReadOnlyList<string>)v.Icons : NoIcons;
            _bubble.ShowNpc(_npcTransform, v.Text, icons);
            ZdLog.Info("Talk", $"{_npc} → {_talk.NodeId}");

            if (!_talk.Ended)
            {
                _bubble.ShowReplies(v.Replies, _session.Reply, TradeAction());
                return;
            }

            string ended = _npc;
            _bubble.ShowReplies(NoIcons, null, TradeAction()); // the answers go; the trade icon stays with the bubble
            _bubble.Linger(_g.Data.Session.NpcBubbleSeconds);
            if (v.Gesture != null) MakeGesture(v.Gesture);
            _talk = null;
            _session.Events.RaiseTalkEnded(ended);
        }

        private System.Action TradeAction()
        {
            if (_npcs == null || !_npcs.ShopOpen(_npc)) return null;
            string who = _npc;
            return () => _session.Events.RaiseTradeRequested(who, null);
        }

        private void MakeGesture(Gesture gesture)
        {
            var target = _session.Index.Find(gesture.Target);
            if (target == null) return;
            ZdLog.Info("Talk", $"{_npc} {gesture.Type} {gesture.Target}");
            _session.Events.RaiseNpcGesture(_npc, gesture.Type, gesture.Target);
        }

        /// <summary>The talk stops without an ending: the bubble goes at once.</summary>
        private void Break(string why)
        {
            ZdLog.Info("Talk", $"{_npc} talk broken: {why}");
            string ended = _npc;
            _talk = null;
            _bubble.Hide();
            if (_ui != null) _ui.HideReplies();
            _session.Events.RaiseTalkEnded(ended);
        }
    }
}
