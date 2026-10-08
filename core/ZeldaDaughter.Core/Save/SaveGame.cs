#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Inventory;
using ZeldaDaughter.Core.Progression;

namespace ZeldaDaughter.Core.Save
{
    /// <summary>
    /// One save slot (project-design.md §6 «Система сохранения»): a versioned JSON snapshot of <see cref="GameState"/>,
    /// written atomically (temp file → replace, the previous kept as .bak). When to save — exit, zone change, every N
    /// minutes — is the game's call (V-11). April: no version, non-atomic, half the state missing.
    /// </summary>
    public enum LoadOutcome { NoSave, Loaded, Rejected }

    public static class SaveGame
    {
        public const int Version = 1;

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
        };

        public static string Capture(GameState g)
        {
            var s = new Snapshot
            {
                Version = Version,
                Zone = g.Zone,
                X = g.HeroPosition.X, Z = g.HeroPosition.Y, Y = g.HeroHeight, Facing = g.HeroFacingDegrees,
                Day = g.Clock.Day, TimeOfDay = g.Clock.TimeOfDay,
                Hp = g.Condition.Hp,
                Hunger = g.Hunger.Value,
                Understanding = g.Language.Understanding,
            };
            foreach (WoundType t in Enum.GetValues(typeof(WoundType)))
                if (g.Condition.Severity(t) > 0) s.Wounds[HeroCondition.Key(t)] = g.Condition.Severity(t);
            foreach (Stat st in Enum.GetValues(typeof(Stat))) s.Stats[Skills.Key(st)] = g.Skills.Get(st);
            foreach (WeaponClass w in Enum.GetValues(typeof(WeaponClass))) s.Weapons[Skills.Key(w)] = g.Skills.Weapon(w);
            s.Bag = g.Bag.Stacks.Select(x => new StackDto { Item = x.ItemId, Count = x.Count }).ToList();
            s.KnownRecipes = g.Crafting.Known.OrderBy(x => x, StringComparer.Ordinal).ToList();
            s.MetNpcs = g.Language.MetNpcs.OrderBy(x => x, StringComparer.Ordinal).ToList();
            s.HeardLines = g.Language.HeardLines.OrderBy(x => x, StringComparer.Ordinal).ToList();
            s.HintsDone = g.Hints.Done.OrderBy(x => x, StringComparer.Ordinal).ToList();
            s.Picked = g.Picked.OrderBy(x => x, StringComparer.Ordinal).ToList();
            return JsonConvert.SerializeObject(s, Json);
        }

        /// <summary>Restores into a fresh state. Throws <see cref="InvalidDataException"/> for a save from a newer game.</summary>
        public static void Restore(GameState g, string json)
        {
            var s = JsonConvert.DeserializeObject<Snapshot>(json, Json) ?? throw new InvalidDataException("empty save");
            if (s.Version > Version) throw new InvalidDataException($"save version {s.Version} is newer than this game ({Version})");
            g.Zone = s.Zone;
            g.HeroPosition = new Vec2(s.X, s.Z);
            g.HeroHeight = s.Y;
            g.HeroFacingDegrees = s.Facing;
            g.Clock.SetTime(Math.Max(1, s.Day), Math.Max(0, Math.Min(0.999999, s.TimeOfDay)));
            g.Condition.Restore(s.Hp, s.Wounds);
            g.Hunger.Restore(s.Hunger);
            g.Skills.Restore(s.Stats, s.Weapons);
            g.Bag.Restore(s.Bag.Select(x => new Stack(x.Item, x.Count)));
            g.Crafting.Restore(s.KnownRecipes);
            g.Language.Restore(s.Understanding, s.MetNpcs, s.HeardLines);
            g.Hints.Restore(s.HintsDone);
            g.Picked.Clear();
            foreach (var p in s.Picked) g.Picked.Add(p);
        }

        /// <summary>
        /// Reads the slot into <paramref name="g"/>. <see cref="LoadOutcome.Rejected"/> — a save exists but must not be
        /// used (newer game, broken with no readable backup): the caller must not overwrite the slot this session.
        /// </summary>
        public static LoadOutcome Load(GameState g, string path, out string? problem)
        {
            problem = null;
            string? text = ReadSlot(path);
            if (text == null)
            {
                if (!File.Exists(path) && !File.Exists(path + ".bak")) return LoadOutcome.NoSave;
                problem = "slot and backup are unreadable";
                return LoadOutcome.Rejected;
            }
            try
            {
                Restore(g, text);
                return LoadOutcome.Loaded;
            }
            catch (Exception e) when (e is InvalidDataException || e is JsonException || e is ArgumentException || e is InvalidOperationException)
            {
                problem = e.Message;
                return LoadOutcome.Rejected;
            }
        }

        /// <summary>Write so that a crash mid-write never leaves a broken slot: temp → replace; the old one stays as .bak.</summary>
        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
            else File.Move(tmp, path);
        }

        /// <summary>The slot, or the backup if the slot is missing or unreadable; null if neither.</summary>
        public static string? ReadSlot(string path)
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    string text = File.ReadAllText(p);
                    JsonConvert.DeserializeObject<Snapshot>(text, Json);
                    return text;
                }
                catch (JsonException) { }
                catch (IOException) { }
            }
            return null;
        }

        sealed class Snapshot
        {
            public int Version { get; set; }
            public string Zone { get; set; } = "";
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Facing { get; set; }
            public int Day { get; set; } = 1;
            public double TimeOfDay { get; set; }
            public float Hp { get; set; }
            public Dictionary<string, float> Wounds { get; set; } = new Dictionary<string, float>();
            public float Hunger { get; set; }
            public Dictionary<string, float> Stats { get; set; } = new Dictionary<string, float>();
            public Dictionary<string, float> Weapons { get; set; } = new Dictionary<string, float>();
            public List<StackDto> Bag { get; set; } = new List<StackDto>();
            public List<string> KnownRecipes { get; set; } = new List<string>();
            public float Understanding { get; set; }
            public List<string> MetNpcs { get; set; } = new List<string>();
            public List<string> HeardLines { get; set; } = new List<string>();
            public List<string> HintsDone { get; set; } = new List<string>();
            public List<string> Picked { get; set; } = new List<string>();
        }

        sealed class StackDto
        {
            public string Item { get; set; } = "";
            public int Count { get; set; }
        }
    }
}
