using UnityEngine;

namespace ZeldaDaughter.Core
{
    public class AutoReturnToPool : MonoBehaviour
    {
        [SerializeField] private float _lifetime = 5f;

        private GameObjectPool _pool;
        private float _timer;

        /// <summary>Инициализировать перед выдачей из пула.</summary>
        public void Init(GameObjectPool pool, float lifetime)
        {
            _pool = pool;
            _lifetime = lifetime;
            _timer = 0f;
        }

        private void OnEnable()
        {
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _lifetime && _pool != null)
                _pool.Release(gameObject);
        }
    }
}
