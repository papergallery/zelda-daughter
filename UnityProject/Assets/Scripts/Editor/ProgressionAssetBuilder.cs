using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    public static class ProgressionAssetBuilder
    {
        private const string DataFolder = "Assets/Data/Progression";

        [MenuItem("ZeldaDaughter/Data/Build Progression Data")]
        public static void Build()
        {
            EnsureFolder(DataFolder);

            BuildGrowthCurves();
            var effectConfig = BuildEffectConfig();
            BuildProgressionConfig(effectConfig);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProgressionAssetBuilder] Progression data built.");
        }

        private static void BuildGrowthCurves()
        {
            // StatType | baseGrowthRate | decayExponent | maxValue | failureMultiplier | victoryBonus
            CreateGrowthCurve(StatType.Strength,     0.8f, 0.5f, 100f, 0.2f, 3.0f);
            CreateGrowthCurve(StatType.Toughness,    1.0f, 0.6f, 100f, 0.3f, 1.0f);
            CreateGrowthCurve(StatType.Agility,      0.5f, 0.5f, 100f, 0.3f, 2.0f);
            CreateGrowthCurve(StatType.Accuracy,     0.7f, 0.4f, 100f, 0.4f, 2.5f);
            CreateGrowthCurve(StatType.Endurance,    0.3f, 0.5f, 100f, 0.3f, 1.0f);
            CreateGrowthCurve(StatType.CarryCapacity, 0.4f, 0.6f, 100f, 0.3f, 0.5f);
        }

        private static void CreateGrowthCurve(StatType type, float baseGrowthRate, float decayExponent,
            float maxValue, float failureMultiplier, float victoryBonus)
        {
            string path = $"{DataFolder}/GrowthCurve_{type}.asset";
            var asset = CreateIfNotExists<StatGrowthCurve>(path);
            if (asset == null) return;

            var so = new SerializedObject(asset);
            so.FindProperty("_statType").enumValueIndex = (int)type;
            so.FindProperty("_baseGrowthRate").floatValue = baseGrowthRate;
            so.FindProperty("_decayExponent").floatValue = decayExponent;
            so.FindProperty("_maxValue").floatValue = maxValue;
            so.FindProperty("_failureMultiplier").floatValue = failureMultiplier;
            so.FindProperty("_victoryBonus").floatValue = victoryBonus;
            so.ApplyModifiedProperties();

            Debug.Log($"[ProgressionAssetBuilder] GrowthCurve создан: {type}");
        }

        private static StatEffectConfig BuildEffectConfig()
        {
            string path = $"{DataFolder}/StatEffectConfig.asset";
            var asset = CreateIfNotExists<StatEffectConfig>(path);

            if (asset == null)
            {
                // Уже существует — загружаем для последующей привязки
                return AssetDatabase.LoadAssetAtPath<StatEffectConfig>(path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("_maxDamageBonus").floatValue = 1.5f;
            so.FindProperty("_maxDamageReduction").floatValue = 0.5f;
            so.FindProperty("_maxAttackSpeedBonus").floatValue = 0.8f;
            so.FindProperty("_baseHitChance").floatValue = 0.5f;
            so.FindProperty("_maxHealBonus").floatValue = 1.0f;
            so.FindProperty("_maxCapacityBonus").floatValue = 2.0f;

            var tierThresholds = so.FindProperty("_tierThresholds");
            tierThresholds.arraySize = 4;
            tierThresholds.GetArrayElementAtIndex(0).floatValue = 0f;
            tierThresholds.GetArrayElementAtIndex(1).floatValue = 25f;
            tierThresholds.GetArrayElementAtIndex(2).floatValue = 50f;
            tierThresholds.GetArrayElementAtIndex(3).floatValue = 80f;

            so.FindProperty("_tierReplicaCooldown").floatValue = 30f;
            so.ApplyModifiedProperties();

            Debug.Log("[ProgressionAssetBuilder] StatEffectConfig создан.");
            return asset;
        }

        private static void BuildProgressionConfig(StatEffectConfig effectConfig)
        {
            string path = $"{DataFolder}/ProgressionConfig.asset";
            var asset = CreateIfNotExists<ProgressionConfig>(path);
            if (asset == null) return;

            // Загружаем все 6 кривых
            var curves = new StatGrowthCurve[]
            {
                AssetDatabase.LoadAssetAtPath<StatGrowthCurve>($"{DataFolder}/GrowthCurve_Strength.asset"),
                AssetDatabase.LoadAssetAtPath<StatGrowthCurve>($"{DataFolder}/GrowthCurve_Toughness.asset"),
                AssetDatabase.LoadAssetAtPath<StatGrowthCurve>($"{DataFolder}/GrowthCurve_Agility.asset"),
                AssetDatabase.LoadAssetAtPath<StatGrowthCurve>($"{DataFolder}/GrowthCurve_Accuracy.asset"),
                AssetDatabase.LoadAssetAtPath<StatGrowthCurve>($"{DataFolder}/GrowthCurve_Endurance.asset"),
                AssetDatabase.LoadAssetAtPath<StatGrowthCurve>($"{DataFolder}/GrowthCurve_CarryCapacity.asset"),
            };

            var so = new SerializedObject(asset);

            var curvesProp = so.FindProperty("_growthCurves");
            curvesProp.arraySize = curves.Length;
            for (int i = 0; i < curves.Length; i++)
                curvesProp.GetArrayElementAtIndex(i).objectReferenceValue = curves[i];

            so.FindProperty("_effectConfig").objectReferenceValue = effectConfig;
            so.ApplyModifiedProperties();

            Debug.Log("[ProgressionAssetBuilder] ProgressionConfig создан и привязан.");
        }

        private static T CreateIfNotExists<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return null;

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
