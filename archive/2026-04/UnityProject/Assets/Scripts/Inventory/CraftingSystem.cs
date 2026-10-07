using UnityEngine;

namespace ZeldaDaughter.Inventory
{
    public static class CraftingSystem
    {
        public static event System.Action<CraftRecipe> OnCraftSuccess;
        public static event System.Action<ItemData, ItemData> OnCraftFailed;

        /// <summary>
        /// Пытается скрафтить предмет из двух слотов.
        /// </summary>
        public static bool TryCraft(int slotA, int slotB, PlayerInventory inventory, CraftRecipeDatabase db)
        {
            if (inventory == null || db == null)
                return false;

            var stackA = inventory.GetSlot(slotA);
            var stackB = inventory.GetSlot(slotB);

            if (stackA.Item == null)
            {
                // Попробовать single-ingredient
                if (stackB.Item != null)
                    return TrySingleCraft(slotB, inventory, db);
                return false;
            }

            if (stackB.Item == null)
            {
                return TrySingleCraft(slotA, inventory, db);
            }

            var recipe = db.FindFieldRecipe(stackA.Item, stackB.Item);
            if (recipe == null)
            {
                OnCraftFailed?.Invoke(stackA.Item, stackB.Item);
                return false;
            }

            // Определяем какой слот соответствует IngredientA рецепта
            bool aIsFirst = recipe.IngredientA == stackA.Item;
            int needA = aIsFirst ? recipe.IngredientACount : recipe.IngredientBCount;
            int needB = aIsFirst ? recipe.IngredientBCount : recipe.IngredientACount;

            if (stackA.Amount < needA || stackB.Amount < needB)
            {
                OnCraftFailed?.Invoke(stackA.Item, stackB.Item);
                return false;
            }

            // Удаляем сначала больший индекс, чтобы не сбить нумерацию при удалении
            int first = Mathf.Max(slotA, slotB);
            int second = Mathf.Min(slotA, slotB);
            int needFirst = first == slotA ? needA : needB;
            int needSecond = second == slotA ? needA : needB;

            inventory.RemoveFromSlot(first, needFirst);
            inventory.RemoveFromSlot(second, needSecond);

            inventory.AddItem(recipe.Result, recipe.ResultAmount);

            OnCraftSuccess?.Invoke(recipe);
            return true;
        }

        /// <summary>
        /// Пытается скрафтить из одного ингредиента (дроп на пустой слот).
        /// </summary>
        public static bool TrySingleCraft(int slot, PlayerInventory inventory, CraftRecipeDatabase db)
        {
            var stack = inventory.GetSlot(slot);
            if (stack.Item == null) return false;

            var recipe = db.FindSingleIngredientRecipe(stack.Item);
            if (recipe == null) return false;

            if (stack.Amount < recipe.IngredientACount)
                return false;

            inventory.RemoveFromSlot(slot, recipe.IngredientACount);
            inventory.AddItem(recipe.Result, recipe.ResultAmount);

            OnCraftSuccess?.Invoke(recipe);
            return true;
        }

        /// <summary>
        /// Крафт на станке.
        /// </summary>
        public static bool TryStationCraft(ItemData a, ItemData b, StationType stationType,
            PlayerInventory inventory, CraftRecipeDatabase db)
        {
            if (inventory == null || db == null)
                return false;

            CraftRecipe recipe;
            if (b == null)
            {
                recipe = db.FindSingleIngredientRecipe(a, stationType);
            }
            else
            {
                recipe = db.FindStationRecipe(a, b, stationType);
            }

            if (recipe == null)
            {
                OnCraftFailed?.Invoke(a, b);
                return false;
            }

            if (!inventory.HasItem(a, recipe.IngredientACount))
            {
                OnCraftFailed?.Invoke(a, b);
                return false;
            }
            if (b != null && !inventory.HasItem(b, recipe.IngredientBCount))
            {
                OnCraftFailed?.Invoke(a, b);
                return false;
            }

            inventory.RemoveItem(a, recipe.IngredientACount);
            if (b != null)
                inventory.RemoveItem(b, recipe.IngredientBCount);

            inventory.AddItem(recipe.Result, recipe.ResultAmount);

            OnCraftSuccess?.Invoke(recipe);
            return true;
        }
    }
}
