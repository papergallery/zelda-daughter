using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Распространяет стихийные эффекты (Fire, Muddy) на соседние объекты через OverlapSphere.
    /// Глубина цепного распространения ограничена maxPropagationDepth из ElementConfig.
    /// </summary>
    [RequireComponent(typeof(ElementState))]
    public class ElementPropagator : MonoBehaviour
    {
        [SerializeField] private ElementConfig _config;
        [SerializeField] private int _currentDepth = 0;

        private ElementState _elementState;

        // Буфер для OverlapSphere — переиспользуем чтобы не аллоцировать каждый раз
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        private void Awake()
        {
            TryGetComponent(out _elementState);
        }

        private void OnEnable()
        {
            _elementState.OnElementApplied += OnElementApplied;
        }

        private void OnDisable()
        {
            _elementState.OnElementApplied -= OnElementApplied;
        }

        private void OnElementApplied(ElementTag tag)
        {
            if (tag == ElementTag.Fire || tag == ElementTag.Muddy)
                StartCoroutine(PropagateDelayed(tag));
        }

        private IEnumerator PropagateDelayed(ElementTag tag)
        {
            if (_config == null || !_config.TryGetSettings(tag, out var settings))
                yield break;

            if (_currentDepth >= settings.maxPropagationDepth)
                yield break;

            yield return new WaitForSeconds(settings.propagationDelay);

            PropagateFrom(tag, _currentDepth);
        }

        /// <summary>
        /// Ищет соседей в радиусе и применяет элемент к совместимым объектам.
        /// Вызывается рекурсивно через соседей с увеличенной глубиной.
        /// </summary>
        public void PropagateFrom(ElementTag tag, int depth)
        {
            if (_config == null || !_config.TryGetSettings(tag, out var settings))
                return;

            if (depth >= settings.maxPropagationDepth)
                return;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                settings.propagationRadius,
                _overlapBuffer
            );

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col.gameObject == gameObject) continue;

                // Fire распространяется только на FlammableTag
                // Muddy распространяется только на WettableTag
                bool canReceive = tag == ElementTag.Fire
                    ? col.TryGetComponent<FlammableTag>(out _)
                    : col.TryGetComponent<WettableTag>(out _);

                if (!canReceive) continue;
                if (!col.TryGetComponent<ElementState>(out var neighborState)) continue;

                // Уже горит/мокрый — пропускаем чтобы не зациклиться
                if (neighborState.HasElement(tag)) continue;

                neighborState.ApplyElement(tag);

                // Передаём глубину соседу чтобы он мог продолжить цепочку
                if (col.TryGetComponent<ElementPropagator>(out var neighborPropagator))
                    neighborPropagator._currentDepth = depth + 1;
            }
        }
    }
}
