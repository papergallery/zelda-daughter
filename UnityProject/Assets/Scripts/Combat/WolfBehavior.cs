using UnityEngine;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Специфика волка: агрится на вид (aggroOnSight=true), наносит Puncture.
    /// EnemyData для волка: aggroOnSight=true, inflictedWoundType=Puncture.
    /// Этот компонент — точка расширения для уникальной логики
    /// (подкрадывание, стайная атака, вой привлекающий других волков).
    /// </summary>
    [RequireComponent(typeof(EnemyFSM))]
    [RequireComponent(typeof(EnemyHealth))]
    public class WolfBehavior : MonoBehaviour
    {
        // Базовый FSM управляет всеми состояниями через EnemyData.
        // Здесь можно добавить: триггер воя (призыв стаи), анимацию подкрадывания
        // при переходе Alert→Chase, реакцию на смерть сородича.
    }
}
