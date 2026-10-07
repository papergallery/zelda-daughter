using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
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
        private const float SpeechBubbleOffsetY = 2.8f;

        private const string ProfileDir = "Assets/Data/NPC/Profiles";

        // ─── MenuItem: старый крестьянин ───────────────────────────────────────

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

        // ─── MenuItem: городские NPC ───────────────────────────────────────────

        [MenuItem("ZeldaDaughter/Prefabs/Build City NPC Prefabs")]
        public static void BuildCityNPCPrefabs()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabOutputDir);

            string modelPath = ResolveModelPath();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);

            BuildCityNPC("Merchant",   modelPath, controller, hasMerchantInventory: true,  merchantId: "merchant");
            BuildCityNPC("Blacksmith", modelPath, controller, hasMerchantInventory: true,  merchantId: "blacksmith");
            BuildCityNPC("Bartender",  modelPath, controller, hasMerchantInventory: true,  merchantId: "bartender");
            BuildCityNPC("Herbalist",  modelPath, controller, hasMerchantInventory: true,  merchantId: "herbalist");
            BuildCityNPC("Guard",      modelPath, controller, hasMerchantInventory: false, merchantId: null);
            BuildCityNPC("Villager1",  modelPath, controller, hasMerchantInventory: false, merchantId: null);
            BuildCityNPC("Villager2",  modelPath, controller, hasMerchantInventory: false, merchantId: null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NPCPrefabBuilder] Городские NPC prefab-ы созданы.");
        }

        // ─── City NPC builder ──────────────────────────────────────────────────

        private static void BuildCityNPC(
            string npcName,
            string modelPath,
            RuntimeAnimatorController controller,
            bool hasMerchantInventory,
            string merchantId)
        {
            string prefabPath = $"{PrefabOutputDir}/{npcName}.prefab";

            var root = new GameObject(npcName);

            // CapsuleCollider
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = CapsuleRadius;
            capsule.height = CapsuleHeight;
            capsule.center = new Vector3(0f, CapsuleCenterY, 0f);

            // NavMeshAgent
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = CapsuleRadius;
            agent.height = CapsuleHeight;
            agent.speed = 2f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.5f;

            // Animator
            var animator = root.AddComponent<Animator>();
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            else
                Debug.LogWarning($"[NPCPrefabBuilder] AnimatorController не найден для {npcName}.");

            // Визуальная модель
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
            }

            // NPCProfile — привяжем если уже создан
            string profilePath = $"{ProfileDir}/NPC_{npcName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<NPCProfile>(profilePath);

            // NPCScheduler
            var scheduler = root.AddComponent<NPCScheduler>();
            var schedulerSo = new SerializedObject(scheduler);
            schedulerSo.FindProperty("_profile").objectReferenceValue = profile;
            schedulerSo.FindProperty("_agent").objectReferenceValue = agent;
            schedulerSo.FindProperty("_animator").objectReferenceValue = animator;
            schedulerSo.ApplyModifiedProperties();

            // NPCInteractable (для взаимодействия через тап)
            var npcInteractable = root.AddComponent<NPCInteractable>();

            // SpeechBubble — дочерний World Space Canvas
            var speechBubbleGO = BuildSpeechBubble(root);
            var speechBubble = speechBubbleGO.GetComponent<NPCSpeechBubble>();

            // IconBubble — дочерний World Space Canvas (legacy иконки)
            var iconBubbleGO = BuildIconBubble(root);
            var iconBubble = iconBubbleGO.GetComponent<IconBubble>();

            // Связываем NPCInteractable
            var interactableSo = new SerializedObject(npcInteractable);

            // NPCInteractable хранит NPCData (legacy) через _data, и NPCProfile через _profile
            // Проверяем наличие поля _profile
            var profileProp = interactableSo.FindProperty("_profile");
            if (profileProp != null)
                profileProp.objectReferenceValue = profile;

            var iconBubbleProp = interactableSo.FindProperty("_iconBubble");
            if (iconBubbleProp != null)
                iconBubbleProp.objectReferenceValue = iconBubble;

            var speechBubbleProp = interactableSo.FindProperty("_speechBubble");
            if (speechBubbleProp != null)
                speechBubbleProp.objectReferenceValue = speechBubble;

            interactableSo.ApplyModifiedProperties();

            // MerchantInventory для торговцев
            if (hasMerchantInventory && merchantId != null)
            {
                var merchantInv = root.AddComponent<MerchantInventory>();
                var merchantSo = new SerializedObject(merchantInv);
                merchantSo.FindProperty("_merchantId").stringValue = merchantId;

                string tradePath = $"Assets/Data/NPC/Trade/Trade_{npcName}.asset";
                var tradeData = AssetDatabase.LoadAssetAtPath<TradeInventoryData>(tradePath);
                merchantSo.FindProperty("_baseData").objectReferenceValue = tradeData;
                merchantSo.ApplyModifiedProperties();
            }

            // Сохраняем prefab
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (saved == null)
                Debug.LogError($"[NPCPrefabBuilder] Не удалось сохранить prefab: {prefabPath}");
            else
                Debug.Log($"[NPCPrefabBuilder] Prefab создан: {prefabPath}");
        }

        // ─── Speech Bubble builder ─────────────────────────────────────────────

        private static GameObject BuildSpeechBubble(GameObject parent)
        {
            var bubbleGO = new GameObject("SpeechBubble");
            bubbleGO.transform.SetParent(parent.transform);
            bubbleGO.transform.localPosition = new Vector3(0f, SpeechBubbleOffsetY, 0f);
            bubbleGO.transform.localRotation = Quaternion.identity;
            bubbleGO.transform.localScale = Vector3.one;

            var canvas = bubbleGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = bubbleGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 80f);
            canvasRect.localScale = Vector3.one * 0.01f;

            var canvasGroup = bubbleGO.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // Фоновый Image
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(bubbleGO.transform);
            bgGO.transform.localPosition = Vector3.zero;
            bgGO.transform.localRotation = Quaternion.identity;
            bgGO.transform.localScale = Vector3.one;
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.7f);

            // TextMeshPro
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(bubbleGO.transform);
            textGO.transform.localPosition = Vector3.zero;
            textGO.transform.localRotation = Quaternion.identity;
            textGO.transform.localScale = Vector3.one;
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.05f);
            textRect.anchorMax = new Vector2(0.95f, 0.95f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            // Иконка
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(bubbleGO.transform);
            iconGO.transform.localPosition = Vector3.zero;
            iconGO.transform.localRotation = Quaternion.identity;
            iconGO.transform.localScale = Vector3.one;
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = iconGO.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconGO.SetActive(false);

            // NPCSpeechBubble MonoBehaviour
            var speechBubble = bubbleGO.AddComponent<NPCSpeechBubble>();
            var speechSo = new SerializedObject(speechBubble);
            speechSo.FindProperty("_textField").objectReferenceValue = tmp;
            speechSo.FindProperty("_iconImage").objectReferenceValue = iconImage;
            speechSo.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            speechSo.ApplyModifiedProperties();

            return bubbleGO;
        }

        // ─── Peasant legacy ────────────────────────────────────────────────────

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
