using System.Linq;
using UnityEngine;
using ZeldaDaughter.Core.Dialogue;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// A conversation with an NPC (T-10, moved out of the session). Tap on an NPC → <c>g.Talk(npc)</c> (so what the lines do — marks on the map,
    /// notes, requests, the coin lesson — happens in the core); replies come as icons; the NPC's bubble shows gibberish, a mix or words by how much
    /// the hero understands. W0 keeps T-10's plain bubble and text buttons; D-12 rewrites the view (icons, «?», the trade icon).
    /// </summary>
    public sealed class TalkPresenter : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        private GameState _g;
        private Conversation _talk;
        private string _npc;
        private Transform _npcTransform;

        public void Configure(GameSession session, SessionUI ui)
        {
            _session = session;
            _ui = ui;
        }

        private void OnEnable() => _session.Events.StateReady += OnReady;

        private void OnDisable()
        {
            if (_session != null) _session.Events.StateReady -= OnReady;
        }

        private void Start()
        {
            _session.OnTap(TapKind.Npc, Begin);
            _session.SetReplyHandler(Reply);
        }

        private void OnReady(GameState g) => _g = g;

        private void Begin(Tappable npc)
        {
            string key = npc.Id.StartsWith("npc_") ? npc.Id.Substring(4) : npc.Id;
            if (!_g.Data.Dialogues.Npcs.ContainsKey(key)) return;
            _talk = _g.Talk(key);
            _npc = key;
            _npcTransform = npc.transform;
            _session.Events.RaiseTalkStarted(key);
            Show();
        }

        private void Reply(string icon)
        {
            if (_talk == null) return;
            var r = _talk.Reply(icon, _session.Rolls.Talk.Next());
            if (!r.Accepted) return;
            if (r.Misunderstood)
            {
                ZdLog.Info("Talk", $"{_npc} did not understand [{icon}]");
                _ui.NpcSay(_npcTransform, "?", _g.Data.Session.NpcBubbleSeconds);
                return;
            }
            Show();
        }

        private void Show()
        {
            var v = _talk.Current;
            string icons = string.Join(" ", v.Icons.Select(i => "[" + i + "]"));
            _ui.NpcSay(_npcTransform, v.Text + "\n" + icons, _g.Data.Session.NpcBubbleSeconds);
            ZdLog.Info("Talk", $"{_npc} → {_talk.NodeId}");
            if (_talk.Ended)
            {
                _ui.HideReplies();
                var target = v.Gesture != null ? _session.Index.Find(v.Gesture.Target) : null;
                if (target != null)
                {
                    var look = target.transform.position - _npcTransform.position;
                    look.y = 0;
                    if (look.sqrMagnitude > 0.01f) _npcTransform.rotation = Quaternion.LookRotation(look);
                    ZdLog.Info("Talk", $"{_npc} {v.Gesture.Type} {v.Gesture.Target}");
                }
                string ended = _npc;
                _talk = null;
                _session.Events.RaiseTalkEnded(ended);
                return;
            }
            _ui.ShowReplies(v.Replies, _session.Reply);
        }
    }
}
