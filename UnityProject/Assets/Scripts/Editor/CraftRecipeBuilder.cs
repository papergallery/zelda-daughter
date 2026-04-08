using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Editor
{
    public static class CraftRecipeBuilder
    {
        private const string ItemsPath = "Assets/Data/Items";
        private const string RecipesPath = "Assets/Data/Recipes";

        [MenuItem("ZeldaDaughter/Build Craft Recipes")]
        public static void BuildCraftRecipes()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(ItemsPath);
            EnsureFolder(RecipesPath);

            // --- Предметы-ингредиенты (уже могут существовать из InteractablePrefabBuilder) ---
            var stick      = GetOrCreateItem("item_stick",        "Палка",             ItemType.Material,   weight: 0.3f, stackable: true,  maxStack: 10, pickupLine: "Палка... сгодится");
            var cloth      = GetOrCreateItem("item_cloth",        "Ткань",             ItemType.Material,   weight: 0.2f, stackable: true,  maxStack: 5,  pickupLine: "Кусок ткани");
            var knife      = GetOrCreateItem("item_knife",        "Нож",               ItemType.Tool,       weight: 0.4f, stackable: false, maxStack: 1,  pickupLine: "Острый нож");
            var axe        = GetOrCreateItem("item_axe",          "Топор",             ItemType.Tool,       weight: 1.2f, stackable: false, maxStack: 1,  pickupLine: "Тяжёлый топор");
            var firewood   = GetOrCreateItem("item_firewood",     "Дрова",             ItemType.Material,   weight: 1.0f, stackable: true,  maxStack: 5,  pickupLine: "Дрова. Пригодятся", isPlaceable: true);
            var herbs      = GetOrCreateItem("item_herbs",        "Травы",             ItemType.Material,   weight: 0.1f, stackable: true,  maxStack: 10, pickupLine: "Пучок трав");
            var rareHerbs  = GetOrCreateItem("item_rare_herbs",   "Особая трава",      ItemType.Material,   weight: 0.1f, stackable: true,  maxStack: 5,  pickupLine: "Редкая трава с необычным запахом");
            var fat        = GetOrCreateItem("item_fat",          "Жир",               ItemType.Material,   weight: 0.3f, stackable: true,  maxStack: 5,  pickupLine: "Животный жир");
            var healHerbs  = GetOrCreateItem("item_healing_herbs","Целебные травы",    ItemType.Material,   weight: 0.1f, stackable: true,  maxStack: 5,  pickupLine: "Пахнет лекарством");

            // --- Предметы-результаты (ручной крафт) ---
            var unlitTorch = GetOrCreateItem("item_unlit_torch",  "Факел (незажжённый)", ItemType.Tool,     weight: 0.5f, stackable: false, maxStack: 1,  pickupLine: "Факел. Нужен огонь");
            var sharpStick = GetOrCreateItem("item_sharpened_stick", "Заострённая палка", ItemType.Weapon,  weight: 0.4f, stackable: false, maxStack: 1,  pickupLine: "Острый конец внушает уважение");
            var planks     = GetOrCreateItem("item_planks",       "Доски",             ItemType.Material,   weight: 1.5f, stackable: true,  maxStack: 5,  pickupLine: "Ровные доски");
            var bandage    = GetOrCreateItem("item_bandage",      "Бинт",              ItemType.Consumable, weight: 0.1f, stackable: true,  maxStack: 5,  pickupLine: "Пригодится при ранении");
            var splint     = GetOrCreateItem("item_splint",       "Шина",              ItemType.Consumable, weight: 0.3f, stackable: true,  maxStack: 3,  pickupLine: "Для перелома");
            var burnSalve  = GetOrCreateItem("item_burn_salve",   "Мазь от ожогов",    ItemType.Consumable, weight: 0.2f, stackable: true,  maxStack: 3,  pickupLine: "Холодит и заживляет");
            var antidote   = GetOrCreateItem("item_antidote",     "Антидот",           ItemType.Consumable, weight: 0.2f, stackable: true,  maxStack: 3,  pickupLine: "Нейтрализует яд");

            // --- Предметы для станочного крафта ---
            var ore        = GetOrCreateItem("item_ore",          "Руда",              ItemType.Material,   weight: 2.0f, stackable: true,  maxStack: 5,  pickupLine: "Тяжёлая руда");
            var metal      = GetOrCreateItem("item_metal",        "Металл",            ItemType.Material,   weight: 1.5f, stackable: true,  maxStack: 5,  pickupLine: "Выплавленный металл");
            var shortStick = GetOrCreateItem("item_short_stick",  "Короткая палка",    ItemType.Material,   weight: 0.2f, stackable: true,  maxStack: 10, pickupLine: "Короткая палка");
            var sword      = GetOrCreateItem("item_sword",        "Меч",               ItemType.Weapon,     weight: 2.5f, stackable: false, maxStack: 1,  pickupLine: "Боевой меч");

            // --- Рецепты ручного крафта ---
            var recipes = new List<CraftRecipe>
            {
                // 1. Палка + Ткань = Факел незажжённый
                CreateRecipe("Recipe_UnlitTorch",
                    ingredientA: stick,      countA: 1,
                    ingredientB: cloth,      countB: 1,
                    result: unlitTorch,      resultAmount: 1),

                // 2. Нож + Палка = Заострённая палка
                CreateRecipe("Recipe_SharpenedStick",
                    ingredientA: knife,      countA: 1,
                    ingredientB: stick,      countB: 1,
                    result: sharpStick,      resultAmount: 1),

                // 3. Топор + Дрова = Доски
                CreateRecipe("Recipe_Planks",
                    ingredientA: axe,        countA: 1,
                    ingredientB: firewood,   countB: 1,
                    result: planks,          resultAmount: 2),

                // 4. Ткань + Травы = Бинт
                CreateRecipe("Recipe_Bandage",
                    ingredientA: cloth,      countA: 1,
                    ingredientB: herbs,      countB: 1,
                    result: bandage,         resultAmount: 1),

                // 5. Палка + Бинт = Шина
                CreateRecipe("Recipe_Splint",
                    ingredientA: stick,      countA: 1,
                    ingredientB: bandage,    countB: 1,
                    result: splint,          resultAmount: 1),

                // 6. Особая трава + Жир = Мазь от ожогов
                CreateRecipe("Recipe_BurnSalve",
                    ingredientA: rareHerbs,  countA: 1,
                    ingredientB: fat,        countB: 1,
                    result: burnSalve,       resultAmount: 1),

                // 7. Целебные травы = Антидот (одноингредиентный)
                CreateRecipeSingle("Recipe_Antidote",
                    ingredient: healHerbs,   count: 1,
                    result: antidote,        resultAmount: 1),
            };

            // --- Станочные рецепты (Этап 3) ---
            var stationRecipes = BuildStationRecipes(ore, metal, stick, shortStick, knife, sword);
            recipes.AddRange(stationRecipes);

            // --- База рецептов ---
            BuildDatabase(recipes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CraftRecipeBuilder] Готово: {recipes.Count} рецептов ({stationRecipes.Count} станочных).");
        }

        /// <summary>Создаёт станочные рецепты для Плавильни и Наковальни.</summary>
        private static List<CraftRecipe> BuildStationRecipes(
            ItemData ore, ItemData metal,
            ItemData stick, ItemData shortStick,
            ItemData knife, ItemData sword)
        {
            var list = new List<CraftRecipe>();

            // Плавильня: Руда → Металл (одиночный ингредиент)
            list.Add(CreateRecipeStation("Recipe_Station_SmeltOre",
                ingredientA: ore, countA: 1,
                ingredientB: null, countB: 0,
                result: metal, resultAmount: 1,
                stationType: StationType.Smelter));

            // Наковальня: Металл + Палка → Меч
            list.Add(CreateRecipeStation("Recipe_Station_ForgeSword",
                ingredientA: metal, countA: 1,
                ingredientB: stick, countB: 1,
                result: sword, resultAmount: 1,
                stationType: StationType.Anvil));

            // Наковальня: Металл + Короткая палка → Нож
            // Нож уже есть как item_knife — переиспользуем
            list.Add(CreateRecipeStation("Recipe_Station_ForgeKnife",
                ingredientA: metal,      countA: 1,
                ingredientB: shortStick, countB: 1,
                result: knife,           resultAmount: 1,
                stationType: StationType.Anvil));

            return list;
        }

        // Возвращает существующий ItemData по id (ищет в Assets/Data/Items/) или создаёт новый.
        private static ItemData GetOrCreateItem(
            string id, string displayName, ItemType itemType,
            float weight, bool stackable, int maxStack, string pickupLine,
            bool isPlaceable = false)
        {
            string assetPath = $"{ItemsPath}/{id}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (existing != null)
            {
                Debug.Log($"[CraftRecipeBuilder] ItemData уже существует: {assetPath}");
                return existing;
            }

            var item = ScriptableObject.CreateInstance<ItemData>();
            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_itemType").enumValueIndex = (int)itemType;
            so.FindProperty("_weight").floatValue = weight;
            so.FindProperty("_stackable").boolValue = stackable;
            so.FindProperty("_maxStack").intValue = maxStack;
            so.FindProperty("_pickupLine").stringValue = pickupLine;
            so.FindProperty("_isPlaceable").boolValue = isPlaceable;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(item, assetPath);
            Debug.Log($"[CraftRecipeBuilder] ItemData создан: {assetPath}");
            return item;
        }

        private static CraftRecipe CreateRecipe(
            string assetName,
            ItemData ingredientA, int countA,
            ItemData ingredientB, int countB,
            ItemData result, int resultAmount)
        {
            string assetPath = $"{RecipesPath}/{assetName}.asset";
            DeleteIfExists(assetPath);

            var recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            var so = new SerializedObject(recipe);
            so.FindProperty("_ingredientA").objectReferenceValue = ingredientA;
            so.FindProperty("_ingredientACount").intValue = countA;
            so.FindProperty("_ingredientB").objectReferenceValue = ingredientB;
            so.FindProperty("_ingredientBCount").intValue = countB;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_resultAmount").intValue = resultAmount;
            so.FindProperty("_requiresStation").boolValue = false;
            so.FindProperty("_stationType").enumValueIndex = (int)StationType.None;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(recipe, assetPath);
            Debug.Log($"[CraftRecipeBuilder] Рецепт создан: {assetPath}");
            return recipe;
        }

        private static CraftRecipe CreateRecipeSingle(
            string assetName,
            ItemData ingredient, int count,
            ItemData result, int resultAmount)
        {
            string assetPath = $"{RecipesPath}/{assetName}.asset";
            DeleteIfExists(assetPath);

            var recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            var so = new SerializedObject(recipe);
            so.FindProperty("_ingredientA").objectReferenceValue = ingredient;
            so.FindProperty("_ingredientACount").intValue = count;
            // _ingredientB оставляем null — это и есть одноингредиентный рецепт
            so.FindProperty("_ingredientBCount").intValue = 1;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_resultAmount").intValue = resultAmount;
            so.FindProperty("_requiresStation").boolValue = false;
            so.FindProperty("_stationType").enumValueIndex = (int)StationType.None;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(recipe, assetPath);
            Debug.Log($"[CraftRecipeBuilder] Рецепт (одиночный) создан: {assetPath}");
            return recipe;
        }

        private static CraftRecipe CreateRecipeStation(
            string assetName,
            ItemData ingredientA, int countA,
            ItemData ingredientB, int countB,
            ItemData result, int resultAmount,
            StationType stationType)
        {
            string assetPath = $"{RecipesPath}/{assetName}.asset";
            DeleteIfExists(assetPath);

            var recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            var so = new SerializedObject(recipe);
            so.FindProperty("_ingredientA").objectReferenceValue = ingredientA;
            so.FindProperty("_ingredientACount").intValue = countA;
            so.FindProperty("_ingredientB").objectReferenceValue = ingredientB;
            so.FindProperty("_ingredientBCount").intValue = countB;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_resultAmount").intValue = resultAmount;
            so.FindProperty("_requiresStation").boolValue = true;
            so.FindProperty("_stationType").enumValueIndex = (int)stationType;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(recipe, assetPath);
            Debug.Log($"[CraftRecipeBuilder] Станочный рецепт создан: {assetPath} ({stationType})");
            return recipe;
        }

        private static void BuildDatabase(List<CraftRecipe> recipes)
        {
            string dbPath = $"{RecipesPath}/CraftRecipeDatabase.asset";
            DeleteIfExists(dbPath);

            var db = ScriptableObject.CreateInstance<CraftRecipeDatabase>();
            var so = new SerializedObject(db);
            var recipesProp = so.FindProperty("_recipes");
            recipesProp.arraySize = recipes.Count;
            for (int i = 0; i < recipes.Count; i++)
                recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(db, dbPath);
            Debug.Log($"[CraftRecipeBuilder] CraftRecipeDatabase создана: {dbPath}");
        }

        private static void DeleteIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                Debug.Log($"[CraftRecipeBuilder] Перезапись: {assetPath}");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int lastSlash = path.LastIndexOf('/');
            string parent = path[..lastSlash];
            string folderName = path[(lastSlash + 1)..];
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
