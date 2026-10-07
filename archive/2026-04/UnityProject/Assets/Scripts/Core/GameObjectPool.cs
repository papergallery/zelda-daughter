using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Core
{
    public class GameObjectPool : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private int _initialSize = 10;
        [SerializeField] private string _poolId;

        private GenericObjectPool<GameObject> _pool;
        private static readonly Dictionary<string, GameObjectPool> _registry = new();

        public int CountInactive => _pool.CountInactive;

        private void Awake()
        {
            _pool = new GenericObjectPool<GameObject>(
                CreateInstance,
                OnGetInstance,
                OnReleaseInstance,
                _initialSize
            );

            if (!string.IsNullOrEmpty(_poolId))
                _registry[_poolId] = this;
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_poolId))
                _registry.Remove(_poolId);
        }

        /// <summary>Найти пул по ID, заданному в инспекторе.</summary>
        public static GameObjectPool Find(string id)
        {
            _registry.TryGetValue(id, out var pool);
            return pool;
        }

        public GameObject Get() => _pool.Get();

        public void Release(GameObject obj) => _pool.Release(obj);

        private GameObject CreateInstance()
        {
            var obj = Instantiate(_prefab, transform);
            obj.SetActive(false);
            return obj;
        }

        private void OnGetInstance(GameObject obj)
        {
            obj.SetActive(true);
        }

        private void OnReleaseInstance(GameObject obj)
        {
            obj.SetActive(false);
            obj.transform.SetParent(transform);
        }
    }
}
