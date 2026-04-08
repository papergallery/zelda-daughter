using System;
using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Применяет лечебные предметы к активным ранам игрока.
    /// Вешается на тот же GameObject, что и PlayerHealthState.
    /// </summary>
    public class WoundTreatment : MonoBehaviour
    {
        public static event Action<WoundType> OnWoundTreated;

        private PlayerHealthState _healthState;

        private void Awake()
        {
            TryGetComponent(out _healthState);
        }

        /// <summary>
        /// Применить лечебный предмет. Возвращает true, если рана была найдена и вылечена.
        /// </summary>
        public bool TreatWithItem(ItemData item)
        {
            if (item == null || !item.IsMedicine) return false;
            if (_healthState == null) return false;
            if (item.TreatsWoundType == WoundType.None) return false;

            bool hasWound = HasActiveWound(item.TreatsWoundType);
            if (!hasWound) return false;

            _healthState.TreatWound(item.TreatsWoundType);
            OnWoundTreated?.Invoke(item.TreatsWoundType);
            return true;
        }

        private bool HasActiveWound(WoundType type)
        {
            var wounds = _healthState.ActiveWounds;
            for (int i = 0; i < wounds.Count; i++)
            {
                if (wounds[i].Type == type) return true;
            }
            return false;
        }
    }
}
