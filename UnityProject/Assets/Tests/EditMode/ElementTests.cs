using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class ElementTestFactory
    {
        /// <summary>Обнуляет кешированный словарь, чтобы следующий вызов перестроил его из массива.</summary>
        private static void InvalidateLookup(ScriptableObject so, string fieldName)
        {
            var field = so.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(so, null);
        }

        /// <summary>
        /// Создаёт ElementInteractionMatrix с одним взаимодействием Fire+Wet.
        /// </summary>
        internal static ElementInteractionMatrix CreateMatrix(
            ElementTag a = ElementTag.Fire,
            ElementTag b = ElementTag.Wet,
            ElementTag resultAdd = ElementTag.None,
            ElementTag resultRemove = ElementTag.Fire | ElementTag.Wet,
            float damageMultiplier = 2f)
        {
            var matrix = ScriptableObject.CreateInstance<ElementInteractionMatrix>();
            var so = new SerializedObject(matrix);

            var interactions = so.FindProperty("_interactions");
            interactions.arraySize = 1;
            var elem = interactions.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("elementA").intValue = (int)a;
            elem.FindPropertyRelative("elementB").intValue = (int)b;
            elem.FindPropertyRelative("resultAdd").intValue = (int)resultAdd;
            elem.FindPropertyRelative("resultRemove").intValue = (int)resultRemove;
            elem.FindPropertyRelative("damageMultiplier").floatValue = damageMultiplier;

            so.ApplyModifiedPropertiesWithoutUndo();
            
            // Explicitly rebuild lookup to ensure it uses the updated _interactions array
            var buildLookup = matrix.GetType().GetMethod("BuildLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            buildLookup?.Invoke(matrix, null);
            
            return matrix;
        }

        /// <summary>
        /// Создаёт ElementConfig с одной записью для заданного тега.
        /// </summary>
        internal static ElementConfig CreateConfig(
            ElementTag tag = ElementTag.Fire,
            float duration = 5f,
            float propagationRadius = 2f,
            float propagationDelay = 0.5f,
            int maxDepth = 3,
            float damagePerSecond = 10f)
        {
            var config = ScriptableObject.CreateInstance<ElementConfig>();
            var so = new SerializedObject(config);

            var entries = so.FindProperty("_entries");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("tag").intValue = (int)tag;

            var settings = entry.FindPropertyRelative("settings");
            settings.FindPropertyRelative("duration").floatValue = duration;
            settings.FindPropertyRelative("propagationRadius").floatValue = propagationRadius;
            settings.FindPropertyRelative("propagationDelay").floatValue = propagationDelay;
            settings.FindPropertyRelative("maxPropagationDepth").intValue = maxDepth;
            settings.FindPropertyRelative("damagePerSecond").floatValue = damagePerSecond;

            so.ApplyModifiedPropertiesWithoutUndo();
            
            // Explicitly rebuild lookup to ensure it uses the updated _entries array
            var buildLookup = config.GetType().GetMethod("BuildLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            buildLookup?.Invoke(config, null);
            
            return config;
        }
    }

    // -------------------------------------------------------------------------
    // ElementInteractionMatrix Tests
    // -------------------------------------------------------------------------

    public class ElementInteractionMatrixTests
    {
        private ElementInteractionMatrix _matrix;

        [SetUp]
        public void SetUp()
        {
            _matrix = ElementTestFactory.CreateMatrix(
                a: ElementTag.Fire,
                b: ElementTag.Wet,
                resultAdd: ElementTag.None,
                resultRemove: ElementTag.Fire | ElementTag.Wet,
                damageMultiplier: 2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_matrix);
        }

        [Test]
        public void TryGetInteraction_MatchingPair_ReturnsTrue()
        {
            bool found = _matrix.TryGetInteraction(ElementTag.Fire, ElementTag.Wet, out _);
            Assert.IsTrue(found, "Должно найти взаимодействие для пары Fire+Wet");
        }

        [Test]
        public void TryGetInteraction_ReversedOrder_ReturnsTrue()
        {
            // Порядок аргументов не должен влиять на результат
            bool found = _matrix.TryGetInteraction(ElementTag.Wet, ElementTag.Fire, out _);
            Assert.IsTrue(found, "Порядок элементов в паре не должен влиять на поиск");
        }

        [Test]
        public void TryGetInteraction_NoMatch_ReturnsFalse()
        {
            bool found = _matrix.TryGetInteraction(ElementTag.Fire, ElementTag.Electrified, out _);
            Assert.IsFalse(found, "Незарегистрированная пара должна возвращать false");
        }

        [Test]
        public void TryGetInteraction_ResultValues_Correct()
        {
            bool found = _matrix.TryGetInteraction(ElementTag.Fire, ElementTag.Wet, out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(ElementTag.None, result.resultAdd,
                "resultAdd должен совпадать с заданным значением");
            Assert.AreEqual(ElementTag.Fire | ElementTag.Wet, result.resultRemove,
                "resultRemove должен содержать Fire и Wet");
            Assert.AreEqual(2f, result.damageMultiplier, 0.001f,
                "damageMultiplier должен совпадать с заданным значением");
        }

        [Test]
        public void TryGetInteraction_SameElement_ReturnsFalseWhenNotRegistered()
        {
            // Fire+Fire не зарегистрирован — должен вернуть false
            bool found = _matrix.TryGetInteraction(ElementTag.Fire, ElementTag.Fire, out _);
            Assert.IsFalse(found, "Незарегистрированная пара (Fire, Fire) должна вернуть false");
        }
    }
}
