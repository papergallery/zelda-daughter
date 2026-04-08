using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Inventory
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Craft Recipe Database", fileName = "CraftRecipeDatabase")]
    public class CraftRecipeDatabase : ScriptableObject
    {
        [SerializeField] private List<CraftRecipe> _recipes = new();

        public IReadOnlyList<CraftRecipe> Recipes => _recipes;

        /// <summary>
        /// Ищет полевой рецепт (не требующий станка) по двум ингредиентам.
        /// Проверяет оба порядка (A+B и B+A).
        /// </summary>
        public CraftRecipe FindFieldRecipe(ItemData a, ItemData b)
        {
            return FindRecipe(a, b, StationType.None, requireStation: false);
        }

        /// <summary>
        /// Ищет рецепт для конкретного станка.
        /// </summary>
        public CraftRecipe FindStationRecipe(ItemData a, ItemData b, StationType stationType)
        {
            return FindRecipe(a, b, stationType, requireStation: true);
        }

        /// <summary>
        /// Ищет одноингредиентный рецепт.
        /// </summary>
        public CraftRecipe FindSingleIngredientRecipe(ItemData item, StationType stationType = StationType.None)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                var r = _recipes[i];
                if (!r.IsSingleIngredient) continue;
                if (r.IngredientA != item) continue;
                if (stationType == StationType.None && r.RequiresStation) continue;
                if (stationType != StationType.None && r.StationType != stationType) continue;
                return r;
            }
            return null;
        }

        private CraftRecipe FindRecipe(ItemData a, ItemData b, StationType stationType, bool requireStation)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                var r = _recipes[i];
                if (r.IsSingleIngredient) continue;
                if (r.RequiresStation != requireStation) continue;
                if (requireStation && r.StationType != stationType) continue;

                bool matchDirect = r.IngredientA == a && r.IngredientB == b;
                bool matchReverse = r.IngredientA == b && r.IngredientB == a;

                if (matchDirect || matchReverse)
                    return r;
            }
            return null;
        }
    }
}
