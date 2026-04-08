using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    public static class WeaponTrainingAssetBuilder
    {
        private const string ProgressionFolder = "Assets/Data/Progression";
        private const string WeaponsFolder = "Assets/Data/Weapons";
        private const string CombatFolder = "Assets/Data/Combat";

        [MenuItem("ZeldaDaughter/Data/Build Weapon Training Data")]
        public static void Build()
        {
            EnsureFolder(ProgressionFolder);
            EnsureFolder(WeaponsFolder);
            EnsureFolder(CombatFolder);

            BuildWeaponProficiencyData();
            BuildWeaponDataAssets();
            BuildTrainingDummyConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeaponTrainingAssetBuilder] Weapon training data built.");
        }

        private static void BuildWeaponProficiencyData()
        {
            string path = $"{ProgressionFolder}/WeaponProficiencyData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponProficiencyData>(path);

            WeaponProficiencyData asset;
            if (existing != null)
            {
                asset = existing;
            }
            else
            {
                asset = ScriptableObject.CreateInstance<WeaponProficiencyData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("_entries");
            entriesProp.arraySize = 4;

            SetWeaponProfEntry(entriesProp.GetArrayElementAtIndex(0),
                WeaponType.Sword, baseGrowthRate: 0.8f, maxValue: 100f, decay: 0.5f, failureMul: 0.3f);
            SetWeaponProfEntry(entriesProp.GetArrayElementAtIndex(1),
                WeaponType.Bow,   baseGrowthRate: 0.6f, maxValue: 100f, decay: 0.4f, failureMul: 0.4f);
            SetWeaponProfEntry(entriesProp.GetArrayElementAtIndex(2),
                WeaponType.Hammer, baseGrowthRate: 0.7f, maxValue: 100f, decay: 0.5f, failureMul: 0.25f);
            SetWeaponProfEntry(entriesProp.GetArrayElementAtIndex(3),
                WeaponType.Fists, baseGrowthRate: 1.0f, maxValue: 100f, decay: 0.6f, failureMul: 0.5f);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            Debug.Log("[WeaponTrainingAssetBuilder] WeaponProficiencyData создан/обновлён.");
        }

        private static void SetWeaponProfEntry(SerializedProperty entryProp,
            WeaponType type, float baseGrowthRate, float maxValue, float decay, float failureMul)
        {
            entryProp.FindPropertyRelative("Type").enumValueIndex = (int)type;
            entryProp.FindPropertyRelative("BaseGrowthRate").floatValue = baseGrowthRate;
            entryProp.FindPropertyRelative("MaxValue").floatValue = maxValue;
            entryProp.FindPropertyRelative("DecayExponent").floatValue = decay;
            entryProp.FindPropertyRelative("FailureMultiplier").floatValue = failureMul;
        }

        private static void BuildWeaponDataAssets()
        {
            // Bow
            var bow = CreateOrLoad<WeaponData>($"{WeaponsFolder}/WeaponData_Bow.asset");
            {
                var so = new SerializedObject(bow);
                so.FindProperty("_damage").floatValue = 8f;
                so.FindProperty("_attackSpeed").floatValue = 0.7f;
                so.FindProperty("_attackRange").floatValue = 10f;
                so.FindProperty("_weaponType").enumValueIndex = (int)WeaponType.Bow;
                so.FindProperty("_isRanged").boolValue = true;
                so.FindProperty("_projectileSpeed").floatValue = 15f;
                so.FindProperty("_animationTrigger").stringValue = "Attack";
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(bow);
            }

            // Hammer
            var hammer = CreateOrLoad<WeaponData>($"{WeaponsFolder}/WeaponData_Hammer.asset");
            {
                var so = new SerializedObject(hammer);
                so.FindProperty("_damage").floatValue = 20f;
                so.FindProperty("_attackSpeed").floatValue = 0.5f;
                so.FindProperty("_attackRange").floatValue = 1.8f;
                so.FindProperty("_weaponType").enumValueIndex = (int)WeaponType.Hammer;
                so.FindProperty("_isRanged").boolValue = false;
                so.FindProperty("_stunDuration").floatValue = 2f;
                so.FindProperty("_animationTrigger").stringValue = "Attack";
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hammer);
            }

            // Fists
            var fists = CreateOrLoad<WeaponData>($"{WeaponsFolder}/WeaponData_Fists.asset");
            {
                var so = new SerializedObject(fists);
                so.FindProperty("_damage").floatValue = 3f;
                so.FindProperty("_attackSpeed").floatValue = 2.0f;
                so.FindProperty("_attackRange").floatValue = 1.0f;
                so.FindProperty("_weaponType").enumValueIndex = (int)WeaponType.Fists;
                so.FindProperty("_isRanged").boolValue = false;
                so.FindProperty("_rapidHitCount").intValue = 3;
                so.FindProperty("_animationTrigger").stringValue = "Attack";
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(fists);
            }

            Debug.Log("[WeaponTrainingAssetBuilder] WeaponData для Bow, Hammer, Fists создан/обновлён.");
        }

        private static void BuildTrainingDummyConfig()
        {
            string path = $"{CombatFolder}/TrainingDummyConfig.asset";
            var asset = CreateOrLoad<TrainingDummyConfig>(path);

            var so = new SerializedObject(asset);
            so.FindProperty("_hitsRequired").intValue = 10;
            so.FindProperty("_xpReward").floatValue = 5f;
            so.FindProperty("_completionReply").stringValue = "Неплохо. Приходи ещё.";
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            Debug.Log("[WeaponTrainingAssetBuilder] TrainingDummyConfig создан/обновлён.");
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

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
