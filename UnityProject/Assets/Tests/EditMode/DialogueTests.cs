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

    internal static class DialogueTestFactory
    {
        /// <summary>
        /// Создаёт DialogueTree с двумя узлами: стартовым и дополнительным.
        /// Использует SerializedObject для заполнения приватных полей.
        /// </summary>
        internal static DialogueTree CreateTree(
            string startNodeId = "start",
            string extraNodeId = "next",
            string startNodeText = "Hello traveller",
            string extraNodeText = "Farewell")
        {
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = startNodeId;

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 2;

            var node0 = nodes.GetArrayElementAtIndex(0);
            node0.FindPropertyRelative("id").stringValue = startNodeId;
            node0.FindPropertyRelative("npcText").stringValue = startNodeText;
            node0.FindPropertyRelative("conditionKey").stringValue = "";
            node0.FindPropertyRelative("effectKey").stringValue = "";
            node0.FindPropertyRelative("startsTrade").boolValue = false;

            var node1 = nodes.GetArrayElementAtIndex(1);
            node1.FindPropertyRelative("id").stringValue = extraNodeId;
            node1.FindPropertyRelative("npcText").stringValue = extraNodeText;
            node1.FindPropertyRelative("conditionKey").stringValue = "";
            node1.FindPropertyRelative("effectKey").stringValue = "";
            node1.FindPropertyRelative("startsTrade").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
            return tree;
        }

        /// <summary>
        /// Создаёт DialogueConditionResolver с заданным уровнем понимания языка.
        /// PlayerInventory и PlayerStats передаются как null (не нужны для language_level тестов).
        /// </summary>
        internal static DialogueConditionResolver CreateResolverWithLanguage(float comprehension)
        {
            var config = LanguageTestFactory.CreateConfig();
            var (system, go) = LanguageTestFactory.CreateLanguageSystem(config);

            // Устанавливаем comprehension через reflection
            var field = typeof(LanguageSystem).GetField(
                "_comprehension",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(system, comprehension);

            var resolver = new DialogueConditionResolver(null, system, null);

            // Возвращаем resolver; go/config надо уничтожить снаружи
            // Сохраняем ссылки через замыкание не вариант для struct — caller отвечает за cleanup
            return resolver;
        }

        internal static (DialogueConditionResolver resolver, LanguageSystem system, GameObject go, LanguageConfig config)
            CreateResolverWithLanguageFull(float comprehension)
        {
            var config = LanguageTestFactory.CreateConfig();
            var (system, go) = LanguageTestFactory.CreateLanguageSystem(config);

            var field = typeof(LanguageSystem).GetField(
                "_comprehension",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(system, comprehension);

            var resolver = new DialogueConditionResolver(null, system, null);
            return (resolver, system, go, config);
        }
    }

    // -------------------------------------------------------------------------
    // DialogueTree Tests
    // -------------------------------------------------------------------------

    public class DialogueTreeTests
    {
        private DialogueTree _tree;

        [SetUp]
        public void SetUp()
        {
            _tree = DialogueTestFactory.CreateTree(
                startNodeId: "start",
                extraNodeId: "next");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tree);
        }

        [Test]
        public void GetStartNode_ReturnsCorrectNode()
        {
            var node = _tree.GetStartNode();
            Assert.AreEqual("start", node.id, "GetStartNode должен вернуть узел с id='start'");
        }

        [Test]
        public void GetNode_ExistingId_ReturnsNode()
        {
            var node = _tree.GetNode("next");
            Assert.AreEqual("next", node.id, "GetNode('next') должен вернуть узел с id='next'");
        }

        [Test]
        public void GetNode_MissingId_ReturnsDefault()
        {
            var node = _tree.GetNode("nonexistent");
            Assert.IsNull(node.id, "GetNode с несуществующим id должен вернуть default (id=null)");
        }

        [Test]
        public void TryGetNode_ExistingId_ReturnsTrue()
        {
            bool found = _tree.TryGetNode("start", out DialogueNode node);
            Assert.IsTrue(found, "TryGetNode должен вернуть true для существующего узла");
            Assert.AreEqual("start", node.id);
        }

        [Test]
        public void TryGetNode_MissingId_ReturnsFalse()
        {
            bool found = _tree.TryGetNode("ghost", out DialogueNode node);
            Assert.IsFalse(found, "TryGetNode должен вернуть false для несуществующего узла");
            Assert.IsNull(node.id, "out-параметр должен быть default при неудаче");
        }
    }

    // -------------------------------------------------------------------------
    // DialogueConditionResolver Tests
    // -------------------------------------------------------------------------

    public class DialogueConditionResolverTests
    {
        [Test]
        public void Check_EmptyCondition_ReturnsTrue()
        {
            var resolver = new DialogueConditionResolver(null, null, null);
            Assert.IsTrue(resolver.Check(string.Empty),
                "Пустая строка условия всегда должна возвращать true");
        }

        [Test]
        public void Check_NullCondition_ReturnsTrue()
        {
            var resolver = new DialogueConditionResolver(null, null, null);
            Assert.IsTrue(resolver.Check(null),
                "null условие всегда должно возвращать true");
        }

        [Test]
        public void Check_LanguageLevel_AboveThreshold_ReturnsTrue()
        {
            var (resolver, system, go, config) = DialogueTestFactory.CreateResolverWithLanguageFull(0.8f);
            try
            {
                bool result = resolver.Check("language_level:0.5");
                Assert.IsTrue(result,
                    "Condition 'language_level:0.5' при comprehension=0.8 должен вернуть true");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Check_LanguageLevel_BelowThreshold_ReturnsFalse()
        {
            var (resolver, system, go, config) = DialogueTestFactory.CreateResolverWithLanguageFull(0.3f);
            try
            {
                bool result = resolver.Check("language_level:0.5");
                Assert.IsFalse(result,
                    "Condition 'language_level:0.5' при comprehension=0.3 должен вернуть false");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Check_UnknownCondition_ReturnsTrue()
        {
            var resolver = new DialogueConditionResolver(null, null, null);
            Assert.IsTrue(resolver.Check("quest_flag:killed_boar"),
                "Неизвестное условие-заглушка должно возвращать true");
        }
    }
}
