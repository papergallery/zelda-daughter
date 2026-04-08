using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class TestFactory
    {
        /// <summary>
        /// Создаёт ItemData с заданными параметрами через SerializedObject.
        /// </summary>
        internal static ItemData CreateItem(
            string id,
            float weight = 1f,
            bool stackable = true,
            int maxStack = 10)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_weight").floatValue = weight;
            so.FindProperty("_stackable").boolValue = stackable;
            so.FindProperty("_maxStack").intValue = maxStack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        /// <summary>
        /// Создаёт двухингредиентный рецепт без станка.
        /// </summary>
        internal static CraftRecipe CreateFieldRecipe(
            ItemData ingredientA,
            ItemData ingredientB,
            ItemData result,
            int resultAmount = 1,
            int countA = 1,
            int countB = 1)
        {
            var recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            var so = new SerializedObject(recipe);
            so.FindProperty("_ingredientA").objectReferenceValue = ingredientA;
            so.FindProperty("_ingredientB").objectReferenceValue = ingredientB;
            so.FindProperty("_ingredientACount").intValue = countA;
            so.FindProperty("_ingredientBCount").intValue = countB;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_resultAmount").intValue = resultAmount;
            so.FindProperty("_requiresStation").boolValue = false;
            so.FindProperty("_stationType").enumValueIndex = (int)StationType.None;
            so.ApplyModifiedPropertiesWithoutUndo();
            return recipe;
        }

        /// <summary>
        /// Создаёт одноингредиентный рецепт без станка.
        /// </summary>
        internal static CraftRecipe CreateSingleRecipe(
            ItemData ingredient,
            ItemData result,
            int resultAmount = 1,
            int countA = 1)
        {
            var recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            var so = new SerializedObject(recipe);
            so.FindProperty("_ingredientA").objectReferenceValue = ingredient;
            so.FindProperty("_ingredientB").objectReferenceValue = null;
            so.FindProperty("_ingredientACount").intValue = countA;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_resultAmount").intValue = resultAmount;
            so.FindProperty("_requiresStation").boolValue = false;
            so.FindProperty("_stationType").enumValueIndex = (int)StationType.None;
            so.ApplyModifiedPropertiesWithoutUndo();
            return recipe;
        }

        /// <summary>
        /// Создаёт рецепт для станка.
        /// </summary>
        internal static CraftRecipe CreateStationRecipe(
            ItemData ingredientA,
            ItemData ingredientB,
            ItemData result,
            StationType stationType,
            int resultAmount = 1)
        {
            var recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            var so = new SerializedObject(recipe);
            so.FindProperty("_ingredientA").objectReferenceValue = ingredientA;
            so.FindProperty("_ingredientB").objectReferenceValue = ingredientB;
            so.FindProperty("_ingredientACount").intValue = 1;
            so.FindProperty("_ingredientBCount").intValue = 1;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_resultAmount").intValue = resultAmount;
            so.FindProperty("_requiresStation").boolValue = true;
            so.FindProperty("_stationType").enumValueIndex = (int)stationType;
            so.ApplyModifiedPropertiesWithoutUndo();
            return recipe;
        }

        /// <summary>
        /// Создаёт CraftRecipeDatabase с заданными рецептами.
        /// </summary>
        internal static CraftRecipeDatabase CreateDatabase(params CraftRecipe[] recipes)
        {
            var db = ScriptableObject.CreateInstance<CraftRecipeDatabase>();
            var so = new SerializedObject(db);
            var list = so.FindProperty("_recipes");
            list.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        /// <summary>
        /// Создаёт PlayerInventory на временном GameObject с заданным конфигом.
        /// </summary>
        internal static (PlayerInventory inventory, GameObject go) CreateInventory(
            int maxSlots = 20,
            float weightCapacity = 50f,
            float overloadThreshold = 0.8f)
        {
            var config = ScriptableObject.CreateInstance<InventoryConfig>();
            var configSo = new SerializedObject(config);
            configSo.FindProperty("_maxSlots").intValue = maxSlots;
            configSo.FindProperty("_baseWeightCapacity").floatValue = weightCapacity;
            configSo.FindProperty("_overloadThreshold").floatValue = overloadThreshold;
            configSo.FindProperty("_overloadSpeedMultiplier").floatValue = 0.5f;
            configSo.ApplyModifiedPropertiesWithoutUndo();

            var go = new GameObject("TestInventory");
            var inv = go.AddComponent<PlayerInventory>();
            var invSo = new SerializedObject(inv);
            invSo.FindProperty("_config").objectReferenceValue = config;
            invSo.FindProperty("_maxSlots").intValue = maxSlots;
            invSo.ApplyModifiedPropertiesWithoutUndo();

            return (inv, go);
        }
    }

    // -------------------------------------------------------------------------
    // CraftRecipeDatabase Tests
    // -------------------------------------------------------------------------

    public class CraftRecipeDatabaseTests
    {
        private ItemData _itemA;
        private ItemData _itemB;
        private ItemData _result;
        private CraftRecipe _fieldRecipe;
        private CraftRecipeDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _itemA = TestFactory.CreateItem("item_a");
            _itemB = TestFactory.CreateItem("item_b");
            _result = TestFactory.CreateItem("result");
            _fieldRecipe = TestFactory.CreateFieldRecipe(_itemA, _itemB, _result);
            _db = TestFactory.CreateDatabase(_fieldRecipe);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_itemA);
            Object.DestroyImmediate(_itemB);
            Object.DestroyImmediate(_result);
            Object.DestroyImmediate(_fieldRecipe);
            Object.DestroyImmediate(_db);
        }

        [Test]
        public void FindFieldRecipe_DirectOrder_ReturnsRecipe()
        {
            var found = _db.FindFieldRecipe(_itemA, _itemB);
            Assert.IsNotNull(found);
            Assert.AreEqual(_result, found.Result);
        }

        [Test]
        public void FindFieldRecipe_ReverseOrder_ReturnsRecipe()
        {
            var found = _db.FindFieldRecipe(_itemB, _itemA);
            Assert.IsNotNull(found);
            Assert.AreEqual(_result, found.Result);
        }

        [Test]
        public void FindFieldRecipe_WrongItem_ReturnsNull()
        {
            var wrongItem = TestFactory.CreateItem("wrong");
            var found = _db.FindFieldRecipe(_itemA, wrongItem);
            Assert.IsNull(found);
            Object.DestroyImmediate(wrongItem);
        }

        [Test]
        public void FindSingleIngredientRecipe_Works()
        {
            var single = TestFactory.CreateItem("single");
            var singleResult = TestFactory.CreateItem("single_result");
            var singleRecipe = TestFactory.CreateSingleRecipe(single, singleResult);
            var db = TestFactory.CreateDatabase(_fieldRecipe, singleRecipe);

            var found = db.FindSingleIngredientRecipe(single);
            Assert.IsNotNull(found);
            Assert.AreEqual(singleResult, found.Result);
            Assert.IsTrue(found.IsSingleIngredient);

            Object.DestroyImmediate(single);
            Object.DestroyImmediate(singleResult);
            Object.DestroyImmediate(singleRecipe);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void FindSingleIngredientRecipe_NotFoundForFieldItem()
        {
            // Двухингредиентный рецепт не должен возвращаться как single
            var found = _db.FindSingleIngredientRecipe(_itemA);
            Assert.IsNull(found);
        }

        [Test]
        public void FindStationRecipe_FieldRecipeNotFound()
        {
            // Полевой рецепт не найдётся через FindStationRecipe
            var found = _db.FindStationRecipe(_itemA, _itemB, StationType.Anvil);
            Assert.IsNull(found);
        }

        [Test]
        public void FindStationRecipe_CorrectStation_ReturnsRecipe()
        {
            var ore = TestFactory.CreateItem("ore");
            var bar = TestFactory.CreateItem("bar");
            var stationRecipe = TestFactory.CreateStationRecipe(ore, null, bar, StationType.Smelter);

            // Станочный одноингредиентный — через FindSingleIngredientRecipe с типом станка
            var db = TestFactory.CreateDatabase(stationRecipe);

            var found = db.FindSingleIngredientRecipe(ore, StationType.Smelter);
            Assert.IsNotNull(found);
            Assert.AreEqual(bar, found.Result);

            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(bar);
            Object.DestroyImmediate(stationRecipe);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void FindStationRecipe_WrongStation_ReturnsNull()
        {
            var ore = TestFactory.CreateItem("ore2");
            var bar = TestFactory.CreateItem("bar2");
            var stationRecipe = TestFactory.CreateStationRecipe(ore, null, bar, StationType.Smelter);
            var db = TestFactory.CreateDatabase(stationRecipe);

            // Запрашиваем для другого станка
            var found = db.FindSingleIngredientRecipe(ore, StationType.Anvil);
            Assert.IsNull(found);

            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(bar);
            Object.DestroyImmediate(stationRecipe);
            Object.DestroyImmediate(db);
        }
    }

    // -------------------------------------------------------------------------
    // PlayerInventory Tests
    // -------------------------------------------------------------------------

    public class PlayerInventoryTests
    {
        private PlayerInventory _inventory;
        private GameObject _go;
        private ItemData _stackableItem;
        private ItemData _uniqueItem;

        [SetUp]
        public void SetUp()
        {
            (_inventory, _go) = TestFactory.CreateInventory(maxSlots: 5, weightCapacity: 20f);
            _stackableItem = TestFactory.CreateItem("stick", weight: 0.5f, stackable: true, maxStack: 5);
            _uniqueItem = TestFactory.CreateItem("sword", weight: 3f, stackable: false, maxStack: 1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_stackableItem);
            Object.DestroyImmediate(_uniqueItem);
        }

        [Test]
        public void AddItem_Stackable_MergesIntoExistingSlot()
        {
            _inventory.AddItem(_stackableItem, 3);
            _inventory.AddItem(_stackableItem, 2);

            Assert.AreEqual(1, _inventory.SlotCount, "Должен быть один слот");
            Assert.AreEqual(5, _inventory.GetItemCount(_stackableItem));
        }

        [Test]
        public void AddItem_Stackable_OverflowsToNewSlot()
        {
            // maxStack = 5: добавляем 7 → должно быть 2 слота (5 + 2)
            _inventory.AddItem(_stackableItem, 7);

            Assert.AreEqual(2, _inventory.SlotCount);
            Assert.AreEqual(7, _inventory.GetItemCount(_stackableItem));
        }

        [Test]
        public void AddItem_NonStackable_EachItemOccupiesOwnSlot()
        {
            _inventory.AddItem(_uniqueItem, 1);
            _inventory.AddItem(_uniqueItem, 1);

            Assert.AreEqual(2, _inventory.SlotCount);
        }

        [Test]
        public void AddItem_ReturnsFalse_WhenInventoryFull()
        {
            // 5 слотов, нестакуемый предмет занимает по одному
            for (int i = 0; i < 5; i++)
                _inventory.AddItem(_uniqueItem, 1);

            bool result = _inventory.AddItem(_uniqueItem, 1);
            Assert.IsFalse(result);
            Assert.AreEqual(5, _inventory.SlotCount);
        }

        [Test]
        public void AddItem_NullItem_ReturnsFalse()
        {
            bool result = _inventory.AddItem(null);
            Assert.IsFalse(result);
            Assert.AreEqual(0, _inventory.SlotCount);
        }

        [Test]
        public void RemoveItem_DecreasesCount()
        {
            _inventory.AddItem(_stackableItem, 5);
            bool removed = _inventory.RemoveItem(_stackableItem, 3);

            Assert.IsTrue(removed);
            Assert.AreEqual(2, _inventory.GetItemCount(_stackableItem));
        }

        [Test]
        public void RemoveItem_ClearsSlotWhenCountReachesZero()
        {
            _inventory.AddItem(_stackableItem, 3);
            _inventory.RemoveItem(_stackableItem, 3);

            Assert.AreEqual(0, _inventory.SlotCount);
        }

        [Test]
        public void RemoveItem_ReturnsFalse_WhenNotEnough()
        {
            _inventory.AddItem(_stackableItem, 2);
            bool result = _inventory.RemoveItem(_stackableItem, 5);

            Assert.IsFalse(result);
            Assert.AreEqual(2, _inventory.GetItemCount(_stackableItem), "Предметы не должны измениться");
        }

        [Test]
        public void HasItem_ReturnsTrue_WhenSufficientAmount()
        {
            _inventory.AddItem(_stackableItem, 3);
            Assert.IsTrue(_inventory.HasItem(_stackableItem, 3));
        }

        [Test]
        public void HasItem_ReturnsFalse_WhenInsufficient()
        {
            _inventory.AddItem(_stackableItem, 2);
            Assert.IsFalse(_inventory.HasItem(_stackableItem, 3));
        }

        [Test]
        public void TotalWeight_CalculatedCorrectly()
        {
            _inventory.AddItem(_stackableItem, 4); // 4 * 0.5 = 2.0
            _inventory.AddItem(_uniqueItem, 1);    // 1 * 3.0 = 3.0

            Assert.AreEqual(5f, _inventory.TotalWeight, 0.001f);
        }

        [Test]
        public void IsOverloaded_TrueWhenWeightExceedsThreshold()
        {
            // weightCapacity=20, threshold=0.8 → граница при 16f
            // Добавляем предмет весом 17f → перегруз
            var heavyItem = TestFactory.CreateItem("heavy", weight: 17f, stackable: false, maxStack: 1);
            _inventory.AddItem(heavyItem, 1);

            Assert.IsTrue(_inventory.IsOverloaded);

            Object.DestroyImmediate(heavyItem);
        }

        [Test]
        public void IsOverloaded_FalseWhenWeightBelowThreshold()
        {
            var lightItem = TestFactory.CreateItem("light", weight: 1f, stackable: false, maxStack: 1);
            _inventory.AddItem(lightItem, 1); // ratio = 1/20 = 0.05

            Assert.IsFalse(_inventory.IsOverloaded);

            Object.DestroyImmediate(lightItem);
        }

        [Test]
        public void SwapSlots_ExchangesContents()
        {
            var itemC = TestFactory.CreateItem("item_c");
            _inventory.AddItem(_stackableItem, 1);
            _inventory.AddItem(itemC, 1);

            _inventory.SwapSlots(0, 1);

            Assert.AreEqual(itemC, _inventory.GetSlot(0).Item);
            Assert.AreEqual(_stackableItem, _inventory.GetSlot(1).Item);

            Object.DestroyImmediate(itemC);
        }

        [Test]
        public void Clear_RemovesAllItems()
        {
            _inventory.AddItem(_stackableItem, 3);
            _inventory.AddItem(_uniqueItem, 1);
            _inventory.Clear();

            Assert.AreEqual(0, _inventory.SlotCount);
        }

        [Test]
        public void RemoveFromSlot_ByIndex_Works()
        {
            _inventory.AddItem(_stackableItem, 5);
            bool result = _inventory.RemoveFromSlot(0, 3);

            Assert.IsTrue(result);
            Assert.AreEqual(2, _inventory.GetItemCount(_stackableItem));
        }
    }

    // -------------------------------------------------------------------------
    // CraftingSystem Tests
    // -------------------------------------------------------------------------

    public class CraftingSystemTests
    {
        private PlayerInventory _inventory;
        private GameObject _go;
        private ItemData _stick;
        private ItemData _stone;
        private ItemData _knife;
        private CraftRecipe _knifeRecipe;
        private CraftRecipeDatabase _db;

        [SetUp]
        public void SetUp()
        {
            (_inventory, _go) = TestFactory.CreateInventory(maxSlots: 20);

            _stick = TestFactory.CreateItem("stick", stackable: true, maxStack: 10);
            _stone = TestFactory.CreateItem("stone", stackable: true, maxStack: 10);
            _knife = TestFactory.CreateItem("knife", stackable: false, maxStack: 1);

            // Рецепт: палка + камень = нож
            _knifeRecipe = TestFactory.CreateFieldRecipe(_stick, _stone, _knife, countA: 1, countB: 1);
            _db = TestFactory.CreateDatabase(_knifeRecipe);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_stick);
            Object.DestroyImmediate(_stone);
            Object.DestroyImmediate(_knife);
            Object.DestroyImmediate(_knifeRecipe);
            Object.DestroyImmediate(_db);
        }

        [Test]
        public void TryCraft_TwoIngredients_ProducesResult()
        {
            _inventory.AddItem(_stick, 1);
            _inventory.AddItem(_stone, 1);

            bool success = CraftingSystem.TryCraft(0, 1, _inventory, _db);

            Assert.IsTrue(success);
            Assert.AreEqual(1, _inventory.GetItemCount(_knife));
            Assert.AreEqual(0, _inventory.GetItemCount(_stick));
            Assert.AreEqual(0, _inventory.GetItemCount(_stone));
        }

        [Test]
        public void TryCraft_ReverseSlotOrder_StillWorks()
        {
            _inventory.AddItem(_stone, 1);  // slot 0
            _inventory.AddItem(_stick, 1);  // slot 1

            bool success = CraftingSystem.TryCraft(1, 0, _inventory, _db);

            Assert.IsTrue(success);
            Assert.AreEqual(1, _inventory.GetItemCount(_knife));
        }

        [Test]
        public void TryCraft_NoMatchingRecipe_ReturnsFalse()
        {
            var unrelated = TestFactory.CreateItem("unrelated");
            _inventory.AddItem(_stick, 1);
            _inventory.AddItem(unrelated, 1);

            bool success = CraftingSystem.TryCraft(0, 1, _inventory, _db);

            Assert.IsFalse(success);
            // Ингредиенты не потрачены
            Assert.AreEqual(1, _inventory.GetItemCount(_stick));

            Object.DestroyImmediate(unrelated);
        }

        [Test]
        public void TryCraft_InsufficientAmount_ReturnsFalse()
        {
            // Рецепт: нужно 2 палки + 1 камень
            var recipe2 = TestFactory.CreateFieldRecipe(_stick, _stone, _knife, countA: 2, countB: 1);
            var db2 = TestFactory.CreateDatabase(recipe2);

            _inventory.AddItem(_stick, 1); // только 1, нужно 2
            _inventory.AddItem(_stone, 1);

            bool success = CraftingSystem.TryCraft(0, 1, _inventory, db2);

            Assert.IsFalse(success);
            Assert.AreEqual(1, _inventory.GetItemCount(_stick));

            Object.DestroyImmediate(recipe2);
            Object.DestroyImmediate(db2);
        }

        [Test]
        public void TrySingleCraft_ProducesResult()
        {
            var rope = TestFactory.CreateItem("rope");
            var net = TestFactory.CreateItem("net");
            var singleRecipe = TestFactory.CreateSingleRecipe(rope, net, countA: 3);
            var db = TestFactory.CreateDatabase(singleRecipe);

            _inventory.AddItem(rope, 5);

            bool success = CraftingSystem.TrySingleCraft(0, _inventory, db);

            Assert.IsTrue(success);
            Assert.AreEqual(1, _inventory.GetItemCount(net));
            Assert.AreEqual(2, _inventory.GetItemCount(rope)); // 5 - 3 = 2

            Object.DestroyImmediate(rope);
            Object.DestroyImmediate(net);
            Object.DestroyImmediate(singleRecipe);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void TryStationCraft_ProducesResult()
        {
            var ore = TestFactory.CreateItem("iron_ore");
            var bar = TestFactory.CreateItem("iron_bar");
            var smeltRecipe = TestFactory.CreateStationRecipe(ore, null, bar, StationType.Smelter);
            var db = TestFactory.CreateDatabase(smeltRecipe);

            _inventory.AddItem(ore, 3);

            bool success = CraftingSystem.TryStationCraft(ore, null, StationType.Smelter, _inventory, db);

            Assert.IsTrue(success);
            Assert.AreEqual(1, _inventory.GetItemCount(bar));
            Assert.AreEqual(2, _inventory.GetItemCount(ore));

            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(bar);
            Object.DestroyImmediate(smeltRecipe);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void TryStationCraft_WrongStation_ReturnsFalse()
        {
            var ore = TestFactory.CreateItem("ore_x");
            var bar = TestFactory.CreateItem("bar_x");
            var smeltRecipe = TestFactory.CreateStationRecipe(ore, null, bar, StationType.Smelter);
            var db = TestFactory.CreateDatabase(smeltRecipe);

            _inventory.AddItem(ore, 1);

            bool success = CraftingSystem.TryStationCraft(ore, null, StationType.Anvil, _inventory, db);

            Assert.IsFalse(success);
            Assert.AreEqual(0, _inventory.GetItemCount(bar));

            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(bar);
            Object.DestroyImmediate(smeltRecipe);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void TryCraft_NullInventory_ReturnsFalse()
        {
            bool result = CraftingSystem.TryCraft(0, 1, null, _db);
            Assert.IsFalse(result);
        }

        [Test]
        public void TryCraft_NullDatabase_ReturnsFalse()
        {
            _inventory.AddItem(_stick, 1);
            bool result = CraftingSystem.TryCraft(0, 1, _inventory, null);
            Assert.IsFalse(result);
        }
    }
}
