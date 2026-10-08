using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// W0: Assets/Art/Registries/*.json (the source of truth, edited on the server; checked by tools/check-registries.py) → the registry assets
    /// the game loads (docs/demo/unity-architecture.md §2.5). Menu Zelda → Art → Build registries; <c>SceneBuilder.BuildAll</c> calls it first.
    /// An asset is rebuilt only when its source or the files it names changed (the hash is kept as an asset label), so repeated runs change nothing
    /// in git. The main assets keep their GUID: scenes refer to them.
    /// </summary>
    public static class RegistryBuilder
    {
        public const string Dir = "Assets/Art/Registries";
        public const string CharactersPath = Dir + "/Characters.asset";
        public const string ItemIconsPath = Dir + "/ItemIcons.asset";
        public const string TalkIconsPath = Dir + "/TalkIcons.asset";
        public const string SoundsPath = Dir + "/Sounds.asset";
        public const string FxPath = Dir + "/Fx.asset";
        public const string UiLookPath = Dir + "/UiLook.asset";
        public const string SpriteLookPath = Dir + "/SpriteLook.asset";
        const string PaperPath = "Assets/Art/UI/paper.png";
        const string HashLabel = "zdhash:";

        [MenuItem("Zelda/Art/Build registries")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/UI")) AssetDatabase.CreateFolder("Assets/Art", "UI");
            int c = Characters(), i = Icons("item-icons.json", ItemIconsPath), t = Icons("talk-icons.json", TalkIconsPath), s = Sounds(), f = Fx();
            Looks();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZD:Art] registries characters={c} itemIcons={i} talkIcons={t} sounds={s} fx={f}");
        }

        /// <summary>Builds only if some registry asset is missing (a scene build on a fresh checkout).</summary>
        public static void EnsureBuilt()
        {
            string[] all = { CharactersPath, ItemIconsPath, TalkIconsPath, SoundsPath, FxPath, UiLookPath, SpriteLookPath };
            if (all.Any(p => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) == null)) BuildAll();
        }

        public static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);

        // ------------------------------------------------------------------ characters

        static int Characters()
        {
            var json = Read("characters.json", "characters");
            string hash = HashOf("characters.json", json);
            var registry = Ensure<CharacterRegistry>(CharactersPath);
            if (Fresh(registry, hash)) return json.Count;

            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(CharactersPath)) if (old is CharacterSpriteSet) UnityEngine.Object.DestroyImmediate(old, true);
            var ids = new List<string>();
            var sets = new List<CharacterSpriteSet>();
            foreach (var kv in json)
            {
                var rec = (JObject)kv.Value;
                var set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
                set.name = kv.Key;
                set.Configure(Frames(rec, "front", true), Frames(rec, "back", true), Frames(rec, "side", true),
                    SpriteAt((string)rec["down"], true), (float?)rec["pixelsPerMeter"] ?? 75f, (float?)rec["strideMeters"] ?? 0.8f,
                    (bool?)rec["placeholder"] ?? false, ColorOf((string)rec["color"]));
                AssetDatabase.AddObjectToAsset(set, registry);
                ids.Add(kv.Key);
                sets.Add(set);
            }
            registry.Configure(ids.ToArray(), sets.ToArray());
            Stamp(registry, hash);
            return ids.Count;
        }

        static Sprite[] Frames(JObject rec, string key, bool feet)
        {
            var arr = rec[key] as JArray;
            return arr == null ? new Sprite[0] : arr.Select(p => SpriteAt((string)p, feet)).Where(s => s != null).ToArray();
        }

        // ------------------------------------------------------------------ icons, sounds, fx

        static int Icons(string file, string assetPath)
        {
            var json = Read(file, "icons");
            string hash = HashOf(file, json);
            var registry = Ensure<IconRegistry>(assetPath);
            if (Fresh(registry, hash)) return json.Count;
            var ids = json.Properties().Select(p => p.Name).ToArray();
            var sprites = json.Properties().Select(p => SpriteAt((string)((JObject)p.Value)["path"], false)).ToArray();
            registry.Configure(ids, sprites);
            Stamp(registry, hash);
            return ids.Length;
        }

        static int Sounds()
        {
            var json = Read("sounds.json", "sounds");
            string hash = HashOf("sounds.json", json);
            var registry = Ensure<SoundRegistry>(SoundsPath);
            if (Fresh(registry, hash)) return json.Count;
            var defs = new List<SoundDef>();
            foreach (var kv in json)
            {
                var rec = (JObject)kv.Value;
                var clips = (rec["clips"] as JArray)?.Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>((string)p)).Where(c => c != null).ToArray() ?? new AudioClip[0];
                defs.Add(new SoundDef
                {
                    Id = kv.Key, Clips = clips, Volume = (float?)rec["volume"] ?? 1f,
                    PitchJitter = (float?)rec["pitchJitter"] ?? 0f, Spatial = (bool?)rec["spatial"] ?? false,
                });
            }
            registry.Configure(defs.ToArray());
            Stamp(registry, hash);
            return defs.Count;
        }

        static int Fx()
        {
            var json = Read("fx.json", "fx");
            string hash = HashOf("fx.json", json);
            var registry = Ensure<FxRegistry>(FxPath);
            if (Fresh(registry, hash)) return json.Count;
            var ids = json.Properties().Select(prop => prop.Name).ToArray();
            var prefabs = json.Properties().Select(prop =>
            {
                string path = (string)((JObject)prop.Value)["prefab"];
                return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }).ToArray();
            registry.Configure(ids, prefabs);
            Stamp(registry, hash);
            return ids.Length;
        }

        // ------------------------------------------------------------------ UI and sprite looks

        static void Looks()
        {
            var look = Ensure<UiLook>(UiLookPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.MainFontPath);
            var paper = BakePaper();
            string hash = HashOf("uilook", new JObject { ["font"] = font != null ? AssetDatabase.AssetPathToGUID(FontBuilder.MainFontPath) : "", ["paper"] = paper != null });
            if (!Fresh(look, hash))
            {
                look.Configure(font, paper);
                Stamp(look, hash);
            }

            var sprite = Ensure<SpriteLook>(SpriteLookPath);
            if (!Fresh(sprite, "spritelook-1"))
            {
                var spriteMat = EnsureMaterial(Dir + "/SpriteLook_Sprite.mat", () => SpriteLook.NewSpriteMaterial(0.5f));
                var shadowMat = EnsureMaterial(Dir + "/SpriteLook_Shadow.mat", SpriteLook.NewShadowMaterial);
                sprite.Configure(spriteMat, shadowMat, 0.5f, 0.9f, 0.35f, 0.04f);
                Stamp(sprite, "spritelook-1");
            }
        }

        static Material EnsureMaterial(string path, Func<Material> make)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = make();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>The paper plate PNG, drawn by the same code as the runtime stand-in; baked once so the 9-slice border is a real import setting.</summary>
        static Sprite BakePaper()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath) == null)
            {
                var tex = PlaceholderSprites.PlateTexture();
                File.WriteAllBytes(PaperPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(PaperPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(PaperPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(20, 20, 20, 20);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        }

        // ------------------------------------------------------------------ helpers

        static JObject Read(string file, string key)
        {
            var root = JObject.Parse(File.ReadAllText(Path.Combine(Dir, file)));
            return (JObject)root[key] ?? throw new InvalidDataException($"{file}: no '{key}'");
        }

        /// <summary>A hash of the JSON text plus, for every file path it names, whether the file exists — a picture that appears later rebuilds the registry.</summary>
        static string HashOf(string name, JObject json)
        {
            var sb = new StringBuilder(name).Append(json.ToString(Newtonsoft.Json.Formatting.None));
            foreach (var token in json.DescendantsAndSelf().OfType<JValue>())
            {
                if (token.Value is string s && s.StartsWith("Assets/", StringComparison.Ordinal))
                    sb.Append('|').Append(s).Append(File.Exists(s) ? "+" : "-");
            }
            using (var sha = SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }

        static bool Fresh(UnityEngine.Object asset, string hash) => AssetDatabase.GetLabels(asset).Contains(HashLabel + hash);

        static void Stamp(UnityEngine.Object asset, string hash)
        {
            var labels = AssetDatabase.GetLabels(asset).Where(l => !l.StartsWith(HashLabel, StringComparison.Ordinal)).Append(HashLabel + hash).ToArray();
            AssetDatabase.SetLabels(asset, labels);
            EditorUtility.SetDirty(asset);
        }

        static T Ensure<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        /// <summary>The sprite at a path, set up as a Sprite on import (feet pivot for figures, centre for icons); null for an empty path or a missing file.</summary>
        static Sprite SpriteAt(string path, bool feet)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!File.Exists(path)) { Debug.LogWarning($"[ZD:Art] sprite file missing: {path}"); return null; }
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                int align = (int)(feet ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                if (importer.textureType != TextureImporterType.Sprite || settings.spriteAlignment != align || importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = align;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Color ColorOf(string hex) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(0.8f, 0.65f, 0.4f, 1f);
    }
}
