using System.IO;
using System.Linq;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-04: project-design.md §4 (map bought, filled by talking) and §5 (notebook in her own words, no tracker, requests from residents).</summary>
    public class JournalTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState Fresh() => new GameState(D);

        static void Say(GameState g, string npc, params string[] icons)
        {
            var c = g.Talk(npc);
            foreach (var icon in icons) Assert.False(c.Reply(icon, 0.0).Misunderstood);
        }

        // --- map ---

        [Fact]
        public void Without_a_map_marks_pile_up_unseen_and_show_after_the_purchase()
        {
            var g = Fresh();
            Assert.False(g.Map.HasMap);
            Say(g, "merchant", "town");
            Assert.Contains("smithy", g.Map.Known);
            Assert.Empty(g.Map.VisibleMarks);
            g.Bag.Add("map");
            Assert.True(g.Map.HasMap);
            Assert.Contains("smithy", g.Map.VisibleMarks);
            Assert.Equal(g.Map.Known.Count, g.Map.VisibleMarks.Count);
        }

        [Fact]
        public void A_mark_opens_once_and_selling_the_map_does_not_forget_it()
        {
            var g = Fresh();
            var opened = new System.Collections.Generic.List<string>();
            g.Map.MarkOpened += opened.Add;
            Assert.True(g.Map.Open("tavern"));
            Assert.False(g.Map.Open("tavern"));
            Assert.False(g.Map.Open("no_such_place"));
            Assert.Equal(new[] { "tavern" }, opened);
            g.Bag.Add("map");
            g.Bag.Remove("map");
            Assert.Empty(g.Map.VisibleMarks);
            g.Bag.Add("map");
            Assert.Contains("tavern", g.Map.VisibleMarks);
        }

        // --- notebook ---

        [Fact]
        public void A_conversation_writes_a_notebook_entry_and_it_stays()
        {
            var g = Fresh();
            var seen = new System.Collections.Generic.List<string>();
            g.Notebook.EntryAdded += e => seen.Add(e.Id);
            Say(g, "peasant", "town");
            Assert.Contains(g.Notebook.Entries, e => e.Id == "peasant_town");
            Assert.Equal(new[] { "peasant_town" }, seen);
            var e1 = g.Notebook.Entries.Single(e => e.Id == "peasant_town");
            Assert.Equal("peasant", e1.Who);
            Assert.False(string.IsNullOrEmpty(e1.Text));
            Say(g, "peasant", "town");                   // asking again: no duplicate
            Assert.Single(g.Notebook.Entries, e => e.Id == "peasant_town");
        }

        [Fact]
        public void Notebook_has_no_statuses_and_nothing_reminds_about_requests()
        {
            var t = typeof(NotebookEntry);
            Assert.DoesNotContain(t.GetProperties(), p => p.Name.Contains("Done") || p.Name.Contains("Status") || p.Name.Contains("Complete"));
        }

        // --- requests ---

        [Fact]
        public void A_resident_asks_for_berries_and_a_handed_over_bunch_pays()
        {
            var g = Fresh();
            Say(g, "townswoman", "food");
            Assert.Contains(g.Notebook.Entries, e => e.Id == "quest_berries");
            Assert.True(g.Quests.IsOffered("berries"));
            g.Bag.Add("berries", 4);
            Assert.Equal(QuestOutcome.NotEnough, g.Quests.Give("townswoman", "berries").Outcome);
            Assert.Equal(4, g.Bag.Count("berries"));
            g.Bag.Add("berries");
            var r = g.Quests.Give("townswoman", "berries");
            Assert.Equal(QuestOutcome.Done, r.Outcome);
            Assert.Equal("berries", r.QuestId);
            Assert.Equal(0, g.Bag.Count("berries"));
            Assert.Equal(D.Quests.Quests["berries"].Reward.Items["coin"], g.Bag.Count("coin"));
            Assert.True(g.Quests.IsDone("berries"));
            Assert.Contains(g.Notebook.Entries, e => e.Id == "quest_berries"); // the note stays
            g.Bag.Add("berries", 5);
            Assert.Equal(QuestOutcome.NoQuest, g.Quests.Give("townswoman", "berries").Outcome); // once only
        }

        [Fact]
        public void The_wrong_person_or_no_request_takes_nothing()
        {
            var g = Fresh();
            g.Bag.Add("berries", 5);
            Assert.Equal(QuestOutcome.NoQuest, g.Quests.Give("townswoman", "berries").Outcome); // not asked yet
            Say(g, "townswoman", "food");
            Assert.Equal(QuestOutcome.NoQuest, g.Quests.Give("smith", "berries").Outcome);
            Assert.Equal(5, g.Bag.Count("berries"));
        }

        [Fact]
        public void A_lost_thing_found_before_being_asked_is_still_returned()
        {
            var g = Fresh();
            g.Bag.Add("locket");
            var r = g.Quests.Give("old_man", "locket");
            Assert.Equal(QuestOutcome.Done, r.Outcome);
            Assert.Contains("smithy", g.Map.Known);
        }

        [Fact]
        public void A_letter_is_handed_over_on_asking_and_delivered_to_someone_else()
        {
            var g = Fresh();
            Say(g, "weaver", "hand");
            Assert.Equal(1, g.Bag.Count("letter"));
            Assert.Equal(QuestOutcome.NoQuest, g.Quests.Give("weaver", "letter").Outcome);   // not to herself
            var r = g.Quests.Give("peasant", "letter");
            Assert.Equal(QuestOutcome.Done, r.Outcome);
            Assert.Equal(0, g.Bag.Count("letter"));
            Say(g, "weaver", "hand");                                                        // asked again after it's done: no second letter
            Assert.Equal(0, g.Bag.Count("letter"));
        }

        [Fact]
        public void A_request_is_not_accepted_without_room_for_what_it_hands_over_and_can_be_asked_again()
        {
            var g = Fresh();
            foreach (var id in D.Items.Keys.Where(k => k != "letter")) { if (g.Bag.UsedSlots >= D.Inventory.Slots) break; g.Bag.Add(id); }
            Say(g, "weaver", "hand");
            Assert.False(g.Quests.IsOffered("letter"));
            Assert.DoesNotContain(g.Notebook.Entries, e => e.Id == "quest_letter");
            g.Bag.Remove("stick");
            Say(g, "weaver", "hand");
            Assert.True(g.Quests.IsOffered("letter"));
        }

        [Fact]
        public void A_reward_that_does_not_fit_leaves_everything_as_it_was()
        {
            var g = Fresh();
            Say(g, "townswoman", "food");
            foreach (var id in D.Items.Values.Where(i => i.Id != "berries" && i.Id != "coin" && i.Kind != "quest").Take(18)) g.Bag.Add(id.Id);
            g.Bag.Add("berries", 20);                 // two full stacks: handing over 5 frees no slot
            Assert.Equal(D.Inventory.Slots, g.Bag.UsedSlots);
            var before = SaveGame.Capture(g);
            Assert.Equal(QuestOutcome.NoRoom, g.Quests.Give("townswoman", "berries").Outcome);
            Assert.Equal(before, SaveGame.Capture(g));
            Assert.False(g.Quests.IsDone("berries"));
        }

        [Fact]
        public void Quest_completion_can_open_map_marks_and_everything_round_trips()
        {
            var g = Fresh();
            Say(g, "merchant", "town");
            Say(g, "old_man", "question");
            Say(g, "weaver", "hand");
            g.Bag.Add("locket");
            g.Quests.Give("old_man", "locket");
            g.Bag.Add("map");
            string a = SaveGame.Capture(g);
            var fresh = Fresh();
            SaveGame.Restore(fresh, a);
            Assert.Equal(a, SaveGame.Capture(fresh));
            Assert.Equal(g.Map.Known, fresh.Map.Known);
            Assert.Equal(g.Notebook.Entries.Select(e => e.Id), fresh.Notebook.Entries.Select(e => e.Id));
            Assert.True(fresh.Quests.IsDone("locket"));
            Assert.True(fresh.Quests.IsOffered("letter"));
        }

        [Fact]
        public void Saves_of_version_two_load_with_an_empty_journal()
        {
            var json = SaveGame.Capture(Fresh()).Replace($"\"Version\": {SaveGame.Version}", "\"Version\": 2");
            var g = Fresh();
            SaveGame.Restore(g, json);
            Assert.Empty(g.Map.Known);
            Assert.Empty(g.Notebook.Entries);
        }

        [Fact]
        public void Teaching_coins_in_a_conversation_works_only_after_a_first_barter()
        {
            var g = Fresh();
            g.Clock.SetTime(1, 12.0 / 24.0);
            Say(g, "merchant", "coin");
            Assert.False(g.Trade.KnowsCoins);
            g.Bag.Add("fang", 2);
            g.Trade.Execute("merchant", new Economy.TradeOffer(new[] { new Economy.TradeLine("fang", 2) }, new[] { new Economy.TradeLine("cloth", 1) }));
            Say(g, "merchant", "coin");
            Assert.True(g.Trade.KnowsCoins);
        }

        // --- data ---

        [Fact]
        public void Broken_references_are_named()
        {
            var files = Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
            files["dialogues.json"] = files["dialogues.json"].Replace("\"id\": \"smithy\"", "\"id\": \"smithyy\"");
            files["quests.json"] = files["quests.json"].Replace("\"berries\": 5", "\"berriess\": 5");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("smithyy"));
            Assert.Contains(ex.Problems, p => p.Contains("berriess"));
        }

        [Fact]
        public void Every_dialogue_effect_target_exists()
        {
            foreach (var npc in D.Dialogues.Npcs)
                foreach (var node in npc.Value.Nodes.Values)
                    foreach (var e in node.Effects)
                    {
                        switch (e.Type)
                        {
                            case "mark": Assert.True(D.Map.Marks.ContainsKey(e.Id), e.Id); break;
                            case "note": Assert.True(D.Notebook.Entries.ContainsKey(e.Id), e.Id); break;
                            case "offer": Assert.True(D.Quests.Quests.ContainsKey(e.Id), e.Id); break;
                            case "teach_coins": break;
                            default: Assert.Fail("unknown effect " + e.Type); break;
                        }
                    }
        }

        [Fact]
        public void Three_requests_from_residents_exist_and_asking_is_possible_at_every_language_stage()
        {
            Assert.True(D.Quests.Quests.Count >= 3);
            foreach (var q in D.Quests.Quests)
                Assert.Contains(D.Dialogues.Npcs[q.Value.Giver].Nodes.Values, n => n.Effects.Any(e => e.Type == "offer" && e.Id == q.Key));
        }

        [Fact]
        public void Map_marks_and_quest_objects_exist_in_the_region_scene()
        {
            var path = Path.Combine(Directory.GetParent(TestPaths.CoreRoot)!.FullName, "scenes", "region.json");
            if (!File.Exists(path)) return; // scenes/region.json comes with D-10; until then the contract is data/map.json (object) and the item below
            var objs = SceneConfig.Parse(File.ReadAllText(path)).Objects;
            var ids = objs.Select(o => o.Id).ToHashSet();
            foreach (var m in D.Map.Marks) Assert.True(ids.Contains(m.Value.Object), $"scenes/region.json: нет объекта '{m.Value.Object}' для метки '{m.Key}' (data/map.json)");
            foreach (var q in D.Quests.Quests)
                foreach (var item in q.Value.Need.Keys.Where(k => !q.Value.HandOut.ContainsKey(k) && D.Items[k].Kind == "quest"))
                    Assert.True(objs.Any(o => o.Item == item), $"scenes/region.json: нет объекта с item '{item}' для поручения '{q.Key}'");
        }
    }
}
