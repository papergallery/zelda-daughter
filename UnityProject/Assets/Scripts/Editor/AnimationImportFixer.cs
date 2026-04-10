using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Исправляет import settings для KayKit Single Animations:
    /// устанавливает animationType=Generic и sourceAvatar = avatar от KayKit character FBX.
    /// </summary>
    public static class AnimationImportFixer
    {
        private const string CharacterFbxPath =
            "Assets/Animations/KayKit/fbx/KayKit Animated Character_v1.2.fbx";
        private const string SingleAnimationsFolder =
            "Assets/Animations/KayKit/fbx/Single Animations";

        [MenuItem("ZeldaDaughter/Animation/Fix KayKit Import Settings")]
        public static void FixKayKitImports()
        {
            // Загружаем основной FBX для получения avatar
            var characterImporter = AssetImporter.GetAtPath(CharacterFbxPath) as ModelImporter;
            if (characterImporter == null)
            {
                Debug.LogError($"[AnimationImportFixer] Не найден KayKit character FBX: {CharacterFbxPath}");
                return;
            }

            // Avatar у Generic FBX хранится как sub-asset
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(CharacterFbxPath);
            Avatar sourceAvatar = null;
            foreach (var asset in allAssets)
            {
                if (asset is Avatar av)
                {
                    sourceAvatar = av;
                    break;
                }
            }

            if (sourceAvatar == null)
            {
                Debug.LogError($"[AnimationImportFixer] Avatar не найден в {CharacterFbxPath}. " +
                               "Убедитесь что animationType=Generic и reimport.");
                return;
            }

            Debug.Log($"[AnimationImportFixer] Найден avatar: {sourceAvatar.name} " +
                      $"(guid: {AssetDatabase.AssetPathToGUID(CharacterFbxPath)})");

            // Находим все FBX в папке Single Animations
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { SingleAnimationsFolder });
            int fixedCount = 0;
            int skippedCount = 0;

            foreach (var guid in fbxGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    continue;

                bool needsReimport = false;

                // Устанавливаем Generic если не установлен
                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    needsReimport = true;
                    Debug.Log($"[AnimationImportFixer] {Path.GetFileName(path)}: animationType → Generic");
                }

                // Устанавливаем sourceAvatar если не совпадает
                if (importer.sourceAvatar != sourceAvatar)
                {
                    importer.sourceAvatar = sourceAvatar;
                    needsReimport = true;
                    Debug.Log($"[AnimationImportFixer] {Path.GetFileName(path)}: sourceAvatar → {sourceAvatar.name}");
                }

                if (needsReimport)
                {
                    importer.SaveAndReimport();
                    fixedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            Debug.Log($"[AnimationImportFixer] Готово. Исправлено: {fixedCount}, пропущено (уже OK): {skippedCount}");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
