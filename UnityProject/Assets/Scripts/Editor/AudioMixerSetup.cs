#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Editor utility: creates Assets/Audio/MainMixer.mixer with groups
    /// Master → SFX, Ambient, Music and exposes volume parameters for each.
    ///
    /// Unity 2022 LTS does not expose a public API for programmatic AudioMixer group
    /// creation, so this script uses internal-type reflection via AudioMixerController.
    /// If the internal API changes, the menu item logs guidance for manual setup.
    /// </summary>
    public static class AudioMixerSetup
    {
        private const string MixerPath = "Assets/Audio/MainMixer.mixer";
        private const string AudioFolder = "Assets/Audio";

        [MenuItem("ZeldaDaughter/Audio/Create Mixer")]
        public static void CreateMixer()
        {
            EnsureAudioFolder();

            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
            {
                Debug.Log("[AudioMixerSetup] Mixer already exists at: " + MixerPath);
                return;
            }

            // AudioMixerController is internal to UnityEditor — access via reflection
            var controllerType = System.Type.GetType(
                "UnityEditor.Audio.AudioMixerController, UnityEditor");

            if (controllerType == null)
            {
                LogManualInstructions();
                return;
            }

            var controller = ScriptableObject.CreateInstance(controllerType);
            if (controller == null)
            {
                LogManualInstructions();
                return;
            }

            AssetDatabase.CreateAsset(controller, MixerPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogError("[AudioMixerSetup] Asset created but AudioMixer could not be loaded.");
                return;
            }

            SetupGroupsAndParameters(controller, controllerType);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[AudioMixerSetup] MainMixer created at: " + MixerPath);
            Debug.Log("[AudioMixerSetup] Groups: Master / SFX / Ambient / Music");
            Debug.Log("[AudioMixerSetup] Exposed params: MasterVolume, SFXVolume, AmbientVolume, MusicVolume");

            Selection.activeObject = mixer;
            EditorGUIUtility.PingObject(mixer);
        }

        // -----------------------------------------------------------------------
        // Internal setup
        // -----------------------------------------------------------------------

        private static void SetupGroupsAndParameters(Object controller, System.Type controllerType)
        {
            var masterGroup = GetMasterGroup(controller, controllerType);
            if (masterGroup == null)
            {
                Debug.LogWarning("[AudioMixerSetup] masterGroup not found — add SFX/Ambient/Music groups manually.");
                return;
            }

            TryExposeVolume(controller, controllerType, masterGroup, "MasterVolume");

            var createGroup = controllerType.GetMethod(
                "CreateNewGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (createGroup == null)
            {
                Debug.LogWarning("[AudioMixerSetup] CreateNewGroup not found — add child groups manually.");
                return;
            }

            (string group, string param)[] children =
            {
                ("SFX",     "SFXVolume"),
                ("Ambient", "AmbientVolume"),
                ("Music",   "MusicVolume"),
            };

            foreach (var (groupName, paramName) in children)
            {
                var group = createGroup.Invoke(controller, new object[] { groupName, true });
                if (group != null)
                    TryExposeVolume(controller, controllerType, group, paramName);
            }
        }

        private static object GetMasterGroup(Object controller, System.Type controllerType)
        {
            // Property name in Unity 2022: "masterGroup" (public) or "MasterGroup"
            foreach (var name in new[] { "masterGroup", "MasterGroup" })
            {
                var prop = controllerType.GetProperty(
                    name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                    return prop.GetValue(controller);
            }
            return null;
        }

        private static void TryExposeVolume(
            Object controller, System.Type controllerType, object group, string exposedName)
        {
            var groupType = group.GetType();

            // Locate the volume AudioMixerParameter field on the group
            var volumeField = groupType.GetField(
                "volume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (volumeField == null) return;

            var volumeParam = volumeField.GetValue(group);
            if (volumeParam == null) return;

            // Read the GUID of the volume parameter
            var paramType = volumeParam.GetType();
            var guidField = paramType.GetField(
                "m_Guid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (guidField == null) return;

            uint guid = (uint)guidField.GetValue(volumeParam);

            // Access exposed parameters list on controller
            var exposedListField = controllerType.GetField(
                "m_ExposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (exposedListField == null) return;

            var exposedList = exposedListField.GetValue(controller);
            if (exposedList == null) return;

            // ExposedAudioMixerParameter is also internal
            var epType = System.Type.GetType(
                "UnityEditor.Audio.ExposedAudioMixerParameter, UnityEditor");
            if (epType == null) return;

            var epInstance = System.Activator.CreateInstance(epType);

            SetField(epType, epInstance, "m_Guid", guid);
            SetField(epType, epInstance, "name",   exposedName);

            var addMethod = exposedList.GetType().GetMethod("Add");
            addMethod?.Invoke(exposedList, new[] { epInstance });
        }

        private static void SetField(System.Type type, object obj, string fieldName, object value)
        {
            var field = type.GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(obj, value);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static void EnsureAudioFolder()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Audio");
                Debug.Log("[AudioMixerSetup] Created folder: " + AudioFolder);
            }
        }

        private static void LogManualInstructions()
        {
            Debug.LogWarning(
                "[AudioMixerSetup] Could not access internal AudioMixerController API.\n" +
                "Create the mixer manually:\n" +
                "  1. Right-click Assets/Audio → Create → Audio Mixer → name it 'MainMixer'\n" +
                "  2. Add groups: SFX, Ambient, Music (all children of Master)\n" +
                "  3. Expose volume for each group: click the group, right-click 'Volume' → Expose\n" +
                "  4. In the Exposed Parameters panel rename them: MasterVolume, SFXVolume, AmbientVolume, MusicVolume");
        }
    }
}
#endif
