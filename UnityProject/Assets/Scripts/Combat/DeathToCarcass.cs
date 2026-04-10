using UnityEngine;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Converts dead enemy into a CarcassObject for loot interaction.
    /// Attach to enemies that don't use EnemySpawnZone.
    /// </summary>
    public class DeathToCarcass : MonoBehaviour
    {
        private void OnEnable()
        {
            EnemyHealth.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            EnemyHealth.OnDeath -= HandleDeath;
        }

        private void HandleDeath(EnemyHealth deadEnemy)
        {
            if (deadEnemy.gameObject != gameObject) return;

            // Get LootTable from EnemyData via EnemyFSM
            LootTable lootTable = null;
            if (TryGetComponent<EnemyFSM>(out var fsm))
            {
                var dataField = typeof(EnemyFSM).GetField("_data",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var data = dataField?.GetValue(fsm) as EnemyData;
                lootTable = data?.LootTable;
            }

            // Add CarcassObject if not already present
            if (!TryGetComponent<CarcassObject>(out var carcass))
                carcass = gameObject.AddComponent<CarcassObject>();

            if (lootTable != null)
                carcass.Setup(lootTable);

            Debugging.ZDLog.Log("Combat", $"Carcass created for {gameObject.name}, loot={lootTable?.name ?? "null"}");
        }
    }
}
