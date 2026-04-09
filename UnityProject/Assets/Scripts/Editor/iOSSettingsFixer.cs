using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class iOSSettingsFixer
    {
        [MenuItem("ZeldaDaughter/Setup/Fix iOS Settings")]
        public static void Fix()
        {
            // IL2CPP обязателен для iOS
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);

            // Bundle ID
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.papergallery.zeldasdaughter");

            // Portrait only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // iOS version
            PlayerSettings.iOS.targetOSVersionString = "15.0";

            // Target architecture
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64

            Debug.Log("[iOSSettingsFixer] All iOS settings fixed.");
        }
    }
}
