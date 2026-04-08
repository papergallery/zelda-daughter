using UnityEngine;

namespace ZeldaDaughter.Inventory
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Craft Recipe", fileName = "NewCraftRecipe")]
    public class CraftRecipe : ScriptableObject
    {
        [SerializeField] private ItemData _ingredientA;
        [SerializeField] private ItemData _ingredientB; // null для одноингредиентного крафта
        [SerializeField] private int _ingredientACount = 1;
        [SerializeField] private int _ingredientBCount = 1;
        [SerializeField] private ItemData _result;
        [SerializeField] private int _resultAmount = 1;
        [SerializeField] private bool _requiresStation;
        [SerializeField] private StationType _stationType = StationType.None;

        public ItemData IngredientA => _ingredientA;
        public ItemData IngredientB => _ingredientB;
        public int IngredientACount => _ingredientACount;
        public int IngredientBCount => _ingredientBCount;
        public ItemData Result => _result;
        public int ResultAmount => _resultAmount;
        public bool RequiresStation => _requiresStation;
        public StationType StationType => _stationType;
        public bool IsSingleIngredient => _ingredientB == null;
    }
}
