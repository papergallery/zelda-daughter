using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Quest;

namespace ZeldaDaughter.Editor
{
    public static class QuestDatabaseBuilder
    {
        private const string QuestDir = "Assets/Data/Quests";

        [MenuItem("ZeldaDaughter/Data/Build Quest Data")]
        public static void Build()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(QuestDir);

            var quests = new QuestData[]
            {
                BuildMerchantQuest(),
                BuildHerbalistQuest(),
                BuildGuardQuest(),
                BuildBlacksmithQuest(),
                BuildBartenderQuest()
            };

            BuildQuestDatabase(quests);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QuestDatabaseBuilder] Quest data built.");
        }

        // ─── Квесты ────────────────────────────────────────────────────────────

        private static QuestData BuildMerchantQuest()
        {
            string path = $"{QuestDir}/Quest_MerchantWolfHides.asset";
            var quest = CreateIfNotExists<QuestData>(path);
            if (quest == null)
            {
                Debug.Log($"[QuestDatabaseBuilder] Квест уже существует: {path}");
                return AssetDatabase.LoadAssetAtPath<QuestData>(path);
            }

            var so = new SerializedObject(quest);
            so.FindProperty("_questId").stringValue = "merchant_wolf_hides";
            so.FindProperty("_questGiverNpcId").stringValue = "merchant";
            so.FindProperty("_notebookText").stringValue =
                "Торговец просил принести шкуры волков. Говорит, на них есть спрос.";
            so.FindProperty("_completionText").stringValue =
                "Отнёс шкуры торговцу. Дал взамен полезные вещи.";

            SetCondition(so, QuestConditionType.BringItem, "wolf_hide", 3);
            so.ApplyModifiedProperties();

            Debug.Log("[QuestDatabaseBuilder] Квест создан: merchant_wolf_hides");
            return quest;
        }

        private static QuestData BuildHerbalistQuest()
        {
            string path = $"{QuestDir}/Quest_HerbalistRareHerb.asset";
            var quest = CreateIfNotExists<QuestData>(path);
            if (quest == null)
            {
                Debug.Log($"[QuestDatabaseBuilder] Квест уже существует: {path}");
                return AssetDatabase.LoadAssetAtPath<QuestData>(path);
            }

            var so = new SerializedObject(quest);
            so.FindProperty("_questId").stringValue = "herbalist_rare_herb";
            so.FindProperty("_questGiverNpcId").stringValue = "herbalist";
            so.FindProperty("_notebookText").stringValue =
                "Травница просила найти лунную траву у реки. Растёт только ночью.";
            so.FindProperty("_completionText").stringValue =
                "Нашёл лунную траву. Травница научила делать мазь от ожогов.";

            SetCondition(so, QuestConditionType.BringItem, "moon_herb", 1);
            so.ApplyModifiedProperties();

            Debug.Log("[QuestDatabaseBuilder] Квест создан: herbalist_rare_herb");
            return quest;
        }

        private static QuestData BuildGuardQuest()
        {
            string path = $"{QuestDir}/Quest_GuardBoarHunt.asset";
            var quest = CreateIfNotExists<QuestData>(path);
            if (quest == null)
            {
                Debug.Log($"[QuestDatabaseBuilder] Квест уже существует: {path}");
                return AssetDatabase.LoadAssetAtPath<QuestData>(path);
            }

            var so = new SerializedObject(quest);
            so.FindProperty("_questId").stringValue = "guard_boar_hunt";
            so.FindProperty("_questGiverNpcId").stringValue = "guard";
            so.FindProperty("_notebookText").stringValue =
                "Стражник жалуется на кабанов у южной дороги. Просит разобраться.";
            so.FindProperty("_completionText").stringValue =
                "Кабаны у дороги больше не проблема.";

            SetCondition(so, QuestConditionType.KillEnemy, "boar", 3);
            so.ApplyModifiedProperties();

            Debug.Log("[QuestDatabaseBuilder] Квест создан: guard_boar_hunt");
            return quest;
        }

        private static QuestData BuildBlacksmithQuest()
        {
            string path = $"{QuestDir}/Quest_BlacksmithOre.asset";
            var quest = CreateIfNotExists<QuestData>(path);
            if (quest == null)
            {
                Debug.Log($"[QuestDatabaseBuilder] Квест уже существует: {path}");
                return AssetDatabase.LoadAssetAtPath<QuestData>(path);
            }

            var so = new SerializedObject(quest);
            so.FindProperty("_questId").stringValue = "blacksmith_ore";
            so.FindProperty("_questGiverNpcId").stringValue = "blacksmith";
            so.FindProperty("_notebookText").stringValue =
                "Кузнецу нужна руда. Говорит, в лесу у скал есть залежи.";
            so.FindProperty("_completionText").stringValue =
                "Принёс руду кузнецу. Теперь он готов ковать мне оружие.";

            SetCondition(so, QuestConditionType.BringItem, "iron_ore", 5);
            so.ApplyModifiedProperties();

            Debug.Log("[QuestDatabaseBuilder] Квест создан: blacksmith_ore");
            return quest;
        }

        private static QuestData BuildBartenderQuest()
        {
            string path = $"{QuestDir}/Quest_BartenderRumors.asset";
            var quest = CreateIfNotExists<QuestData>(path);
            if (quest == null)
            {
                Debug.Log($"[QuestDatabaseBuilder] Квест уже существует: {path}");
                return AssetDatabase.LoadAssetAtPath<QuestData>(path);
            }

            var so = new SerializedObject(quest);
            so.FindProperty("_questId").stringValue = "bartender_rumors";
            so.FindProperty("_questGiverNpcId").stringValue = "bartender";
            so.FindProperty("_notebookText").stringValue =
                "Бармен слышал, что в лесу видели что-то странное. Просит разведать.";
            so.FindProperty("_completionText").stringValue =
                "Рассказал бармену что видел в лесу. Угостил элем.";

            SetCondition(so, QuestConditionType.VisitLocation, "forest_clearing", 1);
            so.ApplyModifiedProperties();

            Debug.Log("[QuestDatabaseBuilder] Квест создан: bartender_rumors");
            return quest;
        }

        // ─── QuestDatabase ─────────────────────────────────────────────────────

        private static void BuildQuestDatabase(QuestData[] quests)
        {
            string path = $"{QuestDir}/QuestDatabase.asset";

            // Для базы всегда обновляем список квестов
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<QuestDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }

            var so = new SerializedObject(db);
            var questsProp = so.FindProperty("_quests");
            questsProp.arraySize = quests.Length;
            for (int i = 0; i < quests.Length; i++)
                questsProp.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            Debug.Log($"[QuestDatabaseBuilder] QuestDatabase обновлён: {quests.Length} квестов.");
        }

        // ─── Утилиты ───────────────────────────────────────────────────────────

        /// <summary>
        /// Устанавливает ровно одно условие в _conditions[0].
        /// </summary>
        private static void SetCondition(SerializedObject so, QuestConditionType type, string targetId, int count)
        {
            var conditionsProp = so.FindProperty("_conditions");
            conditionsProp.arraySize = 1;
            var cond = conditionsProp.GetArrayElementAtIndex(0);
            cond.FindPropertyRelative("Type").enumValueIndex = (int)type;
            cond.FindPropertyRelative("TargetId").stringValue = targetId;
            cond.FindPropertyRelative("RequiredCount").intValue = count;
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
