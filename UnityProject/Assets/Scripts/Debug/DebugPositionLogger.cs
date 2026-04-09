#if ZD_DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;

namespace ZeldaDaughter.Debugging
{
    public class DebugPositionLogger : MonoBehaviour
    {
        [SerializeField] private float _logInterval = 5f;

        private Transform _playerTransform;
        private float _timer;

        private void Awake()
        {
            if (CompareTag("Player"))
            {
                _playerTransform = transform;
                return;
            }

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                _playerTransform = playerObj.transform;
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            _timer += Time.deltaTime;
            if (_timer < _logInterval) return;

            _timer = 0f;

            var pos = _playerTransform.position;
            ZDLog.Log("Move", $"Position pos=({pos.x:F1},{pos.y:F1},{pos.z:F1})");
        }
    }
}
#endif
