using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class AnimatorControllerBuilder
    {
        private const string ControllerOutputPath = "Assets/Animations/Controllers/PlayerAnimator.controller";
        private const string IdleFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Idle.fbx";
        private const string WalkFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Walk.fbx";
        private const string RunFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Run.fbx";

        private const float TransitionDuration = 0.15f;

        [MenuItem("ZeldaDaughter/Animation/Build Player Animator")]
        public static void BuildPlayerAnimator()
        {
            EnsureDirectoryExists("Assets/Animations/Controllers");

            var idleClip = LoadAndConfigureClip(IdleFbxPath, loop: true);
            var walkClip = LoadAndConfigureClip(WalkFbxPath, loop: true);
            var runClip = LoadAndConfigureClip(RunFbxPath, loop: true);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerOutputPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("Idle");
            var walkState = rootStateMachine.AddState("Walk");
            var runState = rootStateMachine.AddState("Run");

            idleState.motion = idleClip;
            walkState.motion = walkClip;
            runState.motion = runClip;

            rootStateMachine.defaultState = idleState;

            // Idle → Walk: IsMoving=true, Speed<0.5
            var idleToWalk = idleState.AddTransition(walkState);
            ConfigureTransition(idleToWalk);
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            idleToWalk.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            // Walk → Run: Speed>=0.5
            var walkToRun = walkState.AddTransition(runState);
            ConfigureTransition(walkToRun);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 0.49f, "Speed");

            // Run → Walk: Speed<0.5
            var runToWalk = runState.AddTransition(walkState);
            ConfigureTransition(runToWalk);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            // Walk → Idle: IsMoving=false
            var walkToIdle = walkState.AddTransition(idleState);
            ConfigureTransition(walkToIdle);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            // Run → Idle: IsMoving=false
            var runToIdle = runState.AddTransition(idleState);
            ConfigureTransition(runToIdle);
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnimatorControllerBuilder] PlayerAnimator создан: {ControllerOutputPath}");
        }

        private static void ConfigureTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.duration = TransitionDuration;
        }

        private static AnimationClip LoadAndConfigureClip(string fbxPath, bool loop)
        {
            EnsureClipIsLooping(fbxPath, loop);

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in clips)
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                    return clip;
            }

            Debug.LogWarning($"[AnimatorControllerBuilder] AnimationClip не найден в: {fbxPath}");
            return null;
        }

        private static void EnsureClipIsLooping(string fbxPath, bool loop)
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "../", fbxPath)))
                return;

            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            var clipAnimations = importer.clipAnimations;
            if (clipAnimations == null || clipAnimations.Length == 0)
                clipAnimations = importer.defaultClipAnimations;

            bool changed = false;
            foreach (var clip in clipAnimations)
            {
                if (clip.loopTime != loop)
                {
                    clip.loopTime = loop;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
                Debug.Log($"[AnimatorControllerBuilder] Loop={loop} применён для: {fbxPath}");
            }
        }

        private static void EnsureDirectoryExists(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    Debug.Log($"[AnimatorControllerBuilder] Создана папка: {next}");
                }
                current = next;
            }
        }
    }
}
