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
        private const string PickUpFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/PickUp.fbx";
        private const string InteractFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Interact.fbx";
        private const string Attack1hFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Attack(1h).fbx";
        private const string DefeatFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Defeat.fbx";
        private const string BlockFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Block.fbx";
        private const string RollFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Roll.fbx";

        private const float TransitionDuration = 0.15f;

        [MenuItem("ZeldaDaughter/Animation/Build Player Animator")]
        public static void BuildPlayerAnimator()
        {
            EnsureDirectoryExists("Assets/Animations/Controllers");

            var idleClip = LoadAndConfigureClip(IdleFbxPath, loop: true);
            var walkClip = LoadAndConfigureClip(WalkFbxPath, loop: true);
            var runClip = LoadAndConfigureClip(RunFbxPath, loop: true);
            var pickUpClip = LoadAndConfigureClip(PickUpFbxPath, loop: false);
            var interactClip = LoadAndConfigureClip(InteractFbxPath, loop: false);
            var attack1hClip = LoadAndConfigureClip(Attack1hFbxPath, loop: false);
            var defeatClip = LoadAndConfigureClip(DefeatFbxPath, loop: false);
            var blockClip = LoadAndConfigureClip(BlockFbxPath, loop: false);
            var rollClip = LoadAndConfigureClip(RollFbxPath, loop: false);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerOutputPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("PickUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Defeat", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("Idle");
            var walkState = rootStateMachine.AddState("Walk");
            var runState = rootStateMachine.AddState("Run");
            var pickUpState = rootStateMachine.AddState("PickUp");
            var interactState = rootStateMachine.AddState("Interact");
            var attack1hState = rootStateMachine.AddState("Attack1h");
            var hitState = rootStateMachine.AddState("Hit");
            var defeatState = rootStateMachine.AddState("Defeat");
            var eatState = rootStateMachine.AddState("Eat");

            idleState.motion = idleClip;
            walkState.motion = walkClip;
            runState.motion = runClip;
            pickUpState.motion = pickUpClip;
            interactState.motion = interactClip;
            attack1hState.motion = attack1hClip;
            if (blockClip != null) hitState.motion = blockClip;
            if (defeatClip != null) defeatState.motion = defeatClip;
            if (interactClip != null) eatState.motion = interactClip;

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

            // AnyState → PickUp, Interact, Attack1h, Hit, Defeat, Eat
            AddActionState(rootStateMachine, idleState, pickUpState, "PickUp");
            AddActionState(rootStateMachine, idleState, interactState, "Interact");
            AddActionState(rootStateMachine, idleState, attack1hState, "Attack");
            AddActionState(rootStateMachine, idleState, hitState, "Hit");
            AddActionState(rootStateMachine, idleState, defeatState, "Defeat");
            AddActionState(rootStateMachine, idleState, eatState, "Eat");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnimatorControllerBuilder] PlayerAnimator создан: {ControllerOutputPath}");
        }

        // AnyState → actionState (trigger), actionState → idle (exitTime=0.9)
        private static void AddActionState(
            AnimatorStateMachine sm,
            AnimatorState idleState,
            AnimatorState actionState,
            string triggerName)
        {
            var anyToAction = sm.AddAnyStateTransition(actionState);
            anyToAction.hasExitTime = false;
            anyToAction.duration = 0.1f;
            anyToAction.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            var actionToIdle = actionState.AddTransition(idleState);
            actionToIdle.hasExitTime = true;
            actionToIdle.exitTime = 0.9f;
            actionToIdle.duration = 0.15f;
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
