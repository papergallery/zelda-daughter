using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Маркер: объект может намокнуть.
    /// При наличии _autoAddElementState автоматически добавляет ElementState в Awake.
    /// </summary>
    public class WettableTag : MonoBehaviour
    {
        [SerializeField] private bool _autoAddElementState = true;

        private void Awake()
        {
            if (_autoAddElementState && !TryGetComponent<ElementState>(out _))
                gameObject.AddComponent<ElementState>();
        }
    }
}
