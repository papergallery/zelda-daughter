using UnityEngine;

namespace ZeldaDaughter.World
{
    public class SpawnZoneMarker : MonoBehaviour
    {
        [SerializeField] private string _enemyType;
        [SerializeField] private float _radius;
        [SerializeField] private int _maxCount;
        [SerializeField] private float _respawnTimeSec;

        public string EnemyType => _enemyType;
        public float Radius => _radius;
        public int MaxCount => _maxCount;
        public float RespawnTimeSec => _respawnTimeSec;

        public void Setup(string enemyType, float radius, int maxCount, float respawnTime)
        {
            _enemyType = enemyType;
            _radius = radius;
            _maxCount = maxCount;
            _respawnTimeSec = respawnTime;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _radius);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"{_enemyType} x{_maxCount}");
        }
#endif
    }
}
