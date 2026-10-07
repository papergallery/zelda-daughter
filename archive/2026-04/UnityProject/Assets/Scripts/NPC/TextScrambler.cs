using System.Collections.Generic;
using System.Text;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// Заменяет слова в тексте на рунические глифы в зависимости от уровня понимания языка.
    /// Детерминирован: одинаковый текст + comprehension всегда даёт один результат.
    /// </summary>
    public class TextScrambler
    {
        private readonly string[] _glyphs;

        public TextScrambler(string[] glyphs)
        {
            _glyphs = glyphs;
        }

        public string Scramble(string original, float comprehension, float scrambleThreshold, float partialThreshold)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            if (comprehension >= partialThreshold)
                return original;

            // Разбиваем на токены: слова и разделители (пробелы, пунктуация)
            var tokens = Tokenize(original);

            // Собираем список слов (только текстовые токены, не разделители)
            var wordIndices = new List<int>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].IsWord)
                    wordIndices.Add(i);
            }

            if (wordIndices.Count == 0)
                return original;

            // Полный скрэмбл: все слова → руны
            if (comprehension < scrambleThreshold)
            {
                int baseSeed = GetDeterministicSeed(original);
                var sb = new StringBuilder();
                foreach (var token in tokens)
                {
                    if (token.IsWord)
                        sb.Append(WordToGlyphs(token.Text, baseSeed));
                    else
                        sb.Append(token.Text);
                }
                return sb.ToString();
            }

            // Частичное понимание: линейная интерполяция между scrambleThreshold и partialThreshold
            float revealRatio = (comprehension - scrambleThreshold) / (partialThreshold - scrambleThreshold);
            int revealCount = (int)(wordIndices.Count * revealRatio);

            // Сортируем слова по приоритету раскрытия: короткие (≤3 символов) раскрываются первыми
            // При равной длине — стабильный порядок по позиции
            var sortedWordIndices = new List<int>(wordIndices);
            sortedWordIndices.Sort((a, b) =>
            {
                int lenA = tokens[a].Text.Length;
                int lenB = tokens[b].Text.Length;
                bool shortA = lenA <= 3;
                bool shortB = lenB <= 3;
                if (shortA != shortB)
                    return shortA ? -1 : 1;
                return a.CompareTo(b);
            });

            // Помечаем, какие слова раскрываем
            var revealedIndices = new HashSet<int>();
            for (int i = 0; i < revealCount && i < sortedWordIndices.Count; i++)
                revealedIndices.Add(sortedWordIndices[i]);

            int seed = GetDeterministicSeed(original);
            var result = new StringBuilder();
            foreach (var token in tokens)
            {
                if (!token.IsWord)
                {
                    result.Append(token.Text);
                    continue;
                }

                if (revealedIndices.Contains(token.OriginalIndex))
                    result.Append(token.Text);
                else
                    result.Append(WordToGlyphs(token.Text, seed));
            }

            return result.ToString();
        }

        private string WordToGlyphs(string word, int seed)
        {
            if (_glyphs == null || _glyphs.Length == 0)
                return word;

            var sb = new StringBuilder(word.Length);
            for (int i = 0; i < word.Length; i++)
            {
                // Детерминированный хеш: сочетание seed, позиции символа и символа
                int hash = unchecked(seed * 31 + i * 1000003 + word[i]);
                int index = ((hash % _glyphs.Length) + _glyphs.Length) % _glyphs.Length;
                sb.Append(_glyphs[index]);
            }
            return sb.ToString();
        }

        // Собственный хеш без зависимости от платформы (GetHashCode не детерминирован между сессиями)
        private static int GetDeterministicSeed(string text)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        private struct Token
        {
            public string Text;
            public bool IsWord;
            public int OriginalIndex; // индекс слова в общем списке слов (для HashSet)
        }

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            int wordIndex = 0;
            int i = 0;

            while (i < text.Length)
            {
                if (char.IsLetter(text[i]))
                {
                    int start = i;
                    while (i < text.Length && char.IsLetter(text[i]))
                        i++;
                    tokens.Add(new Token
                    {
                        Text = text.Substring(start, i - start),
                        IsWord = true,
                        OriginalIndex = wordIndex++
                    });
                }
                else
                {
                    int start = i;
                    while (i < text.Length && !char.IsLetter(text[i]))
                        i++;
                    tokens.Add(new Token
                    {
                        Text = text.Substring(start, i - start),
                        IsWord = false,
                        OriginalIndex = -1
                    });
                }
            }

            return tokens;
        }
    }
}
