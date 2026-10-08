using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// W0: the game's fonts as TextMeshPro assets with a static atlas (docs/demo/unity-architecture.md §2.6): the handwritten Cyrillic Neucha
    /// (OFL) as the main font and Noto Sans Runic (OFL) as its fallback for the runes of data/language.json. TTFs and licences lie in Assets/Art/Fonts.
    /// The atlas holds every character found in the data texts, so nothing is rasterised in play. Two steps, because importing the TMP essentials
    /// can reload scripts: <see cref="ImportEssentials"/> (Zelda → Art → Import TMP essentials), then <see cref="BuildAll"/> (Zelda → Art → Build fonts).
    /// </summary>
    public static class FontBuilder
    {
        public const string FontsDir = "Assets/Art/Fonts";
        public const string MainTtf = FontsDir + "/Neucha.ttf";
        public const string RunicTtf = FontsDir + "/NotoSansRunic-Regular.ttf";
        public const string MainFontPath = FontsDir + "/Neucha SDF.asset";
        public const string RunicFontPath = FontsDir + "/NotoSansRunic SDF.asset";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string HashLabel = "zdhash:";
        const string Latin = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        const string Extra = "ЁёАБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдежзийклмнопрстуфхцчшщъыьэюя«»—–…“”‘’№°×·•→←↑↓";

        [MenuItem("Zelda/Art/Import TMP essentials")]
        public static void ImportEssentials()
        {
            if (File.Exists(TmpSettingsPath)) { Debug.Log("[ZD:Art] TMP essentials already imported"); return; }
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string package = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(package)) { Debug.LogError("[ZD:Art] TMP Essential Resources.unitypackage not found at " + package); return; }
            AssetDatabase.ImportPackage(package, false); // not interactive: the interactive importer opens a modal window and mutes the bridge
            AssetDatabase.Refresh();
            Debug.Log("[ZD:Art] TMP essentials import started");
        }

        [MenuItem("Zelda/Art/Build fonts")]
        public static void BuildAll()
        {
            if (!File.Exists(TmpSettingsPath)) { Debug.LogError("[ZD:Art] TMP essentials are not imported yet — run Zelda → Art → Import TMP essentials, then this again"); return; }
            string dataChars = DataCharacters(out string runes);
            var runic = Build(RunicTtf, RunicFontPath, runes + "·", null);
            var main = Build(MainTtf, MainFontPath, Latin + Extra + dataChars, runic);
            if (main != null)
            {
                var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
                if (settings != null)
                {
                    var so = new SerializedObject(settings);
                    var p = so.FindProperty("m_defaultFontAsset");
                    if (p != null && p.objectReferenceValue != main) { p.objectReferenceValue = main; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings); }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ZD:Art] fonts built main=" + (main != null) + " runic=" + (runic != null));
        }

        /// <summary>One font asset with a static atlas of <paramref name="characters"/>; rebuilt only when the TTF or the characters changed.</summary>
        static TMP_FontAsset Build(string ttf, string assetPath, string characters, TMP_FontAsset fallback)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { Debug.LogError("[ZD:Art] font file missing: " + ttf); return null; }
            string unique = new string(characters.Distinct().OrderBy(c => c).ToArray());
            string hash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
                hash = AssetDatabase.AssetPathToGUID(ttf).Substring(0, 8) + "-" + BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(unique))).Replace("-", "").Substring(0, 12).ToLowerInvariant();

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null && AssetDatabase.GetLabels(existing).Contains(HashLabel + hash)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);

            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTexture.name = fa.name + " Atlas";
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            fa.TryAddCharacters(unique, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[ZD:Art] {fa.name}: {missing.Length} characters not in the font: {missing}");
            var so = new SerializedObject(fa);
            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (fallback != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            AssetDatabase.SetLabels(fa, new[] { HashLabel + hash });
            EditorUtility.SetDirty(fa);
            EditorUtility.SetDirty(fa.atlasTexture);
            EditorUtility.SetDirty(fa.material);
            Debug.Log($"[ZD:Art] font {fa.name}: {fa.characterTable.Count} characters in the atlas");
            return fa;
        }

        /// <summary>Every character of every text value in data/*.json (keys starting with "_" are notes for people, not shown), and the runes of language.json separately.</summary>
        static string DataCharacters(out string runes)
        {
            var all = new HashSet<char>();
            string dataDir = Path.Combine("..", "data");
            runes = "";
            foreach (var file in Directory.GetFiles(dataDir, "*.json"))
            {
                if (Path.GetFileName(file).StartsWith("model", StringComparison.Ordinal)) continue;
                Walk(JToken.Parse(File.ReadAllText(file)), all);
            }
            var language = JObject.Parse(File.ReadAllText(Path.Combine(dataDir, "language.json")));
            runes = (string)language["glyphs"] ?? "";
            foreach (var c in runes) all.Remove(c); // runes belong to the runic font
            var sb = new StringBuilder();
            foreach (var c in all.OrderBy(c => c)) if (c >= ' ') sb.Append(c);
            return sb.ToString();
        }

        static void Walk(JToken token, HashSet<char> into)
        {
            switch (token)
            {
                case JObject o:
                    foreach (var p in o.Properties()) if (!p.Name.StartsWith("_", StringComparison.Ordinal)) Walk(p.Value, into);
                    break;
                case JArray a:
                    foreach (var t in a) Walk(t, into);
                    break;
                case JValue v when v.Value is string s:
                    foreach (var c in s) into.Add(c);
                    break;
            }
        }
    }
}
