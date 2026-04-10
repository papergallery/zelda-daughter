using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// Builds AnimatorController assets for enemy types.
    /// Wolf и Boar используют Generic-риг Quaternius без встроенных анимаций,
    /// поэтому создаются заглушки с пустыми состояниями — FSM будет работать через параметры.
    ///
    /// Параметры соответствуют вызовам в EnemyFSM.cs:
    ///   Speed (float)    — SetFloat("Speed", ...)
    ///   Attack (trigger) — SetTrigger("Attack")
    ///   Hit (trigger)    — SetTrigger("Hit")
    ///   Defeat (trigger) — SetTrigger("Defeat")
    /// </summary>
    public static class EnemyAnimatorBuilder
    {
        private const string ControllersDir = "Assets/Animations/Controllers";
        private const string BoarControllerPath = "Assets/Animations/Controllers/BoarAnimator.controller";
        private const string WolfControllerPath = "Assets/Animations/Controllers/WolfAnimator.controller";

        // KayKit Single Animations — используем для врагов типа Humanoid.
        // Quaternius Animal FBX — Generic без анимаций, поэтому клипы будут null → пустые состояния.
        private const string WalkFbxPath   = "Assets/Animations/KayKit/fbx/Single Animations/Walk.fbx";
        private const string RunFbxPath    = "Assets/Animations/KayKit/fbx/Single Animations/Run.fbx";
        private const string AttackFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Attack(1h).fbx";
        private const string DefeatFbxPath = "Assets/Animations/KayKit/fbx/Single Animations/Defeat.fbx";

        // Пути к моделям животных — используем для проверки наличия встроенных клипов
        private const string WolfFbxPath = "Assets/Models/Animals/Animal Pack Vol.2 by @Quaternius/FBX/Wolf.fbx";
        private const string PigFbxPath  = "Assets/Models/Animals/Farm Animals by @Quaternius/FBX/Pig.fbx";

        [MenuItem("ZeldaDaughter/Animation/Build Enemy Animators")]
        public static void BuildEnemyAnimators()
        {
            EnsureDirectory(ControllersDir);

            BuildEnemyController("Boar", BoarControllerPath, PigFbxPath);
            BuildEnemyController("Wolf", WolfControllerPath, WolfFbxPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[EnemyAnimatorBuilder] Enemy AnimatorControllers созданы.");
        }

        private static void BuildEnemyController(string enemyName, string outputPath, string modelFbxPath)
        {
            // Если контроллер уже существует — пересоздаём
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath) != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
                Debug.Log($"[EnemyAnimatorBuilder] Удалён старый контроллер: {outputPath}");
            }

            // Пробуем загрузить анимации из самой модели животного
            var modelWalkClip   = TryLoadClipFromFbx(modelFbxPath, "Walk");
            var modelRunClip    = TryLoadClipFromFbx(modelFbxPath, "Run");
            var modelAttackClip = TryLoadClipFromFbx(modelFbxPath, "Attack");
            var modelDeathClip  = TryLoadClipFromFbx(modelFbxPath, "Death");

            // Если в модели нет клипов — используем KayKit как визуальную заглушку
            // (риги несовместимы, но хоть что-то играет в Editor)
            if (modelWalkClip == null)   modelWalkClip   = TryLoadClipFromFbx(WalkFbxPath, null);
            if (modelRunClip == null)    modelRunClip    = TryLoadClipFromFbx(RunFbxPath, null);
            if (modelAttackClip == null) modelAttackClip = TryLoadClipFromFbx(AttackFbxPath, null);
            if (modelDeathClip == null)  modelDeathClip  = TryLoadClipFromFbx(DefeatFbxPath, null);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            controller.AddParameter("Speed",  AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit",    AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Defeat", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idleState   = sm.AddState("Idle");
            var walkState   = sm.AddState("Walk");
            var attackState = sm.AddState("Attack");
            var hitState    = sm.AddState("Hit");
            var deathState  = sm.AddState("Death");

            // Назначаем клипы (могут быть null — состояние будет пустым, это нормально)
            idleState.motion   = modelWalkClip;   // idle = медленный walk или пустой
            walkState.motion   = modelRunClip;    // walk/chase = run
            attackState.motion = modelAttackClip;
            hitState.motion    = modelAttackClip; // хит = тот же клип что атака, или пустой
            deathState.motion  = modelDeathClip;

            sm.defaultState = idleState;

            // Idle ↔ Walk по Speed
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // AnyState → Attack
            var anyToAttack = sm.AddAnyStateTransition(attackState);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.1f;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;
            attackToIdle.duration = 0.15f;

            // AnyState → Hit
            var anyToHit = sm.AddAnyStateTransition(hitState);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.1f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.8f;
            hitToIdle.duration = 0.15f;

            // AnyState → Death (без возврата)
            var anyToDeath = sm.AddAnyStateTransition(deathState);
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.1f;
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "Defeat");

            EditorUtility.SetDirty(controller);

            Debug.Log($"[EnemyAnimatorBuilder] {enemyName}Animator создан: {outputPath}" +
                      $" (Walk:{modelWalkClip != null}, Attack:{modelAttackClip != null}, Death:{modelDeathClip != null})");
        }

        /// <summary>
        /// Загружает первый AnimationClip из FBX. Если clipNameHint задан — ищет по имени.
        /// Если не задан — возвращает первый не-preview клип.
        /// </summary>
        private static AnimationClip TryLoadClipFromFbx(string fbxPath, string clipNameHint)
        {
            if (string.IsNullOrEmpty(fbxPath) || !File.Exists(Path.Combine("Assets/../", fbxPath)))
                return null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0)
                return null;

            AnimationClip firstClip = null;
            foreach (var asset in assets)
            {
                if (!(asset is AnimationClip clip)) continue;
                if (clip.name.Contains("__preview__")) continue;

                if (!string.IsNullOrEmpty(clipNameHint))
                {
                    if (clip.name.IndexOf(clipNameHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return clip;
                }
                else if (firstClip == null)
                {
                    firstClip = clip;
                }
            }

            return firstClip;
        }

        private static void EnsureDirectory(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
