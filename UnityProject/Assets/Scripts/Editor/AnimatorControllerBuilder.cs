using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Строит PlayerAnimator.controller из KayKit Single Animations.
    /// KayKit анимации конвертируются в Humanoid rig (reimport) для совместимости
    /// с RPGCharacters/Rogue.fbx (Humanoid Avatar).
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        private const string ControllerOutputPath = "Assets/Animations/Controllers/PlayerAnimator.controller";
        private const string KayKitAnimPath = "Assets/Animations/KayKit/fbx/Single Animations/";

        // Анимации которые нужно конвертировать в Humanoid для KayKit clips
        private static readonly string[] HumanoidClipNames =
        {
            "Idle", "Walk", "Run", "Attack(1h)", "Defeat", "Block", "Interact", "PickUp", "Wave"
        };

        private const float TransitionDuration = 0.15f;

        [MenuItem("ZeldaDaughter/Animation/Build Player Animator")]
        public static void BuildPlayerAnimator()
        {
            EnsureDirectoryExists("Assets/Animations/Controllers");

            // Шаг 1: конвертировать KayKit анимации в Humanoid для использования с RPGCharacters/Rogue
            bool needsReimport = ConvertKayKitAnimsToHumanoid();
            if (needsReimport)
            {
                // После reimport нужно обновить Asset Database перед загрузкой клипов
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            // Шаг 2: загружаем клипы
            var clips = LoadKayKitClips();

            foreach (var kv in clips)
                Debug.Log($"[AnimatorControllerBuilder] Found clip: {kv.Key}");

            if (clips.Count == 0)
            {
                Debug.LogError("[AnimatorControllerBuilder] KayKit clips не найдены в папке: " + KayKitAnimPath);
                return;
            }

            var idleClip     = GetClip(clips, "Idle");
            var walkClip     = GetClip(clips, "Walk");
            var runClip      = GetClip(clips, "Run");
            var attackClip   = GetClip(clips, "Attack(1h)");
            var defeatClip   = GetClip(clips, "Defeat");
            var hitClip      = GetClip(clips, "Block");
            var interactClip = GetClip(clips, "Interact");
            var pickUpClip   = GetClip(clips, "PickUp");

            if (idleClip == null || walkClip == null)
            {
                Debug.LogError("[AnimatorControllerBuilder] Idle или Walk клип не найден. Прерывание.");
                return;
            }

            // Шаг 3: удаляем старый controller, создаём новый
            if (File.Exists(Path.Combine(Application.dataPath, "../", ControllerOutputPath)))
            {
                AssetDatabase.DeleteAsset(ControllerOutputPath);
                Debug.Log("[AnimatorControllerBuilder] Старый контроллер удалён.");
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerOutputPath);

            controller.AddParameter("Speed",    AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("PickUp",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit",      AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Defeat",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Eat",      AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idleState     = sm.AddState("Idle");
            var walkState     = sm.AddState("Walk");
            var runState      = sm.AddState("Run");
            var pickUpState   = sm.AddState("PickUp");
            var interactState = sm.AddState("Interact");
            var attack1hState = sm.AddState("Attack1h");
            var hitState      = sm.AddState("Hit");
            var defeatState   = sm.AddState("Defeat");
            var eatState      = sm.AddState("Eat");

            idleState.motion     = idleClip;
            walkState.motion     = walkClip;
            runState.motion      = runClip   != null ? runClip   : walkClip;
            attack1hState.motion = attackClip != null ? attackClip : idleClip;
            pickUpState.motion   = pickUpClip != null ? pickUpClip : (interactClip != null ? interactClip : idleClip);
            interactState.motion = interactClip != null ? interactClip : idleClip;
            hitState.motion      = hitClip   != null ? hitClip   : idleClip;
            defeatState.motion   = defeatClip != null ? defeatClip : idleClip;
            eatState.motion      = interactClip != null ? interactClip : idleClip;

            sm.defaultState = idleState;

            // Idle → Walk: IsMoving=true AND Speed < 0.65
            var idleToWalk = idleState.AddTransition(walkState);
            ConfigureTransition(idleToWalk);
            idleToWalk.AddCondition(AnimatorConditionMode.If,   0f,   "IsMoving");
            idleToWalk.AddCondition(AnimatorConditionMode.Less, 0.65f,"Speed");

            // Idle → Run: IsMoving=true AND Speed >= 0.65
            var idleToRun = idleState.AddTransition(runState);
            ConfigureTransition(idleToRun);
            idleToRun.AddCondition(AnimatorConditionMode.If,      0f,   "IsMoving");
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.64f,"Speed");

            // Walk → Run: Speed >= 0.65
            var walkToRun = walkState.AddTransition(runState);
            ConfigureTransition(walkToRun);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 0.64f,"Speed");

            // Run → Walk: Speed < 0.65 AND IsMoving
            var runToWalk = runState.AddTransition(walkState);
            ConfigureTransition(runToWalk);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 0.65f,"Speed");
            runToWalk.AddCondition(AnimatorConditionMode.If,   0f,   "IsMoving");

            // Walk → Idle: IsMoving=false
            var walkToIdle = walkState.AddTransition(idleState);
            ConfigureTransition(walkToIdle);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            // Run → Idle: IsMoving=false
            var runToIdle = runState.AddTransition(idleState);
            ConfigureTransition(runToIdle);
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            // AnyState → action states
            AddActionState(sm, idleState, pickUpState,   "PickUp");
            AddActionState(sm, idleState, interactState, "Interact");
            AddActionState(sm, idleState, attack1hState, "Attack");
            AddActionState(sm, idleState, hitState,      "Hit");
            AddActionState(sm, idleState, defeatState,   "Defeat");
            AddActionState(sm, idleState, eatState,      "Eat");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnimatorControllerBuilder] PlayerAnimator создан из KayKit Humanoid clips: {ControllerOutputPath}");
        }

        /// <summary>
        /// Восстанавливает KayKit Single Animations в Generic rig (если были конвертированы в Humanoid).
        /// KayKit использует Generic rig (PrototypePete skeleton) — Humanoid конвертация ломает клипы.
        /// Возвращает true если был произведён reimport.
        /// </summary>
        private static bool ConvertKayKitAnimsToHumanoid()
        {
            bool anyChanged = false;

            foreach (var clipName in HumanoidClipNames)
            {
                var fbxPath = KayKitAnimPath + clipName + ".fbx";
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) continue;

                // Возвращаем в Generic если был Humanoid — Humanoid ломает KayKit clips
                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                    importer.SaveAndReimport();
                    Debug.Log($"[AnimatorControllerBuilder] Возвращён в Generic: {fbxPath}");
                    anyChanged = true;
                }
            }

            if (anyChanged)
                Debug.Log("[AnimatorControllerBuilder] KayKit анимации восстановлены как Generic.");
            else
                Debug.Log("[AnimatorControllerBuilder] KayKit анимации уже Generic — пропуск реимпорта.");

            return anyChanged;
        }

        /// <summary>
        /// Загружает первый AnimationClip из каждого KayKit FBX напрямую по пути.
        /// Ключ = имя файла без расширения (например "Idle", "Walk", "Run").
        /// Используем прямую загрузку вместо FindAssets для надёжности после reimport.
        /// </summary>
        private static Dictionary<string, AnimationClip> LoadKayKitClips()
        {
            var result = new Dictionary<string, AnimationClip>(System.StringComparer.OrdinalIgnoreCase);

            // Ищем все FBX в папке через файловую систему
            var fullPath = Path.Combine(Application.dataPath, "../", KayKitAnimPath);
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"[AnimatorControllerBuilder] Папка не существует: {fullPath}");
                return result;
            }

            var fbxFiles = Directory.GetFiles(fullPath, "*.fbx", SearchOption.TopDirectoryOnly);
            foreach (var fbxFullPath in fbxFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(fbxFullPath);
                var assetPath = KayKitAnimPath + Path.GetFileName(fbxFullPath);

                var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in allAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    {
                        if (!result.ContainsKey(fileName))
                            result[fileName] = clip;
                        break;
                    }
                }
            }

            return result;
        }

        private static AnimationClip GetClip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (clips.TryGetValue(name, out var clip))
                return clip;

            Debug.LogWarning($"[AnimatorControllerBuilder] Клип не найден: {name}");
            return null;
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
