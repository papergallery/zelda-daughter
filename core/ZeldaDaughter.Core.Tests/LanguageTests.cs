using System.Linq;
using ZeldaDaughter.Core.Language;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C-11: project-design.md §3 «Языковая механика».</summary>
    public class LanguageTests
    {
        static readonly LanguageSettings S = TestData.Load<LanguageSettings>("language.json");
        const string Line = "Город там, за полем. Иди по дороге и не сворачивай!";

        [Fact]
        public void Starts_with_no_understanding_icons_and_gibberish()
        {
            var l = new Comprehension(S);
            Assert.Equal(1, l.Stage);
            Assert.True(l.ShowIcons);
            var s = l.Render(Line, "peasant");
            Assert.DoesNotContain("Город", s);
            Assert.Contains(",", s);
            Assert.Contains("!", s);
        }

        [Fact]
        public void Stage_two_comes_after_two_or_three_npcs()
        {
            var l = new Comprehension(S);
            Talk(l, "peasant", 3);
            Assert.Equal(1, l.Stage);
            Talk(l, "guard", 3);
            Talk(l, "merchant", 3);
            Assert.Equal(2, l.Stage);
        }

        [Fact]
        public void Repeating_one_line_does_not_teach()
        {
            // April: +0.02 for every line, so one line could be ground forever.
            var l = new Comprehension(S);
            l.Heard("peasant", "line_1");
            float once = l.Understanding;
            for (int i = 0; i < 100; i++) l.Heard("peasant", "line_1");
            Assert.Equal(once, l.Understanding);
        }

        [Fact]
        public void Same_word_same_npc_same_gibberish_and_punctuation_kept()
        {
            var l = new Comprehension(S);
            Assert.Equal(l.Render(Line, "peasant"), l.Render(Line, "peasant"));
            Assert.NotEqual(l.Render(Line, "peasant"), l.Render(Line, "guard"));
            var words = Line.Split(' ');
            var scrambled = l.Render(Line, "peasant").Split(' ');
            Assert.Equal(words.Length, scrambled.Length);
            Assert.Equal(words.Select(w => w.Length), scrambled.Select(w => w.Length));
        }

        [Fact]
        public void Short_words_come_through_first_and_all_at_stage_three()
        {
            var l = new Comprehension(S);
            l.Restore((S.Stage2At + S.Stage3At) / 2);
            var s = l.Render(Line, "peasant");
            Assert.Contains(" и ", s);           // 1-letter word
            Assert.DoesNotContain("сворачивай", s); // long word still hidden
            l.Restore(S.Stage3At);
            Assert.Equal(Line, l.Render(Line, "peasant"));
        }

        [Fact]
        public void Understood_share_grows_with_understanding()
        {
            var l = new Comprehension(S);
            int Clear(float u) { l.Restore(u); var s = l.Render(Line, "peasant"); return Line.Split(' ').Zip(s.Split(' ')).Count(p => p.First == p.Second); }
            Assert.True(Clear(0.35f) <= Clear(0.5f));
            Assert.True(Clear(0.5f) <= Clear(0.65f));
            Assert.True(Clear(0.65f) < Clear(0.7f));
        }

        [Fact]
        public void Npc_sometimes_does_not_understand_the_hero_less_often_later()
        {
            var l = new Comprehension(S);
            Assert.False(l.HeroUnderstood(roll: 0.6));  // p = 0.5 at zero understanding
            Assert.True(l.HeroUnderstood(roll: 0.4));
            l.Restore(0.8f);
            Assert.True(l.HeroUnderstood(roll: 0.85));
        }

        [Fact]
        public void Money_is_understood_from_threshold()
        {
            var l = new Comprehension(S);
            Assert.False(l.UnderstandsMoney);
            l.Restore(S.MoneyAt);
            Assert.True(l.UnderstandsMoney);
        }

        static void Talk(Comprehension l, string npc, int lines)
        {
            for (int i = 0; i < lines; i++) l.Heard(npc, $"{npc}_line_{i}");
        }
    }
}
