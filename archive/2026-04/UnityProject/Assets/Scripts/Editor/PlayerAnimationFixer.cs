using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Исправляет настройку анимаций Player в DemoScene:
    /// - Убирает Humanoid Avatar из Animator (несовместим с Generic KayKit clips)
    /// - Заменяет модель на KayKit/Adventurers/Rogue.fbx (Generic rig)
    /// - Переназначает AnimatorController из PlayerAnimator.controller
    ///
    /// Запускать ПОСЛЕ BuildPlayerAnimator.
    /// </summary>
    public static class PlayerAnimationFixer
    {
        private const string DemoScenePath  = "Assets/Scenes/DemoScene.unity";
        private const string KayKitRoguePath = "Assets/Models/KayKit/Adventurers/Rogue.fbx";
        private const string ControllerPath  = "Assets/Animations/Controllers/PlayerAnimator.controller";

        [MenuItem("ZeldaDaughter/Animation/Fix Player Animation in DemoScene")]
        public static void FixDemoScene()
        {
            FixSceneAnimator(DemoScenePath);
        }

        [MenuItem("ZeldaDaughter/Animation/Fix Player Animation in EmuStage1")]
        public static void FixEmuStage1()
        {
            FixSceneAnimator("Assets/Scenes/EmuStage1.unity");
        }

        /// <summary>
        /// Находит в сцене GameObject с тегом Player, исправляет его Animator:
        /// убирает Avatar, назначает правильный Controller.
        /// Заменяет child "Model" на KayKit Rogue если модель — RPGCharacters/Rogue.
        /// </summary>
        public static void FixSceneAnimator(string scenePath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerAnimationFixer] Controller не найден: {ControllerPath}. Запустите Build Player Animator.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;

            // Ищем Player по тегу
            GameObject playerGO = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                playerGO = FindPlayerInHierarchy(root);
                if (playerGO != null) break;
            }

            if (playerGO == null)
            {
                Debug.LogError($"[PlayerAnimationFixer] GameObject с тегом 'Player' не найден в {scenePath}");
                EditorSceneManager.SaveScene(scene);
                return;
            }

            Debug.Log($"[PlayerAnimationFixer] Найден Player: {playerGO.name} в {scenePath}");

            // Исправляем Animator
            if (!playerGO.TryGetComponent<Animator>(out var animator))
            {
                // Нет Animator на root — ищем в children
                animator = playerGO.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    // Добавляем Animator на root
                    animator = playerGO.AddComponent<Animator>();
                    Debug.Log("[PlayerAnimationFixer] Добавлен Animator на Player root.");
                    changed = true;
                }
            }

            // Убираем Avatar (Generic rig не требует Avatar)
            if (animator.avatar != null)
            {
                animator.avatar = null;
                Debug.Log("[PlayerAnimationFixer] Avatar убран из Animator (Generic rig).");
                changed = true;
            }

            // Назначаем Controller
            if (animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                Debug.Log($"[PlayerAnimationFixer] AnimatorController назначен: {ControllerPath}");
                changed = true;
            }

            // Заменяем child-модель на KayKit/Rogue если нужно
            changed |= ReplaceModelWithKayKit(playerGO);

            if (changed)
            {
                EditorUtility.SetDirty(playerGO);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[PlayerAnimationFixer] Сцена сохранена: {scenePath}");
            }
            else
            {
                Debug.Log($"[PlayerAnimationFixer] Изменений не требовалось: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Находит child "Model" или любой SkinnedMeshRenderer child, проверяет что это RPGCharacters/Rogue.
        /// Если да — заменяет на KayKit/Rogue.
        /// </summary>
        private static bool ReplaceModelWithKayKit(GameObject playerGO)
        {
            var kayKitRogue = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitRoguePath);
            if (kayKitRogue == null)
            {
                Debug.LogWarning($"[PlayerAnimationFixer] KayKit Rogue не найден: {KayKitRoguePath}");
                return false;
            }

            // Ищем child с именем "Model" или "Rogue"
            Transform modelChild = playerGO.transform.Find("Model");
            if (modelChild == null)
                modelChild = playerGO.transform.Find("Rogue");

            if (modelChild == null)
            {
                // Ищем любой child с SkinnedMeshRenderer (это скорее всего FBX модель)
                foreach (Transform child in playerGO.transform)
                {
                    if (child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    {
                        modelChild = child;
                        break;
                    }
                }
            }

            if (modelChild != null)
            {
                // Проверяем — это уже KayKit?
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(modelChild.gameObject);
                if (prefabAsset != null)
                {
                    var prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                    if (prefabPath.Contains("KayKit") && prefabPath.Contains("Rogue"))
                    {
                        Debug.Log("[PlayerAnimationFixer] Модель уже KayKit Rogue — пропуск замены.");
                        return false;
                    }

                    Debug.Log($"[PlayerAnimationFixer] Заменяю модель: {prefabPath} → KayKit Rogue");
                }

                // Сохраняем позицию, удаляем старую модель
                var localPos = modelChild.localPosition;
                var localRot = modelChild.localRotation;
                var localScl = modelChild.localScale;
                Object.DestroyImmediate(modelChild.gameObject);

                // Создаём KayKit Rogue
                var newModel = (GameObject)PrefabUtility.InstantiatePrefab(kayKitRogue, playerGO.transform);
                newModel.name = "Model";
                newModel.transform.localPosition = localPos;
                newModel.transform.localRotation = localRot;
                newModel.transform.localScale = localScl;

                Debug.Log("[PlayerAnimationFixer] Модель заменена на KayKit/Rogue.fbx");
                return true;
            }

            // Нет child-модели — добавляем KayKit Rogue как child
            Debug.Log("[PlayerAnimationFixer] Нет child-модели. Добавляю KayKit/Rogue.fbx.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(kayKitRogue, playerGO.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            return true;
        }

        private static GameObject FindPlayerInHierarchy(GameObject root)
        {
            if (root.CompareTag("Player"))
                return root;

            foreach (Transform child in root.transform)
            {
                var found = FindPlayerInHierarchy(child.gameObject);
                if (found != null) return found;
            }

            return null;
        }
    }
}
