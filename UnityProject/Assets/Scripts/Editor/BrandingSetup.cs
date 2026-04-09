using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class BrandingSetup
    {
        [MenuItem("ZeldaDaughter/Setup/Apply Icon & Splash")]
        public static void Apply()
        {
            // --- App Icon ---
            var icon1024 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/icon_1024.png");
            if (icon1024 == null)
            {
                Debug.LogError("[Branding] icon_1024.png not found in Assets/");
                return;
            }

            // Set icon for all platforms
            var icons = new Texture2D[] { icon1024 };

            // iOS icons - set default
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.iOS, icons);

            // Android icon
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons);

            // Default icon
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, icons);

            // --- Splash Screen ---
            // Disable Unity splash (only works with Pro license, but set anyway)
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // Set background color to match our splash
            PlayerSettings.SplashScreen.backgroundColor = new Color(15f/255f, 12f/255f, 40f/255f);

            // Add our splash logo
            var splashTex = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/splash_screen.png");
            if (splashTex != null)
            {
                var logos = new PlayerSettings.SplashScreenLogo[]
                {
                    PlayerSettings.SplashScreenLogo.Create(2.5f, splashTex)
                };
                PlayerSettings.SplashScreen.logos = logos;
                PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
                Debug.Log("[Branding] Splash screen set.");
            }
            else
            {
                // Try to set texture import settings first
                var importer = AssetImporter.GetAtPath("Assets/splash_screen.png") as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                    Debug.Log("[Branding] Splash texture reimported as Sprite. Run again.");
                }
                else
                {
                    Debug.LogWarning("[Branding] splash_screen.png not found.");
                }
            }

            // Set icon texture import settings
            SetTextureImportSettings("Assets/icon_1024.png");

            AssetDatabase.SaveAssets();
            Debug.Log("[Branding] Icon and splash applied.");
        }

        private static void SetTextureImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
