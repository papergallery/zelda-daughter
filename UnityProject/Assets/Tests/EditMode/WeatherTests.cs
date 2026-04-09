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

    internal static class WeatherTestFactory
    {
        /// <summary>
        /// Создаёт WeatherConfig с одним переходом, одной длительностью и одним визуальным блоком.
        /// </summary>
        internal static WeatherConfig CreateConfig(
            WeatherType from = WeatherType.Clear,
            WeatherType to = WeatherType.Rain,
            float probability = 1f,
            float minDuration = 30f,
            float maxDuration = 60f,
            float fogDensity = 0.02f,
            float ambientIntensity = 0.5f,
            float rainIntensity = 0.8f)
        {
            var config = ScriptableObject.CreateInstance<WeatherConfig>();
            var so = new SerializedObject(config);

            // Переход
            var transitions = so.FindProperty("_transitions");
            transitions.arraySize = 1;
            var t = transitions.GetArrayElementAtIndex(0);
            t.FindPropertyRelative("from").intValue = (int)from;
            t.FindPropertyRelative("to").intValue = (int)to;
            t.FindPropertyRelative("probability").floatValue = probability;

            // Длительность — регистрируем тип-назначения (Rain), чтобы GetRandomDuration работал
            var durations = so.FindProperty("_durations");
            durations.arraySize = 1;
            var d = durations.GetArrayElementAtIndex(0);
            d.FindPropertyRelative("weatherType").intValue = (int)to;
            d.FindPropertyRelative("minDuration").floatValue = minDuration;
            d.FindPropertyRelative("maxDuration").floatValue = maxDuration;

            // Визуальные настройки — тоже для типа-назначения
            var visuals = so.FindProperty("_visuals");
            visuals.arraySize = 1;
            var v = visuals.GetArrayElementAtIndex(0);
            v.FindPropertyRelative("weatherType").intValue = (int)to;
            v.FindPropertyRelative("fogDensity").floatValue = fogDensity;
            v.FindPropertyRelative("ambientIntensity").floatValue = ambientIntensity;
            v.FindPropertyRelative("rainIntensity").floatValue = rainIntensity;

            so.ApplyModifiedPropertiesWithoutUndo();

            // Обнуляем кеши, чтобы следующий вызов перестроил их из заполненных массивов
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            config.GetType().GetField("_durationLookup", flags)?.SetValue(config, null);
            config.GetType().GetField("_visualsLookup", flags)?.SetValue(config, null);

            return config;
        }

        /// <summary>
        /// Создаёт WeatherConfig без каких-либо переходов (для теста fallback).
        /// </summary>
        internal static WeatherConfig CreateEmptyConfig()
        {
            return ScriptableObject.CreateInstance<WeatherConfig>();
        }
    }

    // -------------------------------------------------------------------------
    // WeatherConfig Tests
    // -------------------------------------------------------------------------

    public class WeatherConfigTests
    {
        private WeatherConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = WeatherTestFactory.CreateConfig(
                from: WeatherType.Clear,
                to: WeatherType.Rain,
                probability: 1f,
                minDuration: 30f,
                maxDuration: 60f,
                fogDensity: 0.02f,
                ambientIntensity: 0.5f,
                rainIntensity: 0.8f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void GetNextWeather_WithTransitions_ReturnsValidType()
        {
            // Единственный переход: Clear → Rain с probability=1.0
            // Результат должен быть Rain в 100% случаев
            WeatherType next = _config.GetNextWeather(WeatherType.Clear);
            Assert.AreEqual(WeatherType.Rain, next,
                "При probability=1.0 единственный переход должен всегда выбираться");
        }

        [Test]
        public void GetNextWeather_NoTransitions_ReturnsCurrent()
        {
            var empty = WeatherTestFactory.CreateEmptyConfig();

            try
            {
                WeatherType next = empty.GetNextWeather(WeatherType.Storm);
                Assert.AreEqual(WeatherType.Storm, next,
                    "При отсутствии переходов должен возвращаться текущий тип");
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void GetNextWeather_NoMatchingTransitions_ReturnsCurrent()
        {
            // Конфиг содержит переход Clear→Rain, но запрашиваем Storm
            // Совпадений нет — возвращаем текущий
            WeatherType next = _config.GetNextWeather(WeatherType.Storm);
            Assert.AreEqual(WeatherType.Storm, next,
                "Если из текущего состояния нет переходов — возвращать текущее");
        }

        [Test]
        public void GetRandomDuration_ReturnsInRange()
        {
            // Запрашиваем длительность для WeatherType.Rain (зарегистрирован: 30..60)
            // Проверяем 20 раз чтобы убедиться что результат всегда в диапазоне
            const float min = 30f;
            const float max = 60f;

            for (int i = 0; i < 20; i++)
            {
                float duration = _config.GetRandomDuration(WeatherType.Rain);
                Assert.GreaterOrEqual(duration, min,
                    $"Длительность ({duration}) должна быть >= {min}");
                Assert.LessOrEqual(duration, max,
                    $"Длительность ({duration}) должна быть <= {max}");
            }
        }

        [Test]
        public void GetRandomDuration_UnregisteredType_ReturnsFallback()
        {
            // WeatherType.Cloudy не зарегистрирован — должен вернуть fallback 60f
            float duration = _config.GetRandomDuration(WeatherType.Cloudy);
            Assert.AreEqual(60f, duration, 0.001f,
                "Незарегистрированный тип должен возвращать fallback = 60f");
        }

        [Test]
        public void TryGetVisuals_ExistingType_ReturnsTrue()
        {
            bool found = _config.TryGetVisuals(WeatherType.Rain, out _);
            Assert.IsTrue(found, "TryGetVisuals должен найти визуальные настройки для Rain");
        }

        [Test]
        public void TryGetVisuals_MissingType_ReturnsFalse()
        {
            bool found = _config.TryGetVisuals(WeatherType.Storm, out _);
            Assert.IsFalse(found, "TryGetVisuals должен вернуть false для незарегистрированного типа");
        }

        [Test]
        public void TryGetVisuals_ValuesCorrect()
        {
            _config.TryGetVisuals(WeatherType.Rain, out var visuals);

            Assert.AreEqual(0.02f, visuals.fogDensity, 0.0001f, "fogDensity должен совпадать");
            Assert.AreEqual(0.5f, visuals.ambientIntensity, 0.001f, "ambientIntensity должен совпадать");
            Assert.AreEqual(0.8f, visuals.rainIntensity, 0.001f, "rainIntensity должен совпадать");
        }
    }
}
