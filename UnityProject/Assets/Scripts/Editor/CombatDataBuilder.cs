using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Editor
{
    public static class CombatDataBuilder
    {
        [MenuItem("ZeldaDaughter/Data/Build Combat Data")]
        public static void Build()
        {
            EnsureFolder("Assets/Data/Combat");
            EnsureFolder("Assets/Data/Weapons");

            BuildCombatConfig();
            BuildWoundConfigs();
            BuildWeaponData();
            BuildEnemyData();
            LinkWeaponDataToItems();
            BuildMedicineItems();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CombatDataBuilder] Combat data built.");
        }

        private static void BuildCombatConfig()
        {
            CreateIfNotExists<CombatConfig>("Assets/Data/Combat/CombatConfig.asset");
        }

        private static void BuildWoundConfigs()
        {
            // Puncture — кровотечение
            var puncture = CreateIfNotExists<WoundConfig>("Assets/Data/Combat/WoundConfig_Puncture.asset");
            if (puncture != null)
            {
                var so = new SerializedObject(puncture);
                so.FindProperty("_type").enumValueIndex = (int)WoundType.Puncture;
                so.FindProperty("_healTime").floatValue = 120f;
                so.FindProperty("_hpDrainPerSecond").floatValue = 0.5f;
                so.FindProperty("_speedMultiplier").floatValue = 1f;
                so.FindProperty("_healingItemId").stringValue = "bandage";
                so.ApplyModifiedProperties();
            }

            // Fracture — замедление
            var fracture = CreateIfNotExists<WoundConfig>("Assets/Data/Combat/WoundConfig_Fracture.asset");
            if (fracture != null)
            {
                var so = new SerializedObject(fracture);
                so.FindProperty("_type").enumValueIndex = (int)WoundType.Fracture;
                so.FindProperty("_healTime").floatValue = 180f;
                so.FindProperty("_hpDrainPerSecond").floatValue = 0f;
                so.FindProperty("_speedMultiplier").floatValue = 0.5f;
                so.FindProperty("_healingItemId").stringValue = "splint";
                so.ApplyModifiedProperties();
            }

            // Burn — снижение точности
            var burn = CreateIfNotExists<WoundConfig>("Assets/Data/Combat/WoundConfig_Burn.asset");
            if (burn != null)
            {
                var so = new SerializedObject(burn);
                so.FindProperty("_type").enumValueIndex = (int)WoundType.Burn;
                so.FindProperty("_healTime").floatValue = 150f;
                so.FindProperty("_hpDrainPerSecond").floatValue = 0f;
                so.FindProperty("_accuracyMultiplier").floatValue = 0.5f;
                so.FindProperty("_healingItemId").stringValue = "burn_salve";
                so.ApplyModifiedProperties();
            }

            // Poison — деградация всего
            var poison = CreateIfNotExists<WoundConfig>("Assets/Data/Combat/WoundConfig_Poison.asset");
            if (poison != null)
            {
                var so = new SerializedObject(poison);
                so.FindProperty("_type").enumValueIndex = (int)WoundType.Poison;
                so.FindProperty("_healTime").floatValue = 90f;
                so.FindProperty("_hpDrainPerSecond").floatValue = 0.3f;
                so.FindProperty("_speedMultiplier").floatValue = 0.7f;
                so.FindProperty("_attackSpeedMultiplier").floatValue = 0.7f;
                so.FindProperty("_healingItemId").stringValue = "antidote";
                so.ApplyModifiedProperties();
            }
        }

        private static void BuildWeaponData()
        {
            CreateWeapon("Assets/Data/Weapons/WeaponData_Stick.asset", 8f, 1f, 1.5f, "Attack");
            CreateWeapon("Assets/Data/Weapons/WeaponData_Sword.asset", 20f, 1.2f, 1.8f, "Attack");
            CreateWeapon("Assets/Data/Weapons/WeaponData_Hammer.asset", 30f, 0.7f, 1.5f, "Attack");
        }

        private static void CreateWeapon(string path, float damage, float speed, float range, string animTrigger)
        {
            var weapon = CreateIfNotExists<WeaponData>(path);
            if (weapon != null)
            {
                var so = new SerializedObject(weapon);
                so.FindProperty("_damage").floatValue = damage;
                so.FindProperty("_attackSpeed").floatValue = speed;
                so.FindProperty("_attackRange").floatValue = range;
                so.FindProperty("_animationTrigger").stringValue = animTrigger;
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Прописывает ссылки WeaponData в соответствующие ItemData.
        /// Вызывается после BuildWeaponData — оба SO уже существуют.
        /// </summary>
        private static void LinkWeaponDataToItems()
        {
            // Маппинг: путь к ItemData → путь к WeaponData
            var links = new (string itemPath, string weaponPath)[]
            {
                ("Assets/Data/Items/item_stick.asset",           "Assets/Data/Weapons/WeaponData_Stick.asset"),
                ("Assets/Data/Items/item_sharpened_stick.asset", "Assets/Data/Weapons/WeaponData_Stick.asset"),
                ("Assets/Data/Items/item_sword.asset",           "Assets/Data/Weapons/WeaponData_Sword.asset"),
            };

            foreach (var (itemPath, weaponPath) in links)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
                if (item == null)
                {
                    Debug.LogWarning($"[CombatDataBuilder] ItemData не найден: {itemPath} — пропускаем.");
                    continue;
                }

                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath);
                if (weapon == null)
                {
                    Debug.LogWarning($"[CombatDataBuilder] WeaponData не найден: {weaponPath} — пропускаем.");
                    continue;
                }

                var so = new SerializedObject(item);
                so.FindProperty("_weaponData").objectReferenceValue = weapon;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(item);

                Debug.Log($"[CombatDataBuilder] Привязан {weaponPath} → {itemPath}");
            }
        }

        private static void BuildEnemyData()
        {
            // Кабан
            var boar = CreateIfNotExists<EnemyData>("Assets/Data/Combat/EnemyData_Boar.asset");
            if (boar != null)
            {
                var so = new SerializedObject(boar);
                so.FindProperty("_maxHP").floatValue = 60f;
                so.FindProperty("_damage").floatValue = 20f;
                so.FindProperty("_inflictedWoundType").enumValueIndex = (int)WoundType.Fracture;
                so.FindProperty("_woundSeverity").floatValue = 0.6f;
                so.FindProperty("_moveSpeed").floatValue = 2.5f;
                so.FindProperty("_chaseSpeed").floatValue = 6f;
                so.FindProperty("_aggroRange").floatValue = 6f;
                so.FindProperty("_aggroOnSight").boolValue = false;
                so.FindProperty("_aggroOnDamage").boolValue = true;
                so.FindProperty("_windupTime").floatValue = 0.7f;
                so.ApplyModifiedProperties();
            }

            // Волк
            var wolf = CreateIfNotExists<EnemyData>("Assets/Data/Combat/EnemyData_Wolf.asset");
            if (wolf != null)
            {
                var so = new SerializedObject(wolf);
                so.FindProperty("_maxHP").floatValue = 40f;
                so.FindProperty("_damage").floatValue = 12f;
                so.FindProperty("_inflictedWoundType").enumValueIndex = (int)WoundType.Puncture;
                so.FindProperty("_woundSeverity").floatValue = 0.5f;
                so.FindProperty("_moveSpeed").floatValue = 3f;
                so.FindProperty("_chaseSpeed").floatValue = 7f;
                so.FindProperty("_aggroRange").floatValue = 10f;
                so.FindProperty("_aggroOnSight").boolValue = true;
                so.FindProperty("_aggroOnDamage").boolValue = true;
                so.FindProperty("_windupTime").floatValue = 0.3f;
                so.FindProperty("_attackCooldown").floatValue = 1.5f;
                so.ApplyModifiedProperties();
            }
        }

        private static void BuildMedicineItems()
        {
            EnsureFolder("Assets/Data/Items");

            CreateMedicine("Assets/Data/Items/item_bandage.asset",   "bandage",    "Бинт",             WoundType.Puncture, 0.2f);
            CreateMedicine("Assets/Data/Items/item_splint.asset",    "splint",     "Шина",             WoundType.Fracture, 0.3f);
            CreateMedicine("Assets/Data/Items/item_burn_salve.asset","burn_salve", "Мазь от ожогов",  WoundType.Burn,     0.2f);
            CreateMedicine("Assets/Data/Items/item_antidote.asset",  "antidote",   "Антидот",          WoundType.Poison,   0.2f);
        }

        private static void CreateMedicine(string path, string id, string displayName, WoundType treatsType, float weight)
        {
            var item = CreateIfNotExists<ItemData>(path);
            if (item == null) return; // уже существует

            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_itemType").enumValueIndex = (int)ItemType.Medicine;
            so.FindProperty("_weight").floatValue = weight;
            so.FindProperty("_stackable").boolValue = true;
            so.FindProperty("_maxStack").intValue = 5;
            so.FindProperty("_isMedicine").boolValue = true;
            so.FindProperty("_treatsWoundType").enumValueIndex = (int)treatsType;
            so.ApplyModifiedProperties();

            Debug.Log($"[CombatDataBuilder] Создан лечебный предмет: {displayName} ({id})");
        }

        private static T CreateIfNotExists<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return null; // уже существует, не перезаписываем

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
