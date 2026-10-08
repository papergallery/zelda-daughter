#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Dialogue;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Language;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Movement;
using ZeldaDaughter.Core.Onboarding;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Data
{
    public sealed class DataException : Exception
    {
        public IReadOnlyList<string> Problems { get; }

        public DataException(IReadOnlyList<string> problems) : base("data/: " + string.Join("; ", problems)) { Problems = problems; }
    }

    /// <summary>data/session.json (T-10): how the game session paces saving and bubbles.</summary>
    public sealed class SessionSettings
    {
        public float AutosaveSeconds { get; set; }
        public float RemarkBubbleSeconds { get; set; }
        public float NpcBubbleSeconds { get; set; }
        public float RemarkCheckSeconds { get; set; }
    }

    /// <summary>All of data/*.json, loaded and cross-checked once (C-05, ADR-0008). Unity reads the same files.</summary>
    public sealed class DataSet
    {
        static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public GestureSettings Input { get; private set; } = new GestureSettings();
        public MovementSettings Movement { get; private set; } = new MovementSettings();
        public WorldSettings World { get; private set; } = new WorldSettings();
        public SkillSettings Skills { get; private set; } = new SkillSettings();
        public WoundSettings Wounds { get; private set; } = new WoundSettings();
        public HungerSettings Hunger { get; private set; } = new HungerSettings();
        public InventorySettings Inventory { get; private set; } = new InventorySettings();
        public LanguageSettings Language { get; private set; } = new LanguageSettings();
        public RemarkSettings Remarks { get; private set; } = new RemarkSettings();
        public OnboardingSettings Onboarding { get; private set; } = new OnboardingSettings();
        public DialogueSettings Dialogues { get; private set; } = new DialogueSettings();
        public SessionSettings Session { get; private set; } = new SessionSettings();
        public WeaponSettings Weapons { get; private set; } = new WeaponSettings();
        public EnemySettings Enemies { get; private set; } = new EnemySettings();
        public IReadOnlyDictionary<string, ItemDef> Items { get; private set; } = new Dictionary<string, ItemDef>();
        public IReadOnlyList<FieldRecipe> FieldRecipes { get; private set; } = Array.Empty<FieldRecipe>();
        public IReadOnlyList<StationRecipe> StationRecipes { get; private set; } = Array.Empty<StationRecipe>();
        public IReadOnlyList<WorldRecipe> WorldRecipes { get; private set; } = Array.Empty<WorldRecipe>();

        /// <summary>Loads from a folder; throws <see cref="DataException"/> listing every problem with file and id.</summary>
        public static DataSet Load(string dir) => Load(name => File.ReadAllText(Path.Combine(dir, name)));

        /// <summary>Loads through a reader (Unity: TextAsset / StreamingAssets).</summary>
        public static DataSet Load(Func<string, string> read)
        {
            var problems = new List<string>();
            var d = new DataSet
            {
                Input = Read<GestureSettings>(read, "input.json", problems),
                Movement = Read<MovementSettings>(read, "movement.json", problems),
                World = Read<WorldSettings>(read, "world.json", problems),
                Skills = Read<SkillSettings>(read, "skills.json", problems),
                Wounds = Read<WoundSettings>(read, "wounds.json", problems),
                Hunger = Read<HungerSettings>(read, "hunger.json", problems),
                Inventory = Read<InventorySettings>(read, "inventory.json", problems),
                Language = Read<LanguageSettings>(read, "language.json", problems),
                Remarks = Read<RemarkSettings>(read, "remarks.json", problems),
                Onboarding = Read<OnboardingSettings>(read, "onboarding.json", problems),
                Dialogues = Read<DialogueSettings>(read, "dialogues.json", problems),
                Session = Read<SessionSettings>(read, "session.json", problems),
                Weapons = Read<WeaponSettings>(read, "weapons.json", problems),
                Enemies = Read<EnemySettings>(read, "enemies.json", problems),
            };
            var items = Read<ItemsFile>(read, "items.json", problems).Items;
            var recipes = Read<RecipesFile>(read, "recipes.json", problems);

            var byId = new Dictionary<string, ItemDef>(StringComparer.Ordinal);
            foreach (var it in items)
            {
                if (!IdPattern.IsMatch(it.Id)) problems.Add($"items.json: '{it.Id}' — id только [a-z0-9_] с буквы");
                else if (it.Id.StartsWith("item_", StringComparison.Ordinal)) problems.Add($"items.json: '{it.Id}' — без префикса item_");
                if (byId.ContainsKey(it.Id)) problems.Add($"items.json: '{it.Id}' — повтор id");
                else byId[it.Id] = it;
                if (it.Weight < 0) problems.Add($"items.json: '{it.Id}' — отрицательный вес");
                if (it.Stack < 1) problems.Add($"items.json: '{it.Id}' — stack < 1");
            }

            void Ref(string file, string what, string id)
            {
                if (!byId.ContainsKey(id)) problems.Add($"{file}: {what} '{id}' — нет в items.json");
            }

            foreach (var r in recipes.Field)
            {
                string who = $"{r.A} + {r.B}";
                Ref("recipes.json", who, r.A); Ref("recipes.json", who, r.B); Ref("recipes.json", who, r.Out);
                foreach (var k in r.Keep)
                    if (k != r.A && k != r.B) problems.Add($"recipes.json: {who} — keep '{k}' не ингредиент");
                if (r.Count < 1) problems.Add($"recipes.json: {who} — count < 1");
            }
            foreach (var r in recipes.Station)
            {
                string who = $"{r.Station}: {string.Join(" + ", r.In)}";
                if (r.In.Count == 0) problems.Add($"recipes.json: {who} — пустой вход");
                foreach (var i in r.In) Ref("recipes.json", who, i);
                Ref("recipes.json", who, r.Out);
            }
            foreach (var r in recipes.World)
            {
                Ref("recipes.json", $"{r.Target} ← {r.With}", r.With);
                foreach (var k in r.Keep)
                    if (k != r.With) problems.Add($"recipes.json: {r.Target} ← {r.With} — keep '{k}' не предмет рецепта");
            }
            foreach (var food in d.Hunger.Food.Keys)
            {
                Ref("hunger.json", "food", food);
                if (byId.TryGetValue(food, out var fd) && fd.Kind != "food") problems.Add($"hunger.json: '{food}' — в items.json не food");
            }
            foreach (var kv in d.Wounds.Types)
                if (!string.IsNullOrEmpty(kv.Value.Medicine)) Ref("wounds.json", kv.Key, kv.Value.Medicine);
            foreach (var kv in d.Weapons.Weapons)
            {
                if (kv.Key != WeaponSettings.Fists) Ref("weapons.json", "оружие", kv.Key);
                var w = kv.Value;
                if (w.ParsedClass == null) problems.Add($"weapons.json: '{kv.Key}' — неизвестный класс '{w.Class}'");
                if (w.Damage <= 0 || w.Range <= 0) problems.Add($"weapons.json: '{kv.Key}' — урон и дальность должны быть > 0");
                if (!string.IsNullOrEmpty(w.Wound) && w.ParsedWound == null) problems.Add($"weapons.json: '{kv.Key}' — неизвестная рана '{w.Wound}'");
                if (w.Severity < 0 || w.Severity > 1) problems.Add($"weapons.json: '{kv.Key}' — тяжесть раны вне 0..1");
            }
            foreach (var kv in d.Enemies.Enemies)
            {
                var e = kv.Value;
                if (e.Hp <= 0 || e.Range <= 0) problems.Add($"enemies.json: '{kv.Key}' — HP и радиус удара должны быть > 0");
                if (!string.IsNullOrEmpty(e.Wound) && e.ParsedWound == null) problems.Add($"enemies.json: '{kv.Key}' — неизвестная рана '{e.Wound}'");
                if (e.Windup < d.Enemies.MinWindup) problems.Add($"enemies.json: '{kv.Key}' — замах {e.Windup} с короче минимума {d.Enemies.MinWindup} с");
                if (e.ChaseSpeed >= d.Movement.RunSpeed) problems.Add($"enemies.json: '{kv.Key}' — погоня {e.ChaseSpeed} м/с не медленнее бега героя {d.Movement.RunSpeed} м/с");
                if (e.AggroRange <= 0) problems.Add($"enemies.json: '{kv.Key}' — радиус агро должен быть > 0");
            }
            foreach (var npc in d.Dialogues.Npcs)
            {
                if (!npc.Value.Nodes.ContainsKey("start")) problems.Add($"dialogues.json: '{npc.Key}' — нет узла start");
                foreach (var node in npc.Value.Nodes)
                {
                    foreach (var r in node.Value.Replies)
                        if (!npc.Value.Nodes.ContainsKey(r.To)) problems.Add($"dialogues.json: {npc.Key}.{node.Key} → '{r.To}' — нет узла");
                    foreach (var icon in node.Value.Icons)
                        if (!d.Dialogues.Icons.Contains(icon)) problems.Add($"dialogues.json: {npc.Key}.{node.Key} — иконка '{icon}' не в списке");
                }
            }
            var pairs = recipes.Field.GroupBy(r => string.CompareOrdinal(r.A, r.B) <= 0 ? r.A + "|" + r.B : r.B + "|" + r.A).Where(g => g.Count() > 1);
            foreach (var g in pairs) problems.Add($"recipes.json: пара {g.Key.Replace("|", " + ")} — два рецепта");

            if (problems.Count > 0) throw new DataException(problems);
            d.Items = byId;
            d.FieldRecipes = recipes.Field;
            d.StationRecipes = recipes.Station;
            d.WorldRecipes = recipes.World;
            return d;
        }

        static T Read<T>(Func<string, string> read, string file, List<string> problems) where T : new()
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(read(file), Json) ?? new T();
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                problems.Add($"{file}: {e.Message}");
                return new T();
            }
        }
    }
}
