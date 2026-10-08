using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Save;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C9: data the view needs — how fast residents walk, how far apart grass cells lie, how a resident thanks for a handed-over request.</summary>
    public class C9DataTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        [Fact]
        public void Residents_walk_slower_than_the_hero()
        {
            Assert.InRange(D.Npcs.WalkSpeed, 0.5f, D.Movement.WalkSpeed - 0.1f);
            var g = new GameState(D);
            foreach (var id in g.Npcs.Ids) Assert.Equal(D.Npcs.WalkSpeed, g.Npcs.WalkSpeed(id));
        }

        [Fact]
        public void Grass_cells_lie_close_enough_for_fire_to_travel()
        {
            Assert.True(D.Elements.Grass.CellSpacing > 0f);
            Assert.True(D.Elements.Grass.CellSpacing <= D.Elements.Grass.NeighborDistance, "a grid with this spacing would not be connected");
        }

        [Fact]
        public void Bad_numbers_are_caught_by_the_data_check()
        {
            Assert.Throws<DataException>(() => DataSet.Load(name => TestDataEdit.Edit(name, "npcs.json", "\"walkSpeed\": ", "\"walkSpeed\": 0, \"_was\": ")));
            Assert.Throws<DataException>(() => DataSet.Load(name => TestDataEdit.Edit(name, "elements.json", "\"cellSpacing\": ", "\"cellSpacing\": 99, \"_was\": ")));
        }

        [Fact]
        public void Every_request_has_a_thank_you_line_in_the_receivers_dialogue()
        {
            foreach (var kv in D.Quests.Quests)
            {
                Assert.False(string.IsNullOrEmpty(kv.Value.Thanks), kv.Key);
                var nodes = D.Dialogues.Npcs[kv.Value.Receiver].Nodes;
                Assert.True(nodes.ContainsKey(kv.Value.Thanks), $"{kv.Key}: {kv.Value.Thanks}");
                Assert.True(nodes[kv.Value.Thanks].End, $"{kv.Key}: a thank-you ends the talk");
                Assert.NotEmpty(nodes[kv.Value.Thanks].Icons);
            }
        }

        [Fact]
        public void Handing_over_gives_the_thanks_node_and_the_talk_starts_there()
        {
            var g = new GameState(D);
            g.Quests.Offer("berries");
            g.Bag.Add("berries", 5);
            var r = g.Quests.Give("townswoman", "berries");
            Assert.Equal(QuestOutcome.Done, r.Outcome);
            Assert.Equal(D.Quests.Quests["berries"].Thanks, r.Thanks);

            var talk = g.Talk("townswoman", r.Thanks!);
            Assert.Equal(r.Thanks, talk.NodeId);
            Assert.True(talk.Ended);
            Assert.False(string.IsNullOrEmpty(talk.Current.Text));
        }

        [Fact]
        public void A_failed_handing_over_has_no_thanks()
        {
            var g = new GameState(D);
            g.Quests.Offer("berries");
            g.Bag.Add("berries", 2);
            var r = g.Quests.Give("townswoman", "berries");
            Assert.Equal(QuestOutcome.NotEnough, r.Outcome);
            Assert.Null(r.Thanks);
        }

        [Fact]
        public void Talk_without_a_node_still_starts_at_start()
        {
            var g = new GameState(D);
            Assert.Equal("start", g.Talk("peasant").NodeId);
        }
    }

    static class TestDataEdit
    {
        public static string Edit(string name, string file, string find, string replace)
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.DataRoot, name));
            return name == file ? text.Replace(find, replace) : text;
        }
    }
}
