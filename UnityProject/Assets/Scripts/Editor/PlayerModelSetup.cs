using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Назначает KayKit модель и PlayerAnimator.controller на Player в DemoScene.
    /// Запуск через batch mode:
    ///   -executeMethod ZeldaDaughter.Editor.PlayerModelSetup.SetupPlayerModel
    ///   -executeMethod ZeldaDaughter.Editor.PlayerModelSetup.FixPlayerAnimator
    ///
    /// Не удаляет существующие компоненты — только добавляет/обновляет Animator и модель.
    /// </summary>
    public static class PlayerModelSetup
    {
        private const string ScenePath              = "Assets/Scenes/DemoScene.unity";
        private const string KayKitFbxPath          = "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";
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
                // Remove old model to replace with new one
                Debug.Log($"[PlayerModelSetup] Удаляю старую модель: {existingModel.name}");
                Object.DestroyImmediate(existingModel.gameObject);
            }

            // Инстанцируем модель как дочерний объект
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(player.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            // Деактивируем артефактные MeshRenderer-объекты (синий блок и подобные),
            // которые не являются SkinnedMeshRenderer и не нужны для анимированного персонажа
            foreach (Transform child in modelInstance.transform)
            {
                var smr = child.GetComponent<SkinnedMeshRenderer>();
                var mr  = child.GetComponent<MeshRenderer>();
                if (mr != null && smr == null)
                {
                    child.gameObject.SetActive(false);
                    Debug.Log($"[PlayerModelSetup] Деактивирован артефактный MeshRenderer: {child.name}");
                }
            }

            EditorUtility.SetDirty(player);
            Debug.Log($"[PlayerModelSetup] KayKit модель добавлена как child 'Model': {KayKitFbxPath}");
        }

        /// <summary>
        /// Исправляет Animator на Player в DemoScene: назначает AnimatorController и Avatar.
        /// Если Avatar не найден в FBX — принудительно reimport как Humanoid.
        /// Назначение через SerializedObject для надёжного сохранения в сцене.
        /// Запуск: -executeMethod ZeldaDaughter.Editor.PlayerModelSetup.FixPlayerAnimator
        /// </summary>
        [MenuItem("ZeldaDaughter/Player/Fix Player Animator")]
        public static void FixPlayerAnimator()
        {
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

            // Загружаем controller
            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (animController == null)
            {
                Debug.LogError($"[PlayerModelSetup] PlayerAnimator.controller не найден: {AnimatorControllerPath}");
                return;
            }

            // Загружаем avatar из KayKit FBX; если нет — reimport как Humanoid
            var sourceAvatar = LoadOrReimportAvatar();
            if (sourceAvatar == null)
            {
                Debug.LogError($"[PlayerModelSetup] Avatar не найден даже после reimport. Проверьте {KayKitFbxPath}.");
                return;
            }

            Debug.Log($"[PlayerModelSetup] Avatar найден: {sourceAvatar.name} (valid={sourceAvatar.isValid}, human={sourceAvatar.isHuman})");

            // Ищем Animator — сначала на root, потом в детях
            var animator = player.GetComponent<Animator>();
            if (animator == null)
                animator = player.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                animator = player.AddComponent<Animator>();
                Debug.Log("[PlayerModelSetup] Animator добавлен на Player root.");
            }

            Debug.Log($"[PlayerModelSetup] Found Animator on: {animator.gameObject.name}, controller={animator.runtimeAnimatorController?.name ?? "NULL"}, avatar={animator.avatar?.name ?? "NULL"}");

            // Назначаем через SerializedObject для надёжного сохранения ссылок в сцене
            var so = new SerializedObject(animator);
            so.Update();

            bool changed = false;

            var controllerProp = so.FindProperty("m_Controller");
            if (controllerProp != null && controllerProp.objectReferenceValue != animController)
            {
                controllerProp.objectReferenceValue = animController;
                changed = true;
                Debug.Log($"[PlayerModelSetup] AnimatorController назначен через SerializedObject: {AnimatorControllerPath}");
            }
            else
            {
                Debug.Log("[PlayerModelSetup] AnimatorController уже назначен.");
            }

            var avatarProp = so.FindProperty("m_Avatar");
            if (avatarProp != null && avatarProp.objectReferenceValue != sourceAvatar)
            {
                avatarProp.objectReferenceValue = sourceAvatar;
                changed = true;
                Debug.Log($"[PlayerModelSetup] Avatar назначен через SerializedObject: {sourceAvatar.name}");
            }
            else
            {
                Debug.Log($"[PlayerModelSetup] Avatar уже назначен: {avatarProp?.objectReferenceValue?.name ?? "null"}");
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(animator);
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PlayerModelSetup] FixPlayerAnimator: Animator обновлён, сцена сохранена.");
            }
            else
            {
                Debug.Log("[PlayerModelSetup] FixPlayerAnimator: всё уже настроено корректно.");
            }
        }

        private static Avatar LoadOrReimportAvatar()
        {
            // Первая попытка — загружаем из уже импортированных sub-assets
            var avatar = FindAvatarInAssets(KayKitFbxPath);
            if (avatar != null)
                return avatar;

            Debug.LogWarning($"[PlayerModelSetup] Avatar не найден в {KayKitFbxPath} — принудительный reimport как Humanoid.");

            var importer = AssetImporter.GetAtPath(KayKitFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[PlayerModelSetup] ModelImporter не получен для {KayKitFbxPath}");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();
            Debug.Log("[PlayerModelSetup] FBX reimport завершён как Humanoid.");

            // Вторая попытка после reimport
            avatar = FindAvatarInAssets(KayKitFbxPath);
            return avatar;
        }

        private static Avatar FindAvatarInAssets(string path)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in allAssets)
            {
                if (asset is Avatar av)
                    return av;
            }
            return null;
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
