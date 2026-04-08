using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Generates the Player prefab from the KayKit animated character model.
    /// Run via: ZeldaDaughter/Player/Build Player Prefab
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        private const string FbxPath = "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";
        private const string GlbFallbackPath = "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Rogue.glb";
        private const string AnimatorControllerPath = "Assets/Animations/Controllers/PlayerAnimator.controller";
        private const string PrefabOutputDir = "Assets/Prefabs/Player";
        private const string PrefabOutputPath = "Assets/Prefabs/Player/Player.prefab";

        private const float ControllerRadius = 0.3f;
        private const float ControllerHeight = 1.8f;
        private const float ControllerCenterY = 0.9f;
        private const float SlopeLimit = 45f;
        private const float StepOffset = 0.3f;

        [MenuItem("ZeldaDaughter/Player/Build Player Prefab")]
        public static void BuildPlayerPrefab()
        {
            string modelPath = ResolveModelPath();
            if (modelPath == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] Модель персонажа не найдена. Ожидался FBX или GLB.");
                return;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] Не удалось загрузить модель: {modelPath}");
                return;
            }

            EnsureOutputFolder();
            EnsurePlayerTag();

            var root = new GameObject("Player");

            // Визуальная модель — дочерний объект, чтобы CharacterController оставался на root
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(root.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            // CharacterController на root
            var controller = root.AddComponent<CharacterController>();
            controller.radius = ControllerRadius;
            controller.height = ControllerHeight;
            controller.center = new Vector3(0f, ControllerCenterY, 0f);
            controller.slopeLimit = SlopeLimit;
            controller.stepOffset = StepOffset;

            // Animator на root — ищет clips в иерархии через GetComponentInChildren
            var animator = root.AddComponent<Animator>();
            var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
                Debug.Log($"[PlayerPrefabBuilder] AnimatorController назначен: {AnimatorControllerPath}");
            }
            else
            {
                Debug.LogWarning($"[PlayerPrefabBuilder] AnimatorController не найден: {AnimatorControllerPath}. Запустите ZeldaDaughter/Animation/Build Player Animator.");
            }

            // Игровые компоненты
            root.AddComponent<ZeldaDaughter.Input.CharacterMovement>();
            root.AddComponent<ZeldaDaughter.Input.CharacterAutoMove>();
            root.AddComponent<ZeldaDaughter.World.SurfaceDetector>();
            var inventory = root.AddComponent<ZeldaDaughter.Inventory.PlayerInventory>();

            // --- Progression ---
            var progressionConfig = AssetDatabase.LoadAssetAtPath<ProgressionConfig>("Assets/Data/Progression/ProgressionConfig.asset");
            var effectConfig = AssetDatabase.LoadAssetAtPath<StatEffectConfig>("Assets/Data/Progression/StatEffectConfig.asset");

            if (progressionConfig == null)
                Debug.LogWarning("[PlayerPrefabBuilder] ProgressionConfig не найден: Assets/Data/Progression/ProgressionConfig.asset");
            if (effectConfig == null)
                Debug.LogWarning("[PlayerPrefabBuilder] StatEffectConfig не найден: Assets/Data/Progression/StatEffectConfig.asset");

            var playerStats = root.AddComponent<PlayerStats>();
            var psSO = new SerializedObject(playerStats);
            psSO.FindProperty("_config").objectReferenceValue = progressionConfig;
            psSO.ApplyModifiedPropertiesWithoutUndo();

            var actionTracker = root.AddComponent<ActionTracker>();
            var atSO = new SerializedObject(actionTracker);
            atSO.FindProperty("_playerStats").objectReferenceValue = playerStats;
            atSO.FindProperty("_playerInventory").objectReferenceValue = inventory;
            atSO.ApplyModifiedPropertiesWithoutUndo();

            var effectApplier = root.AddComponent<StatEffectApplier>();
            var eaSO = new SerializedObject(effectApplier);
            eaSO.FindProperty("_playerStats").objectReferenceValue = playerStats;
            var combat = root.GetComponent<ZeldaDaughter.Combat.CombatController>();
            var healthState = root.GetComponent<ZeldaDaughter.Combat.PlayerHealthState>();
            if (combat != null)      eaSO.FindProperty("_combatController").objectReferenceValue = combat;
            if (healthState != null) eaSO.FindProperty("_healthState").objectReferenceValue = healthState;
            eaSO.FindProperty("_inventory").objectReferenceValue = inventory;
            eaSO.ApplyModifiedPropertiesWithoutUndo();

            var feedback = root.AddComponent<ProgressionFeedback>();
            var fbSO = new SerializedObject(feedback);
            fbSO.FindProperty("_effectConfig").objectReferenceValue = effectConfig;
            fbSO.FindProperty("_animator").objectReferenceValue = animator;
            fbSO.ApplyModifiedPropertiesWithoutUndo();

            // Тег
            root.tag = "Player";

            // Сохраняем prefab
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabOutputPath);
            Object.DestroyImmediate(root);

            if (saved != null)
            {
                Debug.Log($"[PlayerPrefabBuilder] Player prefab сохранён: {PrefabOutputPath} (модель: {modelPath})");
            }
            else
            {
                Debug.LogError($"[PlayerPrefabBuilder] Не удалось сохранить prefab: {PrefabOutputPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>Возвращает путь к FBX (приоритет) или GLB fallback. Null если ни один не существует.</summary>
        private static string ResolveModelPath()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) != null)
            {
                Debug.Log($"[PlayerPrefabBuilder] Используется FBX: {FbxPath}");
                return FbxPath;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(GlbFallbackPath) != null)
            {
                Debug.Log($"[PlayerPrefabBuilder] FBX не найден, используется GLB fallback: {GlbFallbackPath}");
                return GlbFallbackPath;
            }

            return null;
        }

        /// <summary>Создаёт тег "Player" если он отсутствует в проекте.</summary>
        private static void EnsurePlayerTag()
        {
            const string playerTag = "Player";

            // Проверяем через InternalEditorUtility
            foreach (string tag in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (tag == playerTag)
                    return;
            }

            // Тег не найден — добавляем через SerializedObject на TagManager
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            // Ищем пустой слот или добавляем новый
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var element = tagsProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = playerTag;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[PlayerPrefabBuilder] Тег '{playerTag}' добавлен в TagManager.");
                    return;
                }
            }

            tagsProp.arraySize++;
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = playerTag;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[PlayerPrefabBuilder] Тег '{playerTag}' добавлен в TagManager.");
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (!AssetDatabase.IsValidFolder(PrefabOutputDir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Player");
        }
    }
}
