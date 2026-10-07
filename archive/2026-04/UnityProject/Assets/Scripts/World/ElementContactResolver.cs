using UnityEngine;
using ZeldaDaughter.Combat;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Проверяет матрицу стихийных взаимодействий при физическом контакте двух объектов.
    /// Throttle: не чаще раза в 0.5 сек чтобы не спамить события и урон.
    /// </summary>
    public class ElementContactResolver : MonoBehaviour
    {
        [SerializeField] private ElementInteractionMatrix _matrix;

        // Базовый урон при стихийном взаимодействии до применения damageMultiplier
        [SerializeField] private float _baseElementalDamage = 5f;

        private const float CheckInterval = 0.5f;
        private float _lastCheckTime = -999f;

        // Кешируем значения enum чтобы не аллоцировать в горячем пути
        private static readonly ElementTag[] ElementTagValues =
            (ElementTag[])System.Enum.GetValues(typeof(ElementTag));

        private ElementState _selfState;
        private IDamageable _selfDamageable;

        private void Awake()
        {
            TryGetComponent(out _selfState);
            TryGetComponent(out _selfDamageable);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolve(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (Time.time - _lastCheckTime < CheckInterval) return;
            TryResolve(other);
        }

        private void TryResolve(Collider other)
        {
            if (_matrix == null) return;
            if (_selfState == null) return;
            if (!other.TryGetComponent<ElementState>(out var otherState)) return;

            _lastCheckTime = Time.time;

            var selfActive = _selfState.ActiveElements;
            var otherActive = otherState.ActiveElements;

            if (selfActive == ElementTag.None || otherActive == ElementTag.None) return;

            // Проверяем все комбинации отдельных флагов
            foreach (ElementTag selfTag in ElementTagValues)
            {
                if (selfTag == ElementTag.None) continue;
                if ((selfActive & selfTag) == 0) continue;

                foreach (ElementTag otherTag in ElementTagValues)
                {
                    if (otherTag == ElementTag.None) continue;
                    if ((otherActive & otherTag) == 0) continue;

                    if (!_matrix.TryGetInteraction(selfTag, otherTag, out var interaction))
                        continue;

                    ApplyInteractionResult(interaction, otherState);
                }
            }
        }

        private void ApplyInteractionResult(ElementInteraction interaction, ElementState otherState)
        {
            // Добавляем результирующие элементы на обоих участников
            if (interaction.resultAdd != ElementTag.None)
            {
                ApplyFlaggedElements(_selfState, interaction.resultAdd);
                ApplyFlaggedElements(otherState, interaction.resultAdd);
            }

            // Снимаем элементы с обоих участников
            if (interaction.resultRemove != ElementTag.None)
            {
                RemoveFlaggedElements(_selfState, interaction.resultRemove);
                RemoveFlaggedElements(otherState, interaction.resultRemove);
            }

            // Урон только если есть множитель и цель реализует IDamageable
            if (interaction.damageMultiplier > 0f && _selfDamageable != null && _selfDamageable.IsAlive)
            {
                float damage = _baseElementalDamage * interaction.damageMultiplier;
                var info = new DamageInfo(damage, WoundType.Burn, 0.3f, otherState.gameObject);
                _selfDamageable.TakeDamage(info);
            }
        }

        private static void ApplyFlaggedElements(ElementState state, ElementTag flags)
        {
            foreach (ElementTag tag in ElementTagValues)
            {
                if (tag == ElementTag.None) continue;
                if ((flags & tag) != 0)
                    state.ApplyElement(tag);
            }
        }

        private static void RemoveFlaggedElements(ElementState state, ElementTag flags)
        {
            foreach (ElementTag tag in ElementTagValues)
            {
                if (tag == ElementTag.None) continue;
                if ((flags & tag) != 0)
                    state.RemoveElement(tag);
            }
        }
    }
}
