using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Назначает KayKit модель и PlayerAnimator.controller на Player в DemoScene.
    /// Запуск через batch mode:
    ///   -executeMethod ZeldaDaughter.Editor.PlayerModelSetup.SetupPlayerModel
    ///
    /// Не удаляет существующие компоненты — только добавляет/обновляет Animator и модель.
    /// </summary>
    public static class PlayerModelSetup
    {
        private const string ScenePath            = "Assets/Scenes/DemoScene.unity";
        private const string KayKitFbxPath        = "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";
        private const string AnimatorControllerPath = "Assets/Animations/Controllers/PlayerAnimator.controller";

        [MenuItem("ZeldaDaughter/Player/Setup Player Model")]
        public static void SetupPlayerModel()
        {
            // Открываем DemoScene если не открыта
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name != "DemoScene")
            {
                if (!System.IO.File.Exists(ScenePath))
                {
                    Debug.LogError($"[PlayerModelSetup] DemoScene не найдена: {ScenePath}");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[PlayerModelSetup] DemoScene загружена.");
            }

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[PlayerModelSetup] Объект с тегом 'Player' не найден в сцене.");
                return;
            }

            EnsureAnimatorController(player);
            EnsureModelChild(player);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PlayerModelSetup] Player модель настроена. Сцена сохранена.");
        }

        private static void EnsureAnimatorController(GameObject player)
        {
            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (animController == null)
            {
                Debug.LogWarning($"[PlayerModelSetup] PlayerAnimator.controller не найден: {AnimatorControllerPath}. " +
                                 "Запустите ZeldaDaughter/Animation/Build Player Animator.");
                return;
            }

            // Ищем Animator — сначала на root, потом в детях
            var animator = player.GetComponent<Animator>();
            if (animator == null)
                animator = player.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                animator = player.AddComponent<Animator>();
                Debug.Log("[PlayerModelSetup] Animator добавлен на Player root.");
            }

            if (animator.runtimeAnimatorController != animController)
            {
                animator.runtimeAnimatorController = animController;
                EditorUtility.SetDirty(animator);
                Debug.Log($"[PlayerModelSetup] AnimatorController назначен: {AnimatorControllerPath}");
            }
            else
            {
                Debug.Log("[PlayerModelSetup] AnimatorController уже назначен — пропускаем.");
            }
        }

        private static void EnsureModelChild(GameObject player)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitFbxPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[PlayerModelSetup] KayKit FBX не найден: {KayKitFbxPath}");
                return;
            }

            // Проверяем — уже ли есть child с именем "Model" или SkinnedMeshRenderer
            var existingModel = player.transform.Find("Model");
            if (existingModel == null)
                existingModel = FindChildWithSkinned(player.transform);

            if (existingModel != null)
            {
                Debug.Log($"[PlayerModelSetup] Модель уже присутствует: {existingModel.name} — пропускаем инстанцирование.");
                return;
            }

            // Инстанцируем модель как дочерний объект
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(player.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            EditorUtility.SetDirty(player);
            Debug.Log($"[PlayerModelSetup] KayKit модель добавлена как child 'Model': {KayKitFbxPath}");
        }

        private static Transform FindChildWithSkinned(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.GetComponent<SkinnedMeshRenderer>() != null ||
                    child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    return child;
            }
            return null;
        }
    }
}
