using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class ProgressionTestFactory
    {
        internal static StatGrowthCurve CreateCurve(
            StatType type,
            float baseRate = 1f,
            float decay = 0.5f,
            float max = 100f,
            float failMul = 0.3f,
            float victoryBonus = 3f)
        {
            var curve = ScriptableObject.CreateInstance<StatGrowthCurve>();
            var so = new SerializedObject(curve);
            so.FindProperty("_statType").enumValueIndex = (int)type;
            so.FindProperty("_baseGrowthRate").floatValue = baseRate;
            so.FindProperty("_decayExponent").floatValue = decay;
            so.FindProperty("_maxValue").floatValue = max;
            so.FindProperty("_failureMultiplier").floatValue = failMul;
            so.FindProperty("_victoryBonus").floatValue = victoryBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return curve;
        }

        internal static StatEffectConfig CreateEffectConfig(
            float maxDamageBonus = 1.5f,
            float maxDamageReduction = 0.5f,
            float maxAttackSpeedBonus = 0.8f,
            float baseHitChance = 0.5f,
            float maxHealBonus = 1f,
            float maxCapacityBonus = 2f)
        {
            var config = ScriptableObject.CreateInstance<StatEffectConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_maxDamageBonus").floatValue = maxDamageBonus;
            so.FindProperty("_maxDamageReduction").floatValue = maxDamageReduction;
            so.FindProperty("_maxAttackSpeedBonus").floatValue = maxAttackSpeedBonus;
            so.FindProperty("_baseHitChance").floatValue = baseHitChance;
            so.FindProperty("_maxHealBonus").floatValue = maxHealBonus;
            so.FindProperty("_maxCapacityBonus").floatValue = maxCapacityBonus;

            // Пороги тиров: 0, 25, 50, 80
            var thresholds = so.FindProperty("_tierThresholds");
            thresholds.arraySize = 4;
            thresholds.GetArrayElementAtIndex(0).floatValue = 0f;
            thresholds.GetArrayElementAtIndex(1).floatValue = 25f;
            thresholds.GetArrayElementAtIndex(2).floatValue = 50f;
            thresholds.GetArrayElementAtIndex(3).floatValue = 80f;

            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        internal static ProgressionConfig CreateProgressionConfig(
            StatEffectConfig effectConfig,
            params StatGrowthCurve[] curves)
        {
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_effectConfig").objectReferenceValue = effectConfig;

            var curvesArray = so.FindProperty("_growthCurves");
            curvesArray.arraySize = curves.Length;
            for (int i = 0; i < curves.Length; i++)
                curvesArray.GetArrayElementAtIndex(i).objectReferenceValue = curves[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>
        /// Создаёт PlayerStats на временном GameObject с полным конфигом для всех StatType.
        /// Вызывает Awake через reflection.
        /// </summary>
        internal static (PlayerStats stats, GameObject go, ProgressionConfig config) CreatePlayerStats()
        {
            var effectConfig = CreateEffectConfig();

            var strengthCurve = CreateCurve(StatType.Strength);
            var toughnessCurve = CreateCurve(StatType.Toughness);
            var agilityCurve = CreateCurve(StatType.Agility);
            var accuracyCurve = CreateCurve(StatType.Accuracy);
            var enduranceCurve = CreateCurve(StatType.Endurance);
            var carryCurve = CreateCurve(StatType.CarryCapacity);

            var progressionConfig = CreateProgressionConfig(
                effectConfig,
                strengthCurve, toughnessCurve, agilityCurve,
                accuracyCurve, enduranceCurve, carryCurve);

            var go = new GameObject("TestPlayer");
            var stats = go.AddComponent<PlayerStats>();

            var so = new SerializedObject(stats);
            so.FindProperty("_config").objectReferenceValue = progressionConfig;
            so.ApplyModifiedPropertiesWithoutUndo();

            var awake = typeof(PlayerStats).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awake?.Invoke(stats, null);

            return (stats, go, progressionConfig);
        }
    }

    // -------------------------------------------------------------------------
    // StatGrowthCurve Tests
    // -------------------------------------------------------------------------

    public class StatGrowthCurveTests
    {
        private StatGrowthCurve _curve;

        [SetUp]
        public void SetUp()
        {
            _curve = ProgressionTestFactory.CreateCurve(StatType.Strength, baseRate: 1f, decay: 0.5f, max: 100f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_curve);
        }

        [Test]
        public void CalculateGrowth_AtZero_ReturnsMaxGrowth()
        {
            // currentValue=0 → normalized=0 → (1-0)^0.5 = 1.0 → growth = rawAmount * baseRate * 1
            float growth = _curve.CalculateGrowth(0f, 5f);
            Assert.AreEqual(5f, growth, 0.001f);
        }

        [Test]
        public void CalculateGrowth_AtMax_ReturnsZero()
        {
            // currentValue=maxValue → normalized=1 → (1-1)^0.5 = 0 → growth = 0
            float growth = _curve.CalculateGrowth(100f, 5f);
            Assert.AreEqual(0f, growth, 0.001f);
        }

        [Test]
        public void CalculateGrowth_AtHalf_ReturnsDecayedGrowth()
        {
            // currentValue=50, max=100 → normalized=0.5 → (0.5)^0.5 = sqrt(0.5) ≈ 0.7071
            float growth = _curve.CalculateGrowth(50f, 1f);
            Assert.AreEqual(Mathf.Pow(0.5f, 0.5f), growth, 0.001f);
        }

        [Test]
        public void CalculateGrowth_NegativeRaw_ReturnsNegativeOrZero()
        {
            // Отрицательный rawAmount — результат отрицателен (или 0), не NaN
            float growth = _curve.CalculateGrowth(0f, -5f);
            Assert.IsFalse(float.IsNaN(growth), "Результат не должен быть NaN");
            Assert.LessOrEqual(growth, 0f, "Отрицательный raw → результат ≤ 0");
        }

        [Test]
        public void CalculateGrowth_BeyondMax_ClampedByNormalized()
        {
            // currentValue > maxValue → normalized clamped к 1 → growth = 0
            float growth = _curve.CalculateGrowth(150f, 5f);
            Assert.AreEqual(0f, growth, 0.001f);
        }

        [Test]
        public void Properties_ReturnConfiguredValues()
        {
            var curve = ProgressionTestFactory.CreateCurve(StatType.Agility,
                baseRate: 2f, decay: 0.7f, max: 200f, failMul: 0.15f, victoryBonus: 5f);

            Assert.AreEqual(StatType.Agility, curve.StatType);
            Assert.AreEqual(2f, curve.BaseGrowthRate, 0.001f);
            Assert.AreEqual(0.7f, curve.DecayExponent, 0.001f);
            Assert.AreEqual(200f, curve.MaxValue, 0.001f);
            Assert.AreEqual(0.15f, curve.FailureMultiplier, 0.001f);
            Assert.AreEqual(5f, curve.VictoryBonus, 0.001f);

            UnityEngine.Object.DestroyImmediate(curve);
        }
    }

    // -------------------------------------------------------------------------
    // StatEffectConfig Tests
    // -------------------------------------------------------------------------

    public class StatEffectConfigTests
    {
        private StatEffectConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ProgressionTestFactory.CreateEffectConfig(
                maxDamageBonus: 1.5f,
                maxDamageReduction: 0.5f,
                maxAttackSpeedBonus: 0.8f,
                baseHitChance: 0.5f,
                maxHealBonus: 1f,
                maxCapacityBonus: 2f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void GetDamageMultiplier_AtZero_ReturnsOne()
        {
            float result = _config.GetDamageMultiplier(0f);
            Assert.AreEqual(1f, result, 0.001f);
        }

        [Test]
        public void GetDamageMultiplier_AtOne_ReturnsMaxBonus()
        {
            // 1 + 1.0 * 1.5 = 2.5
            float result = _config.GetDamageMultiplier(1f);
            Assert.AreEqual(1f + 1.5f, result, 0.001f);
        }

        [Test]
        public void GetDamageReduction_AtZero_ReturnsZero()
        {
            float result = _config.GetDamageReduction(0f);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void GetDamageReduction_AtOne_ReturnsMaxReduction()
        {
            float result = _config.GetDamageReduction(1f);
            Assert.AreEqual(0.5f, result, 0.001f);
        }

        [Test]
        public void GetHitChance_AtZero_ReturnsBaseChance()
        {
            float result = _config.GetHitChance(0f);
            Assert.AreEqual(0.5f, result, 0.001f);
        }

        [Test]
        public void GetHitChance_AtOne_ReturnsOne()
        {
            // baseHitChance + 1.0 * (1 - baseHitChance) = 0.5 + 0.5 = 1.0
            float result = _config.GetHitChance(1f);
            Assert.AreEqual(1f, result, 0.001f);
        }

        [Test]
        public void GetTier_ReturnsCorrectTier()
        {
            // Пороги: 0, 25, 50, 80
            Assert.AreEqual(0, _config.GetTier(0f), "Значение 0 → тир 0");
            Assert.AreEqual(1, _config.GetTier(30f), "Значение 30 → тир 1");
            Assert.AreEqual(2, _config.GetTier(60f), "Значение 60 → тир 2");
            Assert.AreEqual(3, _config.GetTier(90f), "Значение 90 → тир 3");
        }

        [Test]
        public void GetTier_AtExactThreshold_ReturnsCorrectTier()
        {
            // Значение ровно на пороге
            Assert.AreEqual(1, _config.GetTier(25f), "Значение 25 (порог тира 1) → тир 1");
            Assert.AreEqual(2, _config.GetTier(50f), "Значение 50 (порог тира 2) → тир 2");
        }

        [Test]
        public void GetAttackSpeedMultiplier_AtZero_ReturnsOne()
        {
            float result = _config.GetAttackSpeedMultiplier(0f);
            Assert.AreEqual(1f, result, 0.001f);
        }

        [Test]
        public void GetAttackSpeedMultiplier_AtOne_ReturnsMaxBonus()
        {
            // 1 + 1.0 * 0.8 = 1.8
            float result = _config.GetAttackSpeedMultiplier(1f);
            Assert.AreEqual(1f + 0.8f, result, 0.001f);
        }

        [Test]
        public void GetHealMultiplier_AtOne_ReturnsMaxBonus()
        {
            // 1 + 1.0 * 1.0 = 2.0
            float result = _config.GetHealMultiplier(1f);
            Assert.AreEqual(2f, result, 0.001f);
        }

        [Test]
        public void GetCapacityMultiplier_AtOne_ReturnsMaxBonus()
        {
            // 1 + 1.0 * 2.0 = 3.0
            float result = _config.GetCapacityMultiplier(1f);
            Assert.AreEqual(3f, result, 0.001f);
        }
    }

    // -------------------------------------------------------------------------
    // PlayerStats Tests
    // -------------------------------------------------------------------------

    public class PlayerStatsTests
    {
        private PlayerStats _stats;
        private GameObject _go;
        private ProgressionConfig _config;

        [SetUp]
        public void SetUp()
        {
            // Сброс статических событий перед каждым тестом
            PlayerStats.ClearEvents();

            (_stats, _go, _config) = ProgressionTestFactory.CreatePlayerStats();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerStats.ClearEvents();
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void AddExperience_IncreasesStatValue()
        {
            _stats.AddExperience(StatType.Strength, 10f);
            float value = _stats.GetStat(StatType.Strength);
            Assert.Greater(value, 0f, "Значение силы должно вырасти после AddExperience");
        }

        [Test]
        public void AddExperience_FiresOnStatChanged()
        {
            StatType firedType = default;
            float firedOld = -1f;
            float firedNew = -1f;
            bool eventFired = false;

            PlayerStats.OnStatChanged += (type, oldVal, newVal) =>
            {
                firedType = type;
                firedOld = oldVal;
                firedNew = newVal;
                eventFired = true;
            };

            _stats.AddExperience(StatType.Strength, 10f);

            Assert.IsTrue(eventFired, "OnStatChanged должно быть вызвано");
            Assert.AreEqual(StatType.Strength, firedType);
            Assert.AreEqual(0f, firedOld, 0.001f, "Старое значение должно быть 0");
            Assert.Greater(firedNew, 0f, "Новое значение должно быть > 0");
        }

        [Test]
        public void AddExperience_CrossingTier_FiresOnTierReached()
        {
            bool tierEventFired = false;
            int reachedTier = -1;

            PlayerStats.OnTierReached += (type, tier) =>
            {
                tierEventFired = true;
                reachedTier = tier;
            };

            // Добавляем большой опыт — перешагиваем порог тира 1 (25)
            // При currentValue=0, decay=0.5, baseRate=1: growth = rawAmount * (1-0)^0.5 = rawAmount
            // Нужно чтобы значение стало >= 25. Добавляем 30 raw, результат = 30 > порога тира 1
            _stats.AddExperience(StatType.Strength, 30f);

            Assert.IsTrue(tierEventFired, "OnTierReached должно сработать при пересечении порога тира");
            Assert.Greater(reachedTier, 0, "Достигнутый тир должен быть выше 0");
        }

        [Test]
        public void AddExperience_SmallAmount_DoesNotFireTierEvent()
        {
            bool tierEventFired = false;
            PlayerStats.OnTierReached += (type, tier) => tierEventFired = true;

            // Маленькое значение — не достигает первого реального порога (25)
            _stats.AddExperience(StatType.Strength, 1f);

            Assert.IsFalse(tierEventFired, "OnTierReached не должно срабатывать при малом росте");
        }

        [Test]
        public void GetStatNormalized_ReturnsCorrectRatio()
        {
            // Устанавливаем значение 50 через AddExperience при max=100
            // При currentValue=0: growth = 50 * 1 * 1 = 50, итого значение = 50
            _stats.AddExperience(StatType.Strength, 50f);
            float normalized = _stats.GetStatNormalized(StatType.Strength);
            Assert.AreEqual(0.5f, normalized, 0.001f);
        }

        [Test]
        public void GetTier_DelegatesCorrectly()
        {
            // При значении 0 тир должен быть 0
            int tier = _stats.GetTier(StatType.Strength);
            Assert.AreEqual(0, tier);

            // После роста до 30+ → тир 1
            _stats.AddExperience(StatType.Strength, 30f);
            tier = _stats.GetTier(StatType.Strength);
            Assert.AreEqual(1, tier);
        }

        [Test]
        public void GetStat_InitialValue_IsZero()
        {
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                Assert.AreEqual(0f, _stats.GetStat(type), 0.001f,
                    $"Начальное значение {type} должно быть 0");
            }
        }

        [Test]
        public void CaptureState_RestoreState_PreservesValues()
        {
            // Добавляем опыт и запоминаем состояние
            _stats.AddExperience(StatType.Strength, 20f);
            _stats.AddExperience(StatType.Agility, 35f);

            float strengthBefore = _stats.GetStat(StatType.Strength);
            float agilityBefore = _stats.GetStat(StatType.Agility);

            object savedState = _stats.CaptureState();

            // Создаём второй PlayerStats и восстанавливаем в него
            var (otherStats, otherGo, _) = ProgressionTestFactory.CreatePlayerStats();

            try
            {
                otherStats.RestoreState(savedState);

                Assert.AreEqual(strengthBefore, otherStats.GetStat(StatType.Strength), 0.001f,
                    "Сила должна восстановиться");
                Assert.AreEqual(agilityBefore, otherStats.GetStat(StatType.Agility), 0.001f,
                    "Ловкость должна восстановиться");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherGo);
            }
        }

        [Test]
        public void RestoreState_Null_DoesNotCrash()
        {
            Assert.DoesNotThrow(() => _stats.RestoreState(null),
                "RestoreState(null) не должен бросать исключение");
        }

        [Test]
        public void RestoreState_InvalidType_DoesNotCrash()
        {
            Assert.DoesNotThrow(() => _stats.RestoreState("wrong_data"),
                "RestoreState с неправильным типом не должен бросать исключение");

            Assert.DoesNotThrow(() => _stats.RestoreState(42),
                "RestoreState с int не должен бросать исключение");
        }

        [Test]
        public void AddExperience_AtMaxValue_DoesNotExceedMax()
        {
            // Добавляем огромный опыт — значение должно остановиться на maxValue=100
            _stats.AddExperience(StatType.Strength, 10000f);
            float value = _stats.GetStat(StatType.Strength);
            Assert.LessOrEqual(value, 100f, "Значение не должно превышать maxValue");
        }
    }

    // -------------------------------------------------------------------------
    // ProgressionConfig Tests
    // -------------------------------------------------------------------------

    public class ProgressionConfigTests
    {
        private StatEffectConfig _effectConfig;
        private StatGrowthCurve _strengthCurve;
        private ProgressionConfig _config;

        [SetUp]
        public void SetUp()
        {
            _effectConfig = ProgressionTestFactory.CreateEffectConfig();
            _strengthCurve = ProgressionTestFactory.CreateCurve(StatType.Strength);
            _config = ProgressionTestFactory.CreateProgressionConfig(_effectConfig, _strengthCurve);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_effectConfig);
            UnityEngine.Object.DestroyImmediate(_strengthCurve);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void GetCurve_ExistingType_ReturnsCurve()
        {
            var curve = _config.GetCurve(StatType.Strength);
            Assert.IsNotNull(curve, "GetCurve должен вернуть кривую для зарегистрированного типа");
            Assert.AreEqual(StatType.Strength, curve.StatType);
        }

        [Test]
        public void GetCurve_MissingType_ReturnsNull()
        {
            // В конфиге только Strength, остальные должны вернуть null
            var curve = _config.GetCurve(StatType.Agility);
            Assert.IsNull(curve, "GetCurve должен вернуть null для отсутствующего типа");
        }

        [Test]
        public void EffectConfig_ReturnsAssignedConfig()
        {
            Assert.IsNotNull(_config.EffectConfig);
            Assert.AreEqual(_effectConfig, _config.EffectConfig);
        }

        [Test]
        public void GrowthCurves_ContainsRegisteredCurves()
        {
            Assert.AreEqual(1, _config.GrowthCurves.Count);
            Assert.AreEqual(_strengthCurve, _config.GrowthCurves[0]);
        }
    }
}
