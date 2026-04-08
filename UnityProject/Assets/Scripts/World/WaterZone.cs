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

        private void Awake()
        {
            // Верхняя грань коллайдера = поверхность воды
            if (TryGetComponent<BoxCollider>(out var box))
                _waterSurfaceY = transform.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f).y;
            else
                _waterSurfaceY = transform.position.y;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<CharacterMovement>(out var movement))
            {
                movement.SetInWater(true);
                _trackedPlayer = movement;
                other.TryGetComponent(out _trackedController);
                _lastSafePosition = other.transform.position;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_trackedPlayer == null || !other.CompareTag("Player")) return;

            float playerFeetY = other.transform.position.y;
            float depth = _waterSurfaceY - playerFeetY;

            if (depth <= _maxWadeDepth)
            {
                _lastSafePosition = other.transform.position;
                return;
            }

            // Глубина превышена — выталкиваем к последней безопасной позиции
            if (_trackedController != null)
            {
                Vector3 pushDir = (_lastSafePosition - other.transform.position);
                pushDir.y = 0f;

                if (pushDir.sqrMagnitude < 0.01f)
                    pushDir = -other.transform.forward; // fallback: назад

                pushDir = pushDir.normalized;
                _trackedController.Move(pushDir * _pushBackForce * Time.deltaTime);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<CharacterMovement>(out var movement))
            {
                movement.SetInWater(false);
                _trackedPlayer = null;
                _trackedController = null;
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
