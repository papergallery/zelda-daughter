using UnityEngine;
using UnityEditor;
using ZeldaDaughter.NPC;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Создаёт все ScriptableObject данные NPC города:
    /// NPCScheduleData, TradeInventoryData, LanguageConfig, NPCProfile.
    /// </summary>
    public static class NPCDataBuilder
    {
        private const string ScheduleDir   = "Assets/Data/NPC/Schedules";
        private const string TradeDir      = "Assets/Data/NPC/Trade";
        private const string ProfileDir    = "Assets/Data/NPC/Profiles";
        private const string DialogueDir   = "Assets/Data/NPC/Dialogues";
        private const string LangConfigPath = "Assets/Data/NPC/LanguageConfig.asset";

        [MenuItem("ZeldaDaughter/Data/Build NPC Data")]
        public static void Build()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/NPC");
            EnsureFolder(ScheduleDir);
            EnsureFolder(TradeDir);
            EnsureFolder(ProfileDir);

            BuildSchedules();
            BuildTradeInventories();
            BuildLanguageConfig();
            BuildProfiles();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NPCDataBuilder] Все данные NPC созданы.");
        }

        // ─── Schedules ─────────────────────────────────────────────────────────

        private static void BuildSchedules()
        {
            BuildSchedule("Schedule_Merchant",
                (TimeOfDay.Dawn,  "market_stall",       NPCActivity.Working),
                (TimeOfDay.Day,   "market_stall",       NPCActivity.Working),
                (TimeOfDay.Dusk,  "tavern",             NPCActivity.Idle),
                (TimeOfDay.Night, "home_merchant",      NPCActivity.Sleeping));

            BuildSchedule("Schedule_Blacksmith",
                (TimeOfDay.Dawn,  "forge",              NPCActivity.Working),
                (TimeOfDay.Day,   "forge",              NPCActivity.Working),
                (TimeOfDay.Dusk,  "tavern",             NPCActivity.Idle),
                (TimeOfDay.Night, "home_blacksmith",    NPCActivity.Sleeping));

            BuildSchedule("Schedule_Bartender",
                (TimeOfDay.Dawn,  "tavern_bar",         NPCActivity.Working),
                (TimeOfDay.Day,   "tavern_bar",         NPCActivity.Working),
                (TimeOfDay.Dusk,  "tavern_bar",         NPCActivity.Working),
                (TimeOfDay.Night, "tavern_bar",         NPCActivity.Working));

            BuildSchedule("Schedule_Herbalist",
                (TimeOfDay.Dawn,  "herb_garden",        NPCActivity.Working),
                (TimeOfDay.Day,   "herb_shop",          NPCActivity.Working),
                (TimeOfDay.Dusk,  "tavern",             NPCActivity.Idle),
                (TimeOfDay.Night, "home_herbalist",     NPCActivity.Sleeping));

            BuildSchedule("Schedule_Guard",
                (TimeOfDay.Dawn,  "gate",               NPCActivity.Patrolling),
                (TimeOfDay.Day,   "gate",               NPCActivity.Patrolling),
                (TimeOfDay.Dusk,  "gate",               NPCActivity.Patrolling),
                (TimeOfDay.Night, "guardhouse",         NPCActivity.Sleeping));

            BuildSchedule("Schedule_Villager1",
                (TimeOfDay.Dawn,  "home_v1",            NPCActivity.Idle),
                (TimeOfDay.Day,   "town_square",        NPCActivity.Walking),
                (TimeOfDay.Dusk,  "tavern",             NPCActivity.Idle),
                (TimeOfDay.Night, "home_v1",            NPCActivity.Sleeping));

            BuildSchedule("Schedule_Villager2",
                (TimeOfDay.Dawn,  "field",              NPCActivity.Working),
                (TimeOfDay.Day,   "field",              NPCActivity.Working),
                (TimeOfDay.Dusk,  "home_v2",            NPCActivity.Idle),
                (TimeOfDay.Night, "home_v2",            NPCActivity.Sleeping));
        }

        private static void BuildSchedule(string assetName,
            params (TimeOfDay time, string waypointId, NPCActivity activity)[] entries)
        {
            string path = $"{ScheduleDir}/{assetName}.asset";
            var asset = CreateIfNotExists<NPCScheduleData>(path);
            if (asset == null)
            {
                Debug.Log($"[NPCDataBuilder] Schedule уже существует: {path}");
                return;
            }

            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("_entries");
            entriesProp.arraySize = entries.Length;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("time").enumValueIndex        = (int)entries[i].time;
                entry.FindPropertyRelative("waypointId").stringValue     = entries[i].waypointId;
                entry.FindPropertyRelative("activity").enumValueIndex    = (int)entries[i].activity;
            }

            so.ApplyModifiedProperties();
            Debug.Log($"[NPCDataBuilder] Schedule создан: {path}");
        }

        // ─── Trade Inventories ─────────────────────────────────────────────────

        private static void BuildTradeInventories()
        {
            // Создаём пустые стоки — предметы привяжем отдельно через LinkItems
            CreateEmptyTrade("Trade_Merchant");
            CreateEmptyTrade("Trade_Blacksmith");
            CreateEmptyTrade("Trade_Bartender");
            CreateEmptyTrade("Trade_Herbalist");
        }

        private static void CreateEmptyTrade(string assetName)
        {
            string path = $"{TradeDir}/{assetName}.asset";
            var asset = CreateIfNotExists<TradeInventoryData>(path);
            if (asset == null)
            {
                Debug.Log($"[NPCDataBuilder] Trade уже существует: {path}");
                return;
            }

            // Оставляем пустой stock — заполняется отдельным пайплайном
            var so = new SerializedObject(asset);
            so.FindProperty("_stock").arraySize = 0;
            so.FindProperty("_priceModifiers").arraySize = 0;
            so.ApplyModifiedProperties();
            Debug.Log($"[NPCDataBuilder] TradeInventory создан: {path}");
        }

        // ─── Language Config ───────────────────────────────────────────────────

        private static void BuildLanguageConfig()
        {
            var asset = CreateIfNotExists<LanguageConfig>(LangConfigPath);
            if (asset == null)
            {
                Debug.Log($"[NPCDataBuilder] LanguageConfig уже существует: {LangConfigPath}");
                return;
            }

            // Дефолтные значения совпадают с полями в классе — просто создаём asset
            var so = new SerializedObject(asset);
            so.FindProperty("_scrambleThreshold").floatValue   = 0.3f;
            so.FindProperty("_partialThreshold").floatValue    = 0.7f;
            so.FindProperty("_experiencePerLine").floatValue   = 0.02f;
            so.FindProperty("_iconModeThreshold").floatValue   = 0.2f;
            so.FindProperty("_currencyThreshold").floatValue   = 0.5f;

            // Руническое письмо — дефолтный набор глифов
            var glyphs = so.FindProperty("_glyphs");
            string[] defaultGlyphs =
            {
                "ᚠ", "ᚢ", "ᚦ", "ᚨ", "ᚱ", "ᚲ", "ᚷ", "ᚹ", "ᚺ", "ᚾ",
                "ᛁ", "ᛃ", "ᛈ", "ᛊ", "ᛏ", "ᛒ", "ᛖ", "ᛗ", "ᛚ", "ᛞ"
            };
            glyphs.arraySize = defaultGlyphs.Length;
            for (int i = 0; i < defaultGlyphs.Length; i++)
                glyphs.GetArrayElementAtIndex(i).stringValue = defaultGlyphs[i];

            so.ApplyModifiedProperties();
            Debug.Log($"[NPCDataBuilder] LanguageConfig создан: {LangConfigPath}");
        }

        // ─── Profiles ──────────────────────────────────────────────────────────

        private static void BuildProfiles()
        {
            BuildProfile("NPC_Merchant",   "merchant",   "Торговец",   NPCRole.Merchant,    hasTrade: true);
            BuildProfile("NPC_Blacksmith", "blacksmith", "Кузнец",     NPCRole.Blacksmith,  hasTrade: true);
            BuildProfile("NPC_Bartender",  "bartender",  "Бармен",     NPCRole.Bartender,   hasTrade: true);
            BuildProfile("NPC_Herbalist",  "herbalist",  "Травница",   NPCRole.Herbalist,   hasTrade: true);
            BuildProfile("NPC_Guard",      "guard",      "Стражник",   NPCRole.Guard,       hasTrade: false);
            BuildProfile("NPC_Villager1",  "villager1",  "Житель",     NPCRole.Villager,    hasTrade: false);
            BuildProfile("NPC_Villager2",  "villager2",  "Крестьянин", NPCRole.Villager,    hasTrade: false);
        }

        private static void BuildProfile(
            string assetName,
            string npcId,
            string npcName,
            NPCRole role,
            bool hasTrade)
        {
            string path = $"{ProfileDir}/{assetName}.asset";
            var asset = CreateIfNotExists<NPCProfile>(path);
            if (asset == null)
            {
                Debug.Log($"[NPCDataBuilder] Profile уже существует: {path}");
                return;
            }

            var so = new SerializedObject(asset);
            so.FindProperty("_npcId").stringValue   = npcId;
            so.FindProperty("_npcName").stringValue = npcName;
            so.FindProperty("_role").enumValueIndex = (int)role;

            // Расписание
            string scheduleRole = GetScheduleRole(role, npcId);
            string schedulePath = $"{ScheduleDir}/Schedule_{scheduleRole}.asset";
            var schedule = AssetDatabase.LoadAssetAtPath<NPCScheduleData>(schedulePath);
            so.FindProperty("_schedule").objectReferenceValue = schedule;

            // Дерево диалогов
            string dialoguePath = GetDialoguePath(role, npcId);
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueTree>(dialoguePath);
            so.FindProperty("_dialogueTree").objectReferenceValue = dialogue;

            // Торговый инвентарь
            if (hasTrade)
            {
                string tradeName = GetTradeName(role);
                string tradePath = $"{TradeDir}/{tradeName}.asset";
                var trade = AssetDatabase.LoadAssetAtPath<TradeInventoryData>(tradePath);
                so.FindProperty("_tradeInventory").objectReferenceValue = trade;
            }

            so.FindProperty("_iconDisplayInterval").floatValue = 1.5f;

            so.ApplyModifiedProperties();
            Debug.Log($"[NPCDataBuilder] Profile создан: {path}");
        }

        // ─── Resolvers ─────────────────────────────────────────────────────────

        private static string GetScheduleRole(NPCRole role, string npcId)
        {
            return role switch
            {
                NPCRole.Merchant   => "Merchant",
                NPCRole.Blacksmith => "Blacksmith",
                NPCRole.Bartender  => "Bartender",
                NPCRole.Herbalist  => "Herbalist",
                NPCRole.Guard      => "Guard",
                NPCRole.Villager   => npcId == "villager1" ? "Villager1" : "Villager2",
                _                  => "Villager1"
            };
        }

        private static string GetDialoguePath(NPCRole role, string npcId)
        {
            string roleName = role switch
            {
                NPCRole.Merchant   => "Merchant",
                NPCRole.Blacksmith => "Blacksmith",
                NPCRole.Bartender  => "Bartender",
                NPCRole.Herbalist  => "Herbalist",
                NPCRole.Guard      => "Guard",
                NPCRole.Villager   => npcId == "villager1" ? "Villager1" : "Villager2",
                _                  => "Villager1"
            };
            return $"{DialogueDir}/Dialogue_{roleName}.asset";
        }

        private static string GetTradeName(NPCRole role)
        {
            return role switch
            {
                NPCRole.Merchant   => "Trade_Merchant",
                NPCRole.Blacksmith => "Trade_Blacksmith",
                NPCRole.Bartender  => "Trade_Bartender",
                NPCRole.Herbalist  => "Trade_Herbalist",
                _                  => "Trade_Merchant"
            };
        }

        // ─── Utilities ─────────────────────────────────────────────────────────

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
