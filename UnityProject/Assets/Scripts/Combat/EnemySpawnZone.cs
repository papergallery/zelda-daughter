using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Зона спавна врагов. Держит до maxEnemies живых врагов.
    /// После смерти врага спавнит тушу и ставит в очередь респаун.
    /// </summary>
    public class EnemySpawnZone : MonoBehaviour
    {
        [SerializeField] private EnemyData _enemyData;
        [SerializeField] private GameObject _enemyPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private int _maxEnemies = 3;
        [SerializeField] private float _spawnRadius = 15f;

        [Header("Carcass")]
        [SerializeField] private GameObject _carcassPrefab;

        private readonly List<EnemyHealth> _activeEnemies = new();
        private readonly List<float> _respawnTimers = new();

        private void Start()
        {
            for (int i = 0; i < _maxEnemies; i++)
                SpawnEnemy();
        }

        private void OnEnable()
        {
            EnemyHealth.OnDeath += HandleEnemyDeath;
        }

        private void OnDisable()
        {
            EnemyHealth.OnDeath -= HandleEnemyDeath;
        }

        private void Update()
        {
            for (int i = _respawnTimers.Count - 1; i >= 0; i--)
            {
                _respawnTimers[i] -= Time.deltaTime;
                if (_respawnTimers[i] <= 0f)
                {
                    _respawnTimers.RemoveAt(i);
                    SpawnEnemy();
                }
            }
        }

        private void SpawnEnemy()
        {
            if (_enemyPrefab == null || _activeEnemies.Count >= _maxEnemies)
                return;

            var offset = Random.insideUnitSphere * _spawnRadius;
            offset.y = 0f;
            var spawnPos = transform.position + offset;

            var enemyGo = Instantiate(_enemyPrefab, spawnPos, Quaternion.identity);

            if (_enemyData == null)
                return;

            if (!enemyGo.TryGetComponent<EnemyHealth>(out var health))
                return;

            health.Initialize(_enemyData);

            if (enemyGo.TryGetComponent<EnemyFSM>(out var fsm))
                fsm.Initialize(_enemyData, spawnPos);

            _activeEnemies.Add(health);
        }

        private void HandleEnemyDeath(EnemyHealth deadEnemy)
        {
            if (!_activeEnemies.Contains(deadEnemy))
                return;

            _activeEnemies.Remove(deadEnemy);

            SpawnCarcass(deadEnemy);

            float respawnTime = _enemyData != null ? _enemyData.RespawnTime : 60f;
            _respawnTimers.Add(respawnTime);
        }

        private void SpawnCarcass(EnemyHealth deadEnemy)
        {
            if (_enemyData == null || _enemyData.LootTable == null)
                return;

            GameObject carcassGo;

            if (_carcassPrefab != null)
            {
                // Спавним отдельный prefab туши на месте гибели врага
                carcassGo = Instantiate(_carcassPrefab, deadEnemy.transform.position, deadEnemy.transform.rotation);
            }
            else
            {
                // Fallback: превращаем объект врага в тушу, отключая враждебные компоненты
                carcassGo = deadEnemy.gameObject;
                DisableEnemyComponents(deadEnemy);
            }

            if (carcassGo.TryGetComponent<CarcassObject>(out var carcass))
            {
                carcass.Setup(_enemyData.LootTable);
            }
            else
            {
                var newCarcass = carcassGo.AddComponent<CarcassObject>();
                newCarcass.Setup(_enemyData.LootTable);
            }
        }

        private static void DisableEnemyComponents(EnemyHealth health)
        {
            // Останавливаем FSM (уже отключён в HandleDeath EnemyFSM,
            // но на всякий случай)
            if (health.TryGetComponent<EnemyFSM>(out var fsm))
                fsm.enabled = false;

            // Коллайдеры переводим из триггера в solid для корректного тапа
            var colliders = health.GetComponents<Collider>();
            foreach (var col in colliders)
                col.isTrigger = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, _spawnRadius);
            Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        }
#endif
    }
}
