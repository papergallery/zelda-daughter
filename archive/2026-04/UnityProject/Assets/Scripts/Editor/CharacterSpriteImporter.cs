using System.IO;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Imports character sprites from art/sprites/{characterId}/ into the project
    /// and creates a CharacterVisualConfig ScriptableObject with all sprites assigned.
    ///
    /// Expected file naming convention in source directory:
    ///   idle_front.png, idle_back.png, idle_left.png, idle_right.png
    ///   walk_front.png, walk_back.png, walk_left.png, walk_right.png
    ///   state_wounded.png, state_burned.png, state_poisoned.png, state_overloaded.png
    /// </summary>
    public static class CharacterSpriteImporter
    {
        private const string ArtSpritesRoot    = "/var/www/html/Zelda's daughter/art/sprites";
        private const string AssetsSpritesRoot = "Assets/Sprites/Characters";
        private const string ConfigOutputRoot  = "Assets/Configs/Rendering";
        private const float  PixelsPerUnit     = 100f;

        [MenuItem("Zelda's Daughter/Import Character Sprites")]
        public static void ImportCharacterSprites()
        {
            if (!Directory.Exists(ArtSpritesRoot))
            {
                Debug.LogError($"[CharacterSpriteImporter] Source directory not found: {ArtSpritesRoot}");
                return;
            }

            string[] characterDirs = Directory.GetDirectories(ArtSpritesRoot);
            if (characterDirs.Length == 0)
            {
                Debug.LogWarning("[CharacterSpriteImporter] No character folders found in art/sprites/");
                return;
            }

            foreach (string dir in characterDirs)
            {
                string characterId = Path.GetFileName(dir);
                ImportCharacter(characterId, dir);
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterSpriteImporter] Import complete.");
        }

        [MenuItem("Zelda's Daughter/Import Character Sprites (Select Character)")]
        public static void ImportSelectedCharacter()
        {
            string characterId = EditorUtility.SaveFilePanelInProject(
                "Select character folder name", "", "", "").Trim();

            if (string.IsNullOrEmpty(characterId)) return;

            string sourceDir = Path.Combine(ArtSpritesRoot, characterId);
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"[CharacterSpriteImporter] Folder not found: {sourceDir}");
                return;
            }

            ImportCharacter(characterId, sourceDir);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        private static void ImportCharacter(string characterId, string sourceDir)
        {
            string destDir = $"{AssetsSpritesRoot}/{characterId}";
            EnsureDirectory(destDir);

            // Copy all PNG files
            string[] pngFiles = Directory.GetFiles(sourceDir, "*.png", SearchOption.TopDirectoryOnly);
            foreach (string src in pngFiles)
            {
                string filename = Path.GetFileName(src);
                string dest = $"{destDir}/{filename}";
                File.Copy(src, dest, overwrite: true);
            }

            AssetDatabase.Refresh();

            // Configure TextureImporter for each copied sprite
            foreach (string src in pngFiles)
            {
                string filename = Path.GetFileName(src);
                string assetPath = $"{destDir}/{filename}";
                ConfigureTextureImporter(assetPath);
            }

            AssetDatabase.Refresh();

            // Build or update CharacterVisualConfig
            CreateOrUpdateConfig(characterId, destDir);
        }

        private static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode          = FilterMode.Point; // crisp pixel art
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;

            // Pivot: bottom-center (matches isometric character placement)
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdateConfig(string characterId, string destDir)
        {
            EnsureDirectory(ConfigOutputRoot);

            string configPath = $"{ConfigOutputRoot}/CharacterVisual_{characterId}.asset";
            var config = AssetDatabase.LoadAssetAtPath<CharacterVisualConfig>(configPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<CharacterVisualConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            // Use SerializedObject to assign characterId and sprites through serialized fields
            var so = new SerializedObject(config);

            so.FindProperty("_characterId").stringValue = characterId;

            AssignSprite(so, "_idleFront",      $"{destDir}/idle_front.png");
            AssignSprite(so, "_idleBack",       $"{destDir}/idle_back.png");
            AssignSprite(so, "_idleLeft",       $"{destDir}/idle_left.png");
            AssignSprite(so, "_idleRight",      $"{destDir}/idle_right.png");
            AssignSprite(so, "_walkFront",      $"{destDir}/walk_front.png");
            AssignSprite(so, "_walkBack",       $"{destDir}/walk_back.png");
            AssignSprite(so, "_walkLeft",       $"{destDir}/walk_left.png");
            AssignSprite(so, "_walkRight",      $"{destDir}/walk_right.png");
            AssignSprite(so, "_woundedOverlay", $"{destDir}/state_wounded.png");
            AssignSprite(so, "_burnedOverlay",  $"{destDir}/state_burned.png");
            AssignSprite(so, "_poisonedOverlay", $"{destDir}/state_poisoned.png");
            AssignSprite(so, "_overloadedOverlay", $"{destDir}/state_overloaded.png");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);

            Debug.Log($"[CharacterSpriteImporter] {(isNew ? "Created" : "Updated")} config: {configPath}");
        }

        private static void AssignSprite(SerializedObject so, string propertyName, string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) return; // optional files are allowed to be missing

            var prop = so.FindProperty(propertyName);
            if (prop != null)
                prop.objectReferenceValue = sprite;
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
