using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Маркер: объект может загореться.
    /// При наличии _autoAddElementState автоматически добавляет ElementState в Awake.
    /// </summary>
    public class FlammableTag : MonoBehaviour
    {
        [SerializeField] private bool _autoAddElementState = true;

        private void Awake()
        {
            if (_autoAddElementState && !TryGetComponent<ElementState>(out _))
                gameObject.AddComponent<ElementState>();
        }
    }
}
