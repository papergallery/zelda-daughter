using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class LanguageTestFactory
    {
        internal static readonly string[] DefaultGlyphs = new[]
        {
            "ᚠ", "ᚢ", "ᚦ", "ᚨ", "ᚱ", "ᚲ", "ᚷ", "ᚹ", "ᚺ", "ᚾ",
            "ᛁ", "ᛃ", "ᛈ", "ᛊ", "ᛏ", "ᛒ", "ᛖ", "ᛗ", "ᛚ", "ᛞ"
        };

        internal static LanguageConfig CreateConfig(
            string[] glyphs = null,
            float scrambleThreshold = 0.3f,
            float partialThreshold = 0.7f,
            float experiencePerLine = 0.02f,
            float iconModeThreshold = 0.2f,
            float currencyThreshold = 0.5f)
        {
            var config = ScriptableObject.CreateInstance<LanguageConfig>();
            var so = new SerializedObject(config);

            var glyphsArray = so.FindProperty("_glyphs");
            var resolvedGlyphs = glyphs ?? DefaultGlyphs;
            glyphsArray.arraySize = resolvedGlyphs.Length;
            for (int i = 0; i < resolvedGlyphs.Length; i++)
                glyphsArray.GetArrayElementAtIndex(i).stringValue = resolvedGlyphs[i];

            so.FindProperty("_scrambleThreshold").floatValue = scrambleThreshold;
            so.FindProperty("_partialThreshold").floatValue = partialThreshold;
            so.FindProperty("_experiencePerLine").floatValue = experiencePerLine;
            so.FindProperty("_iconModeThreshold").floatValue = iconModeThreshold;
            so.FindProperty("_currencyThreshold").floatValue = currencyThreshold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>
        /// Создаёт LanguageSystem на временном GameObject.
        /// Вызывает Awake через reflection.
        /// </summary>
        internal static (LanguageSystem system, GameObject go) CreateLanguageSystem(LanguageConfig config)
        {
            var go = new GameObject("TestLanguageSystem");
            var system = go.AddComponent<LanguageSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            var awake = typeof(LanguageSystem).GetMethod(
                "Awake",
                BindingFlags.NonPublic | BindingFlags.Instance);
            awake?.Invoke(system, null);

            return (system, go);
        }
    }

    // -------------------------------------------------------------------------
    // TextScrambler Tests
    // -------------------------------------------------------------------------

    public class TextScramblerTests
    {
        private static readonly string[] Glyphs = LanguageTestFactory.DefaultGlyphs;

        [Test]
        public void Scramble_AtZeroComprehension_AllWordsReplaced()
        {
            var scrambler = new TextScrambler(Glyphs);
            string input = "Привет путник";
            string result = scrambler.Scramble(input, 0f, 0.3f, 0.7f);

            // Все символы в словах должны быть заменены на глифы
            // Оригинальных русских букв быть не должно
            foreach (char c in result)
            {
                if (c == ' ') continue;
                Assert.IsFalse(char.IsLetter(c) && c < 0x0500,
                    $"При comprehension=0 обнаружена нескрэмбленная буква '{c}' в результате: '{result}'");
            }
        }

        [Test]
        public void Scramble_AtFullComprehension_TextUnchanged()
        {
            var scrambler = new TextScrambler(Glyphs);
            string input = "Привет, путник!";
            string result = scrambler.Scramble(input, 1f, 0.3f, 0.7f);
            Assert.AreEqual(input, result);
        }

        [Test]
        public void Scramble_PreservesPunctuation()
        {
            var scrambler = new TextScrambler(Glyphs);
            string input = "Привет, путник!";
            string result = scrambler.Scramble(input, 0f, 0.3f, 0.7f);

            // Запятая и восклицательный знак должны сохраниться
            Assert.IsTrue(result.Contains(","), $"Запятая не сохранена в: '{result}'");
            Assert.IsTrue(result.Contains("!"), $"Восклицательный знак не сохранён в: '{result}'");
            // Пробел тоже
            Assert.IsTrue(result.Contains(" "), $"Пробел не сохранён в: '{result}'");
        }

        [Test]
        public void Scramble_IsDeterministic()
        {
            var scramblerA = new TextScrambler(Glyphs);
            var scramblerB = new TextScrambler(Glyphs);
            string input = "Добро пожаловать в деревню";

            string resultA = scramblerA.Scramble(input, 0.1f, 0.3f, 0.7f);
            string resultB = scramblerB.Scramble(input, 0.1f, 0.3f, 0.7f);

            Assert.AreEqual(resultA, resultB, "Два вызова с одними параметрами должны давать одинаковый результат");

            // Повторный вызов на том же экземпляре тоже должен совпадать
            string resultC = scramblerA.Scramble(input, 0.1f, 0.3f, 0.7f);
            Assert.AreEqual(resultA, resultC, "Повторный вызов на том же объекте должен давать тот же результат");
        }

        [Test]
        public void Scramble_PartialComprehension_SomeWordsRevealed()
        {
            var scrambler = new TextScrambler(Glyphs);
            // 4 слова, comprehension=0.5 (между 0.3 и 0.7)
            // revealRatio = (0.5 - 0.3) / (0.7 - 0.3) = 0.5 → раскрыть 2 из 4 слов
            string input = "один два три четыре";
            string result = scrambler.Scramble(input, 0.5f, 0.3f, 0.7f);

            Assert.AreNotEqual(input, result, "При частичном понимании текст не должен быть полным оригиналом");
            Assert.AreNotEqual(
                scrambler.Scramble(input, 0f, 0.3f, 0.7f),
                result,
                "При частичном понимании текст не должен быть полным скрэмблом");
        }

        [Test]
        public void Scramble_ShortWordsRevealedFirst()
        {
            var scrambler = new TextScrambler(Glyphs);
            // "да" (2 символа) и "нет" (3 символа) — короткие слова
            // "понимание" (9 символов) — длинное
            // При comprehension чуть выше scrambleThreshold: раскрывается только 1 слово (короткое)
            // revealRatio = (0.35 - 0.3) / (0.7 - 0.3) = 0.125 → revealCount = floor(3 * 0.125) = 0
            // Попробуем comprehension=0.45: ratio = (0.45-0.3)/(0.7-0.3) = 0.375 → floor(3*0.375)=1 → раскрыть 1 слово
            string input = "да нет понимание";
            string resultPartial = scrambler.Scramble(input, 0.45f, 0.3f, 0.7f);
            string resultFull = scrambler.Scramble(input, 0f, 0.3f, 0.7f);

            // Результат при частичном должен содержать одно из коротких слов ("да" или "нет")
            bool containsShortWord = resultPartial.Contains("да") || resultPartial.Contains("нет");
            Assert.IsTrue(containsShortWord,
                $"При частичном понимании короткое слово должно быть раскрыто, результат: '{resultPartial}'");

            // Длинное слово "понимание" не должно появиться, если не раскрыто короткое первым
            // Убеждаемся, что "понимание" не в результате (оно длиннее → раскрывается позже)
            Assert.IsFalse(resultPartial.Contains("понимание"),
                $"При частичном понимании длинное слово не должно быть раскрыто первым, результат: '{resultPartial}'");
        }

        [Test]
        public void Scramble_EmptyString_ReturnsEmpty()
        {
            var scrambler = new TextScrambler(Glyphs);
            Assert.AreEqual(string.Empty, scrambler.Scramble(string.Empty, 0f, 0.3f, 0.7f));
            Assert.AreEqual(string.Empty, scrambler.Scramble(string.Empty, 1f, 0.3f, 0.7f));
        }
    }

    // -------------------------------------------------------------------------
    // LanguageSystem Tests
    // -------------------------------------------------------------------------

    public class LanguageSystemTests
    {
        private LanguageConfig _config;
        private LanguageSystem _system;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            LanguageSystem.ClearEvents();
            _config = LanguageTestFactory.CreateConfig();
            (_system, _go) = LanguageTestFactory.CreateLanguageSystem(_config);
        }

        [TearDown]
        public void TearDown()
        {
            LanguageSystem.ClearEvents();
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AddDialogueExperience_IncreasesComprehension()
        {
            float before = _system.Comprehension;
            _system.AddDialogueExperience();
            Assert.Greater(_system.Comprehension, before,
                "Comprehension должен вырасти после AddDialogueExperience");
        }

        [Test]
        public void ProcessText_DelegatesToScrambler()
        {
            // При comprehension=0 текст должен быть скрэмблен
            string input = "Привет путник";
            string result = _system.ProcessText(input);

            // Результат не совпадает с оригиналом при нулевом понимании
            Assert.AreNotEqual(input, result,
                "ProcessText при comprehension=0 должен вернуть скрэмбленный текст");
        }

        [Test]
        public void CaptureState_RestoreState_PreservesComprehension()
        {
            // Поднимаем comprehension до 0.3
            for (int i = 0; i < 15; i++)
                _system.AddDialogueExperience();

            float savedComp = _system.Comprehension;
            object state = _system.CaptureState();

            // Создаём второй экземпляр и восстанавливаем в него
            var config2 = LanguageTestFactory.CreateConfig();
            var (system2, go2) = LanguageTestFactory.CreateLanguageSystem(config2);

            try
            {
                system2.RestoreState(state);
                Assert.AreEqual(savedComp, system2.Comprehension, 0.0001f,
                    "Comprehension должен восстановиться после RestoreState");
            }
            finally
            {
                Object.DestroyImmediate(go2);
                Object.DestroyImmediate(config2);
            }
        }

        [Test]
        public void AddDialogueExperience_FiresOnComprehensionChanged()
        {
            bool fired = false;
            float firedValue = -1f;

            LanguageSystem.OnComprehensionChanged += v =>
            {
                fired = true;
                firedValue = v;
            };

            _system.AddDialogueExperience();

            Assert.IsTrue(fired, "OnComprehensionChanged должно сработать");
            Assert.Greater(firedValue, 0f, "Переданное значение должно быть > 0");
        }

        [Test]
        public void IsIconMode_TrueWhenBelowThreshold()
        {
            // comprehension = 0 < iconModeThreshold (0.2) → IsIconMode = true
            Assert.IsTrue(_system.IsIconMode,
                "IsIconMode должен быть true при comprehension=0");
        }

        [Test]
        public void KnowsCurrency_FalseWhenBelowThreshold()
        {
            // comprehension = 0 < currencyThreshold (0.5) → KnowsCurrency = false
            Assert.IsFalse(_system.KnowsCurrency,
                "KnowsCurrency должен быть false при comprehension=0");
        }

        [Test]
        public void RestoreState_SetsComprehension_AboveThresholds()
        {
            // Восстанавливаем состояние с comprehension=0.6 (выше currency 0.5, выше icon 0.2)
            var config2 = LanguageTestFactory.CreateConfig();
            var (system2, go2) = LanguageTestFactory.CreateLanguageSystem(config2);

            try
            {
                // Сначала поднимаем comprehension вручную через многократный AddDialogueExperience
                // или через RestoreState с мокированным состоянием
                // Используем CaptureState/RestoreState pattern: создадим объект с нужным значением
                // через reflection (приватное поле _comprehension)
                var field = typeof(LanguageSystem).GetField(
                    "_comprehension",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(system2, 0.6f);

                Assert.IsFalse(system2.IsIconMode, "IsIconMode должен быть false при comprehension=0.6");
                Assert.IsTrue(system2.KnowsCurrency, "KnowsCurrency должен быть true при comprehension=0.6");
            }
            finally
            {
                Object.DestroyImmediate(go2);
                Object.DestroyImmediate(config2);
            }
        }

        [Test]
        public void RestoreState_InvalidType_DoesNotCrash()
        {
            Assert.DoesNotThrow(() => _system.RestoreState(null),
                "RestoreState(null) не должен бросать исключение");
            Assert.DoesNotThrow(() => _system.RestoreState("wrong"),
                "RestoreState с неверным типом не должен бросать исключение");
        }

        [Test]
        public void ProcessText_AtFullComprehension_ReturnsOriginal()
        {
            // Устанавливаем comprehension = 1f через reflection
            var field = typeof(LanguageSystem).GetField(
                "_comprehension",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(_system, 1f);

            string input = "Добро пожаловать в таверну";
            string result = _system.ProcessText(input);
            Assert.AreEqual(input, result,
                "ProcessText при comprehension=1 должен вернуть оригинальный текст");
        }
    }
}
