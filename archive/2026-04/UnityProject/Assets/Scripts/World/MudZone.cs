using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Trigger zone placed on mud/swamp objects. Slows the player similarly to WaterZone.
    /// Attach to a GameObject with a BoxCollider (isTrigger = true).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MudZone : MonoBehaviour
    {
        [SerializeField] private float _speedMultiplier = 0.6f;

        private void Awake()
        {
            // Ensure the collider is a trigger
            if (TryGetComponent<Collider>(out var col) && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"[MudZone] Collider on {gameObject.name} was not a trigger — fixed automatically.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<CharacterMovement>(out var movement))
                movement.SetInMud(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<CharacterMovement>(out var movement))
                movement.SetInMud(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f, 0.35f);

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
