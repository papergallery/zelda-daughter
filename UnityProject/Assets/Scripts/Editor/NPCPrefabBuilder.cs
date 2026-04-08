using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.Editor
{
    public static class NPCPrefabBuilder
    {
        // Barbarian — наиболее подходящий для крестьянина по телосложению
        private const string ModelPrimaryPath =
            "Assets/Models/KayKit/Adventurers/KayKit-Character-Pack-Adventures-1.0-main/addons/kaykit_character_pack_adventures/Characters/gltf/Barbarian.glb";
        private const string ModelFallbackPath =
            "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";

        private const string AnimatorControllerPath = "Assets/Animations/Controllers/PlayerAnimator.controller";
        private const string NpcDataOutputDir = "Assets/Content/NPCs";
        private const string PrefabOutputDir = "Assets/Prefabs/NPCs";
        private const string PrefabOutputPath = "Assets/Prefabs/NPCs/Peasant.prefab";
        private const string NpcDataPath = "Assets/Content/NPCs/NPC_Peasant.asset";

        private const float CapsuleRadius = 0.3f;
        private const float CapsuleHeight = 1.8f;
        private const float CapsuleCenterY = 0.9f;
        private const float IconBubbleWorldSize = 1f;

        [MenuItem("ZeldaDaughter/Prefabs/Build NPC Prefabs")]
        public static void BuildNPCPrefabs()
        {
            EnsureFolder("Assets/Content");
            EnsureFolder(NpcDataOutputDir);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabOutputDir);

            var npcData = CreateOrLoadNPCData();
            var modelPath = ResolveModelPath();

            if (modelPath == null)
            {
                Debug.LogError("[NPCPrefabBuilder] Модель NPC не найдена. Создаётся prefab без модели.");
            }

            BuildPeasantPrefab(npcData, modelPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NPCPrefabBuilder] Prefab крестьянина создан: {PrefabOutputPath}");
        }

        private static NPCData CreateOrLoadNPCData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NPCData>(NpcDataPath);
            if (existing != null)
            {
                Debug.Log($"[NPCPrefabBuilder] NPCData уже существует: {NpcDataPath}");
                return existing;
            }

            var data = ScriptableObject.CreateInstance<NPCData>();
            var so = new SerializedObject(data);
            so.FindProperty("_npcName").stringValue = "Крестьянин";
            so.FindProperty("_iconDisplayInterval").floatValue = 1.5f;
            // _iconSequence оставляем пустым — иконки назначаются вручную или через контент-пайплайн
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(data, NpcDataPath);
            Debug.Log($"[NPCPrefabBuilder] NPCData создан: {NpcDataPath}");
            return data;
        }

        private static void BuildPeasantPrefab(NPCData npcData, string modelPath)
        {
            var root = new GameObject("Peasant");

            // Capsule Collider
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = CapsuleRadius;
            capsule.height = CapsuleHeight;
            capsule.center = new Vector3(0f, CapsuleCenterY, 0f);

            // Animator
            var animator = root.AddComponent<Animator>();
            var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (animatorController != null)
                animator.runtimeAnimatorController = animatorController;
            else
                Debug.LogWarning("[NPCPrefabBuilder] AnimatorController не найден. Запустите ZeldaDaughter/Animation/Build Player Animator.");

            // Визуальная модель — дочерний объект
            if (modelPath != null)
            {
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (modelAsset != null)
                {
                    var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                    modelInstance.name = "Model";
                    modelInstance.transform.SetParent(root.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                }
                else
                {
                    Debug.LogWarning($"[NPCPrefabBuilder] Модель не загружена: {modelPath}");
                }
            }

            // NPCInteractable
            var npcInteractable = root.AddComponent<NPCInteractable>();
            var npcSo = new SerializedObject(npcInteractable);
            npcSo.FindProperty("_data").objectReferenceValue = npcData;
            npcSo.ApplyModifiedProperties();

            // IconBubble — дочерний World Space Canvas
            var iconBubbleGO = BuildIconBubble(root);

            // Связываем NPCInteractable с IconBubble
            var iconBubbleComponent = iconBubbleGO.GetComponent<IconBubble>();
            npcSo.FindProperty("_iconBubble").objectReferenceValue = iconBubbleComponent;
            npcSo.ApplyModifiedProperties();

            // Сохраняем prefab
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabOutputPath);
            Object.DestroyImmediate(root);

            if (saved == null)
                Debug.LogError($"[NPCPrefabBuilder] Не удалось сохранить prefab: {PrefabOutputPath}");
        }

        private static GameObject BuildIconBubble(GameObject parent)
        {
            var bubbleGO = new GameObject("IconBubble");
            bubbleGO.transform.SetParent(parent.transform);
            bubbleGO.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            bubbleGO.transform.localRotation = Quaternion.identity;
            bubbleGO.transform.localScale = Vector3.one;

            // Canvas (World Space)
            var canvas = bubbleGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = bubbleGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(IconBubbleWorldSize, IconBubbleWorldSize);
            canvasRect.localScale = Vector3.one * 0.01f; // 1 unit = 100px

            // CanvasGroup для fade-in/out
            var canvasGroup = bubbleGO.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // Image (иконка)
            var imageGO = new GameObject("Icon");
            imageGO.transform.SetParent(bubbleGO.transform);
            imageGO.transform.localPosition = Vector3.zero;
            imageGO.transform.localRotation = Quaternion.identity;
            imageGO.transform.localScale = Vector3.one;

            var imageRect = imageGO.AddComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            var image = imageGO.AddComponent<Image>();
            image.preserveAspect = true;

            // IconBubble MonoBehaviour — ссылки через SerializedObject
            var iconBubble = bubbleGO.AddComponent<IconBubble>();
            var iconSo = new SerializedObject(iconBubble);
            iconSo.FindProperty("_iconImage").objectReferenceValue = image;
            iconSo.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            iconSo.ApplyModifiedProperties();

            return bubbleGO;
        }

        private static string ResolveModelPath()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrimaryPath) != null)
            {
                Debug.Log($"[NPCPrefabBuilder] Модель: {ModelPrimaryPath}");
                return ModelPrimaryPath;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelFallbackPath) != null)
            {
                Debug.Log($"[NPCPrefabBuilder] Fallback модель: {ModelFallbackPath}");
                return ModelFallbackPath;
            }

            return null;
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
