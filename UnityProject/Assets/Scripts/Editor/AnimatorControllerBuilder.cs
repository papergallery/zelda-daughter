using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class AnimatorControllerBuilder
    {
        private const string ControllerOutputPath = "Assets/Animations/Controllers/PlayerAnimator.controller";

        // RPGCharacters FBX со встроенными анимациями
        private const string RogueFbxPath = "Assets/Models/RPGCharacters/Rogue.fbx";

        private const float TransitionDuration = 0.15f;

        [MenuItem("ZeldaDaughter/Animation/Build Player Animator")]
        public static void BuildPlayerAnimator()
        {
            EnsureDirectoryExists("Assets/Animations/Controllers");

            // Выводим все найденные клипы для диагностики
            var allClips = LoadAllClipsFromFbx(RogueFbxPath);
            foreach (var kv in allClips)
                Debug.Log($"[AnimatorControllerBuilder] Found clip: {kv.Key}");

            var idleClip    = FindClip(allClips, "Idle");
            var walkClip    = FindClip(allClips, "Walk");
            var runClip     = FindClip(allClips, "Run");
            var attackClip  = FindClip(allClips, "Attack");
            var defeatClip  = FindClip(allClips, "Death", "Defeat", "Die");
            var hitClip     = FindClip(allClips, "Hit", "Hurt", "Block");
            var interactClip = FindClip(allClips, "Interact", "PickUp", "Use");

            // Настраиваем loop-флаги через ModelImporter
            ConfigureClipLooping(RogueFbxPath, new[]
            {
                ("Idle",  true),
                ("Walk",  true),
                ("Run",   true),
            });

            // Если контроллер уже существует — удаляем, создаём заново
            if (File.Exists(Path.Combine(Application.dataPath, "../", ControllerOutputPath)))
            {
                AssetDatabase.DeleteAsset(ControllerOutputPath);
                Debug.Log("[AnimatorControllerBuilder] Старый контроллер удалён.");
            }

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

            var idleState    = rootStateMachine.AddState("Idle");
            var walkState    = rootStateMachine.AddState("Walk");
            var runState     = rootStateMachine.AddState("Run");
            var pickUpState  = rootStateMachine.AddState("PickUp");
            var interactState = rootStateMachine.AddState("Interact");
            var attack1hState = rootStateMachine.AddState("Attack1h");
            var hitState     = rootStateMachine.AddState("Hit");
            var defeatState  = rootStateMachine.AddState("Defeat");
            var eatState     = rootStateMachine.AddState("Eat");

            idleState.motion    = idleClip;
            walkState.motion    = walkClip;
            runState.motion     = runClip;
            attack1hState.motion = attackClip;

            // Fallback: если нет специального клипа — используем Idle
            pickUpState.motion  = interactClip != null ? interactClip : idleClip;
            interactState.motion = interactClip != null ? interactClip : idleClip;
            hitState.motion     = hitClip    != null ? hitClip    : idleClip;
            defeatState.motion  = defeatClip != null ? defeatClip : idleClip;
            eatState.motion     = interactClip != null ? interactClip : idleClip;

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

            Debug.Log($"[AnimatorControllerBuilder] PlayerAnimator создан из RPGCharacters: {ControllerOutputPath}");
        }

        /// <summary>Загружает все AnimationClip из FBX, ключ = имя клипа (нижний регистр).</summary>
        private static Dictionary<string, AnimationClip> LoadAllClipsFromFbx(string fbxPath)
        {
            var result = new Dictionary<string, AnimationClip>(System.StringComparer.OrdinalIgnoreCase);
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    result[clip.name] = clip;
            }
            return result;
        }

        /// <summary>Находит клип по одному из возможных имён (первое совпадение).</summary>
        private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, params string[] candidateNames)
        {
            foreach (var name in candidateNames)
            {
                if (clips.TryGetValue(name, out var clip))
                    return clip;

                // Частичное совпадение (например "Attack_1h" содержит "Attack")
                foreach (var kv in clips)
                {
                    if (kv.Key.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return kv.Value;
                }
            }
            Debug.LogWarning($"[AnimatorControllerBuilder] Клип не найден для: {string.Join("/", candidateNames)}");
            return null;
        }

        /// <summary>Настраивает loop через ModelImporter для указанных клипов.</summary>
        private static void ConfigureClipLooping(string fbxPath, (string name, bool loop)[] settings)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool changed = false;
            foreach (var clip in clips)
            {
                foreach (var (name, loop) in settings)
                {
                    if (clip.name.Equals(name, System.StringComparison.OrdinalIgnoreCase) && clip.loopTime != loop)
                    {
                        clip.loopTime = loop;
                        changed = true;
                        Debug.Log($"[AnimatorControllerBuilder] Loop={loop} для клипа '{clip.name}'");
                    }
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
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
