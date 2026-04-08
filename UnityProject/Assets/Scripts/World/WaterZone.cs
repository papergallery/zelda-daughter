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

        // Reserved for future depth-blocking logic (CharacterController Y-clamp)
        [SerializeField] private float _maxWadeDepth = 1.0f;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<CharacterMovement>(out var movement))
                movement.SetInWater(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<CharacterMovement>(out var movement))
                movement.SetInWater(false);
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
