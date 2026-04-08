using UnityEngine;
using UnityEditor;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.Editor
{
    public static class DialogueTreeBuilder
    {
        private const string OutputDir = "Assets/Data/NPC/Dialogues";

        [MenuItem("ZeldaDaughter/Data/Build Dialogue Trees")]
        public static void Build()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/NPC");
            EnsureFolder(OutputDir);

            BuildMerchantTree();
            BuildBlacksmithTree();
            BuildBartenderTree();
            BuildHerbalistTree();
            BuildGuardTree();
            BuildVillager1Tree();
            BuildVillager2Tree();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DialogueTreeBuilder] Деревья диалогов созданы.");
        }

        // ─── Merchant ──────────────────────────────────────────────────────────

        private static void BuildMerchantTree()
        {
            string path = $"{OutputDir}/Dialogue_Merchant.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 4;

            // start
            SetNode(nodes, 0, id: "start",
                text: "Добро пожаловать, путник! Взгляни на мои товары.",
                options: new[]
                {
                    MakeOption("Показать товары",       "trade"),
                    MakeOption("Где найти кузнеца?",    "directions"),
                    MakeOption("Уйти",                  "")
                });

            // trade
            SetNode(nodes, 1, id: "trade",
                text: "Что желаешь?",
                startsTrade: true);

            // directions
            SetNode(nodes, 2, id: "directions",
                text: "Кузница за углом, у стены.",
                options: new[] { MakeOption("Спасибо", "") });

            // end (запасной узел без опций — диалог завершится)
            SetNode(nodes, 3, id: "end",
                text: "");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Merchant: {path}");
        }

        // ─── Blacksmith ────────────────────────────────────────────────────────

        private static void BuildBlacksmithTree()
        {
            string path = $"{OutputDir}/Dialogue_Blacksmith.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 3;

            SetNode(nodes, 0, id: "start",
                text: "Руда, металл, оружие. Чего надо?",
                options: new[]
                {
                    MakeOption("Нужно оружие",       "trade"),
                    MakeOption("Как плавить руду?",  "teaching"),
                    MakeOption("Уйти",               "")
                });

            SetNode(nodes, 1, id: "trade",
                text: "Что желаешь купить?",
                startsTrade: true);

            SetNode(nodes, 2, id: "teaching",
                text: "Положи руду в плавильню. Жди. Бери металл.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Blacksmith: {path}");
        }

        // ─── Bartender ─────────────────────────────────────────────────────────

        private static void BuildBartenderTree()
        {
            string path = $"{OutputDir}/Dialogue_Bartender.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 4;

            SetNode(nodes, 0, id: "start",
                text: "Садись, выпей. Что слышно?",
                options: new[]
                {
                    MakeOption("Есть что поесть?",        "trade"),
                    MakeOption("Что нового?",             "rumors"),
                    MakeOption("Где переночевать?",       "rest"),
                    MakeOption("Уйти",                    "")
                });

            SetNode(nodes, 1, id: "trade",
                text: "Конечно. Есть похлёбка и хлеб.",
                startsTrade: true);

            SetNode(nodes, 2, id: "rumors",
                text: "Говорят, в лесу волки обнаглели. Будь осторожен.");

            SetNode(nodes, 3, id: "rest",
                text: "Комната наверху. Ляг и отдохни.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Bartender: {path}");
        }

        // ─── Herbalist ─────────────────────────────────────────────────────────

        private static void BuildHerbalistTree()
        {
            string path = $"{OutputDir}/Dialogue_Herbalist.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 3;

            SetNode(nodes, 0, id: "start",
                text: "Тебе нужны травы или лекарства?",
                options: new[]
                {
                    MakeOption("Покажи товар",        "trade"),
                    MakeOption("Как лечить раны?",    "teaching"),
                    MakeOption("Уйти",                "")
                });

            SetNode(nodes, 1, id: "trade",
                text: "Смотри.",
                startsTrade: true);

            SetNode(nodes, 2, id: "teaching",
                text: "Бинт от порезов, шину для переломов, мазь от ожогов.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Herbalist: {path}");
        }

        // ─── Guard ─────────────────────────────────────────────────────────────

        private static void BuildGuardTree()
        {
            string path = $"{OutputDir}/Dialogue_Guard.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 4;

            SetNode(nodes, 0, id: "start",
                text: "Стой. Что тебе нужно?",
                options: new[]
                {
                    MakeOption("Что за город?",     "info"),
                    MakeOption("Что за стеной?",    "warning"),
                    MakeOption("Уйти",              "")
                });

            SetNode(nodes, 1, id: "info",
                text: "Город людей. Торгуй, отдыхай. Не нарушай порядок.");

            SetNode(nodes, 2, id: "warning",
                text: "Лес полон зверей. Не суйся без оружия.");

            SetNode(nodes, 3, id: "end",
                text: "");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Guard: {path}");
        }

        // ─── Villager1 ─────────────────────────────────────────────────────────

        private static void BuildVillager1Tree()
        {
            string path = $"{OutputDir}/Dialogue_Villager1.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 2;

            SetNode(nodes, 0, id: "start",
                text: "О, чужак! Не часто вижу новые лица.",
                options: new[]
                {
                    MakeOption("Расскажи о городе", "info"),
                    MakeOption("Уйти",              "")
                });

            SetNode(nodes, 1, id: "info",
                text: "Травница знает про лекарства. Кузнец делает оружие. В таверне можно поесть и поспать.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Villager1: {path}");
        }

        // ─── Villager2 ─────────────────────────────────────────────────────────

        private static void BuildVillager2Tree()
        {
            string path = $"{OutputDir}/Dialogue_Villager2.asset";
            var tree = CreateOrLoadTree(path);
            var so = new SerializedObject(tree);

            so.FindProperty("_startNodeId").stringValue = "start";

            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = 2;

            SetNode(nodes, 0, id: "start",
                text: "Погода сегодня хорошая, не находишь?",
                options: new[]
                {
                    MakeOption("Да",            ""),
                    MakeOption("Где тут что?",  "directions")
                });

            SetNode(nodes, 1, id: "directions",
                text: "Торговец на площади. Стражник у ворот.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
            Debug.Log($"[DialogueTreeBuilder] Villager2: {path}");
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static DialogueTree CreateOrLoadTree(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<DialogueTree>(path);
            if (existing != null) return existing;

            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            AssetDatabase.CreateAsset(tree, path);
            return tree;
        }

        /// <summary>
        /// Заполняет элемент массива _nodes через SerializedProperty.
        /// </summary>
        private static void SetNode(
            SerializedProperty nodesArray,
            int index,
            string id,
            string text = "",
            bool startsTrade = false,
            (string text, string nextNodeId)[] options = null)
        {
            var node = nodesArray.GetArrayElementAtIndex(index);
            node.FindPropertyRelative("id").stringValue = id;
            node.FindPropertyRelative("npcText").stringValue = text;
            node.FindPropertyRelative("startsTrade").boolValue = startsTrade;
            node.FindPropertyRelative("conditionKey").stringValue = "";
            node.FindPropertyRelative("effectKey").stringValue = "";

            var npcIcons = node.FindPropertyRelative("npcIcons");
            npcIcons.arraySize = 0;

            var optionsProp = node.FindPropertyRelative("options");
            if (options == null || options.Length == 0)
            {
                optionsProp.arraySize = 0;
                return;
            }

            optionsProp.arraySize = options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var opt = optionsProp.GetArrayElementAtIndex(i);
                opt.FindPropertyRelative("text").stringValue = options[i].text;
                opt.FindPropertyRelative("nextNodeId").stringValue = options[i].nextNodeId;
                opt.FindPropertyRelative("effectKey").stringValue = "";
                opt.FindPropertyRelative("requiredCondition").stringValue = "";
            }
        }

        private static (string text, string nextNodeId) MakeOption(string text, string nextNodeId)
            => (text, nextNodeId);

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
