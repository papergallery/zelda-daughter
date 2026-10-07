using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Dialogue;
using ZeldaDaughter.Core.Language;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-15: project-design.md §2 «Пролог», §3 этап 1 и «провалы коммуникации».</summary>
    public class ConversationTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        [Fact]
        public void Stage_one_shows_gibberish_and_icons_and_offers_icon_replies()
        {
            var lang = new Comprehension(D.Language);
            var c = new Conversation(D.Dialogues, lang, "peasant");
            var v = c.Current;
            Assert.NotEqual(D.Dialogues.Npcs["peasant"].Nodes["start"].Line, v.Text);
            Assert.Contains("question", v.Icons);
            Assert.Contains("town", v.Replies);
        }

        [Fact]
        public void Misunderstood_reply_keeps_the_node_and_another_try_moves_on()
        {
            var lang = new Comprehension(D.Language);
            var c = new Conversation(D.Dialogues, lang, "peasant");
            var r = c.Reply("town", roll: 0.99); // at zero understanding the NPC gets it half the time
            Assert.True(r.Misunderstood);
            Assert.Equal("start", c.NodeId);
            r = c.Reply("town", roll: 0.1);
            Assert.False(r.Misunderstood);
            Assert.Equal("town", c.NodeId);
            Assert.True(c.Ended);
            Assert.Equal("point", c.Current.Gesture?.Type);
            Assert.Equal("town_gate", c.Current.Gesture?.Target);
        }

        [Fact]
        public void Talking_teaches_the_language()
        {
            var lang = new Comprehension(D.Language);
            var c = new Conversation(D.Dialogues, lang, "peasant");
            float before = lang.Understanding;
            c.Reply("food", 0.1);
            Assert.True(lang.Understanding > before);
        }

        [Fact]
        public void Reply_not_offered_is_ignored()
        {
            var c = new Conversation(D.Dialogues, new Comprehension(D.Language), "guard");
            var r = c.Reply("coin", 0.1);
            Assert.False(r.Accepted);
            Assert.Equal("start", c.NodeId);
        }

        [Fact]
        public void Dialogue_data_is_consistent()
        {
            foreach (var npc in D.Dialogues.Npcs)
            {
                Assert.True(npc.Value.Nodes.ContainsKey("start"), npc.Key);
                foreach (var node in npc.Value.Nodes.Values)
                {
                    Assert.All(node.Icons, i => Assert.Contains(i, D.Dialogues.Icons));
                    Assert.All(node.Replies, r => Assert.True(npc.Value.Nodes.ContainsKey(r.To), $"{npc.Key}: {r.To}"));
                    Assert.True(node.End || node.Replies.Count > 0, $"{npc.Key}: dead end without end flag");
                }
            }
        }

        [Fact]
        public void At_stage_three_the_line_reads_as_written()
        {
            var lang = new Comprehension(D.Language);
            lang.Restore(1f);
            var c = new Conversation(D.Dialogues, lang, "guard");
            Assert.Equal(D.Dialogues.Npcs["guard"].Nodes["start"].Line, c.Current.Text);
        }
    }
}
