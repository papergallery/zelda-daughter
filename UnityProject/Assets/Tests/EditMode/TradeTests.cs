using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.Tests.EditMode
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static class TradeTestFactory
    {
        internal static ItemData CreateItem(string id, int baseValue, ItemType itemType = ItemType.Generic)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = id;
            so.FindProperty("_baseValue").intValue = baseValue;
            so.FindProperty("_itemType").enumValueIndex = (int)itemType;
            so.FindProperty("_stackable").boolValue = true;
            so.FindProperty("_maxStack").intValue = 99;
            so.FindProperty("_weight").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        /// <summary>
        /// Создаёт TradeInventoryData с одним предметом и опциональным модификатором цены.
        /// </summary>
        internal static TradeInventoryData CreateTradeInventoryData(
            ItemData item,
            int stockQty = 5,
            float modifier = 1f)
        {
            var data = ScriptableObject.CreateInstance<TradeInventoryData>();
            var so = new SerializedObject(data);

            var stock = so.FindProperty("_stock");
            stock.arraySize = 1;
            var entry = stock.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("quantity").intValue = stockQty;
            entry.FindPropertyRelative("basePrice").intValue = item.BaseValue;

            if (!Mathf.Approximately(modifier, 1f))
            {
                var mods = so.FindProperty("_priceModifiers");
                mods.arraySize = 1;
                var mod = mods.GetArrayElementAtIndex(0);
                mod.FindPropertyRelative("itemType").enumValueIndex = (int)item.ItemType;
                mod.FindPropertyRelative("multiplier").floatValue = modifier;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        /// <summary>
        /// Создаёт MerchantInventory на временном GameObject.
        /// Вызывает Awake через reflection для инициализации stock.
        /// </summary>
        internal static (MerchantInventory merchant, GameObject go) CreateMerchant(
            TradeInventoryData tradeData,
            string merchantId = "test_merchant")
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<MerchantInventory>();

            var so = new SerializedObject(merchant);
            so.FindProperty("_baseData").objectReferenceValue = tradeData;
            so.FindProperty("_merchantId").stringValue = merchantId;
            so.ApplyModifiedPropertiesWithoutUndo();

            var awake = typeof(MerchantInventory).GetMethod(
                "Awake",
                BindingFlags.NonPublic | BindingFlags.Instance);
            awake?.Invoke(merchant, null);

            return (merchant, go);
        }
    }

    // -------------------------------------------------------------------------
    // TradeInventoryData Tests
    // -------------------------------------------------------------------------

    public class TradeInventoryDataTests
    {
        private ItemData _item;
        private TradeInventoryData _data;

        [SetUp]
        public void SetUp()
        {
            _item = TradeTestFactory.CreateItem("sword", baseValue: 100, itemType: ItemType.Weapon);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_item);
            if (_data != null) Object.DestroyImmediate(_data);
        }

        [Test]
        public void GetBuyPrice_WithModifier_AppliesMultiplier()
        {
            // Модификатор 1.5 для типа Weapon: baseValue(100) * 1.5 = 150
            _data = TradeTestFactory.CreateTradeInventoryData(_item, modifier: 1.5f);
            int price = _data.GetBuyPrice(_item);
            Assert.AreEqual(150, price,
                "GetBuyPrice должен применять модификатор типа предмета");
        }

        [Test]
        public void GetBuyPrice_WithoutModifier_ReturnsBaseValue()
        {
            // Без модификаторов: baseValue(100) * 1.0 = 100
            _data = TradeTestFactory.CreateTradeInventoryData(_item, modifier: 1f);
            int price = _data.GetBuyPrice(_item);
            Assert.AreEqual(100, price,
                "GetBuyPrice без модификатора должен вернуть BaseValue предмета");
        }

        [Test]
        public void GetSellPrice_HigherThanBuyPrice()
        {
            // GetSellPrice = GetBuyPrice * 1.5, значит SellPrice > BuyPrice всегда
            _data = TradeTestFactory.CreateTradeInventoryData(_item, modifier: 1f);
            int buy = _data.GetBuyPrice(_item);
            int sell = _data.GetSellPrice(_item);
            Assert.Greater(sell, buy,
                "GetSellPrice должен быть больше GetBuyPrice (торговец продаёт дороже)");
        }
    }

    // -------------------------------------------------------------------------
    // TradeManager Static Tests
    // -------------------------------------------------------------------------

    public class TradeManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            TradeManager.ClearEvents();
        }

        [Test]
        public void IsTradeBalanced_EqualValues_ReturnsTrue()
        {
            Assert.IsTrue(TradeManager.IsTradeBalanced(100, 100),
                "Равные ценности должны быть справедливой сделкой");
        }

        [Test]
        public void IsTradeBalanced_PlayerOfferingMore_ReturnsTrue()
        {
            Assert.IsTrue(TradeManager.IsTradeBalanced(80, 100),
                "Игрок предлагает больше — сделка должна быть разрешена");
        }

        [Test]
        public void IsTradeBalanced_PlayerOfferingLess_ReturnsFalse()
        {
            Assert.IsFalse(TradeManager.IsTradeBalanced(100, 50),
                "Игрок предлагает меньше — торговец не согласится");
        }
    }

    // -------------------------------------------------------------------------
    // MerchantInventory Tests
    // -------------------------------------------------------------------------

    public class MerchantInventoryTests
    {
        private ItemData _item;
        private TradeInventoryData _tradeData;
        private MerchantInventory _merchant;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _item = TradeTestFactory.CreateItem("potion", baseValue: 20);
            _tradeData = TradeTestFactory.CreateTradeInventoryData(_item, stockQty: 5);
            (_merchant, _go) = TradeTestFactory.CreateMerchant(_tradeData);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_tradeData);
            Object.DestroyImmediate(_item);
        }

        [Test]
        public void SellToPlayer_DecreasesQuantity()
        {
            Assert.AreEqual(5, _merchant.CurrentStock[0].Quantity,
                "Начальный stock должен быть 5");

            _merchant.SellToPlayer(0, 2);

            Assert.AreEqual(3, _merchant.CurrentStock[0].Quantity,
                "После продажи 2 единиц должно остаться 3");
        }

        [Test]
        public void BuyFromPlayer_AddsToStock()
        {
            var extraItem = TradeTestFactory.CreateItem("herb", baseValue: 5);

            try
            {
                int countBefore = _merchant.CurrentStock.Count;
                _merchant.BuyFromPlayer(extraItem, 3);

                Assert.AreEqual(countBefore + 1, _merchant.CurrentStock.Count,
                    "BuyFromPlayer с новым предметом должен добавить новый слот");

                // Найдём добавленный слот
                TradeSlot? found = null;
                foreach (var slot in _merchant.CurrentStock)
                {
                    if (slot.Item == extraItem) { found = slot; break; }
                }

                Assert.IsNotNull(found, "Предмет должен быть найден в stock после BuyFromPlayer");
                Assert.AreEqual(3, found.Value.Quantity, "Количество в новом слоте должно быть 3");
            }
            finally
            {
                Object.DestroyImmediate(extraItem);
            }
        }
    }
}
