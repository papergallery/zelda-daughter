using UnityEngine;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Специфика кабана: не агрится на вид, агрится только при ударе.
    /// EnemyData для кабана: aggroOnSight=false, aggroOnDamage=true,
    /// inflictedWoundType=Fracture.
    /// Этот компонент — точка расширения для уникальной idle-логики
    /// (рытьё земли, хрюканье, кормёжка).
    /// </summary>
    [RequireComponent(typeof(EnemyFSM))]
    [RequireComponent(typeof(EnemyHealth))]
    public class BoarBehavior : MonoBehaviour
    {
        // Базовый FSM управляет всеми состояниями через EnemyData.
        // Здесь можно добавить: анимации рытья в Idle, звуки кормёжки,
        // визуальную реакцию на приближение игрока без агро.
    }
}
