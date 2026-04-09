#if ZD_DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Input;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Debugging
{
    public class DebugEventLogger : MonoBehaviour
    {
        private void OnEnable()
        {
            // Input
            GestureDispatcher.OnTap += OnTap;
            GestureDispatcher.OnMoveInput += OnMoveInput;
            GestureDispatcher.OnMoveStop += OnMoveStop;
            GestureDispatcher.OnLongPressStart += OnLongPressStart;
            GestureDispatcher.OnLongPressEnd += OnLongPressEnd;

            // AutoMove
            CharacterAutoMove.OnReachedTarget += OnAutoMoveReached;
            CharacterAutoMove.OnAutoMoveCancelled += OnAutoMoveCancelled;

            // CharacterMovement
            CharacterMovement.OnSpeedChanged += OnSpeedChanged;
            CharacterMovement.OnMovingStateChanged += OnMovingStateChanged;

            // Inventory
            PlayerInventory.OnItemAdded += OnItemAdded;
            PlayerInventory.OnItemRemoved += OnItemRemoved;
            PlayerInventory.OnWeightChanged += OnWeightChanged;

            // Crafting
            CraftingSystem.OnCraftSuccess += OnCraftSuccess;
            CraftingSystem.OnCraftFailed += OnCraftFailed;

            // Health
            PlayerHealthState.OnDamageTaken += OnPlayerDamaged;
            PlayerHealthState.OnWoundAdded += OnWoundAdded;
            PlayerHealthState.OnWoundRemoved += OnWoundRemoved;
            PlayerHealthState.OnKnockout += OnKnockout;
            PlayerHealthState.OnRevive += OnRevive;

            // Combat
            CombatController.OnAttackPerformed += OnAttackPerformed;
            CombatController.OnAttackResult += OnAttackResult;

            // Enemy
            EnemyHealth.OnDamaged += OnEnemyDamaged;
            EnemyHealth.OnStagger += OnEnemyStagger;
            EnemyHealth.OnDeath += OnEnemyDeath;

            // Progression
            PlayerStats.OnStatChanged += OnStatChanged;
            PlayerStats.OnTierReached += OnTierReached;
        }

        private void OnDisable()
        {
            GestureDispatcher.OnTap -= OnTap;
            GestureDispatcher.OnMoveInput -= OnMoveInput;
            GestureDispatcher.OnMoveStop -= OnMoveStop;
            GestureDispatcher.OnLongPressStart -= OnLongPressStart;
            GestureDispatcher.OnLongPressEnd -= OnLongPressEnd;

            CharacterAutoMove.OnReachedTarget -= OnAutoMoveReached;
            CharacterAutoMove.OnAutoMoveCancelled -= OnAutoMoveCancelled;

            CharacterMovement.OnSpeedChanged -= OnSpeedChanged;
            CharacterMovement.OnMovingStateChanged -= OnMovingStateChanged;

            PlayerInventory.OnItemAdded -= OnItemAdded;
            PlayerInventory.OnItemRemoved -= OnItemRemoved;
            PlayerInventory.OnWeightChanged -= OnWeightChanged;

            CraftingSystem.OnCraftSuccess -= OnCraftSuccess;
            CraftingSystem.OnCraftFailed -= OnCraftFailed;

            PlayerHealthState.OnDamageTaken -= OnPlayerDamaged;
            PlayerHealthState.OnWoundAdded -= OnWoundAdded;
            PlayerHealthState.OnWoundRemoved -= OnWoundRemoved;
            PlayerHealthState.OnKnockout -= OnKnockout;
            PlayerHealthState.OnRevive -= OnRevive;

            CombatController.OnAttackPerformed -= OnAttackPerformed;
            CombatController.OnAttackResult -= OnAttackResult;

            EnemyHealth.OnDamaged -= OnEnemyDamaged;
            EnemyHealth.OnStagger -= OnEnemyStagger;
            EnemyHealth.OnDeath -= OnEnemyDeath;

            PlayerStats.OnStatChanged -= OnStatChanged;
            PlayerStats.OnTierReached -= OnTierReached;
        }

        // --- Input ---

        private void OnTap(Vector2 pos) =>
            ZDLog.Log("Input", $"Tap pos=({pos.x:F0},{pos.y:F0})");

        private void OnMoveInput(Vector2 dir, float intensity) =>
            ZDLog.Log("Input", $"SwipeInput dir=({dir.x:F2},{dir.y:F2}) intensity={intensity:F2}");

        private void OnMoveStop() =>
            ZDLog.Log("Input", "SwipeStop");

        private void OnLongPressStart() =>
            ZDLog.Log("Input", "LongPressStart");

        private void OnLongPressEnd() =>
            ZDLog.Log("Input", "LongPressEnd");

        // --- AutoMove ---

        private void OnAutoMoveReached() =>
            ZDLog.Log("Move", "AutoMoveReached");

        private void OnAutoMoveCancelled() =>
            ZDLog.Log("Move", "AutoMoveCancelled");

        // --- CharacterMovement ---

        private void OnSpeedChanged(float speed) =>
            ZDLog.Log("Move", $"SpeedChanged speed={speed:F2}");

        private void OnMovingStateChanged(bool isMoving) =>
            ZDLog.Log("Move", $"MovingState={isMoving}");

        // --- Inventory ---

        private void OnItemAdded(ItemData item, int amount) =>
            ZDLog.Log("Inventory", $"Added item={item?.DisplayName ?? "null"} amount={amount}");

        private void OnItemRemoved(ItemData item, int amount) =>
            ZDLog.Log("Inventory", $"Removed item={item?.DisplayName ?? "null"} amount={amount}");

        private void OnWeightChanged(float weight) =>
            ZDLog.Log("Inventory", $"WeightChanged weight={weight:F1}");

        // --- Crafting ---

        private void OnCraftSuccess(CraftRecipe recipe) =>
            ZDLog.Log("Craft", $"Success result={recipe?.Result?.DisplayName ?? "null"}");

        private void OnCraftFailed(ItemData a, ItemData b) =>
            ZDLog.Log("Craft", $"Failed items={a?.DisplayName ?? "null"}+{b?.DisplayName ?? "null"}");

        // --- Health ---

        private void OnPlayerDamaged(float amount) =>
            ZDLog.Log("Combat", $"PlayerDamaged amount={amount:F1}");

        private void OnWoundAdded(Wound wound) =>
            ZDLog.Log("Combat", $"WoundAdded type={wound.Type} severity={wound.Severity:F2}");

        private void OnWoundRemoved(WoundType type) =>
            ZDLog.Log("Combat", $"WoundRemoved type={type}");

        private void OnKnockout() =>
            ZDLog.Log("Combat", "PlayerKnockout");

        private void OnRevive() =>
            ZDLog.Log("Combat", "PlayerRevive");

        // --- Combat ---

        private void OnAttackPerformed(WeaponData weapon) =>
            ZDLog.Log("Combat", $"AttackPerformed weapon={weapon?.name ?? "Unarmed"}");

        private void OnAttackResult(bool isHit) =>
            ZDLog.Log("Combat", $"AttackResult hit={isHit}");

        // --- Enemy ---

        private void OnEnemyDamaged(EnemyHealth enemy, float hpRatio) =>
            ZDLog.Log("Combat", $"EnemyDamaged enemy={enemy?.Data?.name ?? "unknown"} hp={hpRatio:F2}");

        private void OnEnemyStagger(EnemyHealth enemy) =>
            ZDLog.Log("Combat", $"EnemyStagger enemy={enemy?.Data?.name ?? "unknown"}");

        private void OnEnemyDeath(EnemyHealth enemy) =>
            ZDLog.Log("Combat", $"EnemyDeath enemy={enemy?.Data?.name ?? "unknown"}");

        // --- Progression ---

        private void OnStatChanged(StatType type, float oldVal, float newVal) =>
            ZDLog.Log("Progression", $"StatChanged type={type} old={oldVal:F2} new={newVal:F2}");

        private void OnTierReached(StatType type, int tier) =>
            ZDLog.Log("Progression", $"TierReached type={type} tier={tier}");
    }
}
#endif
