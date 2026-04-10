using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Trigger zone placed on water objects. Slows the player and blocks deep wading.
    /// Attach to a GameObject with a trigger Collider (added by WaterSetup editor tool).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WaterZone : MonoBehaviour
    {
        [SerializeField] private float _slowdownMultiplier = 0.4f;
        [SerializeField] private float _maxWadeDepth = 1.0f;

        [Tooltip("Сила выталкивания при превышении глубины")]
        [SerializeField] private float _pushBackForce = 3f;

        private float _waterSurfaceY;
        private CharacterMovement _trackedPlayer;
        private CharacterController _trackedController;
        private Vector3 _lastSafePosition;

        private BoxCollider _box;
        private bool _playerInside;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            // Верхняя грань коллайдера = поверхность воды
            if (_box != null)
                _waterSurfaceY = transform.TransformPoint(_box.center + Vector3.up * _box.size.y * 0.5f).y;
            else
                _waterSurfaceY = transform.position.y;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            EnterWater(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if (_trackedPlayer == null || !other.CompareTag("Player")) return;
            CheckDepth(other.transform);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            ExitWater(other.gameObject);
        }

        // Fallback for CharacterController which doesn't reliably trigger OnTriggerEnter
        private void Update()
        {
            if (_box == null) return;
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            bool inside = _box.bounds.Contains(player.transform.position);
            if (inside && !_playerInside)
            {
                _playerInside = true;
                EnterWater(player);
            }
            else if (!inside && _playerInside)
            {
                _playerInside = false;
                ExitWater(player);
            }
            else if (inside && _trackedPlayer != null)
            {
                CheckDepth(player.transform);
            }
        }

        private void EnterWater(GameObject playerGo)
        {
            if (playerGo.TryGetComponent<CharacterMovement>(out var movement))
            {
                movement.SetInWater(true);
                _trackedPlayer = movement;
                playerGo.TryGetComponent(out _trackedController);
                _lastSafePosition = playerGo.transform.position;
                Debugging.ZDLog.Log("Move", "WaterZone entered");
            }
        }

        private void ExitWater(GameObject playerGo)
        {
            if (playerGo.TryGetComponent<CharacterMovement>(out var movement))
            {
                movement.SetInWater(false);
                _trackedPlayer = null;
                _trackedController = null;
                Debugging.ZDLog.Log("Move", "WaterZone exited");
            }
        }

        private void CheckDepth(Transform playerTransform)
        {
            float playerFeetY = playerTransform.position.y;
            float depth = _waterSurfaceY - playerFeetY;

            if (depth <= _maxWadeDepth)
            {
                _lastSafePosition = playerTransform.position;
                return;
            }

            // Глубина превышена — выталкиваем к последней безопасной позиции
            if (_trackedController != null)
            {
                Vector3 pushDir = (_lastSafePosition - playerTransform.position);
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.01f)
                    pushDir = -playerTransform.forward;
                pushDir = pushDir.normalized;
                _trackedController.Move(pushDir * _pushBackForce * Time.deltaTime);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f);

            if (TryGetComponent<BoxCollider>(out var box))
            {
                var matrix = Matrix4x4.TRS(
                    transform.TransformPoint(box.center),
                    transform.rotation,
                    Vector3.Scale(transform.lossyScale, box.size));
                Gizmos.matrix = matrix;
                Gizmos.DrawCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawCube(transform.position, transform.lossyScale);
            }
        }
#endif
    }
}
