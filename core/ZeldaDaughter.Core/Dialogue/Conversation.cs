#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.Language;

namespace ZeldaDaughter.Core.Dialogue
{
    /// <summary>data/dialogues.json (C-15).</summary>
    public sealed class DialogueSettings
    {
        public List<string> Icons { get; set; } = new List<string>();
        public Dictionary<string, NpcDialogue> Npcs { get; set; } = new Dictionary<string, NpcDialogue>();
    }

    public sealed class NpcDialogue
    {
        public string Name { get; set; } = "";
        public Dictionary<string, DialogueNode> Nodes { get; set; } = new Dictionary<string, DialogueNode>();
    }

    public sealed class DialogueNode
    {
        /// <summary>What the NPC says — shown through the hero's understanding (C-11).</summary>
        public string Line { get; set; } = "";
        public List<string> Icons { get; set; } = new List<string>();
        public List<DialogueReply> Replies { get; set; } = new List<DialogueReply>();
        public Gesture? Gesture { get; set; }
        public bool End { get; set; }
        /// <summary>What reaching this node does in the world (D-04): runs each time the node is entered; the effects are idempotent.</summary>
        public List<DialogueEffect> Effects { get; set; } = new List<DialogueEffect>();
    }

    /// <summary>mark (open a map mark) | note (write a notebook entry) | offer (a resident asks for something: a request) | teach_coins (someone explains coins — only after a first barter).</summary>
    public sealed class DialogueEffect
    {
        public static readonly string[] Types = { "mark", "note", "offer", "teach_coins" };
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
    }

    public sealed class DialogueReply
    {
        public string Icon { get; set; } = "";
        public string To { get; set; } = "";
    }

    public sealed class Gesture
    {
        /// <summary>point | shrug | wave …</summary>
        public string Type { get; set; } = "";
        /// <summary>Scene object id the NPC points at.</summary>
        public string Target { get; set; } = "";
    }

    /// <summary>What the speech bubble shows now.</summary>
    public readonly struct DialogueView
    {
        public readonly string Text;
        public readonly IReadOnlyList<string> Icons;
        public readonly IReadOnlyList<string> Replies;
        public readonly Gesture? Gesture;

        public DialogueView(string text, IReadOnlyList<string> icons, IReadOnlyList<string> replies, Gesture? gesture)
        {
            Text = text; Icons = icons; Replies = replies; Gesture = gesture;
        }
    }

    public readonly struct ReplyResult
    {
        public readonly bool Accepted;
        /// <summary>The NPC did not get the hero's icon (§3 «провалы коммуникации») — try another way.</summary>
        public readonly bool Misunderstood;

        public ReplyResult(bool accepted, bool misunderstood) { Accepted = accepted; Misunderstood = misunderstood; }
    }

    /// <summary>
    /// A conversation with one NPC (project-design.md §2–3): the NPC's line through the hero's understanding plus icons,
    /// the hero answers with icons, the NPC may not understand. Every line heard teaches the language.
    /// </summary>
    public sealed class Conversation
    {
        readonly NpcDialogue _npc;
        readonly Comprehension _lang;
        readonly string _npcId;
        readonly Action<DialogueEffect>? _onEffect;

        /// <param name="onEffect">Called for each effect of every node reached (including «start»); GameState.Talk wires it to the map, notebook, requests and trade.</param>
        /// <param name="startNode">Where the talk begins; «start» by default. A thank-you after a handed-over request begins at its own node (C9).</param>
        public Conversation(DialogueSettings data, Comprehension language, string npcId, Action<DialogueEffect>? onEffect = null, string startNode = "start")
        {
            _onEffect = onEffect;
            if (data == null) throw new ArgumentNullException(nameof(data));
            _lang = language ?? throw new ArgumentNullException(nameof(language));
            _npc = data.Npcs.TryGetValue(npcId, out var n) ? n : throw new ArgumentException($"dialogues.json: no npc '{npcId}'", nameof(npcId));
            _npcId = npcId;
            if (!_npc.Nodes.ContainsKey(startNode)) throw new ArgumentException($"dialogues.json: '{npcId}' has no node '{startNode}'", nameof(startNode));
            Go(startNode);
        }

        public string NodeId { get; private set; } = "";
        public bool Ended => _npc.Nodes[NodeId].End;

        public DialogueView Current
        {
            get
            {
                var node = _npc.Nodes[NodeId];
                var replies = new List<string>();
                foreach (var r in node.Replies) replies.Add(r.Icon);
                return new DialogueView(_lang.Render(node.Line, _npcId), node.Icons, replies, node.Gesture);
            }
        }

        /// <param name="roll">Uniform 0..1 from the caller's seeded random — whether the NPC gets the icon.</param>
        public ReplyResult Reply(string icon, double roll)
        {
            var node = _npc.Nodes[NodeId];
            var reply = node.Replies.Find(r => r.Icon == icon);
            if (reply == null) return new ReplyResult(false, false);
            if (!_lang.HeroUnderstood(roll)) return new ReplyResult(true, true);
            Go(reply.To);
            return new ReplyResult(true, false);
        }

        void Go(string node)
        {
            NodeId = node;
            _lang.Heard(_npcId, node);
            if (_onEffect != null)
                foreach (var e in _npc.Nodes[node].Effects) _onEffect(e);
        }
    }
}
