using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZeldaDaughter.Quest;
using ZeldaDaughter.World;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Tests.EditMode
{
    // ─── Фабрика ──────────────────────────────────────────────────────────────

    internal static class QuestTestFactory
    {
        internal static QuestData CreateQuestData(
            string questId,
            string giverNpcId = "npc",
            string notebookText = "Задание",
            string completionText = "Выполнено",
            QuestConditionType condType = QuestConditionType.BringItem,
            string targetId = "item",
            int count = 1)
        {
            var quest = ScriptableObject.CreateInstance<QuestData>();
            var so = new SerializedObject(quest);
            so.FindProperty("_questId").stringValue         = questId;
            so.FindProperty("_questGiverNpcId").stringValue = giverNpcId;
            so.FindProperty("_notebookText").stringValue    = notebookText;
            so.FindProperty("_completionText").stringValue  = completionText;

            var conditions = so.FindProperty("_conditions");
            conditions.arraySize = 1;
            var cond = conditions.GetArrayElementAtIndex(0);
            cond.FindPropertyRelative("Type").enumValueIndex        = (int)condType;
            cond.FindPropertyRelative("TargetId").stringValue       = targetId;
            cond.FindPropertyRelative("RequiredCount").intValue     = count;

            so.ApplyModifiedPropertiesWithoutUndo();
            return quest;
        }

        internal static QuestDatabase CreateDatabase(params QuestData[] quests)
        {
            var db = ScriptableObject.CreateInstance<QuestDatabase>();
            var so = new SerializedObject(db);
            var arr = so.FindProperty("_quests");
            arr.arraySize = quests.Length;
            for (int i = 0; i < quests.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        /// <summary>
        /// Создаёт QuestManager на временном GameObject.
        /// Не вызывает OnEnable — подписки на SaveManager не нужны в изолированных тестах.
        /// База данных задаётся через SerializedObject.
        /// </summary>
        internal static (QuestManager manager, GameObject go, QuestDatabase db, QuestData quest)
            CreateQuestManager(string questId = "test_quest")
        {
            var questData = CreateQuestData(questId);
            var db = CreateDatabase(questData);

            var go = new GameObject("TestQuestManager");
            var manager = go.AddComponent<QuestManager>();

            var so = new SerializedObject(manager);
            so.FindProperty("_database").objectReferenceValue = db;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (manager, go, db, questData);
        }

        internal static (MapManager manager, GameObject go) CreateMapManager()
        {
            var go = new GameObject("TestMapManager");
            var manager = go.AddComponent<MapManager>();
            return (manager, go);
        }

        internal static (NotebookManager manager, GameObject go, NotebookConfig config) CreateNotebookManager()
        {
            var config = ScriptableObject.CreateInstance<NotebookConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_maxEntries").intValue = 50;
            so.ApplyModifiedPropertiesWithoutUndo();

            var go = new GameObject("TestNotebookManager");
            var manager = go.AddComponent<NotebookManager>();

            var mso = new SerializedObject(manager);
            mso.FindProperty("_config").objectReferenceValue = config;
            mso.ApplyModifiedPropertiesWithoutUndo();

            return (manager, go, config);
        }

        internal static void InvokeOnEnable(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(mb, null);
        }

        internal static void InvokeOnDisable(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "OnDisable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(mb, null);
        }
    }

    // ─── QuestManager Tests ───────────────────────────────────────────────────

    public class QuestManagerTests
    {
        private QuestManager _manager;
        private GameObject _go;
        private QuestDatabase _db;
        private QuestData _questData;

        [SetUp]
        public void SetUp()
        {
            (_manager, _go, _db, _questData) = QuestTestFactory.CreateQuestManager("test_quest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_db);
            Object.DestroyImmediate(_questData);
        }

        [Test]
        public void StartQuest_SetsActive()
        {
            _manager.StartQuest("test_quest");

            Assert.IsTrue(_manager.IsQuestActive("test_quest"),
                "Квест должен стать активным после StartQuest");
        }

        [Test]
        public void StartQuest_AlreadyActive_DoesNotDuplicate()
        {
            _manager.StartQuest("test_quest");
            _manager.StartQuest("test_quest");

            // Квест активен — не дублируется и не переходит в completed
            Assert.IsTrue(_manager.IsQuestActive("test_quest"),
                "Повторный StartQuest не должен менять состояние");
            Assert.IsFalse(_manager.IsQuestComplete("test_quest"),
                "Повторный StartQuest не должен завершать квест");
        }

        [Test]
        public void CompleteQuest_WhenNotActive_DoesNothing()
        {
            // Квест не начат — TryCompleteQuest должен молча игнорировать
            _manager.TryCompleteQuest("test_quest");

            Assert.IsFalse(_manager.IsQuestComplete("test_quest"),
                "Квест не должен завершиться если не был активен");
        }

        [Test]
        public void IsQuestActive_ReturnsCorrectly()
        {
            Assert.IsFalse(_manager.IsQuestActive("test_quest"), "До старта — не активен");

            _manager.StartQuest("test_quest");

            Assert.IsTrue(_manager.IsQuestActive("test_quest"), "После старта — активен");
        }

        [Test]
        public void IsQuestComplete_ReturnsCorrectly()
        {
            Assert.IsFalse(_manager.IsQuestComplete("test_quest"), "До завершения — не завершён");

            _manager.StartQuest("test_quest");

            // TryCompleteQuest не выполняется без предметов в инвентаре — квест остаётся активным
            _manager.TryCompleteQuest("test_quest");
            Assert.IsFalse(_manager.IsQuestComplete("test_quest"),
                "Квест без выполненных условий не должен завершиться");
            Assert.IsTrue(_manager.IsQuestActive("test_quest"),
                "Квест должен оставаться активным при невыполненных условиях");
        }
    }

    // ─── MapManager Tests ─────────────────────────────────────────────────────

    public class MapManagerTests
    {
        private MapManager _manager;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            (_manager, _go) = QuestTestFactory.CreateMapManager();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void RevealMarker_AddsToRevealed()
        {
            _manager.RevealMarker("market");

            Assert.IsTrue(_manager.IsMarkerRevealed("market"),
                "Маркер должен быть в revealed после RevealMarker");
        }

        [Test]
        public void RevealMarker_Duplicate_DoesNotFireEventTwice()
        {
            int eventCount = 0;

            void Handler(string id) => eventCount++;
            MapManager.OnMarkerRevealed += Handler;

            try
            {
                _manager.RevealMarker("market");
                _manager.RevealMarker("market");

                Assert.AreEqual(1, eventCount,
                    "OnMarkerRevealed должен срабатывать только один раз для дублирующегося маркера");
            }
            finally
            {
                MapManager.OnMarkerRevealed -= Handler;
            }
        }

        [Test]
        public void IsMarkerRevealed_ReturnsCorrectly()
        {
            Assert.IsFalse(_manager.IsMarkerRevealed("tavern"), "До открытия — не открыт");

            _manager.RevealMarker("tavern");

            Assert.IsTrue(_manager.IsMarkerRevealed("tavern"), "После открытия — открыт");
        }
    }

    // ─── NotebookManager Tests ────────────────────────────────────────────────

    public class NotebookManagerTests
    {
        private NotebookManager _manager;
        private GameObject _go;
        private NotebookConfig _config;

        [SetUp]
        public void SetUp()
        {
            (_manager, _go, _config) = QuestTestFactory.CreateNotebookManager();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AddEntry_IncreasesCount()
        {
            int before = _manager.Entries.Count;
            _manager.AddEntry(NotebookCategory.Quest, "Тестовая запись");

            Assert.AreEqual(before + 1, _manager.Entries.Count,
                "AddEntry должен увеличить количество записей на 1");
        }

        [Test]
        public void AddEntry_StoresCorrectText()
        {
            _manager.AddEntry(NotebookCategory.Lore, "Важная лорная заметка");
            var last = _manager.Entries[_manager.Entries.Count - 1];

            Assert.AreEqual("Важная лорная заметка", last.Text);
            Assert.AreEqual(NotebookCategory.Lore, last.Category);
        }

        [Test]
        public void MarkAsRead_ResetsNewCount()
        {
            _manager.AddEntry(NotebookCategory.Quest, "Запись 1");
            _manager.AddEntry(NotebookCategory.Quest, "Запись 2");

            Assert.AreEqual(2, _manager.NewEntriesCount, "Должно быть 2 непрочитанных");

            _manager.MarkAsRead();

            Assert.AreEqual(0, _manager.NewEntriesCount,
                "После MarkAsRead счётчик непрочитанных должен обнулиться");
        }

        [Test]
        public void AddEntry_FiresOnEntryAdded()
        {
            NotebookEntryData received = null;

            void Handler(NotebookEntryData e) => received = e;
            NotebookManager.OnEntryAdded += Handler;

            try
            {
                _manager.AddEntry(NotebookCategory.Recipe, "Рецепт А + Б");

                Assert.IsNotNull(received, "OnEntryAdded должен сработать");
                Assert.AreEqual("Рецепт А + Б", received.Text);
                Assert.AreEqual(NotebookCategory.Recipe, received.Category);
            }
            finally
            {
                NotebookManager.OnEntryAdded -= Handler;
            }
        }

        [Test]
        public void AddEntry_ExceedsMaxEntries_TrimsOldest()
        {
            // MaxEntries = 50 (задано в фабрике)
            for (int i = 0; i < 51; i++)
                _manager.AddEntry(NotebookCategory.Lore, $"Запись {i}");

            Assert.LessOrEqual(_manager.Entries.Count, 50,
                "Количество записей не должно превышать MaxEntries");
        }
    }
}
