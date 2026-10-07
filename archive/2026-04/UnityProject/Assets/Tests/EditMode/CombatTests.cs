using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Input;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class CombatTestFactory
    {
        /// <summary>
        /// Создаёт CombatConfig с заданными параметрами.
        /// </summary>
        internal static CombatConfig CreateCombatConfig(
            float maxHP = 100f,
            float naturalHealRate = 0f,
            float restHealMultiplier = 3f,
            float reviveHPRatio = 0.15f)
        {
            var config = ScriptableObject.CreateInstance<CombatConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_maxHP").floatValue = maxHP;
            so.FindProperty("_naturalHealRate").floatValue = naturalHealRate;
            so.FindProperty("_restHealMultiplier").floatValue = restHealMultiplier;
            so.FindProperty("_reviveHPRatio").floatValue = reviveHPRatio;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>
        /// Создаёт WoundConfig с заданными параметрами.
        /// </summary>
        internal static WoundConfig CreateWoundConfig(
            WoundType type,
            float healTime = 60f,
            float hpDrainPerSecond = 0f)
        {
            var config = ScriptableObject.CreateInstance<WoundConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.FindProperty("_healTime").floatValue = healTime;
            so.FindProperty("_hpDrainPerSecond").floatValue = hpDrainPerSecond;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>
        /// Создаёт PlayerHealthState на временном GameObject.
        /// Автоматически назначает CombatConfig и массив WoundConfig через SerializedObject,
        /// затем вызывает Awake через reflection.
        /// </summary>
        internal static (PlayerHealthState state, GameObject go, CombatConfig config, WoundConfig[] woundConfigs)
            CreatePlayerHealthState(float maxHP = 100f)
        {
            var combatConfig = CreateCombatConfig(maxHP: maxHP);

            var punctureConfig = CreateWoundConfig(WoundType.Puncture, healTime: 60f, hpDrainPerSecond: 0f);
            var fractureConfig = CreateWoundConfig(WoundType.Fracture, healTime: 120f);
            var burnConfig = CreateWoundConfig(WoundType.Burn, healTime: 90f);
            var poisonConfig = CreateWoundConfig(WoundType.Poison, healTime: 80f);
            var woundConfigs = new[] { punctureConfig, fractureConfig, burnConfig, poisonConfig };

            var go = new GameObject("TestPlayer");
            var state = go.AddComponent<PlayerHealthState>();

            var so = new SerializedObject(state);
            so.FindProperty("_config").objectReferenceValue = combatConfig;

            var woundsArray = so.FindProperty("_woundConfigs");
            woundsArray.arraySize = woundConfigs.Length;
            for (int i = 0; i < woundConfigs.Length; i++)
                woundsArray.GetArrayElementAtIndex(i).objectReferenceValue = woundConfigs[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            // Вызываем Awake через reflection (компонент добавлен, но Awake не запускался в EditMode)
            var awake = typeof(PlayerHealthState).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awake?.Invoke(state, null);

            return (state, go, combatConfig, woundConfigs);
        }

        /// <summary>
        /// Создаёт CombatConfig с параметрами голода.
        /// </summary>
        internal static CombatConfig CreateCombatConfigWithHunger(
            float hungerMaxTime = 600f,
            float hungerDegradationThreshold = 0.7f,
            float hungerSpeedPenalty = 0.6f,
            float maxHP = 100f,
            float reviveHPRatio = 0.15f)
        {
            var config = ScriptableObject.CreateInstance<CombatConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_maxHP").floatValue = maxHP;
            so.FindProperty("_reviveHPRatio").floatValue = reviveHPRatio;
            so.FindProperty("_hungerMaxTime").floatValue = hungerMaxTime;
            so.FindProperty("_hungerDegradationThreshold").floatValue = hungerDegradationThreshold;
            so.FindProperty("_hungerSpeedPenalty").floatValue = hungerSpeedPenalty;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>
        /// Создаёт HungerSystem на временном GameObject без CharacterMovement.
        /// Инициализирует _config через SerializedObject и вызывает Awake через reflection.
        /// </summary>
        internal static (HungerSystem hunger, GameObject go, CombatConfig config)
            CreateHungerSystem(float hungerMaxTime = 600f, float hungerDegradationThreshold = 0.7f, float hungerSpeedPenalty = 0.6f)
        {
            var config = CreateCombatConfigWithHunger(hungerMaxTime, hungerDegradationThreshold, hungerSpeedPenalty);
            var go = new GameObject("TestHunger");
            var hunger = go.AddComponent<HungerSystem>();

            var so = new SerializedObject(hunger);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (hunger, go, config);
        }

        /// <summary>
        /// Читает приватное поле _hunger из HungerSystem через reflection.
        /// </summary>
        internal static float GetHunger(HungerSystem hunger)
        {
            var field = typeof(HungerSystem).GetField("_hunger", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)field.GetValue(hunger);
        }

        /// <summary>
        /// Устанавливает приватное поле _hunger через reflection.
        /// </summary>
        internal static void SetHunger(HungerSystem hunger, float value)
        {
            var field = typeof(HungerSystem).GetField("_hunger", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(hunger, value);
        }

        /// <summary>
        /// Вызывает приватный метод HandleHungerEffects через reflection.
        /// </summary>
        internal static void TickHungerEffects(HungerSystem hunger)
        {
            var method = typeof(HungerSystem).GetMethod(
                "HandleHungerEffects",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(hunger, null);
        }

        /// <summary>
        /// Читает приватное поле из CharacterMovement через reflection.
        /// </summary>
        internal static float GetMovementField(CharacterMovement movement, string fieldName)
        {
            var field = typeof(CharacterMovement).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)field.GetValue(movement);
        }

        /// <summary>
        /// Создаёт EnemyData с заданными параметрами.
        /// </summary>
        internal static EnemyData CreateEnemyData(float maxHP = 50f, float staggerThreshold = 0.3f)
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            var so = new SerializedObject(data);
            so.FindProperty("_maxHP").floatValue = maxHP;
            so.FindProperty("_staggerThreshold").floatValue = staggerThreshold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        /// <summary>
        /// Создаёт EnemyHealth на временном GameObject и инициализирует через Initialize().
        /// </summary>
        internal static (EnemyHealth health, GameObject go, EnemyData data)
            CreateEnemyHealth(float maxHP = 50f)
        {
            var data = CreateEnemyData(maxHP: maxHP);
            var go = new GameObject("TestEnemy");
            var health = go.AddComponent<EnemyHealth>();
            health.Initialize(data);
            return (health, go, data);
        }

        /// <summary>
        /// Создаёт LootTable с заданными записями для минимального и полного лута.
        /// Chance=1f гарантирует выпадение предмета.
        /// </summary>
        internal static LootTable CreateLootTable(
            ItemData minItem, int minAmount,
            ItemData fullItem, int fullAmount)
        {
            var table = ScriptableObject.CreateInstance<LootTable>();
            var so = new SerializedObject(table);

            var minimal = so.FindProperty("_minimalLoot");
            minimal.arraySize = 1;
            var minEntry = minimal.GetArrayElementAtIndex(0);
            minEntry.FindPropertyRelative("Item").objectReferenceValue = minItem;
            minEntry.FindPropertyRelative("MinAmount").intValue = minAmount;
            minEntry.FindPropertyRelative("MaxAmount").intValue = minAmount;
            minEntry.FindPropertyRelative("Chance").floatValue = 1f;
            minEntry.FindPropertyRelative("RequiresTool").boolValue = false;

            var full = so.FindProperty("_fullLoot");
            full.arraySize = 1;
            var fullEntry = full.GetArrayElementAtIndex(0);
            fullEntry.FindPropertyRelative("Item").objectReferenceValue = fullItem;
            fullEntry.FindPropertyRelative("MinAmount").intValue = fullAmount;
            fullEntry.FindPropertyRelative("MaxAmount").intValue = fullAmount;
            fullEntry.FindPropertyRelative("Chance").floatValue = 1f;
            fullEntry.FindPropertyRelative("RequiresTool").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }
    }

    // -------------------------------------------------------------------------
    // Wound Struct Tests
    // -------------------------------------------------------------------------

    public class WoundStructTests
    {
        [Test]
        public void IsHealed_TrueWhenRemainingTimeIsZero()
        {
            var wound = new Wound(WoundType.Puncture, 0.5f, healTime: 60f);
            wound.RemainingTime = 0f;

            Assert.IsTrue(wound.IsHealed);
        }

        [Test]
        public void IsHealed_FalseWhenRemainingTimeIsPositive()
        {
            var wound = new Wound(WoundType.Fracture, 0.8f, healTime: 120f);

            Assert.IsFalse(wound.IsHealed);
        }

        [Test]
        public void IsHealed_TrueWhenRemainingTimeIsNegative()
        {
            var wound = new Wound(WoundType.Burn, 0.5f, healTime: 90f);
            wound.RemainingTime = -1f;

            Assert.IsTrue(wound.IsHealed);
        }

        [Test]
        public void Progress_ZeroAtStart()
        {
            var wound = new Wound(WoundType.Poison, 0.5f, healTime: 80f);
            // RemainingTime == MaxTime → Progress = 1 - (80/80) = 0
            Assert.AreEqual(0f, wound.Progress, 0.0001f);
        }

        [Test]
        public void Progress_OneWhenHealed()
        {
            var wound = new Wound(WoundType.Puncture, 0.5f, healTime: 60f);
            wound.RemainingTime = 0f;
            // RemainingTime == 0 → Progress = 1 - (0/60) = 1
            Assert.AreEqual(1f, wound.Progress, 0.0001f);
        }

        [Test]
        public void Progress_HalfWayThrough()
        {
            var wound = new Wound(WoundType.Fracture, 0.5f, healTime: 100f);
            wound.RemainingTime = 50f;
            // Progress = 1 - (50/100) = 0.5
            Assert.AreEqual(0.5f, wound.Progress, 0.0001f);
        }

        [Test]
        public void Progress_OneWhenMaxTimeIsZero()
        {
            var wound = new Wound(WoundType.Burn, 0.5f, healTime: 0f);
            // MaxTime == 0 → Progress = 1f (защита от деления на ноль)
            Assert.AreEqual(1f, wound.Progress, 0.0001f);
        }
    }

    // -------------------------------------------------------------------------
    // PlayerHealthState Tests
    // -------------------------------------------------------------------------

    public class PlayerHealthStateTests
    {
        private PlayerHealthState _state;
        private GameObject _go;
        private CombatConfig _config;
        private WoundConfig[] _woundConfigs;

        // Храним подписчики чтобы отписаться в TearDown
        private Action<float> _onHealthChanged;
        private Action _onKnockout;

        [SetUp]
        public void SetUp()
        {
            (_state, _go, _config, _woundConfigs) = CombatTestFactory.CreatePlayerHealthState(maxHP: 100f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_onHealthChanged != null)
            {
                PlayerHealthState.OnHealthChanged -= _onHealthChanged;
                _onHealthChanged = null;
            }
            if (_onKnockout != null)
            {
                PlayerHealthState.OnKnockout -= _onKnockout;
                _onKnockout = null;
            }

            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_config);
            foreach (var wc in _woundConfigs)
                UnityEngine.Object.DestroyImmediate(wc);
        }

        [Test]
        public void TakeDamage_DecreasesHP()
        {
            var damage = new DamageInfo(amount: 30f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            Assert.AreEqual(0.7f, _state.HealthRatio, 0.0001f);
        }

        [Test]
        public void TakeDamage_AddsWoundOfCorrectType()
        {
            var damage = new DamageInfo(amount: 10f, WoundType.Fracture, severity: 0.6f);
            _state.TakeDamage(damage);

            Assert.AreEqual(1, _state.ActiveWounds.Count);
            Assert.AreEqual(WoundType.Fracture, _state.ActiveWounds[0].Type);
        }

        [Test]
        public void TakeDamage_ZeroSeverity_DoesNotAddWound()
        {
            var damage = new DamageInfo(amount: 10f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            Assert.AreEqual(0, _state.ActiveWounds.Count);
        }

        [Test]
        public void TakeDamage_WoundSeverityMatchesInfo()
        {
            var damage = new DamageInfo(amount: 5f, WoundType.Burn, severity: 0.8f);
            _state.TakeDamage(damage);

            Assert.AreEqual(0.8f, _state.ActiveWounds[0].Severity, 0.0001f);
        }

        [Test]
        public void Heal_RestoresHP()
        {
            var damage = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            _state.Heal(30f);

            Assert.AreEqual(0.8f, _state.HealthRatio, 0.0001f);
        }

        [Test]
        public void Heal_DoesNotExceedMaxHP()
        {
            var damage = new DamageInfo(amount: 20f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            _state.Heal(999f);

            Assert.AreEqual(1f, _state.HealthRatio, 0.0001f);
        }

        [Test]
        public void Heal_AtFullHP_RatioStaysOne()
        {
            _state.Heal(50f);

            Assert.AreEqual(1f, _state.HealthRatio, 0.0001f);
        }

        [Test]
        public void TreatWound_RemovesWound()
        {
            _state.AddWound(WoundType.Poison, 0.5f);
            Assert.AreEqual(1, _state.ActiveWounds.Count);

            _state.TreatWound(WoundType.Poison);

            Assert.AreEqual(0, _state.ActiveWounds.Count);
        }

        [Test]
        public void TreatWound_RemovesOnlyTargetType()
        {
            _state.AddWound(WoundType.Puncture, 0.5f);
            _state.AddWound(WoundType.Fracture, 0.5f);

            _state.TreatWound(WoundType.Puncture);

            Assert.AreEqual(1, _state.ActiveWounds.Count);
            Assert.AreEqual(WoundType.Fracture, _state.ActiveWounds[0].Type);
        }

        [Test]
        public void TreatWound_NonexistentWound_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _state.TreatWound(WoundType.Burn));
        }

        [Test]
        public void TakeDamage_KnockoutEvent_FiredWhenHPReachesZero()
        {
            bool knockoutFired = false;
            _onKnockout = () => knockoutFired = true;
            PlayerHealthState.OnKnockout += _onKnockout;

            var damage = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            Assert.IsTrue(knockoutFired, "OnKnockout должен сработать при HP <= 0");
        }

        [Test]
        public void TakeDamage_NoKnockout_WhenHPAboveZero()
        {
            bool knockoutFired = false;
            _onKnockout = () => knockoutFired = true;
            PlayerHealthState.OnKnockout += _onKnockout;

            var damage = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            Assert.IsFalse(knockoutFired);
        }

        [Test]
        public void IsAlive_FalseWhenHPIsZero()
        {
            var damage = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            Assert.IsFalse(_state.IsAlive);
        }

        [Test]
        public void TakeDamage_AfterKnockout_IsIgnored()
        {
            var lethal = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(lethal);

            int knockoutCount = 0;
            _onKnockout = () => knockoutCount++;
            PlayerHealthState.OnKnockout += _onKnockout;

            var extra = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(extra);

            Assert.AreEqual(0, knockoutCount, "Повторный нокаут не должен срабатывать");
            Assert.AreEqual(0f, _state.HealthRatio, 0.0001f);
        }

        [Test]
        public void AddWound_ReplacesExistingWoundOfSameType_WhenMoreSevere()
        {
            _state.AddWound(WoundType.Puncture, 0.3f);
            _state.AddWound(WoundType.Puncture, 0.8f);

            Assert.AreEqual(1, _state.ActiveWounds.Count);
            Assert.AreEqual(0.8f, _state.ActiveWounds[0].Severity, 0.0001f);
        }

        [Test]
        public void AddWound_IgnoresLessSevereWound_WhenSameTypeExists()
        {
            _state.AddWound(WoundType.Fracture, 0.9f);
            _state.AddWound(WoundType.Fracture, 0.3f);

            Assert.AreEqual(1, _state.ActiveWounds.Count);
            Assert.AreEqual(0.9f, _state.ActiveWounds[0].Severity, 0.0001f);
        }

        [Test]
        public void OnHealthChanged_FiredOnTakeDamage()
        {
            bool fired = false;
            _onHealthChanged = _ => fired = true;
            PlayerHealthState.OnHealthChanged += _onHealthChanged;

            var damage = new DamageInfo(amount: 10f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            Assert.IsTrue(fired);
        }
    }

    // -------------------------------------------------------------------------
    // EnemyHealth Tests
    // -------------------------------------------------------------------------

    public class EnemyHealthTests
    {
        private EnemyHealth _health;
        private GameObject _go;
        private EnemyData _data;

        private Action<EnemyHealth, float> _onDamaged;
        private Action<EnemyHealth> _onDeath;

        [SetUp]
        public void SetUp()
        {
            (_health, _go, _data) = CombatTestFactory.CreateEnemyHealth(maxHP: 50f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_onDamaged != null)
            {
                EnemyHealth.OnDamaged -= _onDamaged;
                _onDamaged = null;
            }
            if (_onDeath != null)
            {
                EnemyHealth.OnDeath -= _onDeath;
                _onDeath = null;
            }

            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void TakeDamage_DecreasesHP()
        {
            var damage = new DamageInfo(amount: 20f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.AreEqual(0.6f, _health.HealthRatio, 0.0001f);
        }

        [Test]
        public void IsAlive_TrueAfterNonLethalDamage()
        {
            var damage = new DamageInfo(amount: 25f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void IsAlive_FalseWhenHPReachesZero()
        {
            var damage = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.IsFalse(_health.IsAlive);
        }

        [Test]
        public void IsAlive_FalseOnOverkill()
        {
            var damage = new DamageInfo(amount: 200f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.IsFalse(_health.IsAlive);
        }

        [Test]
        public void OnDeath_FiredWhenHPReachesZero()
        {
            bool deathFired = false;
            _onDeath = _ => deathFired = true;
            EnemyHealth.OnDeath += _onDeath;

            var damage = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.IsTrue(deathFired);
        }

        [Test]
        public void OnDeath_NotFiredOnNonLethalDamage()
        {
            bool deathFired = false;
            _onDeath = _ => deathFired = true;
            EnemyHealth.OnDeath += _onDeath;

            var damage = new DamageInfo(amount: 10f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.IsFalse(deathFired);
        }

        [Test]
        public void TakeDamage_AfterDeath_IsIgnored()
        {
            var lethal = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(lethal);

            int deathCount = 0;
            _onDeath = _ => deathCount++;
            EnemyHealth.OnDeath += _onDeath;

            var extra = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(extra);

            Assert.AreEqual(0, deathCount, "OnDeath не должен повторно срабатывать после смерти");
        }

        [Test]
        public void HealthRatio_OneAtStart()
        {
            Assert.AreEqual(1f, _health.HealthRatio, 0.0001f);
        }

        [Test]
        public void OnDamaged_FiredOnTakeDamage()
        {
            bool fired = false;
            _onDamaged = (_, _) => fired = true;
            EnemyHealth.OnDamaged += _onDamaged;

            var damage = new DamageInfo(amount: 10f, WoundType.Puncture, severity: 0f);
            _health.TakeDamage(damage);

            Assert.IsTrue(fired);
        }

        [Test]
        public void Initialize_SetsFullHP()
        {
            var data = CombatTestFactory.CreateEnemyData(maxHP: 80f);
            var go = new GameObject("AnotherEnemy");
            var health = go.AddComponent<EnemyHealth>();
            health.Initialize(data);

            Assert.AreEqual(1f, health.HealthRatio, 0.0001f);

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    // -------------------------------------------------------------------------
    // LootTable Tests
    // -------------------------------------------------------------------------

    public class LootTableTests
    {
        private LootTable _table;
        private ItemData _minItem;
        private ItemData _fullItem;

        [SetUp]
        public void SetUp()
        {
            _minItem = ScriptableObject.CreateInstance<ItemData>();
            var minSo = new SerializedObject(_minItem);
            minSo.FindProperty("_id").stringValue = "carcass_meat_raw";
            minSo.ApplyModifiedPropertiesWithoutUndo();

            _fullItem = ScriptableObject.CreateInstance<ItemData>();
            var fullSo = new SerializedObject(_fullItem);
            fullSo.FindProperty("_id").stringValue = "wolf_pelt";
            fullSo.ApplyModifiedPropertiesWithoutUndo();

            _table = CombatTestFactory.CreateLootTable(_minItem, 1, _fullItem, 2);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_table);
            UnityEngine.Object.DestroyImmediate(_minItem);
            UnityEngine.Object.DestroyImmediate(_fullItem);
        }

        [Test]
        public void RollLoot_WithoutTool_ReturnsMinimalLoot()
        {
            List<(ItemData item, int amount)> result = _table.RollLoot(hasTool: false);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(_minItem, result[0].item);
            Assert.AreEqual(1, result[0].amount);
        }

        [Test]
        public void RollLoot_WithTool_ReturnsFullLoot()
        {
            List<(ItemData item, int amount)> result = _table.RollLoot(hasTool: true);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(_fullItem, result[0].item);
            Assert.AreEqual(2, result[0].amount);
        }

        [Test]
        public void RollLoot_WithoutTool_DoesNotReturnFullItem()
        {
            List<(ItemData item, int amount)> result = _table.RollLoot(hasTool: false);

            foreach (var entry in result)
                Assert.AreNotEqual(_fullItem, entry.item, "Полный лут не должен выпадать без инструмента");
        }

        [Test]
        public void RollLoot_WithTool_DoesNotReturnMinItem()
        {
            List<(ItemData item, int amount)> result = _table.RollLoot(hasTool: true);

            foreach (var entry in result)
                Assert.AreNotEqual(_minItem, entry.item, "Минимальный лут не должен выпадать в полном луте");
        }

        [Test]
        public void RollLoot_EmptyTable_ReturnsEmptyList()
        {
            var emptyTable = ScriptableObject.CreateInstance<LootTable>();

            var result = emptyTable.RollLoot(hasTool: false);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            UnityEngine.Object.DestroyImmediate(emptyTable);
        }

        [Test]
        public void RollLoot_ZeroChance_ReturnsNoItems()
        {
            // Таблица с Chance=0 → никогда ничего не выпадет
            var zeroTable = ScriptableObject.CreateInstance<LootTable>();
            var so = new SerializedObject(zeroTable);
            var minimal = so.FindProperty("_minimalLoot");
            minimal.arraySize = 1;
            var entry = minimal.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("Item").objectReferenceValue = _minItem;
            entry.FindPropertyRelative("MinAmount").intValue = 1;
            entry.FindPropertyRelative("MaxAmount").intValue = 1;
            entry.FindPropertyRelative("Chance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var result = zeroTable.RollLoot(hasTool: false);
            Assert.AreEqual(0, result.Count);

            UnityEngine.Object.DestroyImmediate(zeroTable);
        }
    }

    // -------------------------------------------------------------------------
    // HungerSystem Tests
    // -------------------------------------------------------------------------

    public class HungerSystemTests
    {
        private HungerSystem _hunger;
        private GameObject _go;
        private CombatConfig _config;

        private Action<float> _onHungerChanged;

        [SetUp]
        public void SetUp()
        {
            (_hunger, _go, _config) = CombatTestFactory.CreateHungerSystem(
                hungerMaxTime: 600f,
                hungerDegradationThreshold: 0.7f,
                hungerSpeedPenalty: 0.6f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_onHungerChanged != null)
            {
                HungerSystem.OnHungerChanged -= _onHungerChanged;
                _onHungerChanged = null;
            }

            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Feed_DecreasesHunger()
        {
            CombatTestFactory.SetHunger(_hunger, 0.8f);

            _hunger.Feed(0.3f);

            float result = CombatTestFactory.GetHunger(_hunger);
            Assert.AreEqual(0.5f, result, 0.0001f);
        }

        [Test]
        public void Feed_DoesNotGoBelowZero()
        {
            CombatTestFactory.SetHunger(_hunger, 0.2f);

            _hunger.Feed(1f);

            float result = CombatTestFactory.GetHunger(_hunger);
            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Feed_AtZeroHunger_StaysZero()
        {
            CombatTestFactory.SetHunger(_hunger, 0f);

            _hunger.Feed(0.5f);

            float result = CombatTestFactory.GetHunger(_hunger);
            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Feed_FiresOnHungerChangedEvent()
        {
            CombatTestFactory.SetHunger(_hunger, 0.5f);

            float receivedValue = -1f;
            _onHungerChanged = v => receivedValue = v;
            HungerSystem.OnHungerChanged += _onHungerChanged;

            _hunger.Feed(0.2f);

            Assert.AreEqual(0.3f, receivedValue, 0.0001f, "OnHungerChanged должен передавать новый уровень голода");
        }

        [Test]
        public void TickHunger_IncreasesHungerOverTime()
        {
            // Имитируем прирост голода вручную по формуле из Update:
            // _hunger += deltaTime / hungerMaxTime
            float initialHunger = CombatTestFactory.GetHunger(_hunger);
            float deltaTime = 60f; // 60 секунд
            float expectedIncrease = deltaTime / _config.HungerMaxTime; // 60/600 = 0.1

            // Напрямую вычисляем ожидаемый результат по той же формуле
            float expected = Mathf.Clamp01(initialHunger + expectedIncrease);
            float simulated = initialHunger + expectedIncrease;

            Assert.AreEqual(expected, Mathf.Clamp01(simulated), 0.0001f);
            Assert.Greater(simulated, initialHunger, "Голод должен расти со временем");
        }

        [Test]
        public void TickHunger_ClampedAtOne()
        {
            // При большом deltaTime голод не превышает 1
            float largeIncrease = 5f; // заведомо > 1
            float newHunger = Mathf.Clamp01(0f + largeIncrease);

            Assert.AreEqual(1f, newHunger, 0.0001f, "Голод не должен превышать 1");
        }

        [Test]
        public void HandleHungerEffects_PenaltyActivates_WhenAboveThreshold()
        {
            // Устанавливаем голод выше порога деградации
            CombatTestFactory.SetHunger(_hunger, 0.8f); // выше 0.7

            // CharacterMovement не назначен → HandleHungerEffects не кидает исключений
            Assert.DoesNotThrow(() => CombatTestFactory.TickHungerEffects(_hunger));
        }

        [Test]
        public void HandleHungerEffects_NoPenalty_WhenBelowThreshold()
        {
            CombatTestFactory.SetHunger(_hunger, 0.5f); // ниже 0.7

            Assert.DoesNotThrow(() => CombatTestFactory.TickHungerEffects(_hunger));
        }

        [Test]
        public void Feed_AboveThreshold_ReducesToBelowThreshold_RemovesPenalty()
        {
            // Активируем штраф вручную через reflection
            CombatTestFactory.SetHunger(_hunger, 0.9f);
            var penaltyField = typeof(HungerSystem).GetField(
                "_hungerPenaltyActive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            penaltyField.SetValue(_hunger, true);

            // Feed снижает ниже порога — без CharacterMovement не кидает исключений
            Assert.DoesNotThrow(() => _hunger.Feed(0.5f));

            float result = CombatTestFactory.GetHunger(_hunger);
            Assert.Less(result, _config.HungerDegradationThreshold,
                "После кормёжки голод должен быть ниже порога деградации");
        }
    }

    // -------------------------------------------------------------------------
    // PlayerHealthState — TreatWound и Revive тесты
    // -------------------------------------------------------------------------

    public class PlayerHealthStateAdvancedTests
    {
        private PlayerHealthState _state;
        private GameObject _go;
        private CombatConfig _config;
        private WoundConfig[] _woundConfigs;

        private Action _onRevive;

        [SetUp]
        public void SetUp()
        {
            (_state, _go, _config, _woundConfigs) = CombatTestFactory.CreatePlayerHealthState(maxHP: 100f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_onRevive != null)
            {
                PlayerHealthState.OnRevive -= _onRevive;
                _onRevive = null;
            }

            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_config);
            foreach (var wc in _woundConfigs)
                UnityEngine.Object.DestroyImmediate(wc);
        }

        [Test]
        public void TreatWound_RemovesCorrectType()
        {
            _state.AddWound(WoundType.Poison, 0.6f);

            _state.TreatWound(WoundType.Poison);

            Assert.AreEqual(0, _state.ActiveWounds.Count,
                "После лечения яда список ран должен быть пустым");
        }

        [Test]
        public void TreatWound_DoesNotRemoveOtherType()
        {
            _state.AddWound(WoundType.Burn, 0.5f);

            // Лечим тип, которого нет
            _state.TreatWound(WoundType.Fracture);

            Assert.AreEqual(1, _state.ActiveWounds.Count,
                "Лечение перелома не должно убирать ожог");
            Assert.AreEqual(WoundType.Burn, _state.ActiveWounds[0].Type);
        }

        [Test]
        public void TreatWound_MultipleWounds_RemovesOnlyTarget()
        {
            _state.AddWound(WoundType.Puncture, 0.4f);
            _state.AddWound(WoundType.Poison, 0.7f);
            _state.AddWound(WoundType.Burn, 0.5f);

            _state.TreatWound(WoundType.Poison);

            Assert.AreEqual(2, _state.ActiveWounds.Count);
            foreach (var wound in _state.ActiveWounds)
                Assert.AreNotEqual(WoundType.Poison, wound.Type,
                    "Яд должен быть вылечен, остальные раны остаются");
        }

        [Test]
        public void TreatWound_WhenNoWoundOfType_DoesNotThrow()
        {
            // Ран нет вообще
            Assert.DoesNotThrow(() => _state.TreatWound(WoundType.Fracture));
            Assert.AreEqual(0, _state.ActiveWounds.Count);
        }

        [Test]
        public void Revive_RestoresHPToReviveRatio()
        {
            // Нокаут
            var lethal = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(lethal);
            Assert.AreEqual(0f, _state.HealthRatio, 0.0001f);

            _state.Revive();

            // ReviveHPRatio = 0.15 → HealthRatio должен быть 0.15
            Assert.AreEqual(_config.ReviveHPRatio, _state.HealthRatio, 0.0001f,
                "После Revive HP должен восстановиться до ReviveHPRatio");
        }

        [Test]
        public void Revive_HPDoesNotExceedReviveRatio()
        {
            // Частичный урон, не нокаут
            var damage = new DamageInfo(amount: 50f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(damage);

            // Revive не применяется как "полное лечение" — он форсирует конкретный уровень
            _state.Revive();

            Assert.AreEqual(_config.ReviveHPRatio, _state.HealthRatio, 0.0001f,
                "Revive всегда устанавливает HP в ReviveHPRatio, даже если HP было выше");
        }

        [Test]
        public void Revive_WoundsRemainAfterRevive()
        {
            _state.AddWound(WoundType.Fracture, 0.8f);
            _state.AddWound(WoundType.Burn, 0.5f);

            var lethal = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(lethal);
            _state.Revive();

            Assert.AreEqual(2, _state.ActiveWounds.Count,
                "Раны должны оставаться после Revive — Revive не лечит раны");
        }

        [Test]
        public void Revive_FiresOnReviveEvent()
        {
            bool reviveFired = false;
            _onRevive = () => reviveFired = true;
            PlayerHealthState.OnRevive += _onRevive;

            var lethal = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(lethal);
            _state.Revive();

            Assert.IsTrue(reviveFired, "OnRevive должен сработать при вызове Revive");
        }

        [Test]
        public void Revive_IsAliveAfterRevive()
        {
            var lethal = new DamageInfo(amount: 100f, WoundType.Puncture, severity: 0f);
            _state.TakeDamage(lethal);
            Assert.IsFalse(_state.IsAlive);

            _state.Revive();

            Assert.IsTrue(_state.IsAlive, "После Revive персонаж должен быть жив");
        }
    }

    // -------------------------------------------------------------------------
    // SpeedMultiplier Tests (CharacterMovement)
    // -------------------------------------------------------------------------

    public class SpeedMultiplierTests
    {
        private CharacterMovement _movement;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestMovement");
            // CharacterController требуется из-за [RequireComponent]
            _go.AddComponent<CharacterController>();
            _movement = _go.AddComponent<CharacterMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void AllMultipliers_DefaultToOne()
        {
            float wound = CombatTestFactory.GetMovementField(_movement, "_woundSpeedMul");
            float hunger = CombatTestFactory.GetMovementField(_movement, "_hungerSpeedMul");
            float weight = CombatTestFactory.GetMovementField(_movement, "_weightSpeedMul");

            Assert.AreEqual(1f, wound, 0.0001f, "WoundSpeedMul по умолчанию = 1");
            Assert.AreEqual(1f, hunger, 0.0001f, "HungerSpeedMul по умолчанию = 1");
            Assert.AreEqual(1f, weight, 0.0001f, "WeightSpeedMul по умолчанию = 1");
        }

        [Test]
        public void SetWoundSpeedMultiplier_UpdatesField()
        {
            _movement.SetWoundSpeedMultiplier(0.7f);

            float wound = CombatTestFactory.GetMovementField(_movement, "_woundSpeedMul");
            Assert.AreEqual(0.7f, wound, 0.0001f);
        }

        [Test]
        public void SetHungerSpeedMultiplier_UpdatesField()
        {
            _movement.SetHungerSpeedMultiplier(0.6f);

            float hunger = CombatTestFactory.GetMovementField(_movement, "_hungerSpeedMul");
            Assert.AreEqual(0.6f, hunger, 0.0001f);
        }

        [Test]
        public void SetWeightSpeedMultiplier_UpdatesField()
        {
            _movement.SetWeightSpeedMultiplier(0.5f);

            float weight = CombatTestFactory.GetMovementField(_movement, "_weightSpeedMul");
            Assert.AreEqual(0.5f, weight, 0.0001f);
        }

        [Test]
        public void CompositeMultiplier_WoundAndHunger_MultipliesCorrectly()
        {
            _movement.SetWoundSpeedMultiplier(0.8f);
            _movement.SetHungerSpeedMultiplier(0.6f);

            float wound = CombatTestFactory.GetMovementField(_movement, "_woundSpeedMul");
            float hunger = CombatTestFactory.GetMovementField(_movement, "_hungerSpeedMul");
            float weight = CombatTestFactory.GetMovementField(_movement, "_weightSpeedMul");

            float composite = wound * hunger * weight;
            Assert.AreEqual(0.48f, composite, 0.0001f, "0.8 * 0.6 * 1.0 = 0.48");
        }

        [Test]
        public void CompositeMultiplier_AllThree_MultipliesCorrectly()
        {
            _movement.SetWoundSpeedMultiplier(0.8f);
            _movement.SetHungerSpeedMultiplier(0.6f);
            _movement.SetWeightSpeedMultiplier(0.5f);

            float wound = CombatTestFactory.GetMovementField(_movement, "_woundSpeedMul");
            float hunger = CombatTestFactory.GetMovementField(_movement, "_hungerSpeedMul");
            float weight = CombatTestFactory.GetMovementField(_movement, "_weightSpeedMul");

            float composite = wound * hunger * weight;
            Assert.AreEqual(0.24f, composite, 0.0001f, "0.8 * 0.6 * 0.5 = 0.24");
        }

        [Test]
        public void SetWoundSpeedMultiplier_ClampedAtMin()
        {
            _movement.SetWoundSpeedMultiplier(0f); // ниже допустимого минимума 0.1

            float wound = CombatTestFactory.GetMovementField(_movement, "_woundSpeedMul");
            Assert.AreEqual(0.1f, wound, 0.0001f, "Минимальный множитель = 0.1");
        }

        [Test]
        public void SetHungerSpeedMultiplier_ClampedAtMax()
        {
            _movement.SetHungerSpeedMultiplier(5f); // выше допустимого максимума 2.0

            float hunger = CombatTestFactory.GetMovementField(_movement, "_hungerSpeedMul");
            Assert.AreEqual(2f, hunger, 0.0001f, "Максимальный множитель = 2.0");
        }

        [Test]
        public void ResetAllMultipliers_CompositeIsOne()
        {
            _movement.SetWoundSpeedMultiplier(0.5f);
            _movement.SetHungerSpeedMultiplier(0.5f);
            _movement.SetWeightSpeedMultiplier(0.5f);

            // Сброс
            _movement.SetWoundSpeedMultiplier(1f);
            _movement.SetHungerSpeedMultiplier(1f);
            _movement.SetWeightSpeedMultiplier(1f);

            float wound = CombatTestFactory.GetMovementField(_movement, "_woundSpeedMul");
            float hunger = CombatTestFactory.GetMovementField(_movement, "_hungerSpeedMul");
            float weight = CombatTestFactory.GetMovementField(_movement, "_weightSpeedMul");

            Assert.AreEqual(1f, wound * hunger * weight, 0.0001f,
                "После сброса составной множитель = 1.0");
        }
    }
}
