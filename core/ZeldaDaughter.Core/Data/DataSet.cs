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
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Language;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Movement;
using ZeldaDaughter.Core.Npcs;
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
        /// <summary>Metres within which a tappable thing (item, NPC) shows the «tappable_nearby» hint.</summary>
        public float TappableHintRadius { get; set; }
        /// <summary>Daylight (0..1) below which the hero counts it as night for her remarks.</summary>
        public float NightDaylightBelow { get; set; }
        /// <summary>A predator closer than this (m) makes the hero afraid aloud (D-23).</summary>
        public float PredatorFearMeters { get; set; }
        /// <summary>How long the description cloud of an item hangs after a long press in the bag (D-23).</summary>
        public float ItemInfoSeconds { get; set; }

        public bool IsNear(float distance) => distance < TappableHintRadius;
        public bool IsNight(float daylight) => daylight < NightDaylightBelow;
    }

    /// <summary>All of data/*.json, loaded and cross-checked once (C-05, ADR-0008). Unity reads the same files.</summary>
    public sealed class DataSet
    {
        static readonly string[] ItemKinds = { "material", "tool", "weapon", "food", "medicine", "currency", "quest" };
        public const string CoinItem = "coin";
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
        public NpcSettings Npcs { get; private set; } = new NpcSettings();
        public TraderSettings Traders { get; private set; } = new TraderSettings();
        public MapSettings Map { get; private set; } = new MapSettings();
        public CampSettings Camp { get; private set; } = new CampSettings();
        public NightSettings Night { get; private set; } = new NightSettings();
        public ElementsSettings Elements { get; private set; } = new ElementsSettings();
        public NotebookSettings Notebook { get; private set; } = new NotebookSettings();
        public QuestSettings Quests { get; private set; } = new QuestSettings();
        public IReadOnlyDictionary<string, ItemDef> Items { get; private set; } = new Dictionary<string, ItemDef>();
        public IReadOnlyList<FieldRecipe> FieldRecipes { get; private set; } = Array.Empty<FieldRecipe>();
        public IReadOnlyList<StationRecipe> StationRecipes { get; private set; } = Array.Empty<StationRecipe>();
        public IReadOnlyList<WorldRecipe> WorldRecipes { get; private set; } = Array.Empty<WorldRecipe>();
        public IReadOnlyCollection<string> WorldObjects { get; private set; } = Array.Empty<string>();

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
                Npcs = Read<NpcSettings>(read, "npcs.json", problems),
                Traders = Read<TraderSettings>(read, "traders.json", problems),
                Map = Read<MapSettings>(read, "map.json", problems),
                Camp = Read<CampSettings>(read, "camp.json", problems),
                Night = Read<NightSettings>(read, "night.json", problems),
                Elements = Read<ElementsSettings>(read, "elements.json", problems),
                Notebook = Read<NotebookSettings>(read, "notebook.json", problems),
                Quests = Read<QuestSettings>(read, "quests.json", problems),
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
                if (it.Value < 0) problems.Add($"items.json: '{it.Id}' — отрицательная ценность");
                if (Array.IndexOf(ItemKinds, it.Kind) < 0) problems.Add($"items.json: '{it.Id}' — неизвестный вид '{it.Kind}' ({string.Join(" | ", ItemKinds)})");
                if (string.IsNullOrWhiteSpace(it.Description)) problems.Add($"items.json: '{it.Id}' — нет description (долгий тап по предмету, D-23)");
                else if (it.Description.Any(char.IsDigit)) problems.Add($"items.json: '{it.Id}' — в description цифры (вес и ценность скрыты от игрока)");
                else if (it.Description.Length > 160) problems.Add($"items.json: '{it.Id}' — description длиннее 160 знаков (облачко)");
                if (it.Kind == "weapon" && !d.Weapons.Weapons.ContainsKey(it.Id)) problems.Add($"items.json: '{it.Id}' — оружие без записи в weapons.json");
            }
            if (!d.Weapons.Weapons.ContainsKey(WeaponSettings.Fists)) problems.Add($"weapons.json: нет '{WeaponSettings.Fists}' — герой без оружия бьёт кулаками");
            var worldObjects = new HashSet<string>(recipes.WorldObjects, StringComparer.Ordinal);

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
                if (!worldObjects.Contains(r.Target)) problems.Add($"recipes.json: {r.Target} ← {r.With} — цель '{r.Target}' не объявлена в worldObjects");
                if (!byId.ContainsKey(r.Result) && !worldObjects.Contains(r.Result)) problems.Add($"recipes.json: {r.Target} ← {r.With} — результат '{r.Result}' ни предмет, ни объявленный мировой объект (worldObjects)");
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
            if (d.Session.PredatorFearMeters <= 0 || d.Session.ItemInfoSeconds <= 0) problems.Add("session.json: predatorFearMeters и itemInfoSeconds должны быть > 0");
            if (d.Session.TappableHintRadius <= 0) problems.Add("session.json: tappableHintRadius должен быть > 0");
            if (d.Session.NightDaylightBelow <= 0 || d.Session.NightDaylightBelow > 1) problems.Add("session.json: nightDaylightBelow должен быть в (0; 1]");
            foreach (var kv in d.Enemies.Enemies)
            {
                var e = kv.Value;
                if (e.Hp <= 0 || e.Range <= 0) problems.Add($"enemies.json: '{kv.Key}' — HP и радиус удара должны быть > 0");
                if (!string.IsNullOrEmpty(e.Wound) && e.ParsedWound == null) problems.Add($"enemies.json: '{kv.Key}' — неизвестная рана '{e.Wound}'");
                if (e.Windup < d.Enemies.MinWindup) problems.Add($"enemies.json: '{kv.Key}' — замах {e.Windup} с короче минимума {d.Enemies.MinWindup} с");
                if (e.ChaseSpeed >= d.Movement.RunSpeed) problems.Add($"enemies.json: '{kv.Key}' — погоня {e.ChaseSpeed} м/с не медленнее бега героя {d.Movement.RunSpeed} м/с");
                if (e.AggroRange <= 0) problems.Add($"enemies.json: '{kv.Key}' — радиус агро должен быть > 0");
            }
            if (!byId.TryGetValue(d.Enemies.ButcherTool, out var bt) || bt.Kind != "tool") problems.Add($"enemies.json: butcherTool '{d.Enemies.ButcherTool}' — нужен предмет вида tool из items.json");
            foreach (var kv in d.Enemies.Enemies)
            {
                foreach (var set in new[] { ("minimal", kv.Value.Loot.Minimal), ("full", kv.Value.Loot.Full) })
                    foreach (var it in set.Item2)
                    {
                        if (!byId.ContainsKey(it.Key)) problems.Add($"enemies.json: '{kv.Key}' loot.{set.Item1} '{it.Key}' — нет в items.json");
                        if (it.Value < 1) problems.Add($"enemies.json: '{kv.Key}' loot.{set.Item1} '{it.Key}' — количество < 1");
                    }
                if (kv.Value.Loot.Minimal.Count == 0 || kv.Value.Loot.Full.Count == 0) problems.Add($"enemies.json: '{kv.Key}' — пустой loot (minimal и full обязательны)");
                foreach (var it in kv.Value.Loot.Minimal)
                    if (!kv.Value.Loot.Full.TryGetValue(it.Key, out var f) || f < it.Value) problems.Add($"enemies.json: '{kv.Key}' loot — full не содержит minimal '{it.Key}'");
            }
            foreach (var h in d.Onboarding.Hints)
            {
                if (Array.IndexOf(Hints.KnownConditions, h.ShowWhen) < 0) problems.Add($"onboarding.json: '{h.Id}' — showWhen '{h.ShowWhen}' игра не сообщает ({string.Join(" | ", Hints.KnownConditions)})");
                if (Array.IndexOf(Hints.KnownActions, h.DoneBy) < 0) problems.Add($"onboarding.json: '{h.Id}' — doneBy '{h.DoneBy}' игра не сообщает ({string.Join(" | ", Hints.KnownActions)})");
            }
            foreach (var kv in d.Enemies.WoundEffects)
                if (!Enum.GetNames(typeof(WoundType)).Any(n => string.Equals(n, kv.Key, StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"enemies.json: woundEffects '{kv.Key}' — не имя раны (cut | fracture | burn | poison)");
            foreach (var npc in d.Dialogues.Npcs)
            {
                if (!npc.Value.Nodes.ContainsKey("start")) problems.Add($"dialogues.json: '{npc.Key}' — нет узла start");
                foreach (var node in npc.Value.Nodes)
                {
                    foreach (var r in node.Value.Replies)
                    {
                        if (!npc.Value.Nodes.ContainsKey(r.To)) problems.Add($"dialogues.json: {npc.Key}.{node.Key} → '{r.To}' — нет узла");
                        if (!d.Dialogues.Icons.Contains(r.Icon)) problems.Add($"dialogues.json: {npc.Key}.{node.Key} — иконка ответа '{r.Icon}' не в списке");
                    }
                    foreach (var icon in node.Value.Icons)
                        if (!d.Dialogues.Icons.Contains(icon)) problems.Add($"dialogues.json: {npc.Key}.{node.Key} — иконка '{icon}' не в списке");
                    foreach (var e in node.Value.Effects)
                    {
                        string at = $"dialogues.json: {npc.Key}.{node.Key} — эффект {e.Type} '{e.Id}'";
                        switch (e.Type)
                        {
                            case "mark": if (!d.Map.Marks.ContainsKey(e.Id)) problems.Add(at + " — нет в map.json"); break;
                            case "note": if (!d.Notebook.Entries.ContainsKey(e.Id)) problems.Add(at + " — нет в notebook.json"); break;
                            case "offer": if (!d.Quests.Quests.ContainsKey(e.Id)) problems.Add(at + " — нет в quests.json"); break;
                            case "teach_coins": break;
                            default: problems.Add(at + $" — неизвестный тип ({string.Join(" | ", DialogueEffect.Types)})"); break;
                        }
                    }
                }
            }
            if (d.Npcs.WalkSpeed <= 0 || d.Npcs.WalkSpeed > d.Movement.WalkSpeed) problems.Add($"npcs.json: walkSpeed {d.Npcs.WalkSpeed} — должен быть в (0; {d.Movement.WalkSpeed}] (не быстрее героини)");
            if (d.Elements.Grass.CellSpacing <= 0 || d.Elements.Grass.CellSpacing > d.Elements.Grass.NeighborDistance) problems.Add($"elements.json: grass.cellSpacing {d.Elements.Grass.CellSpacing} — должен быть в (0; neighborDistance {d.Elements.Grass.NeighborDistance}], иначе поле травы не связано");
            {
                var tr = d.Traders.TypicalRound;
                foreach (var q in tr.Quests) if (!d.Quests.Quests.ContainsKey(q)) problems.Add($"traders.json: typicalRound.quests '{q}' — нет в quests.json");
                foreach (var kv in tr.Carcasses) if (!d.Enemies.Enemies.ContainsKey(kv.Key) || kv.Value < 1) problems.Add($"traders.json: typicalRound.carcasses '{kv.Key}' — нет в enemies.json или число < 1");
                foreach (var kv in tr.Gathered) if (!byId.ContainsKey(kv.Key) || kv.Value < 1) problems.Add($"traders.json: typicalRound.gathered '{kv.Key}' — нет в items.json или число < 1");
                if (!byId.ContainsKey(tr.Goal)) problems.Add($"traders.json: typicalRound.goal '{tr.Goal}' — нет в items.json");
                else if (!d.Traders.Traders.TryGetValue(tr.Seller, out var seller) || !seller.Stock.Any(x => x.Item == tr.Goal)) problems.Add($"traders.json: typicalRound.seller '{tr.Seller}' не продаёт '{tr.Goal}'");
            }
            foreach (var kv in d.Quests.Quests)
            {
                if (string.IsNullOrEmpty(kv.Value.Thanks)) problems.Add($"quests.json: '{kv.Key}' — нет thanks (узел благодарности получателя)");
                else if (!d.Dialogues.Npcs.TryGetValue(kv.Value.Receiver, out var rd) || !rd.Nodes.TryGetValue(kv.Value.Thanks, out var tn)) problems.Add($"quests.json: '{kv.Key}' — thanks '{kv.Value.Thanks}' нет в диалоге '{kv.Value.Receiver}'");
                else if (!tn.End) problems.Add($"quests.json: '{kv.Key}' — узел благодарности '{kv.Value.Thanks}' должен заканчивать разговор (end: true)");
            }
            foreach (var kv in d.Npcs.Npcs)
            {
                string who = $"npcs.json: '{kv.Key}'";
                if (!d.Dialogues.Npcs.ContainsKey(kv.Key)) problems.Add($"{who} — нет в dialogues.json");
                var sched = kv.Value.Schedule;
                if (sched.Count == 0) problems.Add($"{who} — пустое расписание");
                bool sleeps = false;
                for (int i = 0; i < sched.Count; i++)
                {
                    var e = sched[i];
                    if (e.Hour < 0 || e.Hour >= 24) problems.Add($"{who} — час {e.Hour} вне [0; 24)");
                    if (i > 0 && e.Hour <= sched[i - 1].Hour) problems.Add($"{who} — часы расписания должны расти ({sched[i - 1].Hour} → {e.Hour})");
                    if (!IdPattern.IsMatch(e.Anchor)) problems.Add($"{who} — якорь '{e.Anchor}' (id объекта сцены: [a-z0-9_] с буквы)");
                    var act = e.ParsedActivity;
                    if (act == null) problems.Add($"{who} — занятие '{e.Activity}' (work | trade | tavern | sleep | stroll)");
                    else if (act == NpcActivity.Sleep) sleeps = true;
                    else if (act == NpcActivity.Trade && !kv.Value.Shop) problems.Add($"{who} — торгует, а shop: false");
                }
                if (sched.Count > 0 && !sleeps) problems.Add($"{who} — в расписании нет сна (§2: ночью спят)");
            }
            if (!byId.TryGetValue(CoinItem, out var coin) || coin.Kind != "currency" || coin.Value != 1) problems.Add($"items.json: нет '{CoinItem}' с kind currency и value 1 (монета — единица ценности)");
            if (d.Traders.BuyShare <= 0 || d.Traders.BuyShare > 1) problems.Add("traders.json: buyShare должен быть в (0; 1]");
            if (d.Traders.SellMarkup < 1) problems.Add("traders.json: sellMarkup должен быть ≥ 1");
            foreach (var kv in d.Npcs.Npcs)
                if (kv.Value.Shop != d.Traders.Traders.ContainsKey(kv.Key)) problems.Add($"traders.json: '{kv.Key}' — shop: {kv.Value.Shop.ToString().ToLowerInvariant()} в npcs.json, а торговца {(kv.Value.Shop ? "нет" : "не должно быть")}");
            foreach (var tk in d.Traders.Traders.Keys)
                if (!d.Npcs.Npcs.ContainsKey(tk)) problems.Add($"traders.json: '{tk}' — нет в npcs.json");
            foreach (var kv in d.Traders.Traders)
            {
                string who = $"traders.json: {kv.Key}";
                float best = kv.Value.DefaultBuy;
                if (kv.Value.DefaultBuy < 0) problems.Add($"{who} — defaultBuy < 0");
                foreach (var b in kv.Value.Buys)
                {
                    if (!byId.ContainsKey(b.Key)) problems.Add($"{who}: buys '{b.Key}' — нет в items.json");
                    if (b.Value < 0) problems.Add($"{who}: buys '{b.Key}' — множитель < 0");
                    best = Math.Max(best, b.Value);
                }
                foreach (var b in kv.Value.BuysKinds)
                {
                    if (Array.IndexOf(ItemKinds, b.Key) < 0) problems.Add($"{who}: buysKinds '{b.Key}' — неизвестный вид");
                    if (b.Value < 0) problems.Add($"{who}: buysKinds '{b.Key}' — множитель < 0");
                    best = Math.Max(best, b.Value);
                }
                if (d.Traders.BuyShare * best >= d.Traders.SellMarkup) problems.Add($"{who} — buyShare × {best} ≥ sellMarkup: на круге торговцев можно заработать");
                foreach (var st in kv.Value.Stock)
                {
                    if (!byId.TryGetValue(st.Item, out var sd)) problems.Add($"{who}: stock '{st.Item}' — нет в items.json");
                    else if (sd.Value <= 0 || sd.Kind == "currency") problems.Add($"{who}: stock '{st.Item}' — без ценности или деньги");
                    if (st.Count != null && st.Count < 1) problems.Add($"{who}: stock '{st.Item}' — count < 1");
                }
            }
            foreach (var kv in d.Map.Marks)
            {
                if (string.IsNullOrEmpty(kv.Value.Name)) problems.Add($"map.json: '{kv.Key}' — нет названия");
                if (!IdPattern.IsMatch(kv.Value.Object)) problems.Add($"map.json: '{kv.Key}' — object '{kv.Value.Object}' (id объекта сцены)");
            }
            foreach (var kv in d.Notebook.Entries)
            {
                if (string.IsNullOrEmpty(kv.Value.Text)) problems.Add($"notebook.json: '{kv.Key}' — пустой текст");
                if (!string.IsNullOrEmpty(kv.Value.Who) && !d.Npcs.Npcs.ContainsKey(kv.Value.Who)) problems.Add($"notebook.json: '{kv.Key}' — who '{kv.Value.Who}' нет в npcs.json");
            }
            foreach (var kv in d.Quests.Quests)
            {
                string who = $"quests.json: {kv.Key}";
                var q = kv.Value;
                if (!d.Npcs.Npcs.ContainsKey(q.Giver)) problems.Add($"{who} — giver '{q.Giver}' нет в npcs.json");
                if (!d.Npcs.Npcs.ContainsKey(q.Receiver)) problems.Add($"{who} — receiver '{q.Receiver}' нет в npcs.json");
                if (!d.Notebook.Entries.ContainsKey(q.Note)) problems.Add($"{who} — note '{q.Note}' нет в notebook.json");
                if (q.Need.Count == 0) problems.Add($"{who} — пустое need");
                foreach (var set in new[] { ("handOut", q.HandOut), ("need", q.Need), ("reward.items", q.Reward.Items) })
                    foreach (var it in set.Item2)
                    {
                        if (!byId.ContainsKey(it.Key)) problems.Add($"{who}: {set.Item1} '{it.Key}' — нет в items.json");
                        if (it.Value < 1) problems.Add($"{who}: {set.Item1} '{it.Key}' — количество < 1");
                    }
                foreach (var m in q.Reward.Marks) if (!d.Map.Marks.ContainsKey(m)) problems.Add($"{who}: reward mark '{m}' — нет в map.json");
                foreach (var h in q.HandOut) if (!q.Need.ContainsKey(h.Key)) problems.Add($"{who}: handOut '{h.Key}' не нужен в need — отдавать нечего");
            }
            {
                var c = d.Camp;
                if (c.BurnSeconds <= 0 || c.MaxBurnSeconds < c.BurnSeconds) problems.Add("camp.json: burnSeconds > 0 и maxBurnSeconds ≥ burnSeconds");
                if (c.FadeSeconds < 0 || c.FadeSeconds > c.BurnSeconds) problems.Add("camp.json: fadeSeconds в [0; burnSeconds]");
                if (c.RestRadius <= 0 || c.LightRadius <= 0) problems.Add("camp.json: restRadius и lightRadius должны быть > 0");
                if (c.RainBurnFactor < 1) problems.Add("camp.json: rainBurnFactor ≥ 1 (дождь не продлевает костёр)");
                if (c.PlaceMinSpacing < 0) problems.Add("camp.json: placeMinSpacing < 0");
                foreach (var kv in c.PlacedKinds)
                {
                    Ref("camp.json", "placedKinds", kv.Key);
                    if (!worldObjects.Contains(kv.Value)) problems.Add($"camp.json: placedKinds '{kv.Key}' → '{kv.Value}' — не объявлено в recipes.json worldObjects");
                }
                foreach (var kv in c.Fuel)
                {
                    Ref("camp.json", "fuel", kv.Key);
                    if (kv.Value <= 0) problems.Add($"camp.json: fuel '{kv.Key}' — секунд должно быть > 0");
                }
                var n = d.Night;
                if (!d.Enemies.Enemies.ContainsKey(n.Enemy)) problems.Add($"night.json: enemy '{n.Enemy}' — нет в enemies.json");
                if (n.MaxAtNight < 0) problems.Add("night.json: maxAtNight < 0");
                if (n.DaylightBelow <= 0 || n.DaylightBelow > 1) problems.Add("night.json: daylightBelow должен быть в (0; 1]");
                if (n.SpawnIntervalSeconds <= 0 || n.DespawnDistance <= 0 || n.MinHeroDistance < 0) problems.Add("night.json: spawnIntervalSeconds, despawnDistance должны быть > 0, minHeroDistance ≥ 0");
                if (n.MaxHeroDistance < n.MinHeroDistance) problems.Add("night.json: maxHeroDistance < minHeroDistance");
                if (d.Enemies.Enemies.TryGetValue(n.Enemy, out var nightDef))
                {
                    if (n.DespawnDistance <= nightDef.AggroRange || n.DespawnDistance >= d.Enemies.LoseInterestFactor * nightDef.AggroRange)
                        problems.Add($"night.json: despawnDistance {n.DespawnDistance} — должен быть между aggroRange ({nightDef.AggroRange}) и loseInterestFactor × aggroRange: ушедший волк иначе застрянет или исчезнет у героини на глазах");
                    if (d.Enemies.StalkMeters < n.MaxHeroDistance) problems.Add("enemies.json: stalkMeters меньше night.json maxHeroDistance — ночной волк бросил бы погоню, не начав");
                    if (!nightDef.FearsFire) problems.Add($"enemies.json: ночной '{n.Enemy}' должен бояться огня (fearsFire)");
                }
                if (n.Bounds.MaxX <= n.Bounds.MinX || n.Bounds.MaxZ <= n.Bounds.MinZ) problems.Add("night.json: bounds пусты");
                foreach (var sa in n.SafeAreas)
                {
                    if (!IdPattern.IsMatch(sa.Anchor)) problems.Add($"night.json: safeAreas '{sa.Anchor}' — id объекта сцены ([a-z0-9_] с буквы)");
                    if (sa.Radius <= 0) problems.Add($"night.json: safeAreas '{sa.Anchor}' — радиус > 0");
                }
                var fire = d.Enemies.Fire;
                if (fire.CampfireRadius < c.LightRadius) problems.Add($"enemies.json: fire.campfireRadius {fire.CampfireRadius} меньше света костра camp.json lightRadius {c.LightRadius} — волк стоял бы в свете");
                if (fire.TorchRadius <= 0 || fire.FleeSpeedFactor <= 0 || fire.FleeMargin < 0 || fire.MaxFleeSeconds <= 0) problems.Add("enemies.json: fire.torchRadius, fleeSpeedFactor, maxFleeSeconds > 0, fleeMargin ≥ 0");
                if (c.TorchBurnSeconds <= 0 || c.TorchFadeSeconds < 0 || c.TorchFadeSeconds > c.TorchBurnSeconds) problems.Add("camp.json: torchBurnSeconds > 0 и torchFadeSeconds в [0; torchBurnSeconds]");
                if (c.TorchRainBurnFactor < 1) problems.Add("camp.json: torchRainBurnFactor ≥ 1");
                if (!string.IsNullOrEmpty(c.BurntItem)) Ref("camp.json", "burntItem", c.BurntItem);
                var g = d.Elements.Grass; var r = d.Elements.Rain; var m = d.Elements.Mud; var w = d.Elements.Wind;
                if (g.NeighborDistance <= 0) problems.Add("elements.json: grass.neighborDistance > 0");
                if (g.BurnSeconds <= 0) problems.Add("elements.json: grass.burnSeconds > 0");
                if (g.SpreadPerSecond <= 0) problems.Add("elements.json: grass.spreadPerSecond > 0");
                if (g.MinSpreadFactor <= 0 || g.MinSpreadFactor > 1) problems.Add("elements.json: grass.minSpreadFactor в (0; 1] — против ветра огонь всё же ползёт");
                if (g.WindGain < 0 || g.CampfireSparkRadius < 0 || g.CampfireSparkPerSecond < 0 || g.BurnRadius <= 0) problems.Add("elements.json: grass.windGain, campfireSpark* ≥ 0, burnRadius > 0");
                if (g.ScorchSeverity < 0 || g.ScorchSeverity > 1 || g.ScorchCooldownSeconds < 0) problems.Add("elements.json: grass.scorchSeverity в 0..1, scorchCooldownSeconds ≥ 0");
                if (r.ChancePerSecond < 0 || r.MinSeconds <= 0 || r.MaxSeconds < r.MinSeconds) problems.Add("elements.json: rain.chancePerSecond ≥ 0, 0 < minSeconds ≤ maxSeconds");
                if (r.WetAfterSeconds < 0 || r.DryAfterSeconds < 0) problems.Add("elements.json: rain.wetAfterSeconds, dryAfterSeconds ≥ 0");
                if (m.RiseSeconds <= 0 || m.DryingSeconds <= 0) problems.Add("elements.json: mud.riseSeconds, dryingSeconds > 0");
                if (w.ChangeEverySeconds <= 0 || w.MinStrength < 0 || w.MaxStrength > 1 || w.MinStrength > w.MaxStrength || w.StartStrength < 0 || w.StartStrength > 1) problems.Add("elements.json: wind.changeEverySeconds > 0, 0 ≤ minStrength ≤ maxStrength ≤ 1, startStrength в 0..1");
                if (!d.Movement.Terrain.ContainsKey("mud")) problems.Add("movement.json: нет terrain.mud (грязь после дождя, D-06)");
            }
            var pairs = recipes.Field.GroupBy(r => string.CompareOrdinal(r.A, r.B) <= 0 ? r.A + "|" + r.B : r.B + "|" + r.A).Where(g => g.Count() > 1);
            foreach (var g in pairs) problems.Add($"recipes.json: пара {g.Key.Replace("|", " + ")} — два рецепта");

            if (problems.Count > 0) throw new DataException(problems);
            d.Items = byId;
            d.FieldRecipes = recipes.Field;
            d.StationRecipes = recipes.Station;
            d.WorldRecipes = recipes.World;
            d.WorldObjects = worldObjects;
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
