using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Core;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class WeaponProfTestFactory
    {
        internal static WeaponProficiencyData CreateData(
            float swordRate = 0.8f, float swordMax = 100f, float swordDecay = 0.5f, float swordFailMul = 0.3f,
            float bowRate   = 0.6f, float bowMax   = 100f, float bowDecay   = 0.4f, float bowFailMul   = 0.4f)
        {
            var data = ScriptableObject.CreateInstance<WeaponProficiencyData>();
            var so = new SerializedObject(data);

            var entries = so.FindProperty("_entries");
            entries.arraySize = 2;

            SetEntry(entries.GetArrayElementAtIndex(0), WeaponType.Sword, swordRate, swordMax, swordDecay, swordFailMul);
            SetEntry(entries.GetArrayElementAtIndex(1), WeaponType.Bow,   bowRate,   bowMax,   bowDecay,   bowFailMul);

            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        internal static WeaponProficiency CreateProficiency(WeaponProficiencyData data)
        {
            var go = new GameObject("TestProficiency");
            var prof = go.AddComponent<WeaponProficiency>();

            var so = new SerializedObject(prof);
            so.FindProperty("_data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Вызываем Awake через reflection
            typeof(WeaponProficiency)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(prof, null);

            return prof;
        }

        private static void SetEntry(SerializedProperty entryProp,
            WeaponType type, float baseRate, float maxValue, float decay, float failMul)
        {
            entryProp.FindPropertyRelative("Type").enumValueIndex = (int)type;
            entryProp.FindPropertyRelative("BaseGrowthRate").floatValue = baseRate;
            entryProp.FindPropertyRelative("MaxValue").floatValue = maxValue;
            entryProp.FindPropertyRelative("DecayExponent").floatValue = decay;
            entryProp.FindPropertyRelative("FailureMultiplier").floatValue = failMul;
        }
    }

    // -------------------------------------------------------------------------
    // WeaponProficiencyData Tests
    // -------------------------------------------------------------------------

    public class WeaponProficiencyDataTests
    {
        private WeaponProficiencyData _data;

        [SetUp]
        public void SetUp()
        {
            _data = WeaponProfTestFactory.CreateData();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void CalculateGrowth_AtZero_ReturnsMaxGrowth()
        {
            // currentValue=0, normalized=0 → (1-0)^decay = 1 → growth = rawAmount * baseRate * 1
            float growth = _data.CalculateGrowth(WeaponType.Sword, 0f, 5f);
            // swordRate=0.8 → 5 * 0.8 * 1.0 = 4.0
            Assert.AreEqual(4f, growth, 0.001f);
        }

        [Test]
        public void CalculateGrowth_AtMax_ReturnsZero()
        {
            // currentValue=maxValue → normalized=1 → (1-1)^decay = 0 → growth = 0
            float growth = _data.CalculateGrowth(WeaponType.Sword, 100f, 5f);
            Assert.AreEqual(0f, growth, 0.001f);
        }

        [Test]
        public void GetEntry_ExistingType_ReturnsEntry()
        {
            var entry = _data.GetEntry(WeaponType.Sword);
            Assert.AreEqual(WeaponType.Sword, entry.Type);
            Assert.AreEqual(0.8f, entry.BaseGrowthRate, 0.001f);
            Assert.AreEqual(100f, entry.MaxValue, 0.001f);
        }
    }

    // -------------------------------------------------------------------------
    // WeaponProficiency Tests
    // -------------------------------------------------------------------------

    public class WeaponProficiencyTests
    {
        private WeaponProficiencyData _data;
        private WeaponProficiency _proficiency;
        private GameObject _go;

        // Держим ссылки на подписанные делегаты для отписки в TearDown
        private Action<WeaponType, float, float> _subscribedHandler;

        [SetUp]
        public void SetUp()
        {
            _subscribedHandler = null;
            _data = WeaponProfTestFactory.CreateData();
            _proficiency = WeaponProfTestFactory.CreateProficiency(_data);
            _go = _proficiency.gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            if (_subscribedHandler != null)
                WeaponProficiency.OnProficiencyChanged -= _subscribedHandler;
            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void AddExperience_IncreasesValue()
        {
            _proficiency.AddExperience(WeaponType.Sword, 10f);
            float value = _proficiency.GetProficiency(WeaponType.Sword);
            Assert.Greater(value, 0f, "Мастерство меча должно вырасти после AddExperience");
        }

        [Test]
        public void AddExperience_FiresOnProficiencyChanged()
        {
            WeaponType firedType = default;
            float firedOld = -1f;
            float firedNew = -1f;
            bool eventFired = false;

            _subscribedHandler = (type, oldVal, newVal) =>
            {
                firedType = type;
                firedOld = oldVal;
                firedNew = newVal;
                eventFired = true;
            };
            WeaponProficiency.OnProficiencyChanged += _subscribedHandler;

            _proficiency.AddExperience(WeaponType.Sword, 10f);

            Assert.IsTrue(eventFired, "OnProficiencyChanged должно быть вызвано");
            Assert.AreEqual(WeaponType.Sword, firedType);
            Assert.AreEqual(0f, firedOld, 0.001f, "Старое значение должно быть 0");
            Assert.Greater(firedNew, 0f, "Новое значение должно быть > 0");
        }

        [Test]
        public void GetProficiencyNormalized_ReturnsCorrectRatio()
        {
            // При currentValue=0, baseRate=0.8, decay=0.5, rawAmount=50:
            // growth = 50 * 0.8 * 1.0 = 40.0; normalized = 40/100 = 0.4
            _proficiency.AddExperience(WeaponType.Sword, 50f);
            float normalized = _proficiency.GetProficiencyNormalized(WeaponType.Sword);
            Assert.Greater(normalized, 0f, "Нормализованное значение должно быть > 0");
            Assert.LessOrEqual(normalized, 1f, "Нормализованное значение не должно превышать 1");

            float raw = _proficiency.GetProficiency(WeaponType.Sword);
            Assert.AreEqual(raw / 100f, normalized, 0.001f, "normalized = value / maxValue");
        }

        [Test]
        public void CaptureState_RestoreState_Preserves()
        {
            _proficiency.AddExperience(WeaponType.Sword, 20f);
            _proficiency.AddExperience(WeaponType.Bow, 15f);

            float swordBefore = _proficiency.GetProficiency(WeaponType.Sword);
            float bowBefore = _proficiency.GetProficiency(WeaponType.Bow);

            object savedState = _proficiency.CaptureState();

            // Восстанавливаем в новый экземпляр
            var data2 = WeaponProfTestFactory.CreateData();
            var prof2 = WeaponProfTestFactory.CreateProficiency(data2);
            var go2 = prof2.gameObject;

            try
            {
                prof2.RestoreState(savedState);

                Assert.AreEqual(swordBefore, prof2.GetProficiency(WeaponType.Sword), 0.001f,
                    "Мастерство меча должно восстановиться");
                Assert.AreEqual(bowBefore, prof2.GetProficiency(WeaponType.Bow), 0.001f,
                    "Мастерство лука должно восстановиться");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go2);
                UnityEngine.Object.DestroyImmediate(data2);
            }
        }
    }

    // -------------------------------------------------------------------------
    // GenericObjectPool Tests
    // -------------------------------------------------------------------------

    public class ObjectPoolTests
    {
        [Test]
        public void Get_ReturnsNewObject()
        {
            int createCount = 0;
            var pool = new GenericObjectPool<string>(() =>
            {
                createCount++;
                return $"item_{createCount}";
            });

            string obj = pool.Get();

            Assert.IsNotNull(obj, "Get должен вернуть объект");
            Assert.AreEqual(1, createCount, "Должен быть создан один объект");
        }

        [Test]
        public void Release_And_Get_ReturnsSameObject()
        {
            var pool = new GenericObjectPool<string>(() => System.Guid.NewGuid().ToString());

            string first = pool.Get();
            pool.Release(first);

            string second = pool.Get();

            Assert.AreEqual(first, second,
                "После Release следующий Get должен вернуть тот же объект");
        }

        [Test]
        public void Prewarm_CreatesObjects()
        {
            int createCount = 0;
            var pool = new GenericObjectPool<int>(
                () => { createCount++; return createCount; },
                initialCapacity: 5);

            Assert.AreEqual(5, createCount, "Prewarm должен создать 5 объектов");
            Assert.AreEqual(5, pool.CountInactive, "Все prewarm-объекты должны быть в пуле");
        }

        [Test]
        public void Get_AfterPrewarm_DoesNotCreateNew()
        {
            int createCount = 0;
            var pool = new GenericObjectPool<int>(() => ++createCount, initialCapacity: 3);

            Assert.AreEqual(3, createCount);

            // Берём все 3 из пула — новых не создаём
            pool.Get();
            pool.Get();
            pool.Get();

            Assert.AreEqual(3, createCount, "Get из заполненного пула не должен создавать новые объекты");
        }

        [Test]
        public void Get_BeyondPrewarm_CreatesNew()
        {
            int createCount = 0;
            var pool = new GenericObjectPool<int>(() => ++createCount, initialCapacity: 2);

            pool.Get(); pool.Get();  // из пула
            pool.Get();              // новый

            Assert.AreEqual(3, createCount, "Get сверх пула должен создать новый объект");
        }

        [Test]
        public void Release_IncreasesCountInactive()
        {
            var pool = new GenericObjectPool<string>(() => "item");

            string obj = pool.Get();
            Assert.AreEqual(0, pool.CountInactive);

            pool.Release(obj);
            Assert.AreEqual(1, pool.CountInactive, "После Release CountInactive должен вырасти");
        }

        [Test]
        public void NullCreateFunc_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GenericObjectPool<string>(null));
        }

        [Test]
        public void OnGet_Callback_IsInvoked()
        {
            bool callbackInvoked = false;
            var pool = new GenericObjectPool<int>(
                () => 42,
                onGet: _ => callbackInvoked = true);

            pool.Get();

            Assert.IsTrue(callbackInvoked, "onGet callback должен быть вызван при Get");
        }

        [Test]
        public void OnRelease_Callback_IsInvoked()
        {
            bool callbackInvoked = false;
            var pool = new GenericObjectPool<int>(
                () => 42,
                onRelease: _ => callbackInvoked = true);

            int obj = pool.Get();
            pool.Release(obj);

            Assert.IsTrue(callbackInvoked, "onRelease callback должен быть вызван при Release");
        }

        [Test]
        public void MultipleRelease_AllReturnedToPool()
        {
            var pool = new GenericObjectPool<int>(() => 0);

            int a = pool.Get();
            int b = pool.Get();
            int c = pool.Get();

            pool.Release(a);
            pool.Release(b);
            pool.Release(c);

            Assert.AreEqual(3, pool.CountInactive, "Все 3 объекта должны быть возвращены в пул");
        }
    }
}
